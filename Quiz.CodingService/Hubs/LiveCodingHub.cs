using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Quiz.CodingService.Engine;
using Quiz.CodingService.Messaging;
using Quiz.CodingService.Services;
using Quiz.CodingService.State;

namespace Quiz.CodingService.Hubs;

public sealed class LiveCodingHub(
    LiveCodingSessionStateStore store,
    RabbitBus bus,
    LiveCodingHistoryService history,
    ILogger<LiveCodingHub> log
) : Hub
{
    private const int SessionCodeLength = 6;
    private const int MaxDisplayNameLength = 50;
    private const int MaxStudentCodeLength = 100_000;

    public async Task JoinHost(string sessionCode, string displayName)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);

        displayName = NormalizeDisplayName(displayName, "Profesor");
        EnsureValidDisplayName(displayName);

        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var status = await store.GetStatus(sessionCode);
        if (status == "ended")
            throw new HubException("Sesiunea este deja încheiată.");

        var hostId = BuildHostId(displayName);
        var existingHostId = await store.GetHostId(sessionCode);
        if (!string.IsNullOrWhiteSpace(existingHostId) && existingHostId != "__pending__" && existingHostId != hostId)
            throw new HubException("Sesiunea este deja controlată de un alt host.");

        await store.SetHostId(sessionCode, hostId);
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);

        Context.Items["sessionCode"] = sessionCode;
        Context.Items["playerId"] = hostId;
        Context.Items["displayName"] = displayName;
        Context.Items["isHost"] = true;

        log.LogInformation("Host {HostId} ({Name}) joined session {Code}", hostId, displayName, sessionCode);
    }

    public async Task Join(string sessionCode, string displayName)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);

        displayName = NormalizeDisplayName(displayName);
        EnsureValidDisplayName(displayName);

        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var status = await store.GetStatus(sessionCode);
        if (status == "ended")
            throw new HubException("Sesiunea este deja încheiată.");

        var playerId = BuildPlayerId(displayName);

        await store.AddPlayer(sessionCode, playerId, displayName);
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);

        log.LogInformation("Player {PlayerId} ({Name}) joined session {Code}", playerId, displayName, sessionCode);

        Context.Items["sessionCode"] = sessionCode;
        Context.Items["playerId"] = playerId;
        Context.Items["displayName"] = displayName;

        await history.UpsertParticipantAsync(sessionCode, playerId, displayName);

        await bus.PublishAsync("player.joined", new
        {
            sessionCode,
            playerId,
            displayName,
            at = DateTimeOffset.UtcNow
        });

        await BroadcastLobbyUpdate(sessionCode);

        if (status == "running")
        {
            var deadline = await store.GetDeadlineUtc(sessionCode);
            var taskTitle = await store.GetTaskTitle(sessionCode);
            var taskDescription = await store.GetTaskDescription(sessionCode);
            await Clients.Caller.SendAsync("sessionStarted", new
            {
                sessionCode,
                rulesetName = taskTitle ?? "Live Coding Task",
                taskTitle = taskTitle ?? "Live Coding Task",
                taskDescription = taskDescription ?? "",
                deadlineUtc = deadline?.UtcDateTime
            });
        }
    }

    public async Task StartSession(string sessionCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        if (!IsHostConnection())
            throw new HubException("Doar hostul poate porni sesiunea.");
        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var actorId = GetActorId();
        var hostId = await store.GetHostId(sessionCode);
        if (hostId != actorId)
            throw new HubException("Doar hostul poate porni sesiunea.");

        var status = await store.GetStatus(sessionCode);
        if (status == "ended")
            throw new HubException("Sesiunea este deja încheiată.");
        if (status != "lobby")
            throw new HubException("Sesiunea poate fi pornită doar din lobby.");

        var players = await store.GetPlayers(sessionCode);
        if (players.Count == 0)
            throw new HubException("Nu poți porni sesiunea fără participanți.");

        var ruleset = await store.GetRuleset(sessionCode);
        if (ruleset == null)
            throw new HubException("Sesiunea nu are un ruleset valid.");
        var taskTitle = await store.GetTaskTitle(sessionCode);
        var taskDescription = await store.GetTaskDescription(sessionCode);

        var timeLimit = await store.GetTimeLimitSeconds(sessionCode);
        if (timeLimit <= 0)
            throw new HubException("Sesiunea nu are un timp limită valid.");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeLimit).ToUnixTimeSeconds();
        await store.Db.HashSetAsync($"lc:session:{sessionCode}", new[]
        {
            new StackExchange.Redis.HashEntry("status", "running"),
            new StackExchange.Redis.HashEntry("deadlineUtc", deadline)
        });
        await history.RecordSessionStartedAsync(sessionCode);

        await bus.PublishAsync("session.started", new
        {
            sessionCode,
            deadline,
            at = DateTimeOffset.UtcNow
        });

        await Clients.Group(sessionCode).SendAsync("sessionStarted", new
        {
            sessionCode,
            rulesetName = taskTitle ?? ruleset.name ?? "Live Coding Task",
            taskTitle = taskTitle ?? ruleset.name ?? "Live Coding Task",
            taskDescription = taskDescription ?? "",
            deadlineUtc = DateTimeOffset.FromUnixTimeSeconds(deadline).UtcDateTime
        });
    }

    public async Task SubmitCode(string sessionCode, string studentCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        if (IsHostConnection())
            throw new HubException("Hostul nu poate trimite cod.");
        if (string.IsNullOrWhiteSpace(studentCode))
            throw new HubException("Codul sursă nu poate fi gol.");
        if (studentCode.Length > MaxStudentCodeLength)
            throw new HubException($"Codul sursă poate avea cel mult {MaxStudentCodeLength} de caractere.");
        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var playerId = GetActorId();
        if (!await store.PlayerExists(sessionCode, playerId))
            throw new HubException("Mai întâi trebuie să intri în sesiune.");

        var displayName = await GetDisplayName(sessionCode, playerId);
        var status = await store.GetStatus(sessionCode);
        if (status != "running")
            throw new HubException("Sesiunea nu este în desfășurare.");

        var deadline = await store.GetDeadlineUtc(sessionCode);
        if (deadline.HasValue && DateTimeOffset.UtcNow > deadline.Value)
            throw new HubException("Timpul pentru această sesiune a expirat.");

        var ruleset = await store.GetRuleset(sessionCode);
        if (ruleset == null)
            throw new HubException("Ruleset-ul sesiunii nu este disponibil.");

        var tree = CSharpSyntaxTree.ParseText(studentCode);
        var compilation = RoslynCompilationHelper.CreateCompilation(tree);
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        ValidationResult result;
        if (errors.Count > 0)
        {
                result = new ValidationResult
                {
                    Passed = false,
                    Violations = errors
                        .Select(error => new Violation("COMPILATION_ERROR", $"Codul nu compilează. Corectează eroarea de C# și retrimite soluția. Detaliu: {error}"))
                        .ToList()
                };
            }
        else
        {
            var index = RoslynSymbolIndex.Build(compilation);
            var engine = new RoslynRuleEngine(ruleset);
            result = engine.Evaluate(tree, compilation, index);
        }

        var totalRules = ruleset.rules?.Count ?? 0;
        var failedRules = result.Violations?.Count ?? 0;
        var points = Math.Max(0, (totalRules - failedRules) * 10);
        if (!result.Passed && failedRules == 0)
            points = 0;

        await store.SetScore(sessionCode, playerId, points);
        var scores = await store.GetScores(sessionCode);
        var currentScore = scores.GetValueOrDefault(playerId, 0);

        await history.RecordSubmissionAsync(
            sessionCode,
            playerId,
            displayName,
            studentCode,
            result,
            currentScore);

        await Clients.Caller.SendAsync("codeAck", new
        {
            passed = result.Passed,
            violations = result.Violations,
            pointsEarned = points,
            yourScore = currentScore
        });

        await bus.PublishAsync("code.submitted", new
        {
            sessionCode,
            playerId,
            passed = result.Passed,
            at = DateTimeOffset.UtcNow
        });

        var leaderboard = await BuildLeaderboard(sessionCode);
        await Clients.Group(sessionCode).SendAsync("leaderboard", new { leaderboard });

        await bus.PublishAsync("score.updated", new
        {
            sessionCode,
            scores,
            at = DateTimeOffset.UtcNow
        });
    }

    public async Task EndSession(string sessionCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        if (!IsHostConnection())
            throw new HubException("Doar hostul poate închide sesiunea.");
        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var actorId = GetActorId();
        var hostId = await store.GetHostId(sessionCode);
        if (hostId != actorId)
            throw new HubException("Doar hostul poate închide sesiunea.");

        var status = await store.GetStatus(sessionCode);
        if (status == "ended")
            throw new HubException("Sesiunea este deja încheiată.");

        await store.SetStatus(sessionCode, "ended");
        await history.RecordSessionEndedAsync(sessionCode);
        var leaderboard = await BuildLeaderboard(sessionCode);

        await bus.PublishAsync("session.ended", new
        {
            sessionCode,
            leaderboard,
            endedAt = DateTimeOffset.UtcNow
        });

        await Clients.Group(sessionCode).SendAsync("sessionEnded", new { leaderboard });
    }

    public async Task GetSessionState(string sessionCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        var status = await store.GetStatus(sessionCode);
        if (status == "unknown")
            throw new HubException("Sesiunea nu există.");

        if (!IsHostConnection())
        {
            var playerId = GetActorId();
            if (!await store.PlayerExists(sessionCode, playerId))
                throw new HubException("Mai întâi trebuie să intri în sesiune.");
        }

        var leaderboard = await BuildLeaderboard(sessionCode);
        var players = await store.GetPlayers(sessionCode);
        var deadline = await store.GetDeadlineUtc(sessionCode);
        var taskTitle = await store.GetTaskTitle(sessionCode);
        var taskDescription = await store.GetTaskDescription(sessionCode);

        await Clients.Caller.SendAsync("sessionState", new
        {
            status,
            rulesetName = taskTitle ?? "Live Coding Task",
            taskTitle = taskTitle ?? "Live Coding Task",
            taskDescription = taskDescription ?? "",
            deadlineUtc = deadline?.UtcDateTime,
            leaderboard,
            players = players.Select(p => new { id = p.Key, displayName = p.Value }),
            playerCount = players.Count
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetActorId();

        if (Context.Items.TryGetValue("sessionCode", out var codeObj) && codeObj is string sessionCode)
        {
            if (!IsHostConnection())
            {
                await store.RemovePlayer(sessionCode, playerId);

                await bus.PublishAsync("player.left", new
                {
                    sessionCode,
                    playerId,
                    at = DateTimeOffset.UtcNow
                });

                await BroadcastLobbyUpdate(sessionCode);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string GetActorId()
    {
        if (Context.Items.TryGetValue("playerId", out var id) && id is string s)
            return s;
        return Context.ConnectionId;
    }

    private bool IsHostConnection() =>
        Context.Items.TryGetValue("isHost", out var isHost)
        && isHost is bool value
        && value;

    private async Task<string> GetDisplayName(string sessionCode, string playerId)
    {
        if (Context.Items.TryGetValue("displayName", out var value) && value is string displayName && !string.IsNullOrWhiteSpace(displayName))
            return displayName;

        var players = await store.GetPlayers(sessionCode);
        return players.GetValueOrDefault(playerId, playerId);
    }

    private async Task BroadcastLobbyUpdate(string sessionCode)
    {
        var players = await store.GetPlayers(sessionCode);
        await Clients.Group(sessionCode).SendAsync("lobbyUpdate", new
        {
            players = players.Select(p => new { id = p.Key, displayName = p.Value }),
            playerCount = players.Count
        });
    }

    private async Task<List<LeaderboardEntry>> BuildLeaderboard(string sessionCode)
    {
        var lb = await store.GetLeaderboard(sessionCode);
        return lb.Values
            .OrderByDescending(x => x.Score)
            .ToList();
    }

    private void EnsureConnectionBoundToSession(string sessionCode)
    {
        if (!Context.Items.TryGetValue("sessionCode", out var value) || value is not string boundSessionCode || string.IsNullOrWhiteSpace(boundSessionCode))
            throw new HubException("Mai întâi trebuie să intri în sesiune.");

        if (!string.Equals(boundSessionCode, sessionCode, StringComparison.OrdinalIgnoreCase))
            throw new HubException("Conexiunea curentă nu aparține acestei sesiuni.");
    }

    private static string NormalizeSessionCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    private static string NormalizeDisplayName(string? displayName, string fallback = "") =>
        string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();

    private static void EnsureValidSessionCode(string sessionCode)
    {
        if (sessionCode.Length != SessionCodeLength || sessionCode.Any(ch => !char.IsLetterOrDigit(ch)))
            throw new HubException("Codul sesiunii este invalid.");
    }

    private static void EnsureValidDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new HubException("Numele afișat este obligatoriu.");

        if (displayName.Length > MaxDisplayNameLength)
            throw new HubException($"Numele afișat poate avea cel mult {MaxDisplayNameLength} de caractere.");
    }

    private static string BuildHostId(string displayName) =>
        "host_" + Uri.EscapeDataString(displayName.ToLowerInvariant());

    private static string BuildPlayerId(string displayName) =>
        "usr_" + Uri.EscapeDataString(displayName.ToLowerInvariant());
}
