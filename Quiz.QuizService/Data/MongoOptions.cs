namespace Quiz.QuizService.Data;

public sealed class MongoOptions
{
    public string ConnectionString { get; init; } = "";
    public string Database { get; init; } = "";
}