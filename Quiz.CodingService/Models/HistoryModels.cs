using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quiz.CodingService.Models;

public sealed class LiveCodingSessionHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string TaskName { get; set; } = "";
    public string Language { get; set; } = "";
    public string Status { get; set; } = "lobby";
    public int TimeLimitSeconds { get; set; }
    public int RuleCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public sealed class LiveCodingParticipantHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int LatestScore { get; set; }
    public int SubmissionCount { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSubmittedAt { get; set; }
}

public sealed class LiveCodingSubmissionHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string StudentCode { get; set; } = "";
    public bool Passed { get; set; }
    public int PointsEarned { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CodingViolationHistory> Violations { get; set; } = new();
}

public sealed class CodingViolationHistory
{
    public string RuleId { get; set; } = "";
    public string Message { get; set; } = "";
}
