using Microsoft.AspNetCore.Mvc;
using Quiz.CodingService.Engine;
using Quiz.CodingService.State;

namespace Quiz.CodingService.Controllers;

[ApiController]
[Route("api/coding-sessions")]
public sealed class LiveCodingSessionsController(
    LiveCodingSessionStateStore store,
    ILogger<LiveCodingSessionsController> log
) : ControllerBase
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLiveCodingSessionRequest req,
        CancellationToken ct = default)
    {
        if (req.Ruleset == null)
            return BadRequest(new { message = "Ruleset is required." });

        var code = GenerateCode();

        await store.CreateSession(code, req.Ruleset, req.TimeLimitSeconds);

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
        code = code.Trim().ToUpperInvariant();
        var status = await store.GetStatus(code);
        if (status == "unknown") return NotFound(new { message = "Session not found." });

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

    [HttpPost("{code}/set-host")]
    public async Task<IActionResult> SetHost(string code, [FromBody] SetCodingHostRequest req)
    {
        code = code.Trim().ToUpperInvariant();
        if (!await store.SessionExists(code))
            return NotFound(new { message = "Session not found." });

        return Ok(new { sessionCode = code, hostHint = req.ConnectionId });
    }

    private static string GenerateCode()
    {
        var rnd = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => Chars[rnd.Next(Chars.Length)]).ToArray());
    }
}

public sealed record CreateLiveCodingSessionRequest(Ruleset Ruleset, int TimeLimitSeconds);
public sealed record SetCodingHostRequest(string ConnectionId);
public sealed record CreateLiveCodingSessionResponse(
    string SessionCode,
    string HubUrl,
    DateTimeOffset CreatedAt
);
