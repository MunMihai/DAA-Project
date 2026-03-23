using Microsoft.AspNetCore.Mvc;
using Quiz.LiveSessionService.Services;
using Quiz.LiveSessionService.State;

namespace Quiz.LiveSessionService.Controllers;

[ApiController]
[Route("api/live-sessions")]
public sealed class LiveSessionsController(
    LiveSessionStateStore store,
    QuizServiceClient quizClient,
    LiveQuizHistoryService history,
    ILogger<LiveSessionsController> log
) : ControllerBase
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int SessionCodeLength = 6;

    /// <summary>Create a new live session. The host must subsequently join via SignalR.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLiveSessionRequest req,
        [FromHeader(Name = "X-Host-Id")] string? hostHint = null,
        CancellationToken ct = default)
    {
        var quizId = req.QuizId?.Trim();
        if (string.IsNullOrWhiteSpace(quizId))
            return BadRequest(new { message = "QuizId este obligatoriu." });

        // Verify quiz exists and is published
        try
        {
            var exists = await quizClient.QuizExistsAndPublished(quizId, ct);
            if (!exists)
                return BadRequest(new { message = "Quiz-ul nu există sau nu este publicat." });
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "QuizService check failed for {QuizId}", quizId);
            return StatusCode(503, new { message = "Serviciul de quiz nu este disponibil momentan." });
        }

        var code = await GenerateUniqueCode(ct);

        // hostConnectionId will be set properly when host calls Hub.Join()
        // We use a placeholder; hub will set real connection id on join
        await store.CreateSession(code, quizId, "__pending__");
        await history.RecordSessionCreatedAsync(code, quizId, ct);

        log.LogInformation("Created live session {Code} for quiz {QuizId}", code, quizId);

        return Ok(new CreateLiveSessionResponse(
            SessionCode: code,
            QuizId: quizId,
            HubUrl: "/hubs/live-quiz",
            CreatedAt: DateTimeOffset.UtcNow
        ));
    }

    /// <summary>Get session status/info (polling fallback or admin view).</summary>
    [HttpGet("{code}")]
    public async Task<IActionResult> GetInfo(string code, CancellationToken ct)
    {
        code = NormalizeCode(code);
        if (!IsValidCode(code))
            return BadRequest(new { message = "Codul sesiunii este invalid." });

        var info = await store.GetSessionInfo(code);
        if (info is null) return NotFound(new { message = "Sesiunea nu există." });

        var players = await store.GetPlayers(code);
        var leaderboard = await store.GetLeaderboard(code);

        return Ok(new
        {
            sessionCode = code,
            quizId = info.QuizId,
            status = info.Status,
            sessionDeadline = info.SessionDeadline,
            totalQuestions = info.TotalQuestions,
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

        var item = await history.GetHistoryDetailAsync(code, quizClient.FetchQuizSnapshot, ct);
        if (item is null) return NotFound(new { message = "Istoricul sesiunii nu există." });
        return Ok(item);
    }

    /// <summary>Register host — called by the host client after creating the session,
    /// before connecting via SignalR. Returns a token used to identify the host connection.
    /// In this simplified flow the first player who joins with this code gets host privileges
    /// if host slot is still "__pending__".</summary>
    [HttpPost("{code}/set-host")]
    public async Task<IActionResult> SetHost(string code, [FromBody] SetHostRequest req)
    {
        code = NormalizeCode(code);
        if (!IsValidCode(code))
            return BadRequest(new { message = "Codul sesiunii este invalid." });
        if (string.IsNullOrWhiteSpace(req.ConnectionId))
            return BadRequest(new { message = "ConnectionId este obligatoriu." });

        if (!await store.SessionExists(code))
            return NotFound(new { message = "Sesiunea nu există." });

        // Will be confirmed in hub when host joins
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

public sealed record CreateLiveSessionRequest(string QuizId);
public sealed record SetHostRequest(string ConnectionId);
public sealed record CreateLiveSessionResponse(
    string SessionCode,
    string QuizId,
    string HubUrl,
    DateTimeOffset CreatedAt
);
