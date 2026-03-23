using MongoDB.Driver;
using Quiz.LiveSessionService.Data;
using Quiz.LiveSessionService.Models;
using Quiz.LiveSessionService.State;

namespace Quiz.LiveSessionService.Services;

public sealed class LiveQuizHistoryService(MongoContext db)
{
    public async Task RecordSessionCreatedAsync(string sessionCode, string quizId, CancellationToken ct = default)
    {
        var existing = await db.Sessions.Find(x => x.SessionCode == sessionCode).FirstOrDefaultAsync(ct);
        if (existing is not null)
            return;

        await db.Sessions.InsertOneAsync(new LiveQuizSessionHistory
        {
            SessionCode = sessionCode,
            QuizId = quizId,
            Status = "lobby",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken: ct);
    }

    public Task RecordSessionStartedAsync(string sessionCode, QuizSnapshot snapshot, CancellationToken ct = default)
    {
        var update = Builders<LiveQuizSessionHistory>.Update
            .Set(x => x.Status, "running")
            .Set(x => x.QuizId, snapshot.QuizId)
            .Set(x => x.QuizTitle, snapshot.Title)
            .Set(x => x.TimeLimitSeconds, snapshot.TimeLimitSeconds)
            .Set(x => x.QuestionCount, snapshot.Questions.Count)
            .Set(x => x.StartedAt, DateTimeOffset.UtcNow);

        return db.Sessions.UpdateOneAsync(
            x => x.SessionCode == sessionCode,
            update,
            new UpdateOptions { IsUpsert = false },
            ct);
    }

    public Task RecordSessionEndedAsync(string sessionCode, CancellationToken ct = default)
    {
        var update = Builders<LiveQuizSessionHistory>.Update
            .Set(x => x.Status, "ended")
            .Set(x => x.EndedAt, DateTimeOffset.UtcNow);

        return db.Sessions.UpdateOneAsync(x => x.SessionCode == sessionCode, update, cancellationToken: ct);
    }

    public Task UpsertParticipantAsync(string sessionCode, string playerId, string displayName, CancellationToken ct = default)
    {
        var update = Builders<LiveQuizParticipantHistory>.Update
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

    public async Task RecordAnswerAsync(
        string sessionCode,
        string playerId,
        string displayName,
        int questionIndex,
        QuestionPublicSnapshot question,
        QuestionCorrectSnapshot? correctQuestion,
        AnswerPayload payload,
        AnswerResult result,
        int currentScore,
        CancellationToken ct = default)
    {
        await db.Answers.InsertOneAsync(new LiveQuizAnswerHistory
        {
            SessionCode = sessionCode,
            PlayerId = playerId,
            DisplayName = displayName,
            QuestionIndex = questionIndex,
            QuestionId = question.Id,
            QuestionType = question.Type,
            Prompt = question.Prompt,
            SubmittedAnswer = FormatAnswer(question, payload),
            OfficialAnswer = FormatOfficialAnswer(question, correctQuestion),
            IsCorrect = result.IsCorrect,
            PointsEarned = result.PointsEarned,
            SubmittedAt = DateTimeOffset.UtcNow
        }, cancellationToken: ct);

        var participantUpdate = Builders<LiveQuizParticipantHistory>.Update
            .SetOnInsert(x => x.SessionCode, sessionCode)
            .SetOnInsert(x => x.PlayerId, playerId)
            .SetOnInsert(x => x.JoinedAt, DateTimeOffset.UtcNow)
            .Set(x => x.DisplayName, displayName)
            .Set(x => x.LastSeenAt, DateTimeOffset.UtcNow)
            .Set(x => x.Score, currentScore)
            .Inc(x => x.AnswerCount, 1);

        await db.Participants.UpdateOneAsync(
            x => x.SessionCode == sessionCode && x.PlayerId == playerId,
            participantUpdate,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task<List<LiveQuizHistoryItemDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var sessions = await db.Sessions
            .Find(FilterDefinition<LiveQuizSessionHistory>.Empty)
            .SortByDescending(x => x.CreatedAt)
            .Limit(100)
            .ToListAsync(ct);

        if (sessions.Count == 0)
            return new List<LiveQuizHistoryItemDto>();

        var codes = sessions.Select(x => x.SessionCode).ToList();
        var participants = await db.Participants.Find(x => codes.Contains(x.SessionCode)).ToListAsync(ct);
        var answers = await db.Answers.Find(x => codes.Contains(x.SessionCode)).ToListAsync(ct);

        return sessions.Select(session => new LiveQuizHistoryItemDto(
            session.SessionCode,
            session.QuizId,
            string.IsNullOrWhiteSpace(session.QuizTitle) ? session.QuizId : session.QuizTitle,
            session.Status,
            session.CreatedAt,
            session.StartedAt,
            session.EndedAt,
            session.TimeLimitSeconds,
            session.QuestionCount,
            participants.Count(x => x.SessionCode == session.SessionCode),
            answers.Count(x => x.SessionCode == session.SessionCode)
        )).ToList();
    }

    public async Task<LiveQuizHistoryDetailDto?> GetHistoryDetailAsync(
        string sessionCode,
        Func<string, CancellationToken, Task<QuizSnapshot>>? fetchQuizSnapshot = null,
        CancellationToken ct = default)
    {
        var session = await db.Sessions.Find(x => x.SessionCode == sessionCode).FirstOrDefaultAsync(ct);
        if (session is null)
            return null;

        var participants = await db.Participants
            .Find(x => x.SessionCode == sessionCode)
            .SortBy(x => x.DisplayName)
            .ToListAsync(ct);

        var answers = await db.Answers
            .Find(x => x.SessionCode == sessionCode)
            .SortBy(x => x.DisplayName)
            .ThenBy(x => x.QuestionIndex)
            .ToListAsync(ct);

        QuizSnapshot? quizSnapshot = null;
        if (fetchQuizSnapshot is not null && !string.IsNullOrWhiteSpace(session.QuizId))
        {
            try
            {
                quizSnapshot = await fetchQuizSnapshot(session.QuizId, ct);
            }
            catch
            {
                quizSnapshot = null;
            }
        }

        var quizQuestionsById = quizSnapshot?.Questions
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .ToDictionary(x => x.Id, StringComparer.Ordinal) ?? new Dictionary<string, QuestionSnapshot>(StringComparer.Ordinal);

        var detailParticipants = participants.Select(participant => new LiveQuizParticipantHistoryDto(
            participant.PlayerId,
            participant.DisplayName,
            participant.Score,
            participant.JoinedAt,
            participant.LastSeenAt,
            participant.AnswerCount,
            answers
                .Where(x => x.PlayerId == participant.PlayerId)
                .OrderBy(x => x.QuestionIndex)
                .Select(x => new LiveQuizAnswerHistoryDto(
                    x.QuestionIndex,
                    x.QuestionId,
                    x.QuestionType,
                    x.Prompt,
                    x.SubmittedAnswer,
                    ResolveOfficialAnswer(x, quizQuestionsById),
                    x.IsCorrect,
                    x.PointsEarned,
                    x.SubmittedAt
                ))
                .ToList()
        )).ToList();

        return new LiveQuizHistoryDetailDto(
            session.SessionCode,
            session.QuizId,
            string.IsNullOrWhiteSpace(session.QuizTitle) ? session.QuizId : session.QuizTitle,
            session.Status,
            session.CreatedAt,
            session.StartedAt,
            session.EndedAt,
            session.TimeLimitSeconds,
            session.QuestionCount,
            detailParticipants
        );
    }

    private static string FormatAnswer(QuestionPublicSnapshot question, AnswerPayload payload)
    {
        return question.Type switch
        {
            0 => payload.BoolAnswer is null ? "(fara raspuns)" : (payload.BoolAnswer.Value ? "Adevarat" : "Fals"),
            1 => ResolveSingle(question, payload.SingleOptionId),
            2 => ResolveMultiple(question, payload.MultipleOptionIds),
            3 => string.IsNullOrWhiteSpace(payload.TextAnswer) ? "(fara raspuns)" : payload.TextAnswer.Trim(),
            _ => "(nesuportat)"
        };
    }

    private static string ResolveOfficialAnswer(
        LiveQuizAnswerHistory answer,
        IReadOnlyDictionary<string, QuestionSnapshot> quizQuestionsById)
    {
        if (!string.IsNullOrWhiteSpace(answer.OfficialAnswer))
            return answer.OfficialAnswer;

        return quizQuestionsById.TryGetValue(answer.QuestionId, out var question)
            ? FormatOfficialAnswer(question)
            : "";
    }

    private static string FormatOfficialAnswer(QuestionPublicSnapshot question, QuestionCorrectSnapshot? correctQuestion)
    {
        if (correctQuestion is null)
            return "";

        return FormatOfficialAnswer(
            question.Type,
            question.Options,
            correctQuestion.CorrectBool,
            correctQuestion.CorrectOptionIds,
            correctQuestion.AcceptedAnswers);
    }

    private static string FormatOfficialAnswer(QuestionSnapshot question)
    {
        return FormatOfficialAnswer(
            question.Type,
            question.Options,
            question.CorrectBool,
            question.CorrectOptionIds,
            question.AcceptedAnswers);
    }

    private static string FormatOfficialAnswer(
        int questionType,
        List<OptionSnapshot> options,
        bool? correctBool,
        List<string>? correctOptionIds,
        List<string>? acceptedAnswers)
    {
        return questionType switch
        {
            0 => correctBool is null ? "" : (correctBool.Value ? "Adevarat" : "Fals"),
            1 => ResolveCorrectOptions(options, correctOptionIds),
            2 => ResolveCorrectOptions(options, correctOptionIds),
            3 => ResolveAcceptedAnswers(acceptedAnswers),
            _ => ""
        };
    }

    private static string ResolveSingle(QuestionPublicSnapshot question, string? optionId)
    {
        if (string.IsNullOrWhiteSpace(optionId))
            return "(fara raspuns)";

        return question.Options.FirstOrDefault(x => x.Id == optionId)?.Text ?? optionId;
    }

    private static string ResolveMultiple(QuestionPublicSnapshot question, List<string>? optionIds)
    {
        if (optionIds is null || optionIds.Count == 0)
            return "(fara raspuns)";

        var texts = optionIds
            .Select(id => question.Options.FirstOrDefault(x => x.Id == id)?.Text ?? id)
            .ToList();

        return string.Join(", ", texts);
    }

    private static string ResolveCorrectOptions(List<OptionSnapshot> options, List<string>? optionIds)
    {
        if (optionIds is null || optionIds.Count == 0)
            return "";

        var texts = optionIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => options.FirstOrDefault(x => x.Id == id)?.Text ?? id)
            .ToList();

        return texts.Count == 0 ? "" : string.Join(", ", texts);
    }

    private static string ResolveAcceptedAnswers(List<string>? acceptedAnswers)
    {
        if (acceptedAnswers is null || acceptedAnswers.Count == 0)
            return "";

        return string.Join(" / ", acceptedAnswers.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
    }
}

public sealed record LiveQuizHistoryItemDto(
    string SessionCode,
    string QuizId,
    string QuizTitle,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int TimeLimitSeconds,
    int QuestionCount,
    int ParticipantCount,
    int AnswerCount
);

public sealed record LiveQuizHistoryDetailDto(
    string SessionCode,
    string QuizId,
    string QuizTitle,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    int TimeLimitSeconds,
    int QuestionCount,
    List<LiveQuizParticipantHistoryDto> Participants
);

public sealed record LiveQuizParticipantHistoryDto(
    string PlayerId,
    string DisplayName,
    int Score,
    DateTimeOffset JoinedAt,
    DateTimeOffset LastSeenAt,
    int AnswerCount,
    List<LiveQuizAnswerHistoryDto> Answers
);

public sealed record LiveQuizAnswerHistoryDto(
    int QuestionIndex,
    string QuestionId,
    int QuestionType,
    string Prompt,
    string SubmittedAnswer,
    string OfficialAnswer,
    bool IsCorrect,
    int PointsEarned,
    DateTimeOffset SubmittedAt
);
