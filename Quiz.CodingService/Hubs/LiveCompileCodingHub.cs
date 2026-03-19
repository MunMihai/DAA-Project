using Microsoft.AspNetCore.SignalR;
using Quiz.CodingService.Messaging;
using Quiz.CodingService.Models;
using Quiz.CodingService.Services;
using Quiz.CodingService.State;

namespace Quiz.CodingService.Hubs;

public sealed class LiveCompileCodingHub(
    LiveCompileCodingSessionStateStore store,
    CompileCodeExecutionService executor,
    LiveCompileCodingHistoryService history,
    RabbitBus bus,
    ILogger<LiveCompileCodingHub> log
) : Hub
{
    private const int SessionCodeLength = 6;
    private const int MaxDisplayNameLength = 50;
    private const int MaxSourceCodeLength = 100_000;

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

        log.LogInformation("Compile host {HostId} joined session {Code}", hostId, sessionCode);
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

        Context.Items["sessionCode"] = sessionCode;
        Context.Items["playerId"] = playerId;
        Context.Items["displayName"] = displayName;

        await history.UpsertParticipantAsync(sessionCode, playerId, displayName, Context.ConnectionAborted);
        await BroadcastLobbyUpdate(sessionCode);

        await bus.PublishAsync("compile.player.joined", new
        {
            sessionCode,
            playerId,
            displayName,
            at = DateTimeOffset.UtcNow
        });

        if (status == "running")
        {
            var definition = await store.GetPublicDefinition(sessionCode);
            var deadline = await store.GetDeadlineUtc(sessionCode);
            await Clients.Caller.SendAsync("sessionStarted", new
            {
                sessionCode,
                title = definition?.Title ?? sessionCode,
                allowedLanguages = definition?.AllowedLanguages ?? [],
                tasks = definition?.Tasks ?? [],
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

        var definition = await store.GetPublicDefinition(sessionCode);
        if (definition is null || definition.Tasks.Count == 0)
            throw new HubException("Sesiunea nu are sarcini configurate.");

        var timeLimit = await store.GetTimeLimitSeconds(sessionCode);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeLimit);

        await store.SetStatus(sessionCode, "running");
        await store.SetDeadlineUtc(sessionCode, deadline);
        await history.RecordSessionStartedAsync(sessionCode, Context.ConnectionAborted);

        await Clients.Group(sessionCode).SendAsync("sessionStarted", new
        {
            sessionCode,
            title = definition.Title,
            allowedLanguages = definition.AllowedLanguages,
            tasks = definition.Tasks,
            deadlineUtc = deadline.UtcDateTime
        });

        await bus.PublishAsync("compile.session.started", new
        {
            sessionCode,
            at = DateTimeOffset.UtcNow
        });
    }

    public async Task SubmitSolution(string sessionCode, string taskId, string language, string sourceCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        if (IsHostConnection())
            throw new HubException("Hostul nu poate trimite soluții.");
        if (string.IsNullOrWhiteSpace(taskId))
            throw new HubException("Sarcina selectată este invalidă.");
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new HubException("Codul sursă nu poate fi gol.");
        if (sourceCode.Length > MaxSourceCodeLength)
            throw new HubException($"Codul sursă poate avea cel mult {MaxSourceCodeLength} de caractere.");

        var normalizedLanguage = CompileCodingLanguages.Normalize(language);
        if (!CompileCodingLanguages.IsSupported(normalizedLanguage))
            throw new HubException("Limbajul selectat nu este suportat.");

        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var playerId = GetActorId();
        if (!await store.PlayerExists(sessionCode, playerId))
            throw new HubException("Mai întâi trebuie să intri în sesiune.");

        var status = await store.GetStatus(sessionCode);
        if (status != "running")
            throw new HubException("Sesiunea nu este în desfășurare.");

        var deadline = await store.GetDeadlineUtc(sessionCode);
        if (deadline.HasValue && DateTimeOffset.UtcNow > deadline.Value)
            throw new HubException("Timpul pentru această sesiune a expirat.");

        var definition = await store.GetDefinition(sessionCode);
        if (definition is null)
            throw new HubException("Configurația sesiunii nu este disponibilă.");
        if (!definition.AllowedLanguages.Contains(normalizedLanguage))
            throw new HubException("Limbajul selectat nu este permis în această sesiune.");

        var task = definition.Tasks.FirstOrDefault(x => x.Id == taskId);
        if (task is null)
            throw new HubException("Sarcina selectată nu există.");

        var evaluation = await executor.EvaluateAsync(task, normalizedLanguage, sourceCode, Context.ConnectionAborted);
        var scoreUpdate = await store.UpdateTaskBestScore(sessionCode, playerId, task.Id, evaluation.BestTaskScore);
        evaluation.ScoreDelta = scoreUpdate.ScoreDelta;
        evaluation.BestTaskScore = scoreUpdate.BestTaskScore;
        evaluation.TotalScore = scoreUpdate.TotalScore;

        var displayName = await GetDisplayName(sessionCode, playerId);
        await history.RecordSubmissionAsync(
            sessionCode,
            playerId,
            displayName,
            task,
            normalizedLanguage,
            sourceCode,
            evaluation,
            Context.ConnectionAborted);

        await Clients.Caller.SendAsync("solutionAck", evaluation);

        var leaderboard = await BuildLeaderboard(sessionCode);
        await Clients.Group(sessionCode).SendAsync("leaderboard", new { leaderboard });

        await bus.PublishAsync("compile.solution.submitted", new
        {
            sessionCode,
            taskId = task.Id,
            playerId,
            language = normalizedLanguage,
            passed = evaluation.Passed,
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
        await history.RecordSessionEndedAsync(sessionCode, Context.ConnectionAborted);
        var leaderboard = await BuildLeaderboard(sessionCode);

        await Clients.Group(sessionCode).SendAsync("sessionEnded", new { leaderboard });

        await bus.PublishAsync("compile.session.ended", new
        {
            sessionCode,
            at = DateTimeOffset.UtcNow
        });
    }

    public async Task GetSessionState(string sessionCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        var status = await store.GetStatus(sessionCode);
        if (status == "unknown")
            throw new HubException("Sesiunea nu există.");

        var definition = await store.GetPublicDefinition(sessionCode);
        if (definition is null)
            throw new HubException("Configurația sesiunii nu este disponibilă.");

        var players = await store.GetPlayers(sessionCode);
        var leaderboard = await BuildLeaderboard(sessionCode);
        var deadline = await store.GetDeadlineUtc(sessionCode);
        Dictionary<string, int> playerTaskScores = [];

        if (!IsHostConnection())
        {
            var playerId = GetActorId();
            if (!await store.PlayerExists(sessionCode, playerId))
                throw new HubException("Mai întâi trebuie să intri în sesiune.");

            playerTaskScores = await store.GetPlayerTaskScores(sessionCode, playerId);
        }

        await Clients.Caller.SendAsync("sessionState", new
        {
            status,
            title = definition.Title,
            allowedLanguages = definition.AllowedLanguages,
            tasks = definition.Tasks,
            deadlineUtc = deadline?.UtcDateTime,
            leaderboard,
            players = players.Select(x => new { id = x.Key, displayName = x.Value }),
            playerCount = players.Count,
            playerTaskScores
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetActorId();

        if (Context.Items.TryGetValue("sessionCode", out var value) && value is string sessionCode && !IsHostConnection())
        {
            await store.RemovePlayer(sessionCode, playerId);
            await BroadcastLobbyUpdate(sessionCode);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string GetActorId()
    {
        if (Context.Items.TryGetValue("playerId", out var id) && id is string actorId)
            return actorId;
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
            players = players.Select(x => new { id = x.Key, displayName = x.Value }),
            playerCount = players.Count
        });
    }

    private async Task<List<CompileLeaderboardEntry>> BuildLeaderboard(string sessionCode)
    {
        var leaderboard = await store.GetLeaderboard(sessionCode);
        return leaderboard.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.DisplayName)
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

    private static string BuildHostId(string displayName) => $"host_{Uri.EscapeDataString(displayName.ToLowerInvariant())}";

    private static string BuildPlayerId(string displayName) => $"usr_{Uri.EscapeDataString(displayName.ToLowerInvariant())}";
}
