using System.Text.Json;
using StackExchange.Redis;
using Quiz.CodingService.Engine;

namespace Quiz.CodingService.State;

/// <summary>
/// Redis state store for Live Coding Sessions.
/// Keys:
///   lc:session:{code} -> Hash { status, hostId, RulesetJson }
///   lc:players:{code} -> Hash { playerId -> displayName }
///   lc:scores:{code}  -> Hash { playerId -> int }
/// </summary>
public sealed class LiveCodingSessionStateStore(IConnectionMultiplexer mux)
{
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web);
    public IDatabase Db => mux.GetDatabase();

    private static string SK(string code) => $"lc:session:{code}";
    private static string PK(string code) => $"lc:players:{code}";
    private static string ScK(string code) => $"lc:scores:{code}";
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(6);

    // ── Session lifecycle ─────────────────────────────────────────────────────
    public Task<bool> SessionExists(string code) =>
        Db.KeyExistsAsync(SK(code));

    public async Task CreateSession(string code, Ruleset ruleset, int timeLimitSeconds)
    {
        var rulesetJson = JsonSerializer.Serialize(ruleset, JsonOpt);

        await Db.HashSetAsync(SK(code), new[]
        {
            new HashEntry("status", "lobby"),
            new HashEntry("hostId", "__pending__"),
            new HashEntry("createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            new HashEntry("ruleset", rulesetJson),
            new HashEntry("timeLimitSeconds", timeLimitSeconds)
        });
        await RefreshTtl(code);
    }

    public async Task RefreshTtl(string code)
    {
        var batch = Db.CreateBatch();
        _ = batch.KeyExpireAsync(SK(code), SessionTtl);
        _ = batch.KeyExpireAsync(PK(code), SessionTtl);
        _ = batch.KeyExpireAsync(ScK(code), SessionTtl);
        batch.Execute();
        await Task.CompletedTask;
    }

    public async Task<string> GetStatus(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "status");
        return rv.IsNullOrEmpty ? "unknown" : rv.ToString();
    }

    public Task SetStatus(string code, string status) =>
        Db.HashSetAsync(SK(code), "status", status);

    public async Task<string?> GetHostId(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "hostId");
        return rv.IsNullOrEmpty ? null : rv.ToString();
    }

    public Task SetHostId(string code, string hostId) =>
        Db.HashSetAsync(SK(code), "hostId", hostId);

    public async Task<Ruleset?> GetRuleset(string code)
    {
        var rv = await Db.HashGetAsync(SK(code), "ruleset");
        if (rv.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<Ruleset>(rv.ToString(), JsonOpt);
    }

    // ── Players ───────────────────────────────────────────────────────────────
    public async Task AddPlayer(string code, string playerId, string displayName)
    {
        await Db.HashSetAsync(PK(code), playerId, displayName);
        await Db.HashSetAsync(ScK(code), playerId, 0, When.NotExists);
    }

    public async Task RemovePlayer(string code, string playerId)
    {
        await Task.CompletedTask; // Keep player records for reconnections
    }

    public async Task<Dictionary<string, string>> GetPlayers(string code)
    {
        var entries = await Db.HashGetAllAsync(PK(code));
        return entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
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

    public async Task<long> SetScore(string code, string playerId, int score)
    {
        await Db.HashSetAsync(ScK(code), playerId, score);
        return 1L;
    }
}

public sealed class LeaderboardEntry
{
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
}
