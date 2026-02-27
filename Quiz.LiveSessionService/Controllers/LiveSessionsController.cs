using Microsoft.AspNetCore.Mvc;
using Quiz.LiveSessionService.Services;
using Quiz.LiveSessionService.State;

namespace Quiz.LiveSessionService.Controllers;

[ApiController]
[Route("api/live-sessions")]
public sealed class LiveSessionsController(
    LiveSessionStateStore store,
    QuizServiceClient quizClient,
    ILogger<LiveSessionsController> log
) : ControllerBase
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Create a new live session. The host must subsequently join via SignalR.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLiveSessionRequest req,
        [FromHeader(Name = "X-Host-Id")] string? hostHint = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.QuizId))
            return BadRequest(new { message = "QuizId is required." });

        // Verify quiz exists and is published
        try
        {
            var exists = await quizClient.QuizExistsAndPublished(req.QuizId, ct);
            if (!exists)
                return BadRequest(new { message = "Quiz not found or not published." });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "QuizService check failed for {QuizId}", req.QuizId);
            return StatusCode(503, new { message = "Quiz service temporarily unavailable." });
        }

        var code = GenerateCode();

        // hostConnectionId will be set properly when host calls Hub.Join()
        // We use a placeholder; hub will set real connection id on join
        await store.CreateSession(code, req.QuizId.Trim(), "__pending__");

        log.LogInformation("Created live session {Code} for quiz {QuizId}", code, req.QuizId);

        return Ok(new CreateLiveSessionResponse(
            SessionCode: code,
            QuizId: req.QuizId.Trim(),
            HubUrl: "/hubs/live-quiz",
            CreatedAt: DateTimeOffset.UtcNow
        ));
    }

    /// <summary>Get session status/info (polling fallback or admin view).</summary>
    [HttpGet("{code}")]
    public async Task<IActionResult> GetInfo(string code, CancellationToken ct)
    {
        code = code.Trim().ToUpperInvariant();
        var info = await store.GetSessionInfo(code);
        if (info is null) return NotFound(new { message = "Session not found." });

        var players = await store.GetPlayers(code);
        var leaderboard = await store.GetLeaderboard(code);

        return Ok(new
        {
            sessionCode = code,
            quizId = info.QuizId,
            status = info.Status,
            currentIndex = info.CurrentIndex,
            totalQuestions = info.TotalQuestions,
            playerCount = players.Count,
            leaderboard = leaderboard.Values
                .OrderByDescending(x => x.Score)
                .Select(x => new { x.PlayerId, x.DisplayName, x.Score })
        });
    }

    /// <summary>Register host — called by the host client after creating the session,
    /// before connecting via SignalR. Returns a token used to identify the host connection.
    /// In this simplified flow the first player who joins with this code gets host privileges
    /// if host slot is still "__pending__".</summary>
    [HttpPost("{code}/set-host")]
    public async Task<IActionResult> SetHost(string code, [FromBody] SetHostRequest req)
    {
        code = code.Trim().ToUpperInvariant();
        if (!await store.SessionExists(code))
            return NotFound(new { message = "Session not found." });

        // Will be confirmed in hub when host joins
        return Ok(new { sessionCode = code, hostHint = req.ConnectionId });
    }

    private static string GenerateCode()
    {
        var rnd = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => Chars[rnd.Next(Chars.Length)]).ToArray());
    }
}

public sealed record CreateLiveSessionRequest(string QuizId);
public sealed record SetHostRequest(string ConnectionId);
public sealed record CreateLiveSessionResponse(
    string SessionCode,
    string QuizId,
    string HubUrl,
    DateTimeOffset CreatedAt
);