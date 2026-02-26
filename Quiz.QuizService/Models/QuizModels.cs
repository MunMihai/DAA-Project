using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quiz.QuizService.Models;

public enum QuizStatus { Draft = 0, Published = 1, Archived = 2 }
public enum QuestionType { TrueFalse = 0, SingleChoice = 1, MultipleChoice = 2, ShortText = 3 }

public sealed class QuizEntity
{
    [BsonId] [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public QuizStatus Status { get; set; } = QuizStatus.Draft;

    // categorii: ex "Networking", "Linux", "OOP" etc.
    public List<string> Tags { get; set; } = new();

    // control desfășurare
    public int TimeLimitSeconds { get; set; } = 600; // default 10 min
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;

    public List<Question> Questions { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Question
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public QuestionType Type { get; set; }

    public string Prompt { get; set; } = "";
    public string? Explanation { get; set; }

    public int Points { get; set; } = 1;

    // For choices:
    public List<Option> Options { get; set; } = new();

    // Correct answer representation:
    // - TrueFalse: use CorrectBool
    public bool? CorrectBool { get; set; }

    // - SingleChoice/MultipleChoice: store option ids
    public List<string> CorrectOptionIds { get; set; } = new();

    // - ShortText: accepted answers (normalized)
    public List<string> AcceptedAnswers { get; set; } = new();

    // Optional: for future difficulty, topic, etc.
    public string? Topic { get; set; }
}

public sealed class Option
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
}