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
///   FetchNextQuestion(code)
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
    LiveQuizHistoryService history,
    ILogger<LiveQuizHub> log
) : Hub
{
    private const int SessionCodeLength = 6;
    private const int MaxDisplayNameLength = 50;
    private const int MaxTextAnswerLength = 1000;

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

    /// <summary>Player joins the lobby. Must be called before anything else.</summary>
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
            var info = await store.GetSessionInfo(sessionCode);
            var snap = await store.GetPublicSnapshot(sessionCode);
            var playerIndex = await store.GetPlayerIndex(sessionCode, playerId);

            var question = playerIndex >= 0 && info != null && playerIndex < info.TotalQuestions
                ? await store.GetQuestion(sessionCode, playerIndex)
                : null;

            await Clients.Caller.SendAsync("sessionStarted", new
            {
                sessionCode,
                totalQuestions = info?.TotalQuestions ?? 0,
                quiz = snap,
                deadlineUtc = info?.SessionDeadline
            });

            if (question != null && info != null)
            {
                await Clients.Caller.SendAsync("questionStarted", new
                {
                    index = playerIndex,
                    question,
                    deadlineUtc = info.SessionDeadline,
                    timeLimitSeconds = snap?.TimeLimitSeconds ?? 30
                });
            }
        }
    }

    /// <summary>Host starts the session. Fetches quiz, stores snapshot, begins Q0.</summary>
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

        var playerCount = await store.GetPlayerCount(sessionCode);
        if (playerCount == 0)
            throw new HubException("Nu poți porni sesiunea fără participanți.");

        var quizId = await store.GetQuizId(sessionCode);
        if (string.IsNullOrWhiteSpace(quizId))
            throw new HubException("Sesiunea nu are un quiz valid asociat.");

        QuizSnapshot snap;
        try
        {
            snap = await quizClient.FetchQuizSnapshot(quizId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to fetch quiz {QuizId}", quizId);
            throw new HubException("Quiz-ul nu a putut fi încărcat.");
        }

        if (snap.Questions.Count == 0)
            throw new HubException("Quiz-ul selectat nu conține întrebări.");

        await store.StoreQuizSnapshot(sessionCode, snap);
        await store.SetStatus(sessionCode, "running");
        await store.SetSessionDeadline(sessionCode, snap.TimeLimitSeconds);
        await history.RecordSessionStartedAsync(sessionCode, snap);

        log.LogInformation("Session {Code} started — quiz {QuizId}, {Count} questions", sessionCode, quizId, snap.Questions.Count);

        await bus.PublishAsync("session.started", new
        {
            sessionCode,
            quizId,
            totalQuestions = snap.Questions.Count,
            at = DateTimeOffset.UtcNow
        });

        var sessionInfo = await store.GetSessionInfo(sessionCode);
        var pubSnap = await store.GetPublicSnapshot(sessionCode);
        await Clients.Group(sessionCode).SendAsync("sessionStarted", new
        {
            sessionCode,
            totalQuestions = snap.Questions.Count,
            quiz = pubSnap,
            deadlineUtc = sessionInfo?.SessionDeadline
        });

        var q0 = await store.GetQuestion(sessionCode, 0);
        if (q0 != null)
        {
            await Clients.Group(sessionCode).SendAsync("questionStarted", new
            {
                index = 0,
                question = q0,
                deadlineUtc = sessionInfo?.SessionDeadline,
                timeLimitSeconds = snap.TimeLimitSeconds
            });
        }
    }

    /// <summary>Player requests their next question in the individualized flow.</summary>
    public async Task FetchNextQuestion(string sessionCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        if (IsHostConnection())
            throw new HubException("Hostul nu poate cere întrebările unui participant.");

        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var playerId = GetActorId();
        if (!await store.PlayerExists(sessionCode, playerId))
            throw new HubException("Mai întâi trebuie să intri în sesiune.");

        var info = await store.GetSessionInfo(sessionCode);
        if (info is null)
            throw new HubException("Sesiunea nu există.");
        if (info.Status != "running")
            throw new HubException("Sesiunea nu este în desfășurare.");

        var playerIndex = await store.GetPlayerIndex(sessionCode, playerId);
        if (playerIndex >= info.TotalQuestions)
        {
            await Clients.Caller.SendAsync("playerFinished");
            return;
        }

        var snap = await store.GetPublicSnapshot(sessionCode);
        var question = await store.GetQuestion(sessionCode, playerIndex);
        if (question is null)
            throw new HubException("Întrebarea următoare nu este disponibilă.");

        await Clients.Caller.SendAsync("questionStarted", new
        {
            index = playerIndex,
            question,
            deadlineUtc = info.SessionDeadline,
            timeLimitSeconds = snap?.TimeLimitSeconds ?? 30
        });
    }

    /// <summary>Player submits an answer. Evaluated server-side, score updated in Redis.</summary>
    public async Task SubmitAnswer(string sessionCode, int questionIndex, AnswerPayload payload)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        if (IsHostConnection())
            throw new HubException("Hostul nu poate trimite răspunsuri.");
        if (payload is null)
            throw new HubException("Răspunsul nu a fost trimis corect.");

        if (!await store.SessionExists(sessionCode))
            throw new HubException("Sesiunea nu există.");

        var playerId = GetActorId();
        if (!await store.PlayerExists(sessionCode, playerId))
            throw new HubException("Mai întâi trebuie să intri în sesiune.");

        var displayName = await GetDisplayName(sessionCode, playerId);
        var info = await store.GetSessionInfo(sessionCode);
        if (info is null)
            throw new HubException("Sesiunea nu există.");
        if (info.Status != "running")
            throw new HubException("Sesiunea nu este în desfășurare.");
        if (questionIndex < 0 || questionIndex >= info.TotalQuestions)
            throw new HubException("Întrebarea selectată nu este validă.");

        var playerIndex = await store.GetPlayerIndex(sessionCode, playerId);
        if (questionIndex != playerIndex)
            throw new HubException("Întrebarea trimisă nu corespunde progresului tău curent.");

        var question = await store.GetQuestion(sessionCode, questionIndex);
        if (question is null)
            throw new HubException("Întrebarea nu mai este disponibilă.");

        var correctQuestion = await store.GetCorrectQuestion(sessionCode, questionIndex);

        var payloadError = ValidateAnswerPayload(question, payload);
        if (payloadError is not null)
            throw new HubException(payloadError);

        if (DateTimeOffset.UtcNow > info.SessionDeadline)
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

        if (!result.AlreadyAnswered)
        {
            await store.IncrementPlayerIndex(sessionCode, playerId);
        }

        var scores = await store.GetScores(sessionCode);
        var currentScore = scores.GetValueOrDefault(playerId, 0);

        if (!result.AlreadyAnswered)
        {
            await history.RecordAnswerAsync(
                sessionCode,
                playerId,
                displayName,
                questionIndex,
                question,
                correctQuestion,
                payload,
                result,
                currentScore);
        }

        await Clients.Caller.SendAsync("answerAck", new
        {
            isCorrect = result.IsCorrect,
            pointsEarned = result.PointsEarned,
            alreadyAnswered = result.AlreadyAnswered,
            expired = false,
            yourScore = currentScore
        });

        await bus.PublishAsync("answer.submitted", new
        {
            sessionCode,
            playerId,
            questionIndex,
            isCorrect = result.IsCorrect,
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

        log.LogInformation("Session {Code} ended", sessionCode);
    }

    public async Task GetSessionState(string sessionCode)
    {
        sessionCode = NormalizeSessionCode(sessionCode);
        EnsureValidSessionCode(sessionCode);
        EnsureConnectionBoundToSession(sessionCode);

        var info = await store.GetSessionInfo(sessionCode);
        if (info is null)
            throw new HubException("Sesiunea nu există.");

        var snap = await store.GetPublicSnapshot(sessionCode);
        var leaderboard = await BuildLeaderboard(sessionCode);
        var players = await store.GetPlayers(sessionCode);
        var currentQuestion = default(QuestionPublicSnapshot?);
        var playerIndex = -1;

        if (!IsHostConnection())
        {
            var playerId = GetActorId();
            if (!await store.PlayerExists(sessionCode, playerId))
                throw new HubException("Mai întâi trebuie să intri în sesiune.");

            playerIndex = await store.GetPlayerIndex(sessionCode, playerId);

            if (playerIndex >= 0 && playerIndex < info.TotalQuestions)
                currentQuestion = await store.GetQuestion(sessionCode, playerIndex);
        }

        await Clients.Caller.SendAsync("sessionState", new
        {
            status = info.Status,
            currentIndex = playerIndex,
            totalQuestions = info.TotalQuestions,
            quiz = snap,
            currentQuestion,
            deadlineUtc = info.SessionDeadline,
            leaderboard,
            players = players.Select(p => new { id = p.Key, displayName = p.Value }),
            playerCount = players.Count,
            playerFinished = info.TotalQuestions > 0 && playerIndex >= info.TotalQuestions
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
                log.LogInformation("Player {PlayerId} disconnected from session {Code}", playerId, sessionCode);
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

    private static string? ValidateAnswerPayload(QuestionPublicSnapshot question, AnswerPayload payload) =>
        question.Type switch
        {
            0 => payload.BoolAnswer is null
                ? "Selectează Adevărat sau Fals."
                : null,
            1 => !IsSingleChoiceValid(question, payload.SingleOptionId)
                ? "Selectează o opțiune validă."
                : null,
            2 => !AreMultipleChoicesValid(question, payload.MultipleOptionIds)
                ? "Selectează cel puțin o opțiune validă."
                : null,
            3 => string.IsNullOrWhiteSpace(payload.TextAnswer)
                ? "Introdu un răspuns text."
                : payload.TextAnswer.Trim().Length > MaxTextAnswerLength
                    ? $"Răspunsul text poate avea cel mult {MaxTextAnswerLength} caractere."
                    : null,
            _ => "Tipul întrebării nu este suportat."
        };

    private static bool IsSingleChoiceValid(QuestionPublicSnapshot question, string? optionId) =>
        !string.IsNullOrWhiteSpace(optionId)
        && question.Options.Any(option => option.Id == optionId);

    private static bool AreMultipleChoicesValid(QuestionPublicSnapshot question, List<string>? optionIds)
    {
        if (optionIds is null || optionIds.Count == 0)
            return false;

        var allowedIds = question.Options
            .Select(option => option.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        return optionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .All(allowedIds.Contains);
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
