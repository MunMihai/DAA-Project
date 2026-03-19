namespace Quiz.CodingService.Data;

public sealed class MongoOptions
{
    public string? ConnectionString { get; set; } = "";
    public string Database { get; set; } = "";
}
