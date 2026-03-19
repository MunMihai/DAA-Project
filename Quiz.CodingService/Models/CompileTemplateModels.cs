using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quiz.CodingService.Models;

[BsonIgnoreExtraElements]
public sealed class CompileCodingTemplate
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    [BsonElement("slug")]
    public string Slug { get; set; } = "";
    [BsonElement("fingerprint")]
    public string Fingerprint { get; set; } = "";
    [BsonElement("title")]
    public string Title { get; set; } = "";
    [BsonElement("description")]
    public string Description { get; set; } = "";
    [BsonElement("suggestedTimeLimitSeconds")]
    public int SuggestedTimeLimitSeconds { get; set; } = 900;
    [BsonElement("allowedLanguages")]
    public List<string> AllowedLanguages { get; set; } = [];
    [BsonElement("tasks")]
    public List<CompileCodingTemplateTask> Tasks { get; set; } = [];
    [BsonElement("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [BsonElement("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class CompileCodingTemplateTask
{
    [BsonElement("id")]
    public string Id { get; set; } = "";
    [BsonElement("title")]
    public string Title { get; set; } = "";
    [BsonElement("problemStatement")]
    public string ProblemStatement { get; set; } = "";
    [BsonElement("inputDescription")]
    public string InputDescription { get; set; } = "";
    [BsonElement("outputDescription")]
    public string OutputDescription { get; set; } = "";
    [BsonElement("exampleInput")]
    public string ExampleInput { get; set; } = "";
    [BsonElement("exampleOutput")]
    public string ExampleOutput { get; set; } = "";
    [BsonElement("points")]
    public int Points { get; set; } = 100;
    [BsonElement("testCases")]
    public List<CompileCodingTemplateCase> TestCases { get; set; } = [];
    [BsonElement("exampleSolutions")]
    public List<CompileCodingExampleSolution> ExampleSolutions { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class CompileCodingTemplateCase
{
    [BsonElement("input")]
    public string Input { get; set; } = "";
    [BsonElement("expectedOutput")]
    public string ExpectedOutput { get; set; } = "";
    [BsonElement("isExample")]
    public bool IsExample { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class CompileCodingExampleSolution
{
    [BsonElement("language")]
    public string Language { get; set; } = "";
    [BsonElement("sourceCode")]
    public string SourceCode { get; set; } = "";
    [BsonElement("notes")]
    public string Notes { get; set; } = "";
}
