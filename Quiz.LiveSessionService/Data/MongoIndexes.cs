using MongoDB.Driver;

namespace Quiz.LiveSessionService.Data;

public static class MongoIndexes
{
    public static async Task EnsureAsync(MongoContext db)
    {
        await db.Sessions.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Models.LiveQuizSessionHistory>(
                Builders<Models.LiveQuizSessionHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)),
            new CreateIndexModel<Models.LiveQuizSessionHistory>(
                Builders<Models.LiveQuizSessionHistory>.IndexKeys
                    .Descending(x => x.CreatedAt))
        });

        await db.Participants.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Models.LiveQuizParticipantHistory>(
                Builders<Models.LiveQuizParticipantHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)
                    .Ascending(x => x.PlayerId)),
            new CreateIndexModel<Models.LiveQuizParticipantHistory>(
                Builders<Models.LiveQuizParticipantHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)
                    .Ascending(x => x.DisplayName))
        });

        await db.Answers.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Models.LiveQuizAnswerHistory>(
                Builders<Models.LiveQuizAnswerHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)
                    .Ascending(x => x.PlayerId)
                    .Ascending(x => x.QuestionIndex)),
            new CreateIndexModel<Models.LiveQuizAnswerHistory>(
                Builders<Models.LiveQuizAnswerHistory>.IndexKeys
                    .Descending(x => x.SubmittedAt))
        });
    }
}
