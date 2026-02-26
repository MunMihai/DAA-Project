using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Quiz.QuizService.Models;

namespace Quiz.QuizService.Data;

public sealed class MongoContext
{
    public IMongoCollection<QuizEntity> Quizzes { get; }
    public IMongoCollection<QuizAttempt> Attempts { get; }

    public MongoContext(IOptions<MongoOptions> opt)
    {
        var client = new MongoClient(opt.Value.ConnectionString);
        var db = client.GetDatabase(opt.Value.Database);

        Quizzes = db.GetCollection<QuizEntity>("quizzes");
        Attempts = db.GetCollection<QuizAttempt>("quiz_attempts");
    }
}