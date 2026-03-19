using MongoDB.Bson;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Quiz.CodingService.Models;

namespace Quiz.CodingService.Data;

public sealed class MongoContext
{
    public IMongoCollection<LiveCodingSessionHistory> Sessions { get; }
    public IMongoCollection<LiveCodingParticipantHistory> Participants { get; }
    public IMongoCollection<LiveCodingSubmissionHistory> Submissions { get; }
    public IMongoCollection<LiveCompileCodingSessionHistory> CompileSessions { get; }
    public IMongoCollection<LiveCompileCodingParticipantHistory> CompileParticipants { get; }
    public IMongoCollection<LiveCompileCodingSubmissionHistory> CompileSubmissions { get; }
    public IMongoCollection<CompileCodingTemplate> CompileTemplates { get; }
    public IMongoCollection<BsonDocument> CompileTemplateDocuments { get; }

    public MongoContext(IOptions<MongoOptions> opt)
    {
        var client = new MongoClient(opt.Value.ConnectionString);
        var db = client.GetDatabase(opt.Value.Database);

        Sessions = db.GetCollection<LiveCodingSessionHistory>("live_coding_sessions");
        Participants = db.GetCollection<LiveCodingParticipantHistory>("live_coding_participants");
        Submissions = db.GetCollection<LiveCodingSubmissionHistory>("live_coding_submissions");
        CompileSessions = db.GetCollection<LiveCompileCodingSessionHistory>("live_compile_coding_sessions");
        CompileParticipants = db.GetCollection<LiveCompileCodingParticipantHistory>("live_compile_coding_participants");
        CompileSubmissions = db.GetCollection<LiveCompileCodingSubmissionHistory>("live_compile_coding_submissions");
        CompileTemplates = db.GetCollection<CompileCodingTemplate>("compile_coding_templates");
        CompileTemplateDocuments = db.GetCollection<BsonDocument>("compile_coding_templates");
    }
}
