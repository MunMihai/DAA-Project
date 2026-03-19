using System.Text.Json;
using Quiz.CodingService.Models;
using StackExchange.Redis;

namespace Quiz.CodingService.State;

public sealed class LiveCompileCodingSessionStateStore(IConnectionMultiplexer mux)
{
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(6);

    private IDatabase Db => mux.GetDatabase();

    private static string SK(string code) => $"lcc:session:{code}";
    private static string PK(string code) => $"lcc:players:{code}";
    private static string ScK(string code) => $"lcc:scores:{code}";
    private static string DefK(string code) => $"lcc:def:{code}";
    private static string TaskScK(string code) => $"lcc:taskscores:{code}";

    public Task<bool> SessionExists(string code) => Db.KeyExistsAsync(SK(code));

    public async Task CreateSession(string code, CompileCodingSessionDefinition definition)
    {
        await Db.HashSetAsync(SK(code), new[]
        {
            new HashEntry("status", "lobby"),
            new HashEntry("hostId", "__pending__"),
            new HashEntry("createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            new HashEntry("timeLimitSeconds", definition.TimeLimitSeconds),
            new HashEntry("deadlineUtc", 0L),
            new HashEntry("title", definition.Title)
        });

        await Db.StringSetAsync(DefK(code), JsonSerializer.SerializeToUtf8Bytes(definition, JsonOpt));
        await RefreshTtl(code);
    }

    public async Task RefreshTtl(string code)
    {
        var batch = Db.CreateBatch();
        _ = batch.KeyExpireAsync(SK(code), SessionTtl);
        _ = batch.KeyExpireAsync(PK(code), SessionTtl);
        _ = batch.KeyExpireAsync(ScK(code), SessionTtl);
        _ = batch.KeyExpireAsync(DefK(code), SessionTtl);
        _ = batch.KeyExpireAsync(TaskScK(code), SessionTtl);
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task<string> GetStatus(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "status");
        return rv.IsNullOrEmpty ? "unknown" : rv.ToString();
    }

    public Task SetStatus(string code, string status) => Db.HashSetAsync(SK(code), "status", status);

    public async Task<string?> GetHostId(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "hostId");
        return rv.IsNullOrEmpty ? null : rv.ToString();
    }

    public Task SetHostId(string code, string hostId) => Db.HashSetAsync(SK(code), "hostId", hostId);

    public async Task<int> GetTimeLimitSeconds(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "timeLimitSeconds");
        return int.TryParse(rv.ToString(), out var value) ? value : 1800;
    }

    public async Task<DateTimeOffset?> GetDeadlineUtc(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "deadlineUtc");
        if (rv.IsNullOrEmpty) return null;
        return long.TryParse(rv.ToString(), out var unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;
    }

    public async Task SetDeadlineUtc(string code, DateTimeOffset deadline) =>
        await Db.HashSetAsync(SK(code), "deadlineUtc", deadline.ToUnixTimeSeconds());

    public async Task<CompileCodingSessionDefinition?> GetDefinition(string code)
    {
        var rv = await Db.StringGetAsync(DefK(code));
        if (rv.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<CompileCodingSessionDefinition>(rv.ToString(), JsonOpt);
    }

    public async Task<CompileCodingSessionPublicSnapshot?> GetPublicDefinition(string code)
    {
        var definition = await GetDefinition(code);
        if (definition is null) return null;

        return new CompileCodingSessionPublicSnapshot
        {
            Title = definition.Title,
            AllowedLanguages = definition.AllowedLanguages.ToList(),
            Tasks = definition.Tasks.Select(task => new CompileCodingTaskPublicSnapshot
            {
                Id = task.Id,
                Title = task.Title,
                ProblemStatement = task.ProblemStatement,
                InputDescription = task.InputDescription,
                OutputDescription = task.OutputDescription,
                ExampleInput = task.ExampleInput,
                ExampleOutput = task.ExampleOutput,
                Points = task.Points
            }).ToList()
        };
    }

    public async Task AddPlayer(string code, string playerId, string displayName)
    {
        await Db.HashSetAsync(PK(code), playerId, displayName);
        await Db.HashSetAsync(ScK(code), playerId, 0, When.NotExists);
        await RefreshTtl(code);
    }

    public Task<bool> PlayerExists(string code, string playerId) => Db.HashExistsAsync(PK(code), playerId);

    public Task RemovePlayer(string code, string playerId) => Task.CompletedTask;

    public async Task<Dictionary<string, string>> GetPlayers(string code)
    {
        var entries = await Db.HashGetAllAsync(PK(code));
        return entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
    }

    public async Task<Dictionary<string, int>> GetScores(string code)
    {
        var entries = await Db.HashGetAllAsync(ScK(code));
        return entries.ToDictionary(
            x => x.Name.ToString(),
            x => int.TryParse(x.Value.ToString(), out var value) ? value : 0);
    }

    public async Task<Dictionary<string, int>> GetPlayerTaskScores(string code, string playerId)
    {
        var entries = await Db.HashGetAllAsync(TaskScK(code));
        return entries
            .Where(entry => entry.Name.ToString().StartsWith($"{playerId}|", StringComparison.Ordinal))
            .ToDictionary(
                entry => entry.Name.ToString()[(playerId.Length + 1)..],
                entry => int.TryParse(entry.Value.ToString(), out var value) ? value : 0);
    }

    public async Task<TaskScoreUpdate> UpdateTaskBestScore(string code, string playerId, string taskId, int proposedScore)
    {
        var field = $"{playerId}|{taskId}";
        var currentValue = await Db.HashGetAsync(TaskScK(code), field);
        var currentBest = int.TryParse(currentValue.ToString(), out var best) ? best : 0;

        if (proposedScore > currentBest)
        {
            await Db.HashSetAsync(TaskScK(code), field, proposedScore);
            var delta = proposedScore - currentBest;
            await Db.HashIncrementAsync(ScK(code), playerId, delta);
        }

        var scores = await GetScores(code);
        var totalScore = scores.GetValueOrDefault(playerId, 0);
        var bestScore = Math.Max(currentBest, proposedScore);

        return new TaskScoreUpdate(bestScore, bestScore - currentBest, totalScore);
    }

    public async Task<Dictionary<string, CompileLeaderboardEntry>> GetLeaderboard(string code)
    {
        var players = await GetPlayers(code);
        var scores = await GetScores(code);

        return players.ToDictionary(
            player => player.Key,
            player => new CompileLeaderboardEntry
            {
                PlayerId = player.Key,
                DisplayName = player.Value,
                Score = scores.GetValueOrDefault(player.Key, 0)
            });
    }
}

public sealed record TaskScoreUpdate(int BestTaskScore, int ScoreDelta, int TotalScore);

public sealed class CompileLeaderboardEntry
{
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
}
