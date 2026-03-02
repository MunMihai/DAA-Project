using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Quiz.CodingConsole;

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
                        violations.Add(new(rule.id, $"Unsupported rule type '{rule.type}'."));
                        break;
                }
            }
            catch (Exception ex)
            {
                violations.Add(new(rule.id, $"Rule execution error: {ex.Message}"));
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
            violations.Add(new(rule.id, "Missing params.alias"));
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
            violations.Add(new(rule.id, $"No interface found meeting minImplementations={minImpl}."));
            return;
        }

        _typeAliases[alias] = candidates[0].I;
    }

    private void BindAbstractClass(RuleDef rule, RoslynSymbolIndex index, List<Violation> violations)
    {
        var alias = P(rule, "alias");
        if (string.IsNullOrWhiteSpace(alias))
        {
            violations.Add(new(rule.id, "Missing params.alias"));
            return;
        }

        if (index.AbstractClasses.Count == 0)
        {
            violations.Add(new(rule.id, "No abstract class found in submission."));
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
            violations.Add(new(rule.id, "Missing params.alias or params.implementsAlias"));
            return;
        }

        if (!TryGetType(implementsAlias, out var iface))
        {
            violations.Add(new(rule.id, $"Type alias '{implementsAlias}' not bound."));
            return;
        }

        var candidates = index.ConcreteClasses
            .Where(c => c.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface)))
            .ToList();

        if (candidates.Count == 0)
        {
            violations.Add(new(rule.id, $"No concrete class implements '{iface.Name}' (alias {implementsAlias})."));
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
            violations.Add(new(rule.id, "Missing params.alias or params.baseAlias"));
            return;
        }

        if (!TryGetType(baseAlias, out var baseType))
        {
            violations.Add(new(rule.id, $"Type alias '{baseAlias}' not bound."));
            return;
        }

        var subs = index.ConcreteClasses.Where(c => InheritsFrom(c, baseType)).ToList();
        if (subs.Count < minCount)
        {
            violations.Add(new(rule.id, $"Expected at least {minCount} concrete subclass(es) of '{baseType.Name}'. Found: {subs.Count}."));
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
            violations.Add(new(rule.id, "Missing params.alias or params.onTypeAlias"));
            return;
        }

        if (!TryGetType(onTypeAlias, out var type))
        {
            violations.Add(new(rule.id, $"Type alias '{onTypeAlias}' not bound."));
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
                violations.Add(new(rule.id, $"Return type alias '{returnsAlias}' not bound."));
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
            violations.Add(new(rule.id, $"No method found on '{type.Name}' matching constraints."));
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
            violations.Add(new(rule.id, "Missing params.childAlias or params.baseAlias"));
            return;
        }

        if (!TryGetType(childAlias, out var child))
        {
            violations.Add(new(rule.id, $"Type alias '{childAlias}' not bound. (Ruleset incomplete or wrong bind order)"));
            return;
        }

        if (!TryGetType(baseAlias, out var baseType))
        {
            violations.Add(new(rule.id, $"Type alias '{baseAlias}' not bound. (Ruleset incomplete or wrong bind order)"));
            return;
        }

        if (!InheritsFrom(child, baseType))
            violations.Add(new(rule.id, $"Type '{child.Name}' does not inherit from '{baseType.Name}'."));
    }

    private void RequireImplements(RuleDef rule, List<Violation> violations)
    {
        var typeAlias = P(rule, "typeAlias");
        var ifaceAlias = P(rule, "interfaceAlias");

        if (string.IsNullOrWhiteSpace(typeAlias) || string.IsNullOrWhiteSpace(ifaceAlias))
        {
            violations.Add(new(rule.id, "Missing params.typeAlias or params.interfaceAlias"));
            return;
        }

        if (!TryGetType(typeAlias, out var t))
        {
            violations.Add(new(rule.id, $"Type alias '{typeAlias}' not bound."));
            return;
        }

        if (!TryGetType(ifaceAlias, out var iface))
        {
            violations.Add(new(rule.id, $"Type alias '{ifaceAlias}' not bound."));
            return;
        }

        if (!t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface)))
            violations.Add(new(rule.id, $"Type '{t.Name}' does not implement '{iface.Name}'."));
    }

    private void RequireMethodOverride(RuleDef rule, List<Violation> violations)
    {
        var typeAlias = P(rule, "typeAlias");
        var overridesMethodAlias = P(rule, "overridesMethodAlias");

        if (string.IsNullOrWhiteSpace(typeAlias) || string.IsNullOrWhiteSpace(overridesMethodAlias))
        {
            violations.Add(new(rule.id, "Missing params.typeAlias or params.overridesMethodAlias"));
            return;
        }

        if (!TryGetType(typeAlias, out var t))
        {
            violations.Add(new(rule.id, $"Type alias '{typeAlias}' not bound."));
            return;
        }

        if (!TryGetMethod(overridesMethodAlias, out var m))
        {
            violations.Add(new(rule.id, $"Method alias '{overridesMethodAlias}' not bound."));
            return;
        }

        var ok = t.GetMembers().OfType<IMethodSymbol>().Any(mm =>
            mm.IsOverride && mm.OverriddenMethod is not null &&
            SymbolEqualityComparer.Default.Equals(mm.OverriddenMethod, m));

        if (!ok)
            violations.Add(new(rule.id, $"Type '{t.Name}' does not override method '{m.Name}'."));
    }

    private void RequireCall(RuleDef rule, SyntaxNode root, SemanticModel model, List<Violation> violations)
    {
        var callerTypeAlias = P(rule, "callerTypeAlias");
        var calleeMethodAlias = P(rule, "calleeMethodAlias");
        var excludeMethodAlias = P(rule, "excludeMethodAlias"); // optional

        if (string.IsNullOrWhiteSpace(callerTypeAlias) || string.IsNullOrWhiteSpace(calleeMethodAlias))
        {
            violations.Add(new(rule.id, "Missing params.callerTypeAlias or params.calleeMethodAlias"));
            return;
        }

        if (!TryGetType(callerTypeAlias, out var callerType))
        {
            violations.Add(new(rule.id, $"Type alias '{callerTypeAlias}' not bound."));
            return;
        }

        if (!TryGetMethod(calleeMethodAlias, out var callee))
        {
            violations.Add(new(rule.id, $"Method alias '{calleeMethodAlias}' not bound."));
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
            violations.Add(new(rule.id, $"Caller type '{callerType.Name}' not found in syntax tree."));
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
            violations.Add(new(rule.id, $"Expected '{callerType.Name}' to call '{callee.Name}' (alias {calleeMethodAlias})."));
    }

    // -------------------------
    // FORBID
    // -------------------------

    private void ForbidApi(RuleDef rule, SyntaxNode root, List<Violation> violations)
    {
        if (!rule.@params.TryGetValue("contains", out var v) || v is null)
        {
            violations.Add(new(rule.id, "Missing params.contains array."));
            return;
        }

        var list = ToStringList(v);
        if (list.Count == 0)
        {
            violations.Add(new(rule.id, "params.contains is empty."));
            return;
        }

        var src = root.ToFullString();
        foreach (var token in list)
        {
            if (string.IsNullOrWhiteSpace(token)) continue;
            if (src.Contains(token, StringComparison.Ordinal))
            {
                violations.Add(new(rule.id, $"Forbidden API usage detected: '{token}'."));
            }
        }
    }

    private void ForbidObjectCreation(RuleDef rule, SyntaxNode root, SemanticModel model, List<Violation> violations)
    {
        var inTypeAlias = P(rule, "inTypeAlias");
        var forbiddenTypeAlias = P(rule, "forbiddenTypeAlias");

        if (string.IsNullOrWhiteSpace(inTypeAlias) || string.IsNullOrWhiteSpace(forbiddenTypeAlias))
        {
            violations.Add(new(rule.id, "Missing params.inTypeAlias or params.forbiddenTypeAlias"));
            return;
        }

        if (!TryGetType(inTypeAlias, out var inType))
        {
            violations.Add(new(rule.id, $"Type alias '{inTypeAlias}' not bound."));
            return;
        }

        if (!TryGetType(forbiddenTypeAlias, out var forbiddenType))
        {
            violations.Add(new(rule.id, $"Type alias '{forbiddenTypeAlias}' not bound."));
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
            violations.Add(new(rule.id, $"Type '{inType.Name}' not found in syntax tree."));
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
            violations.Add(new(rule.id, $"Forbidden object creation inside '{inType.Name}' for '{forbiddenType.Name}'."));
    }
}