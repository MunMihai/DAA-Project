using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Quiz.CodingConsole;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var apiKey = "gsk_....";
var model = "llama-3.3-70b-versatile";
var groq = new GroqClient(apiKey, model);

// Upload-sim paths
var professorPath = "/Users/mihaimusteata/Desktop/DAA-Project/Quiz.CodingConsole/Submissions/Professor/Reference.cs";
var studentsDir = "/Users/mihaimusteata/Desktop/DAA-Project/Quiz.CodingConsole/Submissions/Students";
var cachedRulesetPath = "/Users/mihaimusteata/Desktop/DAA-Project/Quiz.CodingConsole/Submissions/ruleset.generated.json";

if (!File.Exists(professorPath))
{
    Console.WriteLine($"Missing professor file: {professorPath}");
    return;
}

if (!Directory.Exists(studentsDir))
{
    Console.WriteLine($"Missing students folder: {studentsDir}");
    return;
}

var professorCode = File.ReadAllText(professorPath);

Console.WriteLine("=== PROFESSOR CODE (UPLOAD) ===");
Console.WriteLine(professorCode);
Console.WriteLine();

// 1) Get ruleset (cached if exists)
string rulesetJson;
if (File.Exists(cachedRulesetPath))
{
    rulesetJson = File.ReadAllText(cachedRulesetPath);
    Console.WriteLine($"=== USING CACHED RULESET: {cachedRulesetPath} ===");
}
else
{
    Console.WriteLine("=== GROQ: GENERATE RULESET (JSON) ===");
    rulesetJson = await groq.ChatAsync(
        systemPrompt: GenerateRulesetPrompt.System,
        userPrompt: GenerateRulesetPrompt.BuildUserPrompt(professorCode));

    File.WriteAllText(cachedRulesetPath, rulesetJson);
    Console.WriteLine($"Ruleset saved to: {cachedRulesetPath}");
}

Console.WriteLine(rulesetJson);
Console.WriteLine();

// 2) Parse ruleset
var ruleset = RulesetParser.Parse(rulesetJson);

// 3) Validate each student submission
var studentFiles = Directory.GetFiles(studentsDir, "*.cs").OrderBy(x => x).ToList();
Console.WriteLine("=== VALIDATION RESULTS ===");

foreach (var file in studentFiles)
{
    Console.WriteLine($"\n--- {Path.GetFileName(file)} ---");

    var studentCode = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(studentCode);

    var compilation = RoslynCompilationHelper.CreateCompilation(tree);
    var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    if (errors.Count > 0)
    {
        Console.WriteLine("Verdict: CE (Compilation Error)");
        foreach (var e in errors) Console.WriteLine(e.ToString());
        continue;
    }

    // Index semantic facts once
    var index = RoslynSymbolIndex.Build(compilation);

    // Apply ruleset
    var engine = new RoslynRuleEngine(ruleset);
    var result = engine.Evaluate(tree, compilation, index);

    Console.WriteLine($"Verdict: {(result.Passed ? "PASS" : "RULE_FAIL")}");

    if (!result.Passed)
    {
        foreach (var v in result.Violations)
            Console.WriteLine($"- [{v.RuleId}] {v.Message}");
    }
}