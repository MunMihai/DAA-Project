using Quiz.QuizService.Models;

namespace Quiz.QuizService.DTOs;

public sealed record QuizCreateRequest(
    string Title,
    string Description,
    List<string> Tags,
    int TimeLimitSeconds,
    bool ShuffleQuestions,
    bool ShuffleOptions,
    List<QuestionUpsert> Questions
);

public sealed record QuizUpdateRequest(
    string Title,
    string Description,
    QuizStatus Status,
    List<string> Tags,
    int TimeLimitSeconds,
    bool ShuffleQuestions,
    bool ShuffleOptions,
    List<QuestionUpsert> Questions
);

public sealed record QuestionUpsert(
    string? Id, // null => new
    QuestionType Type,
    string Prompt,
    string? Explanation,
    int Points,
    List<OptionUpsert> Options,
    bool? CorrectBool,
    List<string> CorrectOptionIds,
    List<string> AcceptedAnswers,
    string? Topic
);

public sealed record OptionUpsert(string? Id, string Text);

// View for playing (safe)
public sealed record QuizPlayView(
    string QuizId,
    string Title,
    string Description,
    int TimeLimitSeconds,
    bool ShuffleQuestions,
    bool ShuffleOptions,
    List<QuestionPlayView> Questions
);

public sealed record QuestionPlayView(
    string Id,
    QuestionType Type,
    string Prompt,
    int Points,
    List<OptionPlayView> Options
);

public sealed record OptionPlayView(string Id, string Text);