using Microsoft.AspNetCore.Mvc;
using Quiz.CodingService.Models;
using Quiz.CodingService.Services;
using Quiz.CodingService.State;

namespace Quiz.CodingService.Controllers;

[ApiController]
[Route("api/compile-coding-sessions")]
public sealed class LiveCompileCodingSessionsController(
    LiveCompileCodingSessionStateStore store,
    LiveCompileCodingHistoryService history,
    CompileCodingTemplateService templates
) : ControllerBase
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int SessionCodeLength = 6;
    private const int MinTimeLimitSeconds = 60;
    private const int MaxTimeLimitSeconds = 7200;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLiveCompileCodingSessionRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "Titlul testului este obligatoriu." });
        if (req.TimeLimitSeconds < MinTimeLimitSeconds || req.TimeLimitSeconds > MaxTimeLimitSeconds)
            return BadRequest(new { message = $"Timpul limită trebuie să fie între {MinTimeLimitSeconds} și {MaxTimeLimitSeconds} secunde." });
        if (req.AllowedLanguages == null || req.AllowedLanguages.Count == 0)
            return BadRequest(new { message = "Selectează cel puțin un limbaj permis." });

        var normalizedLanguages = req.AllowedLanguages
            .Select(CompileCodingLanguages.Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (normalizedLanguages.Count == 0 || normalizedLanguages.Any(x => !CompileCodingLanguages.IsSupported(x)))
            return BadRequest(new { message = "Lista de limbaje conține valori nesuportate." });
        if (req.Tasks == null || req.Tasks.Count == 0)
            return BadRequest(new { message = "Adaugă cel puțin o sarcină." });

        var tasks = new List<CompileCodingTaskDefinition>();
        foreach (var task in req.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Title) ||
                string.IsNullOrWhiteSpace(task.ProblemStatement) ||
                string.IsNullOrWhiteSpace(task.InputDescription) ||
                string.IsNullOrWhiteSpace(task.OutputDescription))
            {
                return BadRequest(new { message = "Fiecare sarcină trebuie să aibă titlu, condiție, date de intrare și date de ieșire." });
            }

            if (task.Points <= 0)
                return BadRequest(new { message = "Punctajul fiecărei sarcini trebuie să fie mai mare decât 0." });

            tasks.Add(new CompileCodingTaskDefinition
            {
                Id = string.IsNullOrWhiteSpace(task.Id) ? Guid.NewGuid().ToString("N") : task.Id.Trim(),
                Title = task.Title.Trim(),
                ProblemStatement = task.ProblemStatement.Trim(),
                InputDescription = task.InputDescription.Trim(),
                OutputDescription = task.OutputDescription.Trim(),
                ExampleInput = task.ExampleInput ?? "",
                ExampleOutput = task.ExampleOutput ?? "",
                Points = task.Points,
                TestCases =
                [
                    new CompileCodingTestCase
                    {
                        Input = task.ExampleInput ?? "",
                        ExpectedOutput = task.ExampleOutput ?? "",
                        IsExample = true
                    }
                ]
            });
        }

        var definition = new CompileCodingSessionDefinition
        {
            Title = req.Title.Trim(),
            TimeLimitSeconds = req.TimeLimitSeconds,
            AllowedLanguages = normalizedLanguages,
            Tasks = tasks
        };

        var code = await GenerateUniqueCode(ct);
        await store.CreateSession(code, definition);
        await history.RecordSessionCreatedAsync(code, definition, ct);
        await templates.SaveSessionTemplateAsync(definition, ct);

        return Ok(new CreateLiveCompileCodingSessionResponse(
            code,
            "/coding-hubs/live-compile-coding",
            DateTimeOffset.UtcNow
        ));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetInfo(string code, CancellationToken ct)
    {
        code = NormalizeCode(code);
        if (!IsValidCode(code))
            return BadRequest(new { message = "Codul sesiunii este invalid." });

        var status = await store.GetStatus(code);
        if (status == "unknown")
            return NotFound(new { message = "Sesiunea nu există." });

        var definition = await store.GetDefinition(code);
        var players = await store.GetPlayers(code);
        var leaderboard = await store.GetLeaderboard(code);

        return Ok(new
        {
            sessionCode = code,
            title = definition?.Title ?? code,
            status,
            taskCount = definition?.Tasks.Count ?? 0,
            allowedLanguages = definition?.AllowedLanguages ?? [],
            playerCount = players.Count,
            leaderboard = leaderboard.Values.OrderByDescending(x => x.Score).Select(x => new { x.PlayerId, x.DisplayName, x.Score })
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        return Ok(await history.GetHistoryAsync(ct));
    }

    [HttpGet("history/{code}")]
    public async Task<IActionResult> GetHistoryDetail(string code, CancellationToken ct)
    {
        code = NormalizeCode(code);
        if (!IsValidCode(code))
            return BadRequest(new { message = "Codul sesiunii este invalid." });

        var item = await history.GetHistoryDetailAsync(code, ct);
        return item is null
            ? NotFound(new { message = "Istoricul sesiunii nu există." })
            : Ok(item);
    }

    [HttpPost("{code}/set-host")]
    public async Task<IActionResult> SetHost(string code, [FromBody] SetCompileCodingHostRequest req)
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
        return new string(Enumerable.Range(0, SessionCodeLength).Select(_ => Chars[rnd.Next(Chars.Length)]).ToArray());
    }

    private static string NormalizeCode(string code) => (code ?? "").Trim().ToUpperInvariant();

    private static bool IsValidCode(string code) =>
        code.Length == SessionCodeLength && code.All(ch => char.IsLetterOrDigit(ch));
}

public sealed record CreateLiveCompileCodingSessionRequest(
    string Title,
    int TimeLimitSeconds,
    List<string> AllowedLanguages,
    List<CreateLiveCompileCodingTaskRequest> Tasks
);

public sealed record CreateLiveCompileCodingTaskRequest(
    string? Id,
    string Title,
    string ProblemStatement,
    string InputDescription,
    string OutputDescription,
    string ExampleInput,
    string ExampleOutput,
    int Points
);

public sealed record SetCompileCodingHostRequest(string ConnectionId);

public sealed record CreateLiveCompileCodingSessionResponse(
    string SessionCode,
    string HubUrl,
    DateTimeOffset CreatedAt
);
