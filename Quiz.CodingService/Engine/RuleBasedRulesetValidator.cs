using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Quiz.CodingService.Engine;

public static class RuleBasedRulesetValidator
{
    public static ValidationResult ValidateReferenceCode(string referenceCode, Ruleset ruleset)
    {
        if (string.IsNullOrWhiteSpace(referenceCode))
        {
            return new ValidationResult
            {
                Passed = false,
                Violations = [new Violation("REFERENCE_CODE", "Codul de referință este obligatoriu.")]
            };
        }

        if (ruleset is null)
        {
            return new ValidationResult
            {
                Passed = false,
                Violations = [new Violation("RULESET", "Ruleset-ul este obligatoriu.")]
            };
        }

        var tree = CSharpSyntaxTree.ParseText(referenceCode);
        var compilation = RoslynCompilationHelper.CreateCompilation(tree);
        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            return new ValidationResult
            {
                Passed = false,
                Violations = errors
                    .Select(error => new Violation("COMPILATION_ERROR", $"Codul nu compilează. Corectează eroarea de C# și retrimite soluția. Detaliu: {error}"))
                    .ToList()
            };
        }

        var index = RoslynSymbolIndex.Build(compilation);
        var engine = new RoslynRuleEngine(ruleset);
        return engine.Evaluate(tree, compilation, index);
    }

    public static Ruleset BuildStableRuleset(
        string referenceCode,
        Ruleset ruleset,
        out List<string> removedRuleIds)
    {
        removedRuleIds = [];
        var keptRules = new List<RuleDef>();

        foreach (var rule in ruleset.rules)
        {
            var candidate = CloneRuleset(ruleset, [.. keptRules, CloneRule(rule)]);
            var validation = ValidateReferenceCode(referenceCode, candidate);

            if (validation.Passed)
            {
                keptRules.Add(CloneRule(rule));
                continue;
            }

            removedRuleIds.Add(rule.id);
        }

        return CloneRuleset(ruleset, keptRules);
    }

    private static Ruleset CloneRuleset(Ruleset source, List<RuleDef> rules) =>
        new()
        {
            name = source.name,
            language = source.language,
            notes = source.notes,
            rules = rules
        };

    private static RuleDef CloneRule(RuleDef source) =>
        new()
        {
            id = source.id,
            type = source.type,
            studentMessage = source.studentMessage,
            @params = new Dictionary<string, object>(source.@params, StringComparer.Ordinal)
        };
}
