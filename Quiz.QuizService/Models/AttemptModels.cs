using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quiz.QuizService.Models;

public enum AttemptStatus { Started = 0, Submitted = 1, Expired = 2 }

public sealed class QuizAttempt
{
    [BsonId] [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonRepresentation(BsonType.ObjectId)]
    public string QuizId { get; set; } = "";

    public string UserIdOrEmail { get; set; } = ""; // MVP (mai târziu: userId din JWT)

    public AttemptStatus Status { get; set; } = AttemptStatus.Started;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; set; }

    public int TimeLimitSeconds { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public List<AttemptAnswer> Answers { get; set; } = new();

    public int TotalPoints { get; set; }
    public int EarnedPoints { get; set; }

    public List<QuestionResult> Results { get; set; } = new();
}

public sealed class AttemptAnswer
{
    public string QuestionId { get; set; } = "";

    public bool? BoolAnswer { get; set; }              // TrueFalse
    public string? SingleOptionId { get; set; }        // SingleChoice
    public List<string>? MultipleOptionIds { get; set; } // MultipleChoice
    public string? TextAnswer { get; set; }            // ShortText

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class QuestionResult
{
    public string QuestionId { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int EarnedPoints { get; set; }
}