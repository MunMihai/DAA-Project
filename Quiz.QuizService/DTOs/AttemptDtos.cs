namespace Quiz.QuizService.DTOs;

public sealed record StartAttemptRequest(string QuizId, string UserIdOrEmail);

public sealed record StartAttemptResponse(
    string AttemptId,
    string QuizId,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    int TimeLimitSeconds
);

public sealed record SaveAnswerRequest(
    string QuestionId,
    bool? BoolAnswer,
    string? SingleOptionId,
    List<string>? MultipleOptionIds,
    string? TextAnswer
);

public sealed record SubmitAttemptResponse(
    string AttemptId,
    int TotalPoints,
    int EarnedPoints,
    List<QuestionResultDto> Results
);

public sealed record QuestionResultDto(string QuestionId, bool IsCorrect, int EarnedPoints);