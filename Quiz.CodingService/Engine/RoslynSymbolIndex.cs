using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Quiz.CodingService.Engine;

public sealed class RoslynSymbolIndex
{
    public List<INamedTypeSymbol> AllTypes { get; init; } = new();
    public List<INamedTypeSymbol> Interfaces { get; init; } = new();
    public List<INamedTypeSymbol> AbstractClasses { get; init; } = new();
    public List<INamedTypeSymbol> ConcreteClasses { get; init; } = new();

    public Dictionary<INamedTypeSymbol, int> InterfaceImplementationCounts { get; init; } =
        new(NamedTypeSymbolComparer.Instance);

    public static RoslynSymbolIndex Build(Compilation compilation)
    {
        var declared = new List<INamedTypeSymbol>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var td in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(td) is INamedTypeSymbol sym)
                    declared.Add(sym);
            }
        }

        // de-dup (partial classes etc.)
        declared = declared.Distinct(NamedTypeSymbolComparer.Instance).ToList();

        var interfaces = declared.Where(t => t.TypeKind == TypeKind.Interface).ToList();
        var abstractClasses = declared.Where(t => t.TypeKind == TypeKind.Class && t.IsAbstract).ToList();
        var concreteClasses = declared.Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract).ToList();

        var counts = new Dictionary<INamedTypeSymbol, int>(NamedTypeSymbolComparer.Instance);
        foreach (var i in interfaces) counts[i] = 0;

        foreach (var c in concreteClasses)
        {
            foreach (var i in c.AllInterfaces)
            {
                var match = interfaces.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x, i));
                if (match is not null)
                    counts[match] = counts[match] + 1;
            }
        }

        return new RoslynSymbolIndex
        {
            AllTypes = declared,
            Interfaces = interfaces,
            AbstractClasses = abstractClasses,
            ConcreteClasses = concreteClasses,
            InterfaceImplementationCounts = counts
        };
    }
}

internal sealed class NamedTypeSymbolComparer : IEqualityComparer<INamedTypeSymbol>
{
    public static readonly NamedTypeSymbolComparer Instance = new();

    public bool Equals(INamedTypeSymbol? x, INamedTypeSymbol? y) =>
        SymbolEqualityComparer.Default.Equals(x, y);

    public int GetHashCode(INamedTypeSymbol obj) =>
        SymbolEqualityComparer.Default.GetHashCode(obj);
}
