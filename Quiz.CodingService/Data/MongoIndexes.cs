using MongoDB.Bson;
using MongoDB.Driver;
using Quiz.CodingService.Models;

namespace Quiz.CodingService.Data;

public static class MongoIndexes
{
    public static async Task EnsureAsync(MongoContext db)
    {
        await db.Sessions.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<LiveCodingSessionHistory>(
                Builders<LiveCodingSessionHistory>.IndexKeys.Ascending(x => x.SessionCode)),
            new CreateIndexModel<LiveCodingSessionHistory>(
                Builders<LiveCodingSessionHistory>.IndexKeys.Descending(x => x.CreatedAt))
        });

        await db.Participants.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<LiveCodingParticipantHistory>(
                Builders<LiveCodingParticipantHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)
                    .Ascending(x => x.PlayerId))
        });

        await db.Submissions.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<LiveCodingSubmissionHistory>(
                Builders<LiveCodingSubmissionHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)
                    .Ascending(x => x.PlayerId)
                    .Descending(x => x.SubmittedAt)),
            new CreateIndexModel<LiveCodingSubmissionHistory>(
                Builders<LiveCodingSubmissionHistory>.IndexKeys
                    .Descending(x => x.SubmittedAt))
        });

        await db.CompileSessions.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<LiveCompileCodingSessionHistory>(
                Builders<LiveCompileCodingSessionHistory>.IndexKeys.Ascending(x => x.SessionCode)),
            new CreateIndexModel<LiveCompileCodingSessionHistory>(
                Builders<LiveCompileCodingSessionHistory>.IndexKeys.Descending(x => x.CreatedAt))
        });

        await db.CompileParticipants.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<LiveCompileCodingParticipantHistory>(
                Builders<LiveCompileCodingParticipantHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)
                    .Ascending(x => x.PlayerId))
        });

        await db.CompileSubmissions.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<LiveCompileCodingSubmissionHistory>(
                Builders<LiveCompileCodingSubmissionHistory>.IndexKeys
                    .Ascending(x => x.SessionCode)
                    .Ascending(x => x.PlayerId)
                    .Descending(x => x.SubmittedAt)),
            new CreateIndexModel<LiveCompileCodingSubmissionHistory>(
                Builders<LiveCompileCodingSubmissionHistory>.IndexKeys
                    .Descending(x => x.SubmittedAt))
        });

        await EnsureCompileTemplateIndexesAsync(db);
    }

    private static async Task EnsureCompileTemplateIndexesAsync(MongoContext db)
    {
        var cursor = await db.CompileTemplateDocuments.Indexes.ListAsync();
        var existingIndexes = await cursor.ToListAsync();

        if (!HasIndex(existingIndexes, new BsonDocument("slug", 1)))
        {
            await db.CompileTemplateDocuments.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("slug"),
                    new CreateIndexOptions { Unique = true }));
        }

        if (!HasIndex(existingIndexes, new BsonDocument("fingerprint", 1)))
        {
            await db.CompileTemplateDocuments.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("fingerprint")));
        }

        if (!HasIndex(existingIndexes, new BsonDocument("updatedAt", -1)))
        {
            await db.CompileTemplateDocuments.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Descending("updatedAt")));
        }
    }

    private static bool HasIndex(IEnumerable<BsonDocument> indexes, BsonDocument expectedKey)
    {
        return indexes.Any(index =>
            index.TryGetValue("key", out var keyValue)
            && keyValue.IsBsonDocument
            && keyValue.AsBsonDocument.Equals(expectedKey));
    }
}
