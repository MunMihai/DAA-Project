namespace Quiz.LiveSessionService.Messaging;

public record PlayerJoined(string SessionCode, string PlayerId, string DisplayName);
public record SessionStarted(string SessionCode, DateTimeOffset StartedAt);
public record QuestionStarted(string SessionCode, string QuestionId, int QuestionIndex, DateTimeOffset StartedAt);
public record AnswerSubmitted(string SessionCode, string PlayerId, string QuestionId, DateTimeOffset At);
public record ScoreUpdated(string SessionCode, Dictionary<string,int> Scores);
public record SessionEnded(string SessionCode, DateTimeOffset EndedAt);