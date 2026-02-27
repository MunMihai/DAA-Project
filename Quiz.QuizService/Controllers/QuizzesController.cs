using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Quiz.QuizService.Data;
using Quiz.QuizService.DTOs;
using Quiz.QuizService.Models;
using Quiz.QuizService.Services;

namespace Quiz.QuizService.Controllers;

[ApiController]
[Route("api/quizzes")]
public sealed class QuizzesController(MongoContext db, RedisJsonCache rc) : ControllerBase
{
    // TTL policy
    private static readonly TimeSpan TtlQuizFull = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TtlQuizPlay = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan TtlQuizList = TimeSpan.FromSeconds(45); // list TTL scurt

    // CRUD (admin)
    [HttpPost]
    public async Task<ActionResult<QuizEntity>> Create([FromBody] QuizCreateRequest req, CancellationToken ct)
    {
        var quiz = new QuizEntity
        {
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            Tags = req.Tags ?? new(),
            TimeLimitSeconds = req.TimeLimitSeconds <= 0 ? 600 : req.TimeLimitSeconds,
            ShuffleQuestions = req.ShuffleQuestions,
            ShuffleOptions = req.ShuffleOptions,
            Questions = req.Questions.Select(MapQuestion).ToList(),
            Status = QuizStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await db.Quizzes.InsertOneAsync(quiz, cancellationToken: ct);

        // ✅ Invalidate lists (via version bump)
        await BumpListVersion(ct);

        // ✅ Warm cache for admin view (optional, but nice)
        await rc.SetAsync(CacheKeys.QuizById(quiz.Id), quiz, TtlQuizFull, ct);

        return CreatedAtAction(nameof(GetById), new { id = quiz.Id }, quiz);
    }

    [HttpGet]
    public async Task<ActionResult<List<QuizEntity>>> List(
        [FromQuery] QuizStatus? status = null,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        // ✅ List cache key depends on query
        var statusKey = status?.ToString() ?? "any";
        var tagKey = string.IsNullOrWhiteSpace(tag) ? "any" : tag.Trim();

        // list version token
        var ver = await GetListVersion(ct);
        var cacheKey = CacheKeys.QuizList(ver, statusKey, tagKey);

        var cached = await rc.GetAsync<List<QuizEntity>>(cacheKey, ct);
        if (cached is not null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cached);
        }

        Response.Headers["X-Cache"] = "MISS";

        var f = Builders<QuizEntity>.Filter.Empty;
        if (status is not null) f &= Builders<QuizEntity>.Filter.Eq(x => x.Status, status.Value);
        if (!string.IsNullOrWhiteSpace(tag)) f &= Builders<QuizEntity>.Filter.AnyEq(x => x.Tags, tag.Trim());

        var items = await db.Quizzes
            .Find(f)
            .SortByDescending(x => x.UpdatedAt)
            .Limit(200)
            .ToListAsync(ct);

        await rc.SetAsync(cacheKey, items, TtlQuizList, ct);
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<QuizEntity>> GetById(string id, CancellationToken ct)
    {
        var key = CacheKeys.QuizById(id);

        var cached = await rc.GetAsync<QuizEntity>(key, ct);
        if (cached is not null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cached);
        }

        Response.Headers["X-Cache"] = "MISS";

        var quiz = await db.Quizzes.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (quiz is null) return NotFound();

        await rc.SetAsync(key, quiz, TtlQuizFull, ct);
        return Ok(quiz);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] QuizUpdateRequest req, CancellationToken ct)
    {
        var quiz = await db.Quizzes.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (quiz is null) return NotFound();

        quiz.Title = req.Title.Trim();
        quiz.Description = req.Description.Trim();
        quiz.Status = req.Status;
        quiz.Tags = req.Tags ?? new();
        quiz.TimeLimitSeconds = req.TimeLimitSeconds <= 0 ? quiz.TimeLimitSeconds : req.TimeLimitSeconds;
        quiz.ShuffleQuestions = req.ShuffleQuestions;
        quiz.ShuffleOptions = req.ShuffleOptions;
        quiz.Questions = req.Questions.Select(MapQuestion).ToList();
        quiz.UpdatedAt = DateTimeOffset.UtcNow;

        await db.Quizzes.ReplaceOneAsync(x => x.Id == id, quiz, cancellationToken: ct);

        // ✅ Invalidate caches
        await rc.RemoveAsync(CacheKeys.QuizById(id), ct);
        await rc.RemoveAsync(CacheKeys.QuizPlay(id), ct);
        await BumpListVersion(ct);

        // ✅ Optional: warm new admin cache
        await rc.SetAsync(CacheKeys.QuizById(id), quiz, TtlQuizFull, ct);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var res = await db.Quizzes.DeleteOneAsync(x => x.Id == id, ct);
        if (res.DeletedCount == 0) return NotFound();

        // ✅ Invalidate caches
        await rc.RemoveAsync(CacheKeys.QuizById(id), ct);
        await rc.RemoveAsync(CacheKeys.QuizPlay(id), ct);
        await BumpListVersion(ct);

        return NoContent();
    }

    // PLAY VIEW (safe)
    [HttpGet("{id}/play")]
    public async Task<ActionResult<QuizPlayView>> GetPlayView(string id, CancellationToken ct)
    {
        var key = CacheKeys.QuizPlay(id);

        var cached = await rc.GetAsync<QuizPlayView>(key, ct);
        if (cached is not null)
        {
            Response.Headers["X-Cache"] = "HIT";
            return Ok(cached);
        }

        Response.Headers["X-Cache"] = "MISS";

        var quiz = await db.Quizzes.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (quiz is null) return NotFound();
        if (quiz.Status != QuizStatus.Published) return BadRequest(new { message = "Quiz is not published." });

        var q = quiz.Questions.Select(x => new QuestionPlayView(
            x.Id, x.Type, x.Prompt, x.Points,
            x.Options.Select(o => new OptionPlayView(o.Id, o.Text)).ToList()
        )).ToList();

        var view = new QuizPlayView(
            quiz.Id, quiz.Title, quiz.Description, quiz.TimeLimitSeconds,
            quiz.ShuffleQuestions, quiz.ShuffleOptions, q
        );

        await rc.SetAsync(key, view, TtlQuizPlay, ct);
        return Ok(view);
    }

    // ---- Helpers ----

    private async Task<string> GetListVersion(CancellationToken ct)
    {
        var key = CacheKeys.QuizListVersion();
        var ver = await rc.GetAsync<string>(key, ct);
        if (!string.IsNullOrWhiteSpace(ver)) return ver;

        ver = Guid.NewGuid().ToString("N");
        await rc.SetAsync(key, ver, TimeSpan.FromDays(30), ct);
        return ver;
    }

    private async Task BumpListVersion(CancellationToken ct)
    {
        var key = CacheKeys.QuizListVersion();
        var ver = Guid.NewGuid().ToString("N");
        await rc.SetAsync(key, ver, TimeSpan.FromDays(30), ct);
    }

    private static Question MapQuestion(QuestionUpsert q)
    {
        var question = new Question
        {
            Id = string.IsNullOrWhiteSpace(q.Id) ? Guid.NewGuid().ToString("N") : q.Id!,
            Type = q.Type,
            Prompt = q.Prompt.Trim(),
            Explanation = string.IsNullOrWhiteSpace(q.Explanation) ? null : q.Explanation.Trim(),
            Points = q.Points <= 0 ? 1 : q.Points,
            Topic = string.IsNullOrWhiteSpace(q.Topic) ? null : q.Topic.Trim(),
        };

        question.Options = (q.Options ?? new())
            .Select(o => new Option
            {
                Id = string.IsNullOrWhiteSpace(o.Id) ? Guid.NewGuid().ToString("N") : o.Id!,
                Text = o.Text.Trim()
            })
            .ToList();

        question.CorrectBool = q.CorrectBool;
        question.CorrectOptionIds = q.CorrectOptionIds ?? new();
        question.AcceptedAnswers = (q.AcceptedAnswers ?? new())
            .Select(Normalize)
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();

        return question;
    }

    private static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();
}