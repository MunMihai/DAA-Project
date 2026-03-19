using MongoDB.Driver;
using Quiz.CodingService.Data;
using Quiz.CodingService.Engine;
using Quiz.CodingService.Models;

namespace Quiz.CodingService.Services;

public sealed class LiveCodingHistoryService(MongoContext db)
{
    public async Task RecordSessionCreatedAsync(string sessionCode, Ruleset ruleset, int timeLimitSeconds, CancellationToken ct = default)
    {
        var existing = await db.Sessions.Find(x => x.SessionCode == sessionCode).FirstOrDefaultAsync(ct);
        if (existing is not null)
            return;

        await db.Sessions.InsertOneAsync(new LiveCodingSessionHistory
        {
            SessionCode = sessionCode,
            TaskName = string.IsNullOrWhiteSpace(ruleset.name) ? "Live Coding Task" : ruleset.name,
            Language = ruleset.language,
            TimeLimitSeconds = timeLimitSeconds,
            RuleCount = ruleset.rules?.Count ?? 0,
            Status = "lobby",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken: ct);
    }

    public Task RecordSessionStartedAsync(string sessionCode, CancellationToken ct = default)
    {
        var update = Builders<LiveCodingSessionHistory>.Update
            .Set(x => x.Status, "running")
            .Set(x => x.StartedAt, DateTimeOffset.UtcNow);

        return db.Sessions.UpdateOneAsync(x => x.SessionCode == sessionCode, update, cancellationToken: ct);
    }

    public Task RecordSessionEndedAsync(string sessionCode, CancellationToken ct = default)
    {
        var update = Builders<LiveCodingSessionHistory>.Update
            .Set(x => x.Status, "ended")
            .Set(x => x.EndedAt, DateTimeOffset.UtcNow);

        return db.Sessions.UpdateOneAsync(x => x.SessionCode == sessionCode, update, cancellationToken: ct);
    }

    public Task UpsertParticipantAsync(string sessionCode, string playerId, string displayName, CancellationToken ct = default)
    {
        var update = Builders<LiveCodingParticipantHistory>.Update
            .SetOnInsert(x => x.SessionCode, sessionCode)
            .SetOnInsert(x => x.PlayerId, playerId)
            .SetOnInsert(x => x.JoinedAt, DateTimeOffset.UtcNow)
            .Set(x => x.DisplayName, displayName)
            .Set(x => x.LastSeenAt, DateTimeOffset.UtcNow);

        return db.Participants.UpdateOneAsync(
            x => x.SessionCode == sessionCode && x.PlayerId == playerId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task RecordSubmissionAsync(
        string sessionCode,
        string playerId,
        string displayName,
        string studentCode,
        ValidationResult result,
        int currentScore,
        CancellationToken ct = default)
    {
        await db.Submissions.InsertOneAsync(new LiveCodingSubmissionHistory
        {
            SessionCode = sessionCode,
            PlayerId = playerId,
            DisplayName = displayName,
            StudentCode = studentCode,
            Passed = result.Passed,
            PointsEarned = currentScore,
            SubmittedAt = DateTimeOffset.UtcNow,
            Violations = result.Violations.Select(x => new CodingViolationHistory
            {
                RuleId = x.RuleId,
                Message = x.Message
            }).ToList()
        }, cancellationToken: ct);

        var update = Builders<LiveCodingParticipantHistory>.Update
            .SetOnInsert(x => x.SessionCode, sessionCode)
            .SetOnInsert(x => x.PlayerId, playerId)
            .SetOnInsert(x => x.JoinedAt, DateTimeOffset.UtcNow)
            .Set(x => x.DisplayName, displayName)
            .Set(x => x.LastSeenAt, DateTimeOffset.UtcNow)
            .Set(x => x.LastSubmittedAt, DateTimeOffset.UtcNow)
            .Set(x => x.LatestScore, currentScore)
            .Inc(x => x.SubmissionCount, 1);

        await db.Participants.UpdateOneAsync(
            x => x.SessionCode == sessionCode && x.PlayerId == playerId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task<List<LiveCodingHistoryItemDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var sessions = await db.Sessions
            .Find(FilterDefinition<LiveCodingSessionHistory>.Empty)
            .SortByDescending(x => x.CreatedAt)
            .Limit(100)
            .ToListAsync(ct);

        if (sessions.Count == 0)
            return new List<LiveCodingHistoryItemDto>();

        var codes = sessions.Select(x => x.SessionCode).ToList();
        var participants = await db.Participants.Find(x => codes.Contains(x.SessionCode)).ToListAsync(ct);
        var submissions = await db.Submissions.Find(x => codes.Contains(x.SessionCode)).ToListAsync(ct);

        return sessions.Select(session => new LiveCodingHistoryItemDto(
            session.SessionCode,
            string.IsNullOrWhiteSpace(session.TaskName) ? session.SessionCode : session.TaskName,
            session.Language,
            session.Status,
            session.CreatedAt,
            session.StartedAt,
            session.EndedAt,
            session.TimeLimitSeconds,
            session.RuleCount,
            participants.Count(x => x.SessionCode == session.SessionCode),
            submissions.Count(x => x.SessionCode == session.SessionCode)
        )).ToList();
    }

    public async Task<LiveCodingHistoryDetailDto?> GetHistoryDetailAsync(string sessionCode, CancellationToken ct = default)
    {
        var session = await db.Sessions.Find(x => x.SessionCode == sessionCode).FirstOrDefaultAsync(ct);
        if (session is null)
            return null;

        var participants = await db.Participants
            .Find(x => x.SessionCode == sessionCode)
            .SortBy(x => x.DisplayName)
            .ToListAsync(ct);

        var submissions = await db.Submissions
            .Find(x => x.SessionCode == sessionCode)
            .SortBy(x => x.DisplayName)
            .ThenByDescending(x => x.SubmittedAt)
            .ToListAsync(ct);

        var detailParticipants = participants.Select(participant => new LiveCodingParticipantHistoryDto(
            participant.PlayerId,
            participant.DisplayName,
            participant.LatestScore,
            participant.SubmissionCount,
            participant.JoinedAt,
            participant.LastSeenAt,
            participant.LastSubmittedAt,
            submissions
                .Where(x => x.PlayerId == participant.PlayerId)
                .OrderByDescending(x => x.SubmittedAt)
                .Select(x => new LiveCodingSubmissionHistoryDto(
                    x.Id,
                    x.Passed,
                    x.PointsEarned,
                    x.SubmittedAt,
                    x.StudentCode,
                    x.Violations.Select(v => new CodingViolationDto(v.RuleId, v.Message)).ToList()
                ))
                .ToList()
        )).ToList();

        return new LiveCodingHistoryDetailDto(
            session.SessionCode,
            string.IsNullOrWhiteSpace(session.TaskName) ? session.SessionCode : session.TaskName,
            session.Language,
            session.Status,
            session.CreatedAt,
            session.StartedAt,
            session.EndedAt,
            session.TimeLimitSeconds,
            session.RuleCount,
            detailParticipants
        );
    }
}

public sealed record LiveCodingHistoryItemDto(
    string SessionCode,
    string TaskName,
    string Language,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int TimeLimitSeconds,
    int RuleCount,
    int ParticipantCount,
    int SubmissionCount
);

public sealed record LiveCodingHistoryDetailDto(
    string SessionCode,
    string TaskName,
    string Language,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int TimeLimitSeconds,
    int RuleCount,
    List<LiveCodingParticipantHistoryDto> Participants
);

public sealed record LiveCodingParticipantHistoryDto(
    string PlayerId,
    string DisplayName,
    int LatestScore,
    int SubmissionCount,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastSubmittedAt,
    List<LiveCodingSubmissionHistoryDto> Submissions
);

public sealed record LiveCodingSubmissionHistoryDto(
    string SubmissionId,
    bool Passed,
    int PointsEarned,
    DateTimeOffset SubmittedAt,
    string StudentCode,
    List<CodingViolationDto> Violations
);

public sealed record CodingViolationDto(string RuleId, string Message);
