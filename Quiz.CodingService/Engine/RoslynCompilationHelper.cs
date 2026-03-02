using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Quiz.CodingService.Engine;

public static class RoslynCompilationHelper
{
    public static CSharpCompilation CreateCompilation(SyntaxTree tree)
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        var runtime = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "System.Runtime");
        if (runtime is not null && !string.IsNullOrWhiteSpace(runtime.Location))
            refs.Add(MetadataReference.CreateFromFile(runtime.Location));

        return CSharpCompilation.Create(
            assemblyName: "Submission",
            syntaxTrees: new[] { tree },
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
