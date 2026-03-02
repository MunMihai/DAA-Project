using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Quiz.CodingService.Engine;
using Quiz.CodingService.Services;

namespace Quiz.CodingService.Controllers;

[ApiController]
[Route("api/coding-quiz")]
public class CodingEvaluationController : ControllerBase
{
    private readonly GroqClient _groqClient;

    public CodingEvaluationController(GroqClient groqClient)
    {
        _groqClient = groqClient;
    }

    public record GenerateRulesetRequest(string ReferenceCode);

    [HttpPost("generate-ruleset")]
    public async Task<IActionResult> GenerateRuleset([FromBody] GenerateRulesetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceCode))
            return BadRequest("Reference code is required.");

        try
        {
            var rulesetJson = await _groqClient.ChatAsync(
                systemPrompt: GenerateRulesetPrompt.System,
                userPrompt: GenerateRulesetPrompt.BuildUserPrompt(request.ReferenceCode));

            return Content(rulesetJson, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    public record EvaluateRequest(string StudentCode, Ruleset Ruleset);

    [HttpPost("evaluate")]
    public IActionResult Evaluate([FromBody] EvaluateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StudentCode) || request.Ruleset == null)
            return BadRequest("Student code and Ruleset are required.");

        try
        {
            var tree = CSharpSyntaxTree.ParseText(request.StudentCode);
            var compilation = RoslynCompilationHelper.CreateCompilation(tree);
            var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            if (errors.Count > 0)
            {
                return Ok(new ValidationResult 
                { 
                    Passed = false, 
                    Violations = errors.Select(e => new Violation("COMPILATION_ERROR", e.ToString())).ToList() 
                });
            }

            var index = RoslynSymbolIndex.Build(compilation);
            var engine = new RoslynRuleEngine(request.Ruleset);
            var result = engine.Evaluate(tree, compilation, index);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
}
