using Microsoft.AspNetCore.SignalR;
using Quiz.LiveSessionService.Messaging;
using Quiz.LiveSessionService.Services;
using Quiz.LiveSessionService.State;

namespace Quiz.LiveSessionService.Hubs;

/// <summary>
/// SignalR hub for real-time quiz sessions.
///
/// Client → Server methods:
///   Join(code, displayName)
///   StartSession(code)
///   NextQuestion(code)
///   SubmitAnswer(code, questionIndex, payload)
///   EndSession(code)
///   GetSessionState(code)
///
/// Server → Client events (via SendAsync):
///   "lobbyUpdate"        { players, playerCount }
///   "sessionStarted"     { sessionCode, totalQuestions, quiz }
///   "questionStarted"    { index, question, deadlineUtc, timeLimitSeconds }
///   "questionEnded"      { index, leaderboard }
///   "answerAck"          { isCorrect, pointsEarned, yourScore } — only to caller
///   "leaderboard"        { entries[] }
///   "sessionEnded"       { leaderboard }
///   "error"              { message }
/// </summary>
public sealed class LiveQuizHub(
    LiveSessionStateStore store,
    RabbitBus bus,
    QuizServiceClient quizClient,
    ILogger<LiveQuizHub> log
) : Hub
{
    // ── Join ──────────────────────────────────────────────────────────────────
    /// <summary>Player joins the lobby. Must be called before anything else.</summary>
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

        var playerId = Context.ConnectionId;
        await store.AddPlayer(sessionCode, playerId, displayName);
        await Groups.AddToGroupAsync(playerId, sessionCode);

        log.LogInformation("Player {PlayerId} ({Name}) joined session {Code}", playerId, displayName, sessionCode);

        // Store session code in connection context (for disconnect cleanup)
        Context.Items["sessionCode"] = sessionCode;

        // Publish to RabbitMQ
        await bus.PublishAsync("player.joined", new
        {
            sessionCode,
            playerId,
            displayName,
            at = DateTimeOffset.UtcNow
        });

        // Broadcast lobby update to all in group
        await BroadcastLobbyUpdate(sessionCode);

        // If session already running, send current state to late joiner
        if (status == "running")
        {
            var info = await store.GetSessionInfo(sessionCode);
            var snap = await store.GetPublicSnapshot(sessionCode);
            var question = info?.CurrentIndex >= 0
                ? await store.GetQuestion(sessionCode, info.CurrentIndex)
                : null;

            await Clients.Caller.SendAsync("sessionStarted", new
            {
                sessionCode,
                totalQuestions = info?.TotalQuestions ?? 0,
                quiz = snap
            });

            if (question != null && info != null)
            {
                await Clients.Caller.SendAsync("questionStarted", new
                {
                    index = info.CurrentIndex,
                    question,
                    deadlineUtc = info.QuestionDeadline,
                    timeLimitSeconds = snap?.TimeLimitSeconds ?? 30
                });
            }
        }
    }

    // ── Start session ─────────────────────────────────────────────────────────
    /// <summary>Host starts the session. Fetches quiz, stores snapshot, begins Q0.</summary>
    public async Task StartSession(string sessionCode)
    {
        sessionCode = Sanitize(sessionCode);
        var playerId = Context.ConnectionId;

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

        var quizId = await store.GetQuizId(sessionCode);
        if (string.IsNullOrWhiteSpace(quizId))
        {
            await Clients.Caller.SendAsync("error", new { message = "Invalid session (no quizId)." });
            return;
        }

        // Fetch quiz from QuizService
        QuizSnapshot snap;
        try
        {
            snap = await quizClient.FetchQuizSnapshot(quizId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to fetch quiz {QuizId}", quizId);
            await Clients.Caller.SendAsync("error", new { message = "Failed to load quiz." });
            return;
        }

        if (snap.Questions.Count == 0)
        {
            await Clients.Caller.SendAsync("error", new { message = "Quiz has no questions." });
            return;
        }

        await store.StoreQuizSnapshot(sessionCode, snap);
        await store.SetStatus(sessionCode, "running");
        await store.SetCurrentIndex(sessionCode, 0, snap.TimeLimitSeconds);

        log.LogInformation("Session {Code} started — quiz {QuizId}, {Count} questions", sessionCode, quizId, snap.Questions.Count);

        // Publish to RabbitMQ
        await bus.PublishAsync("session.started", new
        {
            sessionCode,
            quizId,
            totalQuestions = snap.Questions.Count,
            at = DateTimeOffset.UtcNow
        });

        // Broadcast: session started
        var pubSnap = await store.GetPublicSnapshot(sessionCode);
        await Clients.Group(sessionCode).SendAsync("sessionStarted", new
        {
            sessionCode,
            totalQuestions = snap.Questions.Count,
            quiz = pubSnap
        });

        // Broadcast: first question
        await PushQuestion(sessionCode, 0, snap.TimeLimitSeconds);
    }

    // ── Next question ─────────────────────────────────────────────────────────
    /// <summary>Host advances to the next question.</summary>
    public async Task NextQuestion(string sessionCode)
    {
        sessionCode = Sanitize(sessionCode);
        var playerId = Context.ConnectionId;

        var hostId = await store.GetHostId(sessionCode);
        if (hostId != playerId)
        {
            await Clients.Caller.SendAsync("error", new { message = "Only the host can advance questions." });
            return;
        }

        var info = await store.GetSessionInfo(sessionCode);
        if (info is null || info.Status != "running")
        {
            await Clients.Caller.SendAsync("error", new { message = "Session is not running." });
            return;
        }

        var snap = await store.GetPublicSnapshot(sessionCode);
        var timeLimitSeconds = snap?.TimeLimitSeconds ?? 30;

        // Publish leaderboard for current question
        var leaderboard = await BuildLeaderboard(sessionCode);
        await Clients.Group(sessionCode).SendAsync("questionEnded", new
        {
            index = info.CurrentIndex,
            leaderboard
        });

        await bus.PublishAsync("question.ended", new
        {
            sessionCode,
            questionIndex = info.CurrentIndex,
            at = DateTimeOffset.UtcNow
        });

        var nextIndex = info.CurrentIndex + 1;
        if (nextIndex >= info.TotalQuestions)
        {
            await EndSession(sessionCode);
            return;
        }

        await store.SetCurrentIndex(sessionCode, nextIndex, timeLimitSeconds);

        await bus.PublishAsync("question.started", new
        {
            sessionCode,
            questionIndex = nextIndex,
            at = DateTimeOffset.UtcNow
        });

        await PushQuestion(sessionCode, nextIndex, timeLimitSeconds);
    }

    // ── Submit answer ─────────────────────────────────────────────────────────
    /// <summary>Player submits an answer. Evaluated server-side, score updated in Redis.</summary>
    public async Task SubmitAnswer(string sessionCode, int questionIndex, AnswerPayload payload)
    {
        sessionCode = Sanitize(sessionCode);
        var playerId = Context.ConnectionId;

        var info = await store.GetSessionInfo(sessionCode);
        if (info is null || info.Status != "running")
        {
            await Clients.Caller.SendAsync("error", new { message = "Session not running." });
            return;
        }

        if (questionIndex != info.CurrentIndex)
        {
            await Clients.Caller.SendAsync("error", new { message = "Question index mismatch." });
            return;
        }

        // Check deadline
        if (DateTimeOffset.UtcNow > info.QuestionDeadline)
        {
            await Clients.Caller.SendAsync("answerAck", new
            {
                isCorrect = false,
                pointsEarned = 0,
                alreadyAnswered = false,
                expired = true,
                yourScore = (await store.GetScores(sessionCode)).GetValueOrDefault(playerId, 0)
            });
            return;
        }

        var result = await store.SaveAndCheckAnswer(sessionCode, questionIndex, playerId, payload);

        var scores = await store.GetScores(sessionCode);

        // Ack to caller only
        await Clients.Caller.SendAsync("answerAck", new
        {
            isCorrect = result.IsCorrect,
            pointsEarned = result.PointsEarned,
            alreadyAnswered = result.AlreadyAnswered,
            expired = false,
            yourScore = scores.GetValueOrDefault(playerId, 0)
        });

        // Publish event
        await bus.PublishAsync("answer.submitted", new
        {
            sessionCode,
            playerId,
            questionIndex,
            isCorrect = result.IsCorrect,
            at = DateTimeOffset.UtcNow
        });

        // Broadcast updated leaderboard to all (scores only, no answers)
        var leaderboard = await BuildLeaderboard(sessionCode);
        await Clients.Group(sessionCode).SendAsync("leaderboard", new { leaderboard });

        await bus.PublishAsync("score.updated", new
        {
            sessionCode,
            scores,
            at = DateTimeOffset.UtcNow
        });

        // Check if all players answered
        var answeredCount = await store.GetAnsweredCount(sessionCode, questionIndex);
        var playerCount = await store.GetPlayerCount(sessionCode);
        if (answeredCount >= playerCount && playerCount > 0)
        {
            log.LogInformation("All {Count} players answered Q{Idx} in session {Code}", playerCount, questionIndex, sessionCode);
            await bus.PublishAsync("question.all_answered", new
            {
                sessionCode,
                questionIndex,
                at = DateTimeOffset.UtcNow
            });
        }
    }

    // ── End session ───────────────────────────────────────────────────────────
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

        log.LogInformation("Session {Code} ended", sessionCode);
    }

    // ── Get state (reconnect / polling fallback) ──────────────────────────────
    public async Task GetSessionState(string sessionCode)
    {
        sessionCode = Sanitize(sessionCode);

        var info = await store.GetSessionInfo(sessionCode);
        if (info is null)
        {
            await Clients.Caller.SendAsync("error", new { message = "Session not found." });
            return;
        }

        var snap = await store.GetPublicSnapshot(sessionCode);
        var leaderboard = await BuildLeaderboard(sessionCode);
        var players = await store.GetPlayers(sessionCode);

        QuestionPublicSnapshot? currentQuestion = null;
        if (info.CurrentIndex >= 0)
            currentQuestion = await store.GetQuestion(sessionCode, info.CurrentIndex);

        await Clients.Caller.SendAsync("sessionState", new
        {
            status = info.Status,
            currentIndex = info.CurrentIndex,
            totalQuestions = info.TotalQuestions,
            quiz = snap,
            currentQuestion,
            deadlineUtc = info.QuestionDeadline,
            leaderboard,
            playerCount = players.Count
        });
    }

    // ── Disconnect ────────────────────────────────────────────────────────────
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var playerId = Context.ConnectionId;

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
            log.LogInformation("Player {PlayerId} disconnected from session {Code}", playerId, sessionCode);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task PushQuestion(string sessionCode, int index, int timeLimitSeconds)
    {
        var question = await store.GetQuestion(sessionCode, index);
        var info = await store.GetSessionInfo(sessionCode);

        await Clients.Group(sessionCode).SendAsync("questionStarted", new
        {
            index,
            question,
            deadlineUtc = info?.QuestionDeadline ?? DateTimeOffset.UtcNow.AddSeconds(timeLimitSeconds),
            timeLimitSeconds
        });
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