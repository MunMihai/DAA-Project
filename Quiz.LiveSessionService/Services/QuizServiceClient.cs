using System.Net.Http.Json;
using System.Text.Json;
using Quiz.LiveSessionService.State;

namespace Quiz.LiveSessionService.Services;

/// <summary>
/// HTTP client that calls Quiz.QuizService internal APIs.
/// Uses the /api/quizzes/{id} endpoint (full admin view — includes correct answers).
/// This service should be in the same private network as QuizService.
/// </summary>
public sealed class QuizServiceClient(HttpClient http, ILogger<QuizServiceClient> log)
{
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web);

    public async Task<bool> QuizExistsAndPublished(string quizId, CancellationToken ct = default)
    {
        try
        {
            var resp = await http.GetAsync($"/api/quizzes/{quizId}", ct);
            if (!resp.IsSuccessStatusCode) return false;

            var quiz = await resp.Content.ReadFromJsonAsync<QuizDto>(JsonOpt, ct);
            return quiz?.Status == 1; // Published
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "QuizExistsAndPublished failed for {QuizId}", quizId);
            return false;
        }
    }

    /// <summary>
    /// Fetches full quiz (with correct answers) to build the server-side snapshot.
    /// This call must NEVER be proxied through the public gateway.
    /// </summary>
    public async Task<QuizSnapshot> FetchQuizSnapshot(string quizId, CancellationToken ct = default)
    {
        var resp = await http.GetAsync($"/api/quizzes/{quizId}", ct);
        resp.EnsureSuccessStatusCode();

        var quiz = await resp.Content.ReadFromJsonAsync<QuizDto>(JsonOpt, ct)
                   ?? throw new InvalidOperationException($"Quiz {quizId} returned null.");

        if (quiz.Status != 1)
            throw new InvalidOperationException($"Quiz {quizId} is not published (status={quiz.Status}).");

        return new QuizSnapshot
        {
            QuizId = quiz.Id,
            Title = quiz.Title,
            TimeLimitSeconds = quiz.TimeLimitSeconds > 0 ? quiz.TimeLimitSeconds : 30,
            Questions = (quiz.Questions ?? new()).Select(q => new QuestionSnapshot
            {
                Id = q.Id,
                Type = q.Type,
                Prompt = q.Prompt,
                Points = q.Points > 0 ? q.Points : 1,
                Options = (q.Options ?? new()).Select(o => new OptionSnapshot
                {
                    Id = o.Id,
                    Text = o.Text
                }).ToList(),
                CorrectBool = q.CorrectBool,
                CorrectOptionIds = q.CorrectOptionIds ?? new(),
                AcceptedAnswers = q.AcceptedAnswers ?? new()
            }).ToList()
        };
    }

    // ── Internal DTOs (mirrors QuizService response) ──────────────────────────
    private sealed class QuizDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public int Status { get; set; }
        public int TimeLimitSeconds { get; set; }
        public List<QuestionDto>? Questions { get; set; }
    }

    private sealed class QuestionDto
    {
        public string Id { get; set; } = "";
        public int Type { get; set; }
        public string Prompt { get; set; } = "";
        public int Points { get; set; }
        public List<OptionDto>? Options { get; set; }
        public bool? CorrectBool { get; set; }
        public List<string>? CorrectOptionIds { get; set; }
        public List<string>? AcceptedAnswers { get; set; }
    }

    private sealed class OptionDto
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
    }
}