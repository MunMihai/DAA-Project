using System.Text.Json;
using StackExchange.Redis;

namespace Quiz.LiveSessionService.State;

/// <summary>
/// All live-session state lives in Redis (TTL 6h).
/// Keys layout:
///   ls:session:{code}        → Hash  { quizId, status, currentIndex, hostId, createdAt, questionDeadline }
///   ls:players:{code}        → Hash  { playerId → displayName }
///   ls:scores:{code}         → Hash  { playerId → int }
///   ls:quiz:{code}           → String (JSON QuizSnapshot — public, no correct answers)
///   ls:quiz:correct:{code}   → String (JSON QuizCorrectSnapshot — private, server-only)
///   ls:answers:{code}:{idx}  → Hash  { playerId → JSON AnswerPayload }
///   ls:answered:{code}:{idx} → Set   { playerId } (players who already answered)
/// </summary>
public sealed class LiveSessionStateStore(IConnectionMultiplexer mux)
{
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web);
    private IDatabase Db => mux.GetDatabase();

    // ── Key builders ──────────────────────────────────────────────────────────
    private static string SK(string code) => $"ls:session:{code}";
    private static string PK(string code) => $"ls:players:{code}";
    private static string ScK(string code) => $"ls:scores:{code}";
    private static string SnapK(string code) => $"ls:quiz:{code}";
    private static string CorrectK(string code) => $"ls:quiz:correct:{code}";
    private static string AnsK(string code, int idx) => $"ls:answers:{code}:{idx}";
    private static string AnsweredK(string code, int idx) => $"ls:answered:{code}:{idx}";
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(6);

    // ── Session lifecycle ─────────────────────────────────────────────────────
    public Task<bool> SessionExists(string code) =>
        Db.KeyExistsAsync(SK(code));

    public async Task CreateSession(string code, string quizId, string hostConnectionId)
    {
        await Db.HashSetAsync(SK(code), new[]
        {
            new HashEntry("quizId", quizId),
            new HashEntry("status", "lobby"),
            new HashEntry("currentIndex", -1),
            new HashEntry("hostId", hostConnectionId),
            new HashEntry("createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            new HashEntry("questionDeadline", 0L),
            new HashEntry("totalQuestions", 0),
        });
        await RefreshTtl(code);
    }

    public async Task RefreshTtl(string code)
    {
        var batch = Db.CreateBatch();
        _ = batch.KeyExpireAsync(SK(code), SessionTtl);
        _ = batch.KeyExpireAsync(PK(code), SessionTtl);
        _ = batch.KeyExpireAsync(ScK(code), SessionTtl);
        _ = batch.KeyExpireAsync(SnapK(code), SessionTtl);
        _ = batch.KeyExpireAsync(CorrectK(code), SessionTtl);
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task<SessionInfo?> GetSessionInfo(string code)
    {
        var fields = await Db.HashGetAllAsync(SK(code));
        if (fields.Length == 0) return null;

        var d = fields.ToDictionary(f => f.Name.ToString(), f => f.Value.ToString());
        return new SessionInfo
        {
            QuizId = d.GetValueOrDefault("quizId", ""),
            Status = d.GetValueOrDefault("status", "lobby"),
            CurrentIndex = int.TryParse(d.GetValueOrDefault("currentIndex"), out var ci) ? ci : -1,
            HostId = d.GetValueOrDefault("hostId", ""),
            TotalQuestions = int.TryParse(d.GetValueOrDefault("totalQuestions"), out var tq) ? tq : 0,
            QuestionDeadline = long.TryParse(d.GetValueOrDefault("questionDeadline"), out var dl)
                ? DateTimeOffset.FromUnixTimeSeconds(dl) : DateTimeOffset.MinValue
        };
    }

    public async Task<string?> GetQuizId(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "quizId");
        return rv.IsNullOrEmpty ? null : rv.ToString();
    }

    public async Task<string?> GetHostId(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "hostId");
        return rv.IsNullOrEmpty ? null : rv.ToString();
    }

    public Task SetStatus(string code, string status) =>
        Db.HashSetAsync(SK(code), "status", status);

    public async Task<string> GetStatus(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "status");
        return rv.IsNullOrEmpty ? "unknown" : rv.ToString();
    }

    public async Task<int> GetCurrentIndex(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "currentIndex");
        return int.TryParse(rv.ToString(), out var i) ? i : -1;
    }

    public async Task SetCurrentIndex(string code, int index, int timeLimitSeconds)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeLimitSeconds).ToUnixTimeSeconds();
        await Db.HashSetAsync(SK(code), new[]
        {
            new HashEntry("currentIndex", index),
            new HashEntry("questionDeadline", deadline)
        });
    }

    // ── Players ───────────────────────────────────────────────────────────────
    public async Task AddPlayer(string code, string playerId, string displayName)
    {
        await Db.HashSetAsync(PK(code), playerId, displayName);
        // Initialize score only if not present
        await Db.HashSetAsync(ScK(code), playerId, 0, When.NotExists);
    }

    public async Task RemovePlayer(string code, string playerId)
    {
        await Db.HashDeleteAsync(PK(code), playerId);
        // keep score for final leaderboard
    }

    public async Task<Dictionary<string, string>> GetPlayers(string code)
    {
        var entries = await Db.HashGetAllAsync(PK(code));
        return entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
    }

    public async Task<int> GetPlayerCount(string code)
    {
        var len = await Db.HashLengthAsync(PK(code));
        return (int)len;
    }

    // ── Scores ────────────────────────────────────────────────────────────────
    public async Task<Dictionary<string, int>> GetScores(string code)
    {
        var entries = await Db.HashGetAllAsync(ScK(code));
        return entries.ToDictionary(
            x => x.Name.ToString(),
            x => int.TryParse(x.Value.ToString(), out var v) ? v : 0
        );
    }

    public async Task<Dictionary<string, LeaderboardEntry>> GetLeaderboard(string code)
    {
        var players = await GetPlayers(code);
        var scores = await GetScores(code);

        return players.ToDictionary(
            p => p.Key,
            p => new LeaderboardEntry
            {
                PlayerId = p.Key,
                DisplayName = p.Value,
                Score = scores.GetValueOrDefault(p.Key, 0)
            }
        );
    }

    public Task<long> IncrementScore(string code, string playerId, int delta) =>
        Db.HashIncrementAsync(ScK(code), playerId, delta);

    // ── Quiz snapshot ─────────────────────────────────────────────────────────
    public async Task StoreQuizSnapshot(string code, QuizSnapshot snapshot)
    {
        // Public snapshot (no correct answers)
        var pub = new QuizPublicSnapshot
        {
            QuizId = snapshot.QuizId,
            Title = snapshot.Title,
            TimeLimitSeconds = snapshot.TimeLimitSeconds,
            Questions = snapshot.Questions.Select(q => new QuestionPublicSnapshot
            {
                Id = q.Id,
                Type = q.Type,
                Prompt = q.Prompt,
                Points = q.Points,
                Options = q.Options
            }).ToList()
        };
        await Db.StringSetAsync(SnapK(code), JsonSerializer.SerializeToUtf8Bytes(pub, JsonOpt));

        // Correct answers snapshot (server-only)
        var correct = new QuizCorrectSnapshot
        {
            Questions = snapshot.Questions.Select(q => new QuestionCorrectSnapshot
            {
                Id = q.Id,
                Type = q.Type,
                Points = q.Points,
                CorrectBool = q.CorrectBool,
                CorrectOptionIds = q.CorrectOptionIds ?? new List<string>(),
                AcceptedAnswers = (q.AcceptedAnswers ?? new List<string>())
                    .Select(Normalize)
                    .Where(x => x.Length > 0)
                    .Distinct()
                    .ToList()
            }).ToList()
        };
        await Db.StringSetAsync(CorrectK(code), JsonSerializer.SerializeToUtf8Bytes(correct, JsonOpt));

        // Update total questions count
        await Db.HashSetAsync(SK(code), "totalQuestions", snapshot.Questions.Count);
        await RefreshTtl(code);
    }

    public async Task<int> GetTotalQuestions(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "totalQuestions");
        return int.TryParse(rv.ToString(), out var n) ? n : 0;
    }

    public async Task<QuestionPublicSnapshot?> GetQuestion(string code, int index)
    {
        var rv = await Db.StringGetAsync(SnapK(code));
        if (rv.IsNullOrEmpty) return null;

        var snap = JsonSerializer.Deserialize<QuizPublicSnapshot>(rv.ToString(), JsonOpt);
        if (snap is null || index < 0 || index >= snap.Questions.Count) return null;
        return snap.Questions[index];
    }

    public async Task<QuizPublicSnapshot?> GetPublicSnapshot(string code)
    {
        var rv = await Db.StringGetAsync(SnapK(code));
        if (rv.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<QuizPublicSnapshot>(rv.ToString(), JsonOpt);
    }

    // ── Answers ───────────────────────────────────────────────────────────────
    public async Task<bool> HasAlreadyAnswered(string code, int questionIndex, string playerId)
    {
        return await Db.SetContainsAsync(AnsweredK(code, questionIndex), playerId);
    }

    public async Task<AnswerResult> SaveAndCheckAnswer(
        string code,
        int questionIndex,
        string playerId,
        AnswerPayload ans)
    {
        // Idempotency: if already answered, return previous result
        if (await HasAlreadyAnswered(code, questionIndex, playerId))
            return new AnswerResult { AlreadyAnswered = true };

        // Save raw answer
        var ansJson = JsonSerializer.SerializeToUtf8Bytes(ans, JsonOpt);
        await Db.HashSetAsync(AnsK(code, questionIndex), playerId, ansJson);
        await Db.SetAddAsync(AnsweredK(code, questionIndex), playerId);

        // Evaluate
        var correct = await GetCorrectSnapshot(code);
        if (correct is null || questionIndex >= correct.Questions.Count)
            return new AnswerResult { IsCorrect = false, PointsEarned = 0 };

        var q = correct.Questions[questionIndex];
        var isCorrect = Evaluate(q, ans);
        var points = isCorrect ? q.Points : 0;

        if (isCorrect)
            await IncrementScore(code, playerId, points);

        return new AnswerResult
        {
            IsCorrect = isCorrect,
            PointsEarned = points,
            AlreadyAnswered = false
        };
    }

    public async Task<int> GetAnsweredCount(string code, int questionIndex)
    {
        var count = await Db.SetLengthAsync(AnsweredK(code, questionIndex));
        return (int)count;
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private async Task<QuizCorrectSnapshot?> GetCorrectSnapshot(string code)
    {
        var rv = await Db.StringGetAsync(CorrectK(code));
        if (rv.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<QuizCorrectSnapshot>(rv.ToString(), JsonOpt);
    }

    private static bool Evaluate(QuestionCorrectSnapshot q, AnswerPayload ans) =>
        q.Type switch
        {
            0 => ans.BoolAnswer is not null && q.CorrectBool is not null
                 && ans.BoolAnswer.Value == q.CorrectBool.Value,

            1 => !string.IsNullOrWhiteSpace(ans.SingleOptionId)
                 && q.CorrectOptionIds.Count == 1
                 && ans.SingleOptionId == q.CorrectOptionIds[0],

            2 => ans.MultipleOptionIds is not null
                 && new HashSet<string>(ans.MultipleOptionIds.Where(x => !string.IsNullOrWhiteSpace(x)))
                     .SetEquals(q.CorrectOptionIds.Where(x => !string.IsNullOrWhiteSpace(x))),

            3 => !string.IsNullOrWhiteSpace(ans.TextAnswer)
                 && q.AcceptedAnswers.Contains(Normalize(ans.TextAnswer)),

            _ => false
        };

    private static string Normalize(string? s) => (s ?? "").Trim().ToLowerInvariant();
}

// ── DTOs / Models ─────────────────────────────────────────────────────────────

public sealed class SessionInfo
{
    public string QuizId { get; set; } = "";
    public string Status { get; set; } = "lobby";
    public int CurrentIndex { get; set; } = -1;
    public string HostId { get; set; } = "";
    public int TotalQuestions { get; set; }
    public DateTimeOffset QuestionDeadline { get; set; }
}

public sealed class LeaderboardEntry
{
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
}

public sealed class AnswerResult
{
    public bool IsCorrect { get; set; }
    public int PointsEarned { get; set; }
    public bool AlreadyAnswered { get; set; }
}

// Raw snapshot from QuizService (includes correct answers — never sent to clients)
public sealed class QuizSnapshot
{
    public string QuizId { get; set; } = "";
    public string Title { get; set; } = "";
    public int TimeLimitSeconds { get; set; }
    public List<QuestionSnapshot> Questions { get; set; } = new();
}

public sealed class QuestionSnapshot
{
    public string Id { get; set; } = "";
    public int Type { get; set; }
    public string Prompt { get; set; } = "";
    public int Points { get; set; } = 1;
    public List<OptionSnapshot> Options { get; set; } = new();
    public bool? CorrectBool { get; set; }
    public List<string>? CorrectOptionIds { get; set; }
    public List<string>? AcceptedAnswers { get; set; }
}

public sealed class OptionSnapshot
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

// Public snapshot (sent to clients — no correct answers)
public sealed class QuizPublicSnapshot
{
    public string QuizId { get; set; } = "";
    public string Title { get; set; } = "";
    public int TimeLimitSeconds { get; set; }
    public List<QuestionPublicSnapshot> Questions { get; set; } = new();
}

public sealed class QuestionPublicSnapshot
{
    public string Id { get; set; } = "";
    public int Type { get; set; }
    public string Prompt { get; set; } = "";
    public int Points { get; set; } = 1;
    public List<OptionSnapshot> Options { get; set; } = new();
}

// Correct answers (server-only, stored in Redis)
public sealed class QuizCorrectSnapshot
{
    public List<QuestionCorrectSnapshot> Questions { get; set; } = new();
}

public sealed class QuestionCorrectSnapshot
{
    public string Id { get; set; } = "";
    public int Type { get; set; }
    public int Points { get; set; } = 1;
    public bool? CorrectBool { get; set; }
    public List<string> CorrectOptionIds { get; set; } = new();
    public List<string> AcceptedAnswers { get; set; } = new();
}

public sealed class AnswerPayload
{
    public bool? BoolAnswer { get; set; }
    public string? SingleOptionId { get; set; }
    public List<string>? MultipleOptionIds { get; set; }
    public string? TextAnswer { get; set; }
}