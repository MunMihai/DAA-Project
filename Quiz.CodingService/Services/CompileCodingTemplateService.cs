using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Quiz.CodingService.Data;
using Quiz.CodingService.Models;

namespace Quiz.CodingService.Services;

public sealed class CompileCodingTemplateService(MongoContext db)
{
    private sealed record TemplateSnapshot(ObjectId Id, CompileCodingTemplate Template);

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

    public async Task DeduplicateTemplatesAsync(CancellationToken ct = default)
    {
        var rawDocuments = await db.CompileTemplateDocuments
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync(ct);

        if (rawDocuments.Count <= 1)
            return;

        var prepared = rawDocuments
            .Select(raw =>
            {
                var normalized = NormalizeDocument(raw);
                var template = BsonSerializer.Deserialize<CompileCodingTemplate>(normalized);
                var canonical = NormalizeTemplate(template);
                var key = !string.IsNullOrWhiteSpace(canonical.Fingerprint)
                    ? canonical.Fingerprint
                    : canonical.Slug;

                return new
                {
                    Raw = raw,
                    Canonical = canonical,
                    Key = key,
                    UpdatedAt = canonical.UpdatedAt,
                    CreatedAt = canonical.CreatedAt
                };
            })
            .ToList();

        foreach (var group in prepared.GroupBy(item => item.Key))
        {
            var winner = group
                .OrderByDescending(item => item.UpdatedAt)
                .ThenByDescending(item => item.CreatedAt)
                .First();

            var winnerTemplate = winner.Canonical;
            winnerTemplate.Id = winner.Raw["_id"].AsObjectId.ToString();
            winnerTemplate.CreatedAt = group.Min(item => item.Canonical.CreatedAt);
            winnerTemplate.UpdatedAt = group.Max(item => item.Canonical.UpdatedAt);

            await db.CompileTemplateDocuments.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", winner.Raw["_id"]),
                winnerTemplate.ToBsonDocument(),
                cancellationToken: ct);

            foreach (var duplicate in group.Where(item => !item.Raw["_id"].Equals(winner.Raw["_id"])))
            {
                await db.CompileTemplateDocuments.DeleteOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", duplicate.Raw["_id"]),
                    ct);
            }
        }
    }

    public async Task<List<CompileCodingTemplate>> GetTemplatesAsync(CancellationToken ct = default)
    {
        return (await LoadTemplateSnapshotsAsync(ct))
            .GroupBy(item => item.Template.Fingerprint)
            .Select(group => group
                .OrderByDescending(item => item.Template.UpdatedAt)
                .ThenByDescending(item => item.Template.CreatedAt)
                .First()
                .Template)
            .OrderByDescending(template => template.UpdatedAt)
            .ThenBy(template => template.Title)
            .ToList();
    }

    public async Task<CompileCodingTemplate> SaveSessionTemplateAsync(
        CompileCodingSessionDefinition definition,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var template = BuildTemplateFromDefinition(definition, now);
        return await UpsertTemplateAsync(template, ct);
    }

    public async Task<CompileCodingTemplate> RunSeedAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var template = BuildDemoTemplate(now);
        return await UpsertTemplateAsync(template, ct);
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
                points = task.Points
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

    private static CompileCodingTemplate NormalizeTemplate(CompileCodingTemplate template)
    {
        var normalizedTasks = template.Tasks.Select(task => new CompileCodingTemplateTask
        {
            Id = string.IsNullOrWhiteSpace(task.Id) ? Guid.NewGuid().ToString("N") : task.Id.Trim(),
            Title = task.Title.Trim(),
            ProblemStatement = task.ProblemStatement.Trim(),
            InputDescription = task.InputDescription.Trim(),
            OutputDescription = task.OutputDescription.Trim(),
            ExampleInput = task.ExampleInput ?? "",
            ExampleOutput = task.ExampleOutput ?? "",
            Points = task.Points <= 0 ? 100 : task.Points,
            TestCases = task.TestCases.Select(testCase => new CompileCodingTemplateCase
            {
                Input = testCase.Input ?? "",
                ExpectedOutput = testCase.ExpectedOutput ?? "",
                IsExample = testCase.IsExample
            }).ToList(),
            ExampleSolutions = task.ExampleSolutions.Select(solution => new CompileCodingExampleSolution
            {
                Language = CompileCodingLanguages.Normalize(solution.Language),
                SourceCode = solution.SourceCode ?? "",
                Notes = solution.Notes ?? ""
            }).ToList()
        }).ToList();

        var fingerprint = BuildFingerprint(
            template.Title,
            template.SuggestedTimeLimitSeconds,
            template.AllowedLanguages,
            normalizedTasks);

        return new CompileCodingTemplate
        {
            Id = template.Id,
            Slug = $"{Slugify(template.Title)}-{fingerprint[..8].ToLowerInvariant()}",
            Fingerprint = fingerprint,
            Title = template.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(template.Description)
                ? "Configurație salvată automat dintr-o sesiune Live Coding (Compile)."
                : template.Description.Trim(),
            SuggestedTimeLimitSeconds = template.SuggestedTimeLimitSeconds <= 0 ? 900 : template.SuggestedTimeLimitSeconds,
            AllowedLanguages = template.AllowedLanguages
                .Select(CompileCodingLanguages.Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList(),
            Tasks = normalizedTasks,
            CreatedAt = template.CreatedAt == default ? DateTimeOffset.UtcNow : template.CreatedAt,
            UpdatedAt = template.UpdatedAt == default ? DateTimeOffset.UtcNow : template.UpdatedAt
        };
    }

    private async Task<List<TemplateSnapshot>> LoadTemplateSnapshotsAsync(CancellationToken ct)
    {
        var rawDocuments = await db.CompileTemplateDocuments
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync(ct);

        return rawDocuments.Select(raw =>
        {
            var normalized = NormalizeDocument(raw);
            var template = NormalizeTemplate(BsonSerializer.Deserialize<CompileCodingTemplate>(normalized));
            template.Id = raw["_id"].AsObjectId.ToString();
            return new TemplateSnapshot(raw["_id"].AsObjectId, template);
        }).ToList();
    }

    private async Task<CompileCodingTemplate> UpsertTemplateAsync(CompileCodingTemplate template, CancellationToken ct)
    {
        var normalized = NormalizeTemplate(template);
        var snapshots = await LoadTemplateSnapshotsAsync(ct);
        var matchingSnapshots = snapshots
            .Where(item => item.Template.Fingerprint == normalized.Fingerprint || item.Template.Slug == normalized.Slug)
            .ToList();

        if (matchingSnapshots.Count == 0)
        {
            await db.CompileTemplateDocuments.InsertOneAsync(normalized.ToBsonDocument(), cancellationToken: ct);
            return normalized;
        }

        var winner = matchingSnapshots
            .OrderByDescending(item => item.Template.UpdatedAt)
            .ThenByDescending(item => item.Template.CreatedAt)
            .First();

        var merged = MergeTemplates(winner.Template, normalized);
        merged.Id = winner.Id.ToString();
        merged.CreatedAt = matchingSnapshots.Min(item => item.Template.CreatedAt);
        merged.UpdatedAt = DateTimeOffset.UtcNow;

        await db.CompileTemplateDocuments.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", winner.Id),
            merged.ToBsonDocument(),
            cancellationToken: ct);

        foreach (var duplicate in matchingSnapshots.Where(item => item.Id != winner.Id))
        {
            await db.CompileTemplateDocuments.DeleteOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", duplicate.Id),
                ct);
        }

        return merged;
    }

    private static CompileCodingTemplate MergeTemplates(
        CompileCodingTemplate existing,
        CompileCodingTemplate incoming)
    {
        var existingTasksByTitle = existing.Tasks.ToDictionary(task => task.Title.Trim(), StringComparer.OrdinalIgnoreCase);

        var mergedTasks = incoming.Tasks.Select(incomingTask =>
        {
            if (!existingTasksByTitle.TryGetValue(incomingTask.Title.Trim(), out var existingTask))
                return incomingTask;

            return new CompileCodingTemplateTask
            {
                Id = existingTask.Id,
                Title = incomingTask.Title,
                ProblemStatement = incomingTask.ProblemStatement,
                InputDescription = incomingTask.InputDescription,
                OutputDescription = incomingTask.OutputDescription,
                ExampleInput = incomingTask.ExampleInput,
                ExampleOutput = incomingTask.ExampleOutput,
                Points = incomingTask.Points,
                TestCases = existingTask.TestCases.Count > incomingTask.TestCases.Count
                    ? existingTask.TestCases
                    : incomingTask.TestCases,
                ExampleSolutions = existingTask.ExampleSolutions.Count > incomingTask.ExampleSolutions.Count
                    ? existingTask.ExampleSolutions
                    : incomingTask.ExampleSolutions
            };
        }).ToList();

        return new CompileCodingTemplate
        {
            Id = existing.Id,
            Slug = existing.Slug,
            Fingerprint = incoming.Fingerprint,
            Title = incoming.Title,
            Description = string.IsNullOrWhiteSpace(existing.Description) ? incoming.Description : existing.Description,
            SuggestedTimeLimitSeconds = incoming.SuggestedTimeLimitSeconds,
            AllowedLanguages = incoming.AllowedLanguages,
            Tasks = mergedTasks,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = incoming.UpdatedAt
        };
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
