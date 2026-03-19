using Microsoft.AspNetCore.Mvc;
using Quiz.CodingService.Engine;
using Quiz.CodingService.State;
using Quiz.CodingService.Services;

namespace Quiz.CodingService.Controllers;

[ApiController]
[Route("api/coding-sessions")]
public sealed class LiveCodingSessionsController(
    LiveCodingSessionStateStore store,
    LiveCodingHistoryService history,
    ILogger<LiveCodingSessionsController> log
) : ControllerBase
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int SessionCodeLength = 6;
    private const int MinTimeLimitSeconds = 60;
    private const int MaxTimeLimitSeconds = 7200;

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLiveCodingSessionRequest req,
        CancellationToken ct = default)
    {
        if (req.Ruleset == null)
            return BadRequest(new { message = "Ruleset-ul este obligatoriu." });
        if (string.IsNullOrWhiteSpace(req.Ruleset.name))
            return BadRequest(new { message = "Ruleset-ul trebuie să aibă un nume." });
        if (string.IsNullOrWhiteSpace(req.Ruleset.language))
            return BadRequest(new { message = "Ruleset-ul trebuie să aibă o limbă configurată." });
        if (req.Ruleset.rules == null || req.Ruleset.rules.Count == 0)
            return BadRequest(new { message = "Ruleset-ul trebuie să conțină cel puțin o regulă." });
        if (req.Ruleset.rules.Any(rule => string.IsNullOrWhiteSpace(rule.id) || string.IsNullOrWhiteSpace(rule.type)))
            return BadRequest(new { message = "Fiecare regulă trebuie să aibă id și tip." });
        if (req.TimeLimitSeconds < MinTimeLimitSeconds || req.TimeLimitSeconds > MaxTimeLimitSeconds)
            return BadRequest(new { message = $"Timpul limită trebuie să fie între {MinTimeLimitSeconds} și {MaxTimeLimitSeconds} secunde." });

        var code = await GenerateUniqueCode(ct);

        await store.CreateSession(code, req.Ruleset, req.TimeLimitSeconds);
        await history.RecordSessionCreatedAsync(code, req.Ruleset, req.TimeLimitSeconds, ct);

        log.LogInformation("Created live coding session {Code}", code);

        return Ok(new CreateLiveCodingSessionResponse(
            SessionCode: code,
            HubUrl: "/coding-hubs/live-coding",
            CreatedAt: DateTimeOffset.UtcNow
        ));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetInfo(string code, CancellationToken ct)
    {
        code = NormalizeCode(code);
        if (!IsValidCode(code))
            return BadRequest(new { message = "Codul sesiunii este invalid." });

        var status = await store.GetStatus(code);
        if (status == "unknown") return NotFound(new { message = "Sesiunea nu există." });

        var players = await store.GetPlayers(code);
        var leaderboard = await store.GetLeaderboard(code);

        return Ok(new
        {
            sessionCode = code,
            status = status,
            playerCount = players.Count,
            leaderboard = leaderboard.Values
                .OrderByDescending(x => x.Score)
                .Select(x => new { x.PlayerId, x.DisplayName, x.Score })
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        var items = await history.GetHistoryAsync(ct);
        return Ok(items);
    }

    [HttpGet("history/{code}")]
    public async Task<IActionResult> GetHistoryDetail(string code, CancellationToken ct)
    {
        code = NormalizeCode(code);
        if (!IsValidCode(code))
            return BadRequest(new { message = "Codul sesiunii este invalid." });

        var item = await history.GetHistoryDetailAsync(code, ct);
        if (item is null) return NotFound(new { message = "Istoricul sesiunii nu există." });
        return Ok(item);
    }

    [HttpPost("{code}/set-host")]
    public async Task<IActionResult> SetHost(string code, [FromBody] SetCodingHostRequest req)
    {
        code = NormalizeCode(code);
        if (!IsValidCode(code))
            return BadRequest(new { message = "Codul sesiunii este invalid." });
        if (string.IsNullOrWhiteSpace(req.ConnectionId))
            return BadRequest(new { message = "ConnectionId este obligatoriu." });

        if (!await store.SessionExists(code))
            return NotFound(new { message = "Sesiunea nu există." });

        return Ok(new { sessionCode = code, hostHint = req.ConnectionId });
    }

    private async Task<string> GenerateUniqueCode(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var code = GenerateCode();
            if (!await store.SessionExists(code))
                return code;
        }

        throw new InvalidOperationException("Nu pot genera un cod unic de sesiune.");
    }

    private static string GenerateCode()
    {
        var rnd = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => Chars[rnd.Next(Chars.Length)]).ToArray());
    }

    private static string NormalizeCode(string code) => (code ?? "").Trim().ToUpperInvariant();

    private static bool IsValidCode(string code) =>
        code.Length == SessionCodeLength && code.All(ch => char.IsLetterOrDigit(ch));
}

public sealed record CreateLiveCodingSessionRequest(Ruleset Ruleset, int TimeLimitSeconds);
public sealed record SetCodingHostRequest(string ConnectionId);
public sealed record CreateLiveCodingSessionResponse(
    string SessionCode,
    string HubUrl,
    DateTimeOffset CreatedAt
);
