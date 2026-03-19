using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Quiz.CodingService.Data;
using Quiz.CodingService.Models;

namespace Quiz.CodingService.Services;

public sealed class CompileCodingTemplateService(MongoContext db)
{
    public async Task DropLegacyTemplateIndexesAsync(CancellationToken ct = default)
    {
        var cursor = await db.CompileTemplateDocuments.Indexes.ListAsync(ct);
        var indexes = await cursor.ToListAsync(ct);

        var legacyIndexNames = indexes
            .Where(index =>
            {
                var name = index.GetValue("name", "").AsString;
                if (name == "_id_")
                    return false;

                if (name is "Slug_1" or "Fingerprint_1" or "UpdatedAt_-1")
                    return true;

                if (!index.TryGetValue("key", out var keyValue) || !keyValue.IsBsonDocument)
                    return false;

                return keyValue.AsBsonDocument.Names.Any(fieldName =>
                    !string.IsNullOrWhiteSpace(fieldName) && char.IsUpper(fieldName[0]));
            })
            .Select(index => index["name"].AsString)
            .Distinct()
            .ToList();

        foreach (var indexName in legacyIndexNames)
        {
            await db.CompileTemplateDocuments.Indexes.DropOneAsync(indexName, ct);
        }
    }

    public async Task MigrateLegacyTemplateDocumentsAsync(CancellationToken ct = default)
    {
        var rawDocuments = await db.CompileTemplateDocuments
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync(ct);

        foreach (var rawDocument in rawDocuments)
        {
            var normalized = NormalizeDocument(rawDocument);
            if (rawDocument.Equals(normalized))
                continue;

            await db.CompileTemplateDocuments.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", rawDocument["_id"]),
                normalized,
                cancellationToken: ct);
        }
    }

    public async Task<List<CompileCodingTemplate>> GetTemplatesAsync(CancellationToken ct = default)
    {
        return await db.CompileTemplates
            .Find(FilterDefinition<CompileCodingTemplate>.Empty)
            .SortByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);
    }

    public async Task<CompileCodingTemplate> SaveSessionTemplateAsync(
        CompileCodingSessionDefinition definition,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var template = BuildTemplateFromDefinition(definition, now);

        var filter = Builders<CompileCodingTemplate>.Filter.Or(
            Builders<CompileCodingTemplate>.Filter.Eq(x => x.Fingerprint, template.Fingerprint),
            Builders<CompileCodingTemplate>.Filter.Eq(x => x.Slug, template.Slug));
        var existing = await db.CompileTemplates.Find(filter).FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            await db.CompileTemplates.InsertOneAsync(template, cancellationToken: ct);
            return template;
        }

        template.Id = existing.Id;
        template.CreatedAt = existing.CreatedAt;

        var update = Builders<CompileCodingTemplate>.Update
            .Set(x => x.Fingerprint, template.Fingerprint)
            .Set(x => x.Slug, template.Slug)
            .Set(x => x.Title, template.Title)
            .Set(x => x.Description, template.Description)
            .Set(x => x.SuggestedTimeLimitSeconds, template.SuggestedTimeLimitSeconds)
            .Set(x => x.AllowedLanguages, template.AllowedLanguages)
            .Set(x => x.Tasks, template.Tasks)
            .Set(x => x.UpdatedAt, now);

        await db.CompileTemplates.UpdateOneAsync(filter, update, cancellationToken: ct);
        template.UpdatedAt = now;
        return template;
    }

    public async Task<CompileCodingTemplate> RunSeedAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var template = BuildDemoTemplate(now);

        var filter = Builders<CompileCodingTemplate>.Filter.Eq(x => x.Slug, template.Slug);
        var existing = await db.CompileTemplates.Find(filter).FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            await db.CompileTemplates.InsertOneAsync(template, cancellationToken: ct);
            return template;
        }

        template.Id = existing.Id;
        template.CreatedAt = existing.CreatedAt;

        var update = Builders<CompileCodingTemplate>.Update
            .Set(x => x.Fingerprint, template.Fingerprint)
            .Set(x => x.Title, template.Title)
            .Set(x => x.Description, template.Description)
            .Set(x => x.SuggestedTimeLimitSeconds, template.SuggestedTimeLimitSeconds)
            .Set(x => x.AllowedLanguages, template.AllowedLanguages)
            .Set(x => x.Tasks, template.Tasks)
            .Set(x => x.UpdatedAt, now);

        await db.CompileTemplates.UpdateOneAsync(filter, update, cancellationToken: ct);

        template.UpdatedAt = now;
        return template;
    }

    private static CompileCodingTemplate BuildTemplateFromDefinition(CompileCodingSessionDefinition definition, DateTimeOffset now)
    {
        var tasks = definition.Tasks.Select(task => new CompileCodingTemplateTask
        {
            Id = string.IsNullOrWhiteSpace(task.Id) ? Guid.NewGuid().ToString("N") : task.Id.Trim(),
            Title = task.Title.Trim(),
            ProblemStatement = task.ProblemStatement.Trim(),
            InputDescription = task.InputDescription.Trim(),
            OutputDescription = task.OutputDescription.Trim(),
            ExampleInput = task.ExampleInput ?? "",
            ExampleOutput = task.ExampleOutput ?? "",
            Points = task.Points,
            TestCases = task.TestCases.Select(testCase => new CompileCodingTemplateCase
            {
                Input = testCase.Input ?? "",
                ExpectedOutput = testCase.ExpectedOutput ?? "",
                IsExample = testCase.IsExample
            }).ToList(),
            ExampleSolutions = []
        }).ToList();

        var fingerprint = BuildFingerprint(
            definition.Title,
            definition.TimeLimitSeconds,
            definition.AllowedLanguages,
            tasks);

        return new CompileCodingTemplate
        {
            Slug = $"{Slugify(definition.Title)}-{fingerprint[..8].ToLowerInvariant()}",
            Fingerprint = fingerprint,
            Title = definition.Title.Trim(),
            Description = "Configurație salvată automat dintr-o sesiune Live Coding (Compile).",
            SuggestedTimeLimitSeconds = definition.TimeLimitSeconds,
            AllowedLanguages = definition.AllowedLanguages.Select(CompileCodingLanguages.Normalize).Distinct().ToList(),
            Tasks = tasks,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static CompileCodingTemplate BuildDemoTemplate(DateTimeOffset now)
    {
        var template = new CompileCodingTemplate
        {
            Slug = "sum-of-two-numbers-demo",
            Title = "Demo Compile - Suma a doua numere",
            Description = "Șablon demo pentru Live Coding (Compile), cu o singură sarcină de tip input/output și soluții exemplu pe limbajele suportate.",
            SuggestedTimeLimitSeconds = 600,
            AllowedLanguages =
            [
                CompileCodingLanguages.CSharp,
                CompileCodingLanguages.Python,
                CompileCodingLanguages.JavaScript
            ],
            CreatedAt = now,
            UpdatedAt = now,
            Tasks =
            [
                new CompileCodingTemplateTask
                {
                    Id = "sum-two-numbers",
                    Title = "Suma a două numere",
                    ProblemStatement = "Se citesc două numere întregi a și b. Afișează suma lor.",
                    InputDescription = "Pe prima linie se află două numere întregi separate prin spațiu.",
                    OutputDescription = "Afișează un singur număr întreg, reprezentând suma valorilor citite.",
                    ExampleInput = "2 3",
                    ExampleOutput = "5",
                    Points = 100,
                    TestCases =
                    [
                        new CompileCodingTemplateCase
                        {
                            Input = "2 3",
                            ExpectedOutput = "5",
                            IsExample = true
                        },
                        new CompileCodingTemplateCase
                        {
                            Input = "10 -4",
                            ExpectedOutput = "6",
                            IsExample = false
                        },
                        new CompileCodingTemplateCase
                        {
                            Input = "0 0",
                            ExpectedOutput = "0",
                            IsExample = false
                        }
                    ],
                    ExampleSolutions =
                    [
                        new CompileCodingExampleSolution
                        {
                            Language = CompileCodingLanguages.CSharp,
                            Notes = "Exemplu C# care citește două numere din stdin și afișează suma.",
                            SourceCode =
@"using System;

public static class Program
{
    public static void Main()
    {
        var parts = Console.ReadLine()!
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var a = int.Parse(parts[0]);
        var b = int.Parse(parts[1]);

        Console.WriteLine(a + b);
    }
}"
                        },
                        new CompileCodingExampleSolution
                        {
                            Language = CompileCodingLanguages.Python,
                            Notes = "Exemplu Python pentru aceeași cerință.",
                            SourceCode =
@"a, b = map(int, input().split())
print(a + b)
"
                        },
                        new CompileCodingExampleSolution
                        {
                            Language = CompileCodingLanguages.JavaScript,
                            Notes = "Exemplu JavaScript pentru rulare Node.js.",
                            SourceCode =
@"const fs = require('fs');
const data = fs.readFileSync(0, 'utf8').trim().split(/\s+/).map(Number);
const [a, b] = data;
console.log(a + b);
"
                        }
                    ]
                }
            ]
        };

        template.Fingerprint = BuildFingerprint(
            template.Title,
            template.SuggestedTimeLimitSeconds,
            template.AllowedLanguages,
            template.Tasks);

        return template;
    }

    private static string BuildFingerprint(
        string title,
        int suggestedTimeLimitSeconds,
        IEnumerable<string> allowedLanguages,
        IEnumerable<CompileCodingTemplateTask> tasks)
    {
        var payload = new
        {
            title = title.Trim(),
            suggestedTimeLimitSeconds,
            allowedLanguages = allowedLanguages
                .Select(CompileCodingLanguages.Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList(),
            tasks = tasks.Select(task => new
            {
                title = task.Title.Trim(),
                problemStatement = task.ProblemStatement.Trim(),
                inputDescription = task.InputDescription.Trim(),
                outputDescription = task.OutputDescription.Trim(),
                exampleInput = task.ExampleInput ?? "",
                exampleOutput = task.ExampleOutput ?? "",
                points = task.Points,
                testCases = task.TestCases.Select(testCase => new
                {
                    input = testCase.Input ?? "",
                    expectedOutput = testCase.ExpectedOutput ?? "",
                    isExample = testCase.IsExample
                }).ToList()
            }).ToList()
        };

        var json = JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    private static string Slugify(string value)
    {
        var normalized = new string(
            value.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray());

        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);

        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "compile-template" : normalized;
    }

    private static BsonDocument NormalizeDocument(BsonDocument document)
    {
        var normalized = new BsonDocument();

        foreach (var element in document.Elements)
        {
            normalized[NormalizeElementName(element.Name)] = NormalizeValue(element.Value);
        }

        return normalized;
    }

    private static BsonValue NormalizeValue(BsonValue value)
    {
        if (value.IsBsonDocument)
            return NormalizeDocument(value.AsBsonDocument);

        if (value.IsBsonArray)
            return new BsonArray(value.AsBsonArray.Select(NormalizeValue));

        return value;
    }

    private static string NormalizeElementName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "_id")
            return name;

        if (char.IsLower(name[0]))
            return name;

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
