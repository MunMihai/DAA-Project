using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Quiz.QuizService.Data;
using Quiz.QuizService.DTOs;
using Quiz.QuizService.Models;

namespace Quiz.QuizService.Controllers;

[ApiController]
[Route("api/quizzes")]
public sealed class QuizzesController(MongoContext db) : ControllerBase
{
    // CRUD (admin)
    [HttpPost]
    public async Task<ActionResult<QuizEntity>> Create([FromBody] QuizCreateRequest req)
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

        await db.Quizzes.InsertOneAsync(quiz);
        return CreatedAtAction(nameof(GetById), new { id = quiz.Id }, quiz);
    }

    [HttpGet]
    public async Task<ActionResult<List<QuizEntity>>> List([FromQuery] QuizStatus? status = null, [FromQuery] string? tag = null)
    {
        var f = Builders<QuizEntity>.Filter.Empty;
        if (status is not null) f &= Builders<QuizEntity>.Filter.Eq(x => x.Status, status.Value);
        if (!string.IsNullOrWhiteSpace(tag)) f &= Builders<QuizEntity>.Filter.AnyEq(x => x.Tags, tag.Trim());

        var items = await db.Quizzes.Find(f).SortByDescending(x => x.UpdatedAt).Limit(200).ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<QuizEntity>> GetById(string id)
    {
        var quiz = await db.Quizzes.Find(x => x.Id == id).FirstOrDefaultAsync();
        return quiz is null ? NotFound() : Ok(quiz);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] QuizUpdateRequest req)
    {
        var quiz = await db.Quizzes.Find(x => x.Id == id).FirstOrDefaultAsync();
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

        await db.Quizzes.ReplaceOneAsync(x => x.Id == id, quiz);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var res = await db.Quizzes.DeleteOneAsync(x => x.Id == id);
        return res.DeletedCount == 0 ? NotFound() : NoContent();
    }

    // PLAY VIEW (safe)
    [HttpGet("{id}/play")]
    public async Task<ActionResult<QuizPlayView>> GetPlayView(string id)
    {
        var quiz = await db.Quizzes.Find(x => x.Id == id).FirstOrDefaultAsync();
        if (quiz is null) return NotFound();
        if (quiz.Status != QuizStatus.Published) return BadRequest(new { message = "Quiz is not published." });

        var q = quiz.Questions.Select(x => new QuestionPlayView(
            x.Id, x.Type, x.Prompt, x.Points,
            x.Options.Select(o => new OptionPlayView(o.Id, o.Text)).ToList()
        )).ToList();

        return Ok(new QuizPlayView(
            quiz.Id, quiz.Title, quiz.Description, quiz.TimeLimitSeconds,
            quiz.ShuffleQuestions, quiz.ShuffleOptions, q
        ));
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
            .Select(o => new Option { Id = string.IsNullOrWhiteSpace(o.Id) ? Guid.NewGuid().ToString("N") : o.Id!, Text = o.Text.Trim() })
            .ToList();

        question.CorrectBool = q.CorrectBool;
        question.CorrectOptionIds = q.CorrectOptionIds ?? new();
        question.AcceptedAnswers = (q.AcceptedAnswers ?? new()).Select(Normalize).Where(x => x.Length > 0).Distinct().ToList();

        return question;
    }

    private static string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();
}