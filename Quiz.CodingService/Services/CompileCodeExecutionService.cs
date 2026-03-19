using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Quiz.CodingService.Models;

namespace Quiz.CodingService.Services;

public sealed class CompileCodeExecutionService(ILogger<CompileCodeExecutionService> log)
{
    private static readonly SemaphoreSlim CSharpExecutionLock = new(1, 1);
    private static readonly Lazy<List<MetadataReference>> CSharpReferences = new(BuildCSharpReferences);
    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(5);

    public async Task<CompileCodingSubmissionResult> EvaluateAsync(
        CompileCodingTaskDefinition task,
        string language,
        string sourceCode,
        CancellationToken ct = default)
    {
        var normalizedLanguage = CompileCodingLanguages.Normalize(language);
        if (!CompileCodingLanguages.IsSupported(normalizedLanguage))
            throw new InvalidOperationException("Limbajul selectat nu este suportat.");

        var testCases = task.TestCases.Count > 0
            ? task.TestCases
            : [new CompileCodingTestCase
            {
                Input = task.ExampleInput,
                ExpectedOutput = task.ExampleOutput,
                IsExample = true
            }];

        var caseResults = new List<CompileCodingCaseResult>();
        string? compileError = null;
        string? runtimeError = null;

        foreach (var testCase in testCases)
        {
            var execution = normalizedLanguage switch
            {
                CompileCodingLanguages.CSharp => await ExecuteCSharpAsync(sourceCode, testCase.Input, ct),
                CompileCodingLanguages.Python => await ExecuteExternalAsync("python3", "Python", ".py", sourceCode, testCase.Input, ct),
                CompileCodingLanguages.JavaScript => await ExecuteExternalAsync("node", "JavaScript", ".js", sourceCode, testCase.Input, ct),
                _ => throw new InvalidOperationException("Limbajul selectat nu este suportat.")
            };

            var actualOutput = NormalizeOutput(execution.StandardOutput);
            var expectedOutput = NormalizeOutput(testCase.ExpectedOutput);
            var casePassed = execution.Succeeded && actualOutput == expectedOutput;

            compileError ??= execution.CompileError;
            runtimeError ??= execution.RuntimeError;

            caseResults.Add(new CompileCodingCaseResult
            {
                Input = testCase.Input,
                ExpectedOutput = testCase.ExpectedOutput,
                ActualOutput = execution.StandardOutput,
                Passed = casePassed,
                IsExample = testCase.IsExample,
                ErrorMessage = execution.CompileError ?? execution.RuntimeError
            });

            if (!execution.Succeeded)
                break;
        }

        var passedCaseCount = caseResults.Count(x => x.Passed);
        var totalCaseCount = testCases.Count;
        var allCasesPassed = passedCaseCount == totalCaseCount && compileError is null && runtimeError is null;
        var bestTaskScore = totalCaseCount == 0
            ? 0
            : (int)Math.Round(task.Points * (double)passedCaseCount / totalCaseCount, MidpointRounding.AwayFromZero);

        return new CompileCodingSubmissionResult
        {
            TaskId = task.Id,
            Language = normalizedLanguage,
            Passed = allCasesPassed,
            PassedCaseCount = passedCaseCount,
            TotalCaseCount = totalCaseCount,
            BestTaskScore = bestTaskScore,
            CompileError = compileError,
            RuntimeError = runtimeError,
            Cases = caseResults
        };
    }

    private async Task<ExecutionOutcome> ExecuteCSharpAsync(string sourceCode, string standardInput, CancellationToken ct)
    {
        await CSharpExecutionLock.WaitAsync(ct);
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var compilation = CSharpCompilation.Create(
                $"CompileSubmission_{Guid.NewGuid():N}",
                [syntaxTree],
                CSharpReferences.Value,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            await using var peStream = new MemoryStream();
            var emit = compilation.Emit(peStream);
            if (!emit.Success)
            {
                var diagnostics = string.Join(Environment.NewLine, emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()));

                return ExecutionOutcome.FromCompileError(diagnostics);
            }

            peStream.Position = 0;
            var loadContext = new AssemblyLoadContext($"submission_{Guid.NewGuid():N}", isCollectible: true);
            try
            {
                var assembly = loadContext.LoadFromStream(peStream);
                var entryPoint = assembly.EntryPoint;
                if (entryPoint is null)
                    return ExecutionOutcome.FromCompileError("Programul trebuie să conțină un punct de intrare valid.");

                var originalIn = Console.In;
                var originalOut = Console.Out;
                var originalErr = Console.Error;
                using var inputReader = new StringReader(standardInput ?? "");
                using var outputWriter = new StringWriter();
                using var errorWriter = new StringWriter();

                try
                {
                    Console.SetIn(inputReader);
                    Console.SetOut(outputWriter);
                    Console.SetError(errorWriter);

                    var runTask = Task.Run(async () =>
                    {
                        object?[]? parameters = entryPoint.GetParameters().Length == 0
                            ? null
                            : [Array.Empty<string>()];

                        var result = entryPoint.Invoke(null, parameters);
                        if (result is Task taskResult)
                            await taskResult.ConfigureAwait(false);
                    }, ct);

                    var completed = await Task.WhenAny(runTask, Task.Delay(ExecutionTimeout, ct));
                    if (completed != runTask)
                        return ExecutionOutcome.FromRuntimeError("Execuția a depășit timpul maxim permis.");

                    await runTask.ConfigureAwait(false);
                    return ExecutionOutcome.Success(outputWriter.ToString(), errorWriter.ToString());
                }
                catch (TargetInvocationException ex)
                {
                    return ExecutionOutcome.FromRuntimeError(ex.InnerException?.Message ?? ex.Message);
                }
                catch (Exception ex)
                {
                    return ExecutionOutcome.FromRuntimeError(ex.Message);
                }
                finally
                {
                    Console.SetIn(originalIn);
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                }
            }
            finally
            {
                loadContext.Unload();
            }
        }
        finally
        {
            CSharpExecutionLock.Release();
        }
    }

    private async Task<ExecutionOutcome> ExecuteExternalAsync(
        string executable,
        string displayLanguage,
        string extension,
        string sourceCode,
        string standardInput,
        CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"compile-live-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, $"main{extension}");

        try
        {
            await File.WriteAllTextAsync(sourcePath, sourceCode, ct);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"\"{sourcePath}\"",
                WorkingDirectory = tempDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                log.LogWarning(ex, "{Language} runtime not available", displayLanguage);
                return ExecutionOutcome.FromCompileError($"Runtime-ul pentru {displayLanguage} nu este disponibil pe server.");
            }

            await process.StandardInput.WriteAsync(standardInput ?? "");
            process.StandardInput.Close();

            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);
            var waitTask = process.WaitForExitAsync(ct);
            var completed = await Task.WhenAny(waitTask, Task.Delay(ExecutionTimeout, ct));
            if (completed != waitTask)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return ExecutionOutcome.FromRuntimeError("Execuția a depășit timpul maxim permis.");
            }

            await waitTask;
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
                return ExecutionOutcome.FromRuntimeError(string.IsNullOrWhiteSpace(error) ? $"Procesul s-a închis cu codul {process.ExitCode}." : error);

            return ExecutionOutcome.Success(output, error);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temporary submission files.
            }
        }
    }

    private static string NormalizeOutput(string? output)
    {
        var normalized = (output ?? "")
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var lines = normalized
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        return string.Join('\n', lines);
    }

    private static List<MetadataReference> BuildCSharpReferences()
    {
        var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return tpa
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
    }

    private sealed record ExecutionOutcome(
        bool Succeeded,
        string StandardOutput,
        string StandardError,
        string? CompileError,
        string? RuntimeError)
    {
        public static ExecutionOutcome Success(string output, string error) => new(true, output, error, null, null);

        public static ExecutionOutcome FromCompileError(string message) => new(false, "", "", message, null);

        public static ExecutionOutcome FromRuntimeError(string message) => new(false, "", message, null, message);
    }
}
