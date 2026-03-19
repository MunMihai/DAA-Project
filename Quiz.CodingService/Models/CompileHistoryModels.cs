using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quiz.CodingService.Models;

public sealed class LiveCompileCodingSessionHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "lobby";
    public int TimeLimitSeconds { get; set; }
    public int TaskCount { get; set; }
    public List<string> AllowedLanguages { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public sealed class LiveCompileCodingParticipantHistory
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

public sealed class LiveCompileCodingSubmissionHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public string SessionCode { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string TaskTitle { get; set; } = "";
    public string Language { get; set; } = "";
    public string StudentCode { get; set; } = "";
    public bool Passed { get; set; }
    public int ScoreDelta { get; set; }
    public int BestTaskScore { get; set; }
    public int TotalScore { get; set; }
    public int PassedCaseCount { get; set; }
    public int TotalCaseCount { get; set; }
    public string? CompileError { get; set; }
    public string? RuntimeError { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CompileCodingCaseHistory> Cases { get; set; } = [];
}

public sealed class CompileCodingCaseHistory
{
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public string ActualOutput { get; set; } = "";
    public bool Passed { get; set; }
    public bool IsExample { get; set; }
    public string? ErrorMessage { get; set; }
}
