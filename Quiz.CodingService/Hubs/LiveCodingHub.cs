using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Quiz.CodingService.Engine;
using Quiz.CodingService.Messaging;
using Quiz.CodingService.State;

namespace Quiz.CodingService.Hubs;

public sealed class LiveCodingHub(
    LiveCodingSessionStateStore store,
    RabbitBus bus,
    ILogger<LiveCodingHub> log
) : Hub
{
    public async Task Join(string sessionCode, string displayName)
    {
        sessionCode = Sanitize(sessionCode);
        displayName = displayName.Trim();

        if (!await store.SessionExists(sessionCode))
        {
            await Clients.Caller.SendAsync("error", new { message = "Session not found." });
            return;
        }

        var status = await store.GetStatus(sessionCode);
        if (status == "ended")
        {
            await Clients.Caller.SendAsync("error", new { message = "Session already ended." });
            return;
        }

        var playerId = "usr_" + Uri.EscapeDataString(displayName.ToLowerInvariant());

        await store.AddPlayer(sessionCode, playerId, displayName);
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);

        var hostId = await store.GetHostId(sessionCode);
        if (hostId == "__pending__")
        {
            await store.SetHostId(sessionCode, playerId);
            log.LogInformation("Assigned Player {PlayerId} as Host for session {Code}", playerId, sessionCode);
        }

        log.LogInformation("Player {PlayerId} ({Name}) joined session {Code}", playerId, displayName, sessionCode);

        Context.Items["sessionCode"] = sessionCode;
        Context.Items["playerId"] = playerId;

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
            await Clients.Caller.SendAsync("sessionStarted", new
            {
                sessionCode,
                rulesetName = "Live Coding Task"
            });
        }
    }

    public async Task StartSession(string sessionCode)
    {
        sessionCode = Sanitize(sessionCode);
        var playerId = GetPlayerId();

        var hostId = await store.GetHostId(sessionCode);
        if (hostId != playerId)
        {
            await Clients.Caller.SendAsync("error", new { message = "Only the host can start the session." });
            return;
        }

        var status = await store.GetStatus(sessionCode);
        if (status != "lobby")
        {
            await Clients.Caller.SendAsync("error", new { message = $"Cannot start: session is '{status}'." });
            return;
        }

        var ruleset = await store.GetRuleset(sessionCode);
        if (ruleset == null)
        {
            await Clients.Caller.SendAsync("error", new { message = "Ruleset not found." });
            return;
        }

        var timeLimitVal = await store.Db.HashGetAsync($"lc:session:{sessionCode}", "timeLimitSeconds");
        int timeLimit = timeLimitVal.IsNullOrEmpty ? 600 : (int)timeLimitVal;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeLimit).ToUnixTimeSeconds();

        await store.Db.HashSetAsync($"lc:session:{sessionCode}", new[] {
            new StackExchange.Redis.HashEntry("status", "running"),
            new StackExchange.Redis.HashEntry("deadlineUtc", deadline)
        });

        await bus.PublishAsync("session.started", new
        {
            sessionCode,
            deadline,
            at = DateTimeOffset.UtcNow
        });

        await Clients.Group(sessionCode).SendAsync("sessionStarted", new
        {
            sessionCode,
            rulesetName = "Live Coding Task",
            deadlineUtc = DateTimeOffset.FromUnixTimeSeconds(deadline).UtcDateTime
        });
    }

    public async Task SubmitCode(string sessionCode, string studentCode)
    {
        sessionCode = Sanitize(sessionCode);
        var playerId = GetPlayerId();

        var status = await store.GetStatus(sessionCode);
        if (status != "running")
        {
            await Clients.Caller.SendAsync("error", new { message = "Session not running." });
            return;
        }

        var ruleset = await store.GetRuleset(sessionCode);
        if (ruleset == null)
        {
            await Clients.Caller.SendAsync("error", new { message = "Ruleset missing." });
            return;
        }

        // Evaluate using RoslynRuleEngine
        var tree = CSharpSyntaxTree.ParseText(studentCode);
        var compilation = RoslynCompilationHelper.CreateCompilation(tree);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        ValidationResult result;
        if (errors.Count > 0)
        {
            result = new ValidationResult 
            { 
                Passed = false, 
                Violations = errors.Select(e => new Violation("COMPILATION_ERROR", e.ToString())).ToList() 
            };
        }
        else
        {
            var index = RoslynSymbolIndex.Build(compilation);
            var engine = new RoslynRuleEngine(ruleset);
            result = engine.Evaluate(tree, compilation, index);
        }
        
        // Calculate points based on passed rules
        // Simplistic point logic: total rules minus violations
        int totalRules = ruleset.rules?.Count ?? 0;
        int failedRules = result.Violations?.Count ?? 0;
        int points = Math.Max(0, (totalRules - failedRules) * 10); // each rule passed gets 10 points
        
        // Wait, if it failed compilation, it's 0 points
        if (!result.Passed && failedRules == 0) 
        {
            // Compilation error or structural error
            points = 0;
        }

        await store.SetScore(sessionCode, playerId, points);
        var scores = await store.GetScores(sessionCode);

        await Clients.Caller.SendAsync("codeAck", new
        {
            passed = result.Passed,
            violations = result.Violations,
            pointsEarned = points,
            yourScore = points
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
        sessionCode = Sanitize(sessionCode);

        await store.SetStatus(sessionCode, "ended");
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
        sessionCode = Sanitize(sessionCode);

        var status = await store.GetStatus(sessionCode);
        if (status == "unknown")
        {
            await Clients.Caller.SendAsync("error", new { message = "Session not found." });
            return;
        }

        var ruleset = await store.GetRuleset(sessionCode);
        var leaderboard = await BuildLeaderboard(sessionCode);
        var players = await store.GetPlayers(sessionCode);
        var deadlineVal = await store.Db.HashGetAsync($"lc:session:{sessionCode}", "deadlineUtc");
        DateTime? deadline = null;
        if (!deadlineVal.IsNullOrEmpty && long.TryParse(deadlineVal.ToString(), out var dl))
        {
            deadline = DateTimeOffset.FromUnixTimeSeconds(dl).UtcDateTime;
        }

        await Clients.Caller.SendAsync("sessionState", new
        {
            status = status,
            rulesetName = "Live Coding Task",
            deadlineUtc = deadline,
            leaderboard,
            players = players.Select(p => new { id = p.Key, displayName = p.Value }),
            playerCount = players.Count
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = GetPlayerId();

        if (Context.Items.TryGetValue("sessionCode", out var codeObj) && codeObj is string sessionCode)
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

        await base.OnDisconnectedAsync(exception);
    }

    private string GetPlayerId()
    {
        if (Context.Items.TryGetValue("playerId", out var id) && id is string s)
            return s;
        return Context.ConnectionId;
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

    private static string Sanitize(string code) => (code ?? "").Trim().ToUpperInvariant();
}
