using MongoDB.Driver;
using Quiz.QuizService.Models;

namespace Quiz.QuizService.Data;

public static class MongoIndexes
{
    public static async Task EnsureAsync(MongoContext ctx)
    {
        await ctx.Quizzes.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<QuizEntity>(Builders<QuizEntity>.IndexKeys.Ascending(x => x.Status)),
            new CreateIndexModel<QuizEntity>(Builders<QuizEntity>.IndexKeys.Ascending(x => x.Tags)),
            new CreateIndexModel<QuizEntity>(Builders<QuizEntity>.IndexKeys.Descending(x => x.UpdatedAt)),
        });

        await ctx.Attempts.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<QuizAttempt>(Builders<QuizAttempt>.IndexKeys.Ascending(x => x.QuizId)),
            new CreateIndexModel<QuizAttempt>(Builders<QuizAttempt>.IndexKeys.Ascending(x => x.UserIdOrEmail)),
            new CreateIndexModel<QuizAttempt>(Builders<QuizAttempt>.IndexKeys.Ascending(x => x.Status)),
            new CreateIndexModel<QuizAttempt>(Builders<QuizAttempt>.IndexKeys.Ascending(x => x.ExpiresAt)),
        });
    }
}