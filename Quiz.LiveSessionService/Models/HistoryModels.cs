using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quiz.LiveSessionService.Models;

public sealed class LiveQuizSessionHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string QuizId { get; set; } = "";
    public string QuizTitle { get; set; } = "";
    public string Status { get; set; } = "lobby";
    public int TimeLimitSeconds { get; set; }
    public int QuestionCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public sealed class LiveQuizParticipantHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Score { get; set; }
    public int AnswerCount { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LiveQuizAnswerHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int QuestionIndex { get; set; }
    public string QuestionId { get; set; } = "";
    public int QuestionType { get; set; }
    public string Prompt { get; set; } = "";
    public string SubmittedAnswer { get; set; } = "";
    public string OfficialAnswer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public int PointsEarned { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
}
