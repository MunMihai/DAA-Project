using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;
using Quiz.CodingService.Engine;
using Quiz.CodingService.Services;

namespace Quiz.CodingService.Controllers;

[ApiController]
[Route("api/coding-quiz")]
public class CodingEvaluationController : ControllerBase
{
    private readonly GroqClient _groqClient;
    private static readonly JsonSerializerOptions JsonOpt = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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

            var draft = JsonSerializer.Deserialize<RuleBasedTaskDraft>(rulesetJson, JsonOpt);
            if (draft is null || draft.ruleset is null)
                return StatusCode(502, new { message = "Agentul AI nu a returnat un draft valid." });
            if (string.IsNullOrWhiteSpace(draft.taskTitle) || string.IsNullOrWhiteSpace(draft.studentTask))
                return StatusCode(502, new { message = "Agentul AI nu a returnat sarcina formulată complet." });
            if (string.IsNullOrWhiteSpace(draft.ruleset.name) || draft.ruleset.rules.Count == 0)
                return StatusCode(502, new { message = "Agentul AI nu a returnat un ruleset valid." });

            var initialValidation = RuleBasedRulesetValidator.ValidateReferenceCode(request.ReferenceCode, draft.ruleset);
            if (!initialValidation.Passed)
            {
                var stabilizedRuleset = RuleBasedRulesetValidator.BuildStableRuleset(
                    request.ReferenceCode,
                    draft.ruleset,
                    out var removedRuleIds);

                var stabilizedValidation = RuleBasedRulesetValidator.ValidateReferenceCode(request.ReferenceCode, stabilizedRuleset);
                if (!stabilizedValidation.Passed || stabilizedRuleset.rules.Count == 0)
                {
                    return StatusCode(502, new
                    {
                        message = "Draftul AI nu a putut fi stabilizat pe codul de referință.",
                        violations = initialValidation.Violations
                    });
                }

                draft.ruleset = stabilizedRuleset;
                draft.teacherNotes = BuildTeacherNotes(
                    draft.teacherNotes,
                    $"Draftul a fost validat automat pe codul de referință. Au fost eliminate {removedRuleIds.Count} reguli instabile: {string.Join(", ", removedRuleIds)}.");
            }
            else
            {
                draft.teacherNotes = BuildTeacherNotes(
                    draft.teacherNotes,
                    "Draftul a fost validat automat pe codul de referință și soluția profesorului trece toate regulile.");
            }

            return Ok(draft);
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
                    Violations = errors
                        .Select(e => new Violation("COMPILATION_ERROR", $"Codul nu compilează. Corectează eroarea de C# și retrimite soluția. Detaliu: {e}"))
                        .ToList() 
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

    private static string BuildTeacherNotes(string? current, string suffix)
    {
        if (string.IsNullOrWhiteSpace(current))
            return suffix;

        return $"{current.Trim()}\n\n{suffix}";
    }
}
