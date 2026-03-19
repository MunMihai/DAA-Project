using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Quiz.LiveSessionService.Models;

namespace Quiz.LiveSessionService.Data;

public sealed class MongoContext
{
    public IMongoCollection<LiveQuizSessionHistory> Sessions { get; }
    public IMongoCollection<LiveQuizParticipantHistory> Participants { get; }
    public IMongoCollection<LiveQuizAnswerHistory> Answers { get; }

    public MongoContext(IOptions<MongoOptions> opt)
    {
        var client = new MongoClient(opt.Value.ConnectionString);
        var db = client.GetDatabase(opt.Value.Database);

        Sessions = db.GetCollection<LiveQuizSessionHistory>("live_quiz_sessions");
        Participants = db.GetCollection<LiveQuizParticipantHistory>("live_quiz_participants");
        Answers = db.GetCollection<LiveQuizAnswerHistory>("live_quiz_answers");
    }
}
