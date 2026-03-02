namespace Quiz.CodingService.Engine;

public sealed class Ruleset
{
    public string name { get; set; } = "";
    public string language { get; set; } = "dotnet";
    public string? notes { get; set; }
    public List<RuleDef> rules { get; set; } = new();
}

public sealed class RuleDef
{
    public string id { get; set; } = "";
    public string type { get; set; } = "";
    public Dictionary<string, object> @params { get; set; } = new();
}

public sealed record Violation(string RuleId, string Message);

public sealed class ValidationResult
{
    public bool Passed { get; init; }
    public List<Violation> Violations { get; init; } = new();
}
