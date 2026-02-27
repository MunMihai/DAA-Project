using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Quiz.QuizService.Data;
using Quiz.QuizService.DTOs;
using Quiz.QuizService.Models;
using Quiz.QuizService.Services;

namespace Quiz.QuizService.Controllers;

[ApiController]
[Route("api/quiz-attempts")]
public sealed class AttemptsController(MongoContext db, RedisJsonCache rc) : ControllerBase
{
    private static readonly TimeSpan TtlQuizFull = TimeSpan.FromMinutes(10);

    [HttpPost("start")]
    public async Task<ActionResult<StartAttemptResponse>> Start([FromBody] StartAttemptRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.QuizId))
            return BadRequest(new { message = "QuizId is required." });

        if (string.IsNullOrWhiteSpace(req.UserIdOrEmail))
            return BadRequest(new { message = "UserIdOrEmail is required." });

        // ✅ Get quiz from Redis (or Mongo fallback)
        var quizCacheKey = CacheKeys.QuizById(req.QuizId);

        var cachedQuiz = await rc.GetAsync<QuizEntity>(quizCacheKey, ct);
        if (cachedQuiz is not null) Response.Headers["X-Quiz-Cache"] = "HIT";
        else Response.Headers["X-Quiz-Cache"] = "MISS";

        var quiz = cachedQuiz ?? await db.Quizzes.Find(x => x.Id == req.QuizId).FirstOrDefaultAsync(ct);
        if (quiz is null) return NotFound(new { message = "Quiz not found." });

        // if came from DB, cache it
        if (cachedQuiz is null)
            await rc.SetAsync(quizCacheKey, quiz, TtlQuizFull, ct);

        if (quiz.Status != QuizStatus.Published)
            return BadRequest(new { message = "Quiz is not published." });

        var now = DateTimeOffset.UtcNow;

        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            UserIdOrEmail = req.UserIdOrEmail.Trim().ToLowerInvariant(),
            Status = AttemptStatus.Started,
            StartedAt = now,
            TimeLimitSeconds = quiz.TimeLimitSeconds,
            ExpiresAt = now.AddSeconds(quiz.TimeLimitSeconds),
            TotalPoints = quiz.Questions.Sum(q => q.Points)
        };

        await db.Attempts.InsertOneAsync(attempt, cancellationToken: ct);

        return Ok(new StartAttemptResponse(
            attempt.Id,
            attempt.QuizId,
            attempt.StartedAt,
            attempt.ExpiresAt,
            attempt.TimeLimitSeconds
        ));
    }

    [HttpPut("{attemptId}/answer")]
    public async Task<IActionResult> SaveAnswer(string attemptId, [FromBody] SaveAnswerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(attemptId))
            return BadRequest(new { message = "attemptId is required." });

        if (string.IsNullOrWhiteSpace(req.QuestionId))
            return BadRequest(new { message = "QuestionId is required." });

        var attempt = await db.Attempts.Find(x => x.Id == attemptId).FirstOrDefaultAsync(ct);
        if (attempt is null) return NotFound();
        if (attempt.Status != AttemptStatus.Started) return BadRequest(new { message = "Attempt not active." });
        if (DateTimeOffset.UtcNow > attempt.ExpiresAt) return BadRequest(new { message = "Attempt expired." });

        var ans = attempt.Answers.FirstOrDefault(a => a.QuestionId == req.QuestionId);
        if (ans is null)
        {
            ans = new AttemptAnswer { QuestionId = req.QuestionId };
            attempt.Answers.Add(ans);
        }

        ans.BoolAnswer = req.BoolAnswer;
        ans.SingleOptionId = req.SingleOptionId;
        ans.MultipleOptionIds = req.MultipleOptionIds;
        ans.TextAnswer = req.TextAnswer;
        ans.UpdatedAt = DateTimeOffset.UtcNow;

        await db.Attempts.ReplaceOneAsync(x => x.Id == attemptId, attempt, cancellationToken: ct);
        return NoContent();
    }

    [HttpPost("{attemptId}/submit")]
    public async Task<ActionResult<SubmitAttemptResponse>> Submit(string attemptId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(attemptId))
            return BadRequest(new { message = "attemptId is required." });

        var attempt = await db.Attempts.Find(x => x.Id == attemptId).FirstOrDefaultAsync(ct);
        if (attempt is null) return NotFound();
        if (attempt.Status != AttemptStatus.Started) return BadRequest(new { message = "Attempt not active." });

        // ✅ Get quiz from Redis (or Mongo fallback) by attempt.QuizId
        var quizCacheKey = CacheKeys.QuizById(attempt.QuizId);

        var cachedQuiz = await rc.GetAsync<QuizEntity>(quizCacheKey, ct);
        if (cachedQuiz is not null) Response.Headers["X-Quiz-Cache"] = "HIT";
        else Response.Headers["X-Quiz-Cache"] = "MISS";

        var quiz = cachedQuiz ?? await db.Quizzes.Find(x => x.Id == attempt.QuizId).FirstOrDefaultAsync(ct);
        if (quiz is null) return NotFound(new { message = "Quiz not found." });

        // if came from DB, cache it
        if (cachedQuiz is null)
            await rc.SetAsync(quizCacheKey, quiz, TtlQuizFull, ct);

        // scoring
        var (earned, results) = Score(quiz, attempt);

        attempt.EarnedPoints = earned;
        attempt.Results = results;
        attempt.Status = DateTimeOffset.UtcNow > attempt.ExpiresAt ? AttemptStatus.Expired : AttemptStatus.Submitted;
        attempt.SubmittedAt = DateTimeOffset.UtcNow;

        await db.Attempts.ReplaceOneAsync(x => x.Id == attemptId, attempt, cancellationToken: ct);

        return Ok(new SubmitAttemptResponse(
            attempt.Id,
            attempt.TotalPoints,
            attempt.EarnedPoints,
            attempt.Results.Select(r => new QuestionResultDto(r.QuestionId, r.IsCorrect, r.EarnedPoints)).ToList()
        ));
    }

    private static (int earned, List<QuestionResult> results) Score(QuizEntity quiz, QuizAttempt attempt)
    {
        var byQ = attempt.Answers.ToDictionary(a => a.QuestionId, a => a);
        var results = new List<QuestionResult>();
        var earned = 0;

        foreach (var q in quiz.Questions)
        {
            byQ.TryGetValue(q.Id, out var ans);

            var isCorrect = q.Type switch
            {
                QuestionType.TrueFalse =>
                    ans?.BoolAnswer is not null && q.CorrectBool is not null && ans.BoolAnswer.Value == q.CorrectBool.Value,

                QuestionType.SingleChoice =>
                    !string.IsNullOrWhiteSpace(ans?.SingleOptionId)
                    && q.CorrectOptionIds.Count == 1
                    && ans!.SingleOptionId == q.CorrectOptionIds[0],

                QuestionType.MultipleChoice =>
                    ans?.MultipleOptionIds is not null
                    && SetEquals(ans.MultipleOptionIds, q.CorrectOptionIds),

                QuestionType.ShortText =>
                    !string.IsNullOrWhiteSpace(ans?.TextAnswer)
                    && q.AcceptedAnswers.Any(accepted => Normalize(accepted) == Normalize(ans!.TextAnswer)),

                _ => false
            };

            var points = isCorrect ? q.Points : 0;
            earned += points;

            results.Add(new QuestionResult
            {
                QuestionId = q.Id,
                IsCorrect = isCorrect,
                EarnedPoints = points
            });
        }

        return (earned, results);

        static bool SetEquals(List<string> a, List<string> b)
        {
            var sa = new HashSet<string>(a.Where(x => !string.IsNullOrWhiteSpace(x)));
            var sb = new HashSet<string>(b.Where(x => !string.IsNullOrWhiteSpace(x)));
            return sa.SetEquals(sb);
        }

        static string Normalize(string? s) => (s ?? "").Trim().ToLowerInvariant();
    }
}