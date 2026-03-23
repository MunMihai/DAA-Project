using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Quiz.CodingService.Engine;

public sealed class RoslynRuleEngine
{
    private readonly Ruleset _ruleset;

    private readonly Dictionary<string, INamedTypeSymbol> _typeAliases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IMethodSymbol> _methodAliases = new(StringComparer.Ordinal);

    public RoslynRuleEngine(Ruleset ruleset) => _ruleset = ruleset;

    public ValidationResult Evaluate(SyntaxTree tree, Compilation compilation, RoslynSymbolIndex index)
    {
        var violations = new List<Violation>();
        var model = compilation.GetSemanticModel(tree);
        var root = tree.GetRoot();

        foreach (var rule in _ruleset.rules)
        {
            try
            {
                switch (rule.type)
                {
                    // --- BINDINGS ---
                    case "bind_interface":
                        BindInterface(rule, index, violations);
                        break;

                    case "bind_abstract_class":
                        BindAbstractClass(rule, index, violations);
                        break;

                    case "bind_concrete_class_implementing":
                        BindConcreteClassImplementing(rule, index, violations);
                        break;

                    case "bind_concrete_subclass_of":
                        BindConcreteSubclassOf(rule, index, violations);
                        break;

                    case "bind_method_on_type":
                        BindMethodOnType(rule, violations);
                        break;

                    // --- REQUIRE ---
                    case "require_inheritance":
                        RequireInheritance(rule, violations);
                        break;

                    case "require_implements":
                        RequireImplements(rule, violations);
                        break;

                    case "require_method_override":
                        RequireMethodOverride(rule, violations);
                        break;

                    case "require_call":
                        RequireCall(rule, root, model, violations);
                        break;

                    // --- FORBID ---
                    case "forbid_api":
                        ForbidApi(rule, root, violations);
                        break;

                    case "forbid_object_creation":
                        ForbidObjectCreation(rule, root, model, violations);
                        break;

                    default:
                        violations.Add(CreateViolation(rule, $"Unsupported rule type '{rule.type}'."));
                        break;
                }
            }
            catch (Exception ex)
            {
                violations.Add(CreateViolation(rule, $"Rule execution error: {ex.Message}"));
            }
        }

        return new ValidationResult { Passed = violations.Count == 0, Violations = violations };
    }

    // -------------------------
    // Param helpers
    // -------------------------

    private static string P(RuleDef rule, string key)
    {
        if (!rule.@params.TryGetValue(key, out var v) || v is null) return "";
        return v.ToString() ?? "";
    }

    private static int PI(RuleDef rule, string key, int def = 0)
    {
        var s = P(rule, key);
        return int.TryParse(s, out var x) ? x : def;
    }

    private static Violation CreateViolation(RuleDef rule, string technicalMessage, string? fallbackStudentMessage = null)
    {
        var explicitStudentMessage = (rule.studentMessage ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(explicitStudentMessage))
            return new Violation(rule.id, explicitStudentMessage);

        var studentMessage = !string.IsNullOrWhiteSpace(fallbackStudentMessage)
            ? fallbackStudentMessage.Trim()
            : GetGenericStudentMessage(rule.type);

        if (string.Equals(studentMessage, technicalMessage, StringComparison.Ordinal))
            return new Violation(rule.id, studentMessage);

        return new Violation(rule.id, $"{studentMessage} Detaliu: {technicalMessage}");
    }

    private static string GetGenericStudentMessage(string ruleType) => ruleType switch
    {
        "bind_interface" => "Lipsește interfața cerută de sarcină sau nu este folosită corect.",
        "bind_abstract_class" => "Lipsește clasa abstractă cerută de sarcină.",
        "bind_concrete_class_implementing" => "Lipsește o clasă concretă care implementează interfața cerută.",
        "bind_concrete_subclass_of" => "Lipsește o clasă concretă care extinde baza cerută.",
        "bind_method_on_type" => "Lipsește metoda cerută pe tipul potrivit.",
        "require_inheritance" => "Relația de moștenire cerută de sarcină nu este respectată.",
        "require_implements" => "Tipul cerut nu implementează interfața așteptată.",
        "require_method_override" => "Metoda cerută nu este suprascrisă corect.",
        "require_call" => "Lipsește apelul obligatoriu dintre componentele cerute.",
        "forbid_api" => "Codul folosește un API interzis pentru această sarcină.",
        "forbid_object_creation" => "Codul creează direct un obiect care nu trebuie instanțiat aici.",
        _ => "Soluția nu respectă una dintre regulile cerute de sarcină."
    };

    private bool TryGetType(string alias, out INamedTypeSymbol type) => _typeAliases.TryGetValue(alias, out type!);
    private bool TryGetMethod(string alias, out IMethodSymbol method) => _methodAliases.TryGetValue(alias, out method!);

    private static bool InheritsFrom(INamedTypeSymbol t, INamedTypeSymbol baseType)
    {
        for (var cur = t.BaseType; cur is not null; cur = cur.BaseType)
            if (SymbolEqualityComparer.Default.Equals(cur, baseType)) return true;
        return false;
    }

    private static List<string> ToStringList(object v)
    {
        if (v is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var res = new List<string>();
                foreach (var x in je.EnumerateArray())
                    if (x.ValueKind == System.Text.Json.JsonValueKind.String)
                        res.Add(x.GetString() ?? "");
                return res;
            }

            if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                return new List<string> { je.GetString() ?? "" };

            return new List<string>();
        }

        if (v is IEnumerable<object> arr)
            return arr.Select(x => x?.ToString() ?? "").ToList();

        return new List<string> { v.ToString() ?? "" };
    }

    // -------------------------
    // BINDINGS
    // -------------------------

    private void BindInterface(RuleDef rule, RoslynSymbolIndex index, List<Violation> violations)
    {
        var alias = P(rule, "alias");
        if (string.IsNullOrWhiteSpace(alias))
        {
            violations.Add(CreateViolation(rule, "Missing params.alias", "Lipsește definiția structurii cerute de această regulă."));
            return;
        }

        var minImpl = PI(rule, "minImplementations", 0);

        var candidates = index.Interfaces
            .Select(i => new { I = i, Count = index.InterfaceImplementationCounts.TryGetValue(i, out var c) ? c : 0 })
            .Where(x => x.Count >= minImpl)
            .OrderByDescending(x => x.Count)
            .ToList();

        if (candidates.Count == 0)
        {
            violations.Add(CreateViolation(rule, $"No interface found meeting minImplementations={minImpl}.", "Lipsește interfața cerută de sarcină sau ea nu este implementată suficient."));
            return;
        }

        _typeAliases[alias] = candidates[0].I;
    }

    private void BindAbstractClass(RuleDef rule, RoslynSymbolIndex index, List<Violation> violations)
    {
        var alias = P(rule, "alias");
        if (string.IsNullOrWhiteSpace(alias))
        {
            violations.Add(CreateViolation(rule, "Missing params.alias", "Lipsește definiția clasei abstracte cerute."));
            return;
        }

        if (index.AbstractClasses.Count == 0)
        {
            violations.Add(CreateViolation(rule, "No abstract class found in submission.", "Soluția trebuie să conțină o clasă abstractă, dar aceasta lipsește."));
            return;
        }

        // Heuristic: abstract class with most public methods
        var chosen = index.AbstractClasses
            .OrderByDescending(a => a.GetMembers().OfType<IMethodSymbol>().Count(m => m.DeclaredAccessibility == Accessibility.Public))
            .First();

        _typeAliases[alias] = chosen;
    }

    private void BindConcreteClassImplementing(RuleDef rule, RoslynSymbolIndex index, List<Violation> violations)
    {
        var alias = P(rule, "alias");
        var implementsAlias = P(rule, "implementsAlias");

        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(implementsAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.alias or params.implementsAlias", "Regula pentru implementarea unei interfețe nu este configurată complet."));
            return;
        }

        if (!TryGetType(implementsAlias, out var iface))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{implementsAlias}' not bound.", "Nu am putut identifica interfața pe care soluția trebuia să o implementeze."));
            return;
        }

        var candidates = index.ConcreteClasses
            .Where(c => c.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface)))
            .ToList();

        if (candidates.Count == 0)
        {
            violations.Add(CreateViolation(rule, $"No concrete class implements '{iface.Name}' (alias {implementsAlias}).", "Lipsește o clasă concretă care să implementeze interfața cerută."));
            return;
        }

        _typeAliases[alias] = candidates[0];
    }

    /// <summary>
    /// Useful to avoid AI inventing "C2". Instead, it can bind a subclass.
    /// params: { "alias":"C2", "baseAlias":"A1", "minCount":1 }
    /// </summary>
    private void BindConcreteSubclassOf(RuleDef rule, RoslynSymbolIndex index, List<Violation> violations)
    {
        var alias = P(rule, "alias");
        var baseAlias = P(rule, "baseAlias");
        var minCount = PI(rule, "minCount", 1);

        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(baseAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.alias or params.baseAlias", "Regula pentru moștenire nu este configurată complet."));
            return;
        }

        if (!TryGetType(baseAlias, out var baseType))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{baseAlias}' not bound.", "Nu am putut identifica tipul de bază pe care soluția trebuia să îl extindă."));
            return;
        }

        var subs = index.ConcreteClasses.Where(c => InheritsFrom(c, baseType)).ToList();
        if (subs.Count < minCount)
        {
            violations.Add(CreateViolation(rule, $"Expected at least {minCount} concrete subclass(es) of '{baseType.Name}'. Found: {subs.Count}.", "Lipsește cel puțin o clasă concretă care să extindă baza cerută."));
            return;
        }

        _typeAliases[alias] = subs[0];
    }

    private void BindMethodOnType(RuleDef rule, List<Violation> violations)
    {
        var alias = P(rule, "alias");
        var onTypeAlias = P(rule, "onTypeAlias");

        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(onTypeAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.alias or params.onTypeAlias", "Regula pentru metoda cerută nu este configurată complet."));
            return;
        }

        if (!TryGetType(onTypeAlias, out var type))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{onTypeAlias}' not bound.", "Nu am putut identifica tipul pe care ar trebui să existe metoda cerută."));
            return;
        }

        var access = P(rule, "access");          // public|any
        var kind = P(rule, "kind");              // abstract|virtual|any
        var returnsAlias = P(rule, "returnsAlias"); // I1|C1|any

        INamedTypeSymbol? returnsType = null;
        if (!string.IsNullOrWhiteSpace(returnsAlias) &&
            !string.Equals(returnsAlias, "any", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetType(returnsAlias, out var rt))
            {
                violations.Add(CreateViolation(rule, $"Return type alias '{returnsAlias}' not bound.", "Regula pentru tipul de retur al metodei nu a putut fi verificată."));
                return;
            }
            returnsType = rt;
        }

        var methods = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .ToList();

        if (string.Equals(access, "public", StringComparison.OrdinalIgnoreCase))
            methods = methods.Where(m => m.DeclaredAccessibility == Accessibility.Public).ToList();

        if (string.Equals(kind, "abstract", StringComparison.OrdinalIgnoreCase))
            methods = methods.Where(m => m.IsAbstract).ToList();
        else if (string.Equals(kind, "virtual", StringComparison.OrdinalIgnoreCase))
            methods = methods.Where(m => m.IsVirtual || m.IsAbstract).ToList();

        if (returnsType is not null)
        {
            methods = methods.Where(m => m.ReturnType is INamedTypeSymbol r && SymbolEqualityComparer.Default.Equals(r, returnsType))
                .ToList();
        }

        if (methods.Count == 0)
        {
            violations.Add(CreateViolation(rule, $"No method found on '{type.Name}' matching constraints.", "Lipsește metoda cerută de sarcină pe tipul potrivit."));
            return;
        }

        _methodAliases[alias] = methods[0];
    }

    // -------------------------
    // REQUIRE
    // -------------------------

    private void RequireInheritance(RuleDef rule, List<Violation> violations)
    {
        var childAlias = P(rule, "childAlias");
        var baseAlias = P(rule, "baseAlias");

        if (string.IsNullOrWhiteSpace(childAlias) || string.IsNullOrWhiteSpace(baseAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.childAlias or params.baseAlias", "Regula pentru relația de moștenire nu este configurată complet."));
            return;
        }

        if (!TryGetType(childAlias, out var child))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{childAlias}' not bound. (Ruleset incomplete or wrong bind order)", "Nu am putut identifica tipul care trebuia să moștenească baza cerută."));
            return;
        }

        if (!TryGetType(baseAlias, out var baseType))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{baseAlias}' not bound. (Ruleset incomplete or wrong bind order)", "Nu am putut identifica tipul de bază cerut de sarcină."));
            return;
        }

        if (!InheritsFrom(child, baseType))
            violations.Add(CreateViolation(rule, $"Type '{child.Name}' does not inherit from '{baseType.Name}'.", "Tipul cerut nu moștenește clasa de bază așteptată."));
    }

    private void RequireImplements(RuleDef rule, List<Violation> violations)
    {
        var typeAlias = P(rule, "typeAlias");
        var ifaceAlias = P(rule, "interfaceAlias");

        if (string.IsNullOrWhiteSpace(typeAlias) || string.IsNullOrWhiteSpace(ifaceAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.typeAlias or params.interfaceAlias", "Regula pentru implementarea interfeței nu este configurată complet."));
            return;
        }

        if (!TryGetType(typeAlias, out var t))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{typeAlias}' not bound.", "Nu am putut identifica tipul care trebuie să implementeze interfața cerută."));
            return;
        }

        if (!TryGetType(ifaceAlias, out var iface))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{ifaceAlias}' not bound.", "Nu am putut identifica interfața cerută de sarcină."));
            return;
        }

        if (!t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface)))
            violations.Add(CreateViolation(rule, $"Type '{t.Name}' does not implement '{iface.Name}'.", "Tipul cerut nu implementează interfața așteptată."));
    }

    private void RequireMethodOverride(RuleDef rule, List<Violation> violations)
    {
        var typeAlias = P(rule, "typeAlias");
        var overridesMethodAlias = P(rule, "overridesMethodAlias");

        if (string.IsNullOrWhiteSpace(typeAlias) || string.IsNullOrWhiteSpace(overridesMethodAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.typeAlias or params.overridesMethodAlias", "Regula pentru override nu este configurată complet."));
            return;
        }

        if (!TryGetType(typeAlias, out var t))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{typeAlias}' not bound.", "Nu am putut identifica tipul care trebuia să suprascrie metoda cerută."));
            return;
        }

        if (!TryGetMethod(overridesMethodAlias, out var m))
        {
            violations.Add(CreateViolation(rule, $"Method alias '{overridesMethodAlias}' not bound.", "Nu am putut identifica metoda care trebuia să fie suprascrisă."));
            return;
        }

        var ok = t.GetMembers().OfType<IMethodSymbol>().Any(mm =>
            mm.IsOverride && mm.OverriddenMethod is not null &&
            SymbolEqualityComparer.Default.Equals(mm.OverriddenMethod, m));

        if (!ok)
            violations.Add(CreateViolation(rule, $"Type '{t.Name}' does not override method '{m.Name}'.", "Metoda cerută nu este suprascrisă corect."));
    }

    private void RequireCall(RuleDef rule, SyntaxNode root, SemanticModel model, List<Violation> violations)
    {
        var callerTypeAlias = P(rule, "callerTypeAlias");
        var calleeMethodAlias = P(rule, "calleeMethodAlias");
        var excludeMethodAlias = P(rule, "excludeMethodAlias"); // optional

        if (string.IsNullOrWhiteSpace(callerTypeAlias) || string.IsNullOrWhiteSpace(calleeMethodAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.callerTypeAlias or params.calleeMethodAlias", "Regula pentru apelul obligatoriu nu este configurată complet."));
            return;
        }

        if (!TryGetType(callerTypeAlias, out var callerType))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{callerTypeAlias}' not bound.", "Nu am putut identifica tipul din care trebuie făcut apelul cerut."));
            return;
        }

        if (!TryGetMethod(calleeMethodAlias, out var callee))
        {
            violations.Add(CreateViolation(rule, $"Method alias '{calleeMethodAlias}' not bound.", "Nu am putut identifica metoda care trebuie apelată."));
            return;
        }

        IMethodSymbol? exclude = null;
        if (!string.IsNullOrWhiteSpace(excludeMethodAlias) && TryGetMethod(excludeMethodAlias, out var ex))
            exclude = ex;

        var callerDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(cd =>
            {
                var sym = model.GetDeclaredSymbol(cd) as INamedTypeSymbol;
                return sym is not null && SymbolEqualityComparer.Default.Equals(sym, callerType);
            });

        if (callerDecl is null)
        {
            violations.Add(CreateViolation(rule, $"Caller type '{callerType.Name}' not found in syntax tree.", "Lipsește tipul din care ar trebui să fie făcut apelul obligatoriu."));
            return;
        }

        bool found = false;

        foreach (var md in callerDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodSym = model.GetDeclaredSymbol(md) as IMethodSymbol;
            if (methodSym is null) continue;

            if (exclude is not null && SymbolEqualityComparer.Default.Equals(methodSym, exclude))
                continue;

            foreach (var inv in md.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var invSym = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
                if (invSym is null) continue;

                if (SymbolEqualityComparer.Default.Equals(invSym.OriginalDefinition, callee))
                {
                    found = true;
                    break;
                }
            }

            if (found) break;
        }

        if (!found)
            violations.Add(CreateViolation(rule, $"Expected '{callerType.Name}' to call '{callee.Name}' (alias {calleeMethodAlias}).", "Lipsește apelul obligatoriu dintre metodele sau componentele cerute."));
    }

    // -------------------------
    // FORBID
    // -------------------------

    private void ForbidApi(RuleDef rule, SyntaxNode root, List<Violation> violations)
    {
        if (!rule.@params.TryGetValue("contains", out var v) || v is null)
        {
            violations.Add(CreateViolation(rule, "Missing params.contains array.", "Regula pentru API-urile interzise nu este configurată complet."));
            return;
        }

        var list = ToStringList(v);
        if (list.Count == 0)
        {
            violations.Add(CreateViolation(rule, "params.contains is empty.", "Lista de API-uri interzise nu este configurată corect."));
            return;
        }

        var src = root.ToFullString();
        foreach (var token in list)
        {
            if (string.IsNullOrWhiteSpace(token)) continue;
            if (src.Contains(token, StringComparison.Ordinal))
            {
                violations.Add(CreateViolation(rule, $"Forbidden API usage detected: '{token}'.", "Codul folosește un API interzis pentru această sarcină."));
            }
        }
    }

    private void ForbidObjectCreation(RuleDef rule, SyntaxNode root, SemanticModel model, List<Violation> violations)
    {
        var inTypeAlias = P(rule, "inTypeAlias");
        var forbiddenTypeAlias = P(rule, "forbiddenTypeAlias");

        if (string.IsNullOrWhiteSpace(inTypeAlias) || string.IsNullOrWhiteSpace(forbiddenTypeAlias))
        {
            violations.Add(CreateViolation(rule, "Missing params.inTypeAlias or params.forbiddenTypeAlias", "Regula pentru instanțiere interzisă nu este configurată complet."));
            return;
        }

        if (!TryGetType(inTypeAlias, out var inType))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{inTypeAlias}' not bound.", "Nu am putut identifica tipul în care instanțierea ar trebui interzisă."));
            return;
        }

        if (!TryGetType(forbiddenTypeAlias, out var forbiddenType))
        {
            violations.Add(CreateViolation(rule, $"Type alias '{forbiddenTypeAlias}' not bound.", "Nu am putut identifica tipul care nu trebuie instanțiat direct."));
            return;
        }

        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(cd =>
            {
                var sym = model.GetDeclaredSymbol(cd) as INamedTypeSymbol;
                return sym is not null && SymbolEqualityComparer.Default.Equals(sym, inType);
            });

        if (classDecl is null)
        {
            violations.Add(CreateViolation(rule, $"Type '{inType.Name}' not found in syntax tree.", "Lipsește tipul în care ar trebui evitată instanțierea directă."));
            return;
        }

        bool found = false;

        foreach (var obj in classDecl.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var t = model.GetTypeInfo(obj).Type as INamedTypeSymbol;
            if (t is null) continue;

            if (SymbolEqualityComparer.Default.Equals(t, forbiddenType))
            {
                found = true;
                break;
            }

            if (forbiddenType.TypeKind == TypeKind.Interface &&
                t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, forbiddenType)))
            {
                found = true;
                break;
            }
        }

        if (found)
            violations.Add(CreateViolation(rule, $"Forbidden object creation inside '{inType.Name}' for '{forbiddenType.Name}'.", "Codul instanțiază direct un tip care ar trebui evitat în această zonă."));
    }
}
