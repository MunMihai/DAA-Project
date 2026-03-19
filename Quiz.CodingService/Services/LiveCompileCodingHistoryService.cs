using MongoDB.Driver;
using Quiz.CodingService.Data;
using Quiz.CodingService.Models;

namespace Quiz.CodingService.Services;

public sealed class LiveCompileCodingHistoryService(MongoContext db)
{
    public async Task RecordSessionCreatedAsync(string sessionCode, CompileCodingSessionDefinition definition, CancellationToken ct = default)
    {
        var existing = await db.CompileSessions.Find(x => x.SessionCode == sessionCode).FirstOrDefaultAsync(ct);
        if (existing is not null)
            return;

        await db.CompileSessions.InsertOneAsync(new LiveCompileCodingSessionHistory
        {
            SessionCode = sessionCode,
            Title = string.IsNullOrWhiteSpace(definition.Title) ? sessionCode : definition.Title,
            Status = "lobby",
            TimeLimitSeconds = definition.TimeLimitSeconds,
            TaskCount = definition.Tasks.Count,
            AllowedLanguages = definition.AllowedLanguages.ToList(),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken: ct);
    }

    public Task RecordSessionStartedAsync(string sessionCode, CancellationToken ct = default)
    {
        var update = Builders<LiveCompileCodingSessionHistory>.Update
            .Set(x => x.Status, "running")
            .Set(x => x.StartedAt, DateTimeOffset.UtcNow);

        return db.CompileSessions.UpdateOneAsync(x => x.SessionCode == sessionCode, update, cancellationToken: ct);
    }

    public Task RecordSessionEndedAsync(string sessionCode, CancellationToken ct = default)
    {
        var update = Builders<LiveCompileCodingSessionHistory>.Update
            .Set(x => x.Status, "ended")
            .Set(x => x.EndedAt, DateTimeOffset.UtcNow);

        return db.CompileSessions.UpdateOneAsync(x => x.SessionCode == sessionCode, update, cancellationToken: ct);
    }

    public Task UpsertParticipantAsync(string sessionCode, string playerId, string displayName, CancellationToken ct = default)
    {
        var update = Builders<LiveCompileCodingParticipantHistory>.Update
            .SetOnInsert(x => x.SessionCode, sessionCode)
            .SetOnInsert(x => x.PlayerId, playerId)
            .SetOnInsert(x => x.JoinedAt, DateTimeOffset.UtcNow)
            .Set(x => x.DisplayName, displayName)
            .Set(x => x.LastSeenAt, DateTimeOffset.UtcNow);

        return db.CompileParticipants.UpdateOneAsync(
            x => x.SessionCode == sessionCode && x.PlayerId == playerId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task RecordSubmissionAsync(
        string sessionCode,
        string playerId,
        string displayName,
        CompileCodingTaskDefinition task,
        string language,
        string studentCode,
        CompileCodingSubmissionResult result,
        CancellationToken ct = default)
    {
        await db.CompileSubmissions.InsertOneAsync(new LiveCompileCodingSubmissionHistory
        {
            SessionCode = sessionCode,
            PlayerId = playerId,
            DisplayName = displayName,
            TaskId = task.Id,
            TaskTitle = task.Title,
            Language = language,
            StudentCode = studentCode,
            Passed = result.Passed,
            ScoreDelta = result.ScoreDelta,
            BestTaskScore = result.BestTaskScore,
            TotalScore = result.TotalScore,
            PassedCaseCount = result.PassedCaseCount,
            TotalCaseCount = result.TotalCaseCount,
            CompileError = result.CompileError,
            RuntimeError = result.RuntimeError,
            SubmittedAt = DateTimeOffset.UtcNow,
            Cases = result.Cases.Select(x => new CompileCodingCaseHistory
            {
                Input = x.Input,
                ExpectedOutput = x.ExpectedOutput,
                ActualOutput = x.ActualOutput,
                Passed = x.Passed,
                IsExample = x.IsExample,
                ErrorMessage = x.ErrorMessage
            }).ToList()
        }, cancellationToken: ct);

        var update = Builders<LiveCompileCodingParticipantHistory>.Update
            .SetOnInsert(x => x.SessionCode, sessionCode)
            .SetOnInsert(x => x.PlayerId, playerId)
            .SetOnInsert(x => x.JoinedAt, DateTimeOffset.UtcNow)
            .Set(x => x.DisplayName, displayName)
            .Set(x => x.LastSeenAt, DateTimeOffset.UtcNow)
            .Set(x => x.LastSubmittedAt, DateTimeOffset.UtcNow)
            .Set(x => x.LatestScore, result.TotalScore)
            .Inc(x => x.SubmissionCount, 1);

        await db.CompileParticipants.UpdateOneAsync(
            x => x.SessionCode == sessionCode && x.PlayerId == playerId,
            update,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task<List<LiveCompileCodingHistoryItemDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var sessions = await db.CompileSessions
            .Find(FilterDefinition<LiveCompileCodingSessionHistory>.Empty)
            .SortByDescending(x => x.CreatedAt)
            .Limit(100)
            .ToListAsync(ct);

        if (sessions.Count == 0)
            return [];

        var codes = sessions.Select(x => x.SessionCode).ToList();
        var participants = await db.CompileParticipants.Find(x => codes.Contains(x.SessionCode)).ToListAsync(ct);
        var submissions = await db.CompileSubmissions.Find(x => codes.Contains(x.SessionCode)).ToListAsync(ct);

        return sessions.Select(session => new LiveCompileCodingHistoryItemDto(
            session.SessionCode,
            session.Title,
            session.Status,
            session.CreatedAt,
            session.StartedAt,
            session.EndedAt,
            session.TimeLimitSeconds,
            session.TaskCount,
            session.AllowedLanguages,
            participants.Count(x => x.SessionCode == session.SessionCode),
            submissions.Count(x => x.SessionCode == session.SessionCode)
        )).ToList();
    }

    public async Task<LiveCompileCodingHistoryDetailDto?> GetHistoryDetailAsync(string sessionCode, CancellationToken ct = default)
    {
        var session = await db.CompileSessions.Find(x => x.SessionCode == sessionCode).FirstOrDefaultAsync(ct);
        if (session is null)
            return null;

        var participants = await db.CompileParticipants
            .Find(x => x.SessionCode == sessionCode)
            .SortBy(x => x.DisplayName)
            .ToListAsync(ct);

        var submissions = await db.CompileSubmissions
            .Find(x => x.SessionCode == sessionCode)
            .SortBy(x => x.DisplayName)
            .ThenByDescending(x => x.SubmittedAt)
            .ToListAsync(ct);

        var detailParticipants = participants.Select(participant => new LiveCompileCodingParticipantHistoryDto(
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
                .Select(x => new LiveCompileCodingSubmissionHistoryDto(
                    x.Id,
                    x.TaskId,
                    x.TaskTitle,
                    x.Language,
                    x.Passed,
                    x.ScoreDelta,
                    x.BestTaskScore,
                    x.TotalScore,
                    x.PassedCaseCount,
                    x.TotalCaseCount,
                    x.CompileError,
                    x.RuntimeError,
                    x.SubmittedAt,
                    x.StudentCode,
                    x.Cases.Select(c => new CompileCaseResultDto(
                        c.Input,
                        c.ExpectedOutput,
                        c.ActualOutput,
                        c.Passed,
                        c.IsExample,
                        c.ErrorMessage
                    )).ToList()
                ))
                .ToList()
        )).ToList();

        return new LiveCompileCodingHistoryDetailDto(
            session.SessionCode,
            session.Title,
            session.Status,
            session.CreatedAt,
            session.StartedAt,
            session.EndedAt,
            session.TimeLimitSeconds,
            session.TaskCount,
            session.AllowedLanguages,
            detailParticipants
        );
    }
}

public sealed record LiveCompileCodingHistoryItemDto(
    string SessionCode,
    string Title,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int TimeLimitSeconds,
    int TaskCount,
    List<string> AllowedLanguages,
    int ParticipantCount,
    int SubmissionCount
);

public sealed record LiveCompileCodingHistoryDetailDto(
    string SessionCode,
    string Title,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int TimeLimitSeconds,
    int TaskCount,
    List<string> AllowedLanguages,
    List<LiveCompileCodingParticipantHistoryDto> Participants
);

public sealed record LiveCompileCodingParticipantHistoryDto(
    string PlayerId,
    string DisplayName,
    int LatestScore,
    int SubmissionCount,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastSubmittedAt,
    List<LiveCompileCodingSubmissionHistoryDto> Submissions
);

public sealed record LiveCompileCodingSubmissionHistoryDto(
    string SubmissionId,
    string TaskId,
    string TaskTitle,
    string Language,
    bool Passed,
    int ScoreDelta,
    int BestTaskScore,
    int TotalScore,
    int PassedCaseCount,
    int TotalCaseCount,
    string? CompileError,
    string? RuntimeError,
    DateTimeOffset SubmittedAt,
    string StudentCode,
    List<CompileCaseResultDto> Cases
);

public sealed record CompileCaseResultDto(
    string Input,
    string ExpectedOutput,
    string ActualOutput,
    bool Passed,
    bool IsExample,
    string? ErrorMessage
);
