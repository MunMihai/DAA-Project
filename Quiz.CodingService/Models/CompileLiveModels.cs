namespace Quiz.CodingService.Models;

public static class CompileCodingLanguages
{
    public const string CSharp = "csharp";
    public const string Python = "python";
    public const string JavaScript = "javascript";

    public static readonly string[] All = [CSharp, Python, JavaScript];

    public static string Normalize(string? language) => (language ?? "").Trim().ToLowerInvariant();

    public static bool IsSupported(string? language) => All.Contains(Normalize(language));

    public static string DisplayName(string? language) =>
        Normalize(language) switch
        {
            CSharp => "C#",
            Python => "Python",
            JavaScript => "JavaScript",
            _ => Normalize(language)
        };
}

public sealed class CompileCodingSessionDefinition
{
    public string Title { get; set; } = "";
    public int TimeLimitSeconds { get; set; } = 1800;
    public List<string> AllowedLanguages { get; set; } = [];
    public List<CompileCodingTaskDefinition> Tasks { get; set; } = [];
}

public sealed class CompileCodingTaskDefinition
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string ProblemStatement { get; set; } = "";
    public string InputDescription { get; set; } = "";
    public string OutputDescription { get; set; } = "";
    public string ExampleInput { get; set; } = "";
    public string ExampleOutput { get; set; } = "";
    public int Points { get; set; } = 100;
    public List<CompileCodingTestCase> TestCases { get; set; } = [];
}

public sealed class CompileCodingTestCase
{
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public bool IsExample { get; set; }
}

public sealed class CompileCodingTaskPublicSnapshot
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string ProblemStatement { get; set; } = "";
    public string InputDescription { get; set; } = "";
    public string OutputDescription { get; set; } = "";
    public string ExampleInput { get; set; } = "";
    public string ExampleOutput { get; set; } = "";
    public int Points { get; set; }
}

public sealed class CompileCodingSessionPublicSnapshot
{
    public string Title { get; set; } = "";
    public List<string> AllowedLanguages { get; set; } = [];
    public List<CompileCodingTaskPublicSnapshot> Tasks { get; set; } = [];
}

public sealed class CompileCodingCaseResult
{
    public string Input { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    public string ActualOutput { get; set; } = "";
    public bool Passed { get; set; }
    public bool IsExample { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class CompileCodingSubmissionResult
{
    public string TaskId { get; set; } = "";
    public string Language { get; set; } = "";
    public bool Passed { get; set; }
    public int PassedCaseCount { get; set; }
    public int TotalCaseCount { get; set; }
    public int BestTaskScore { get; set; }
    public int ScoreDelta { get; set; }
    public int TotalScore { get; set; }
    public string? CompileError { get; set; }
    public string? RuntimeError { get; set; }
    public List<CompileCodingCaseResult> Cases { get; set; } = [];
}
