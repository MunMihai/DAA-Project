using System.Text.Json;

namespace Quiz.CodingService.Engine;

public static class RulesetParser
{
    public static Ruleset Parse(string raw)
    {
        var json = ExtractJsonObject(raw);

        try
        {
            var rs = JsonSerializer.Deserialize<Ruleset>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (rs is null) throw new Exception("Ruleset deserialized to null.");
            if (!string.Equals(rs.language, "dotnet", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Unsupported language: {rs.language}");

            if (rs.rules is null || rs.rules.Count == 0)
                throw new Exception("Ruleset has no rules.");

            foreach (var r in rs.rules)
            {
                if (string.IsNullOrWhiteSpace(r.id)) throw new Exception("Rule missing id.");
                if (string.IsNullOrWhiteSpace(r.type)) throw new Exception($"Rule '{r.id}' missing type.");
                r.@params ??= new();
            }

            return rs;
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Invalid ruleset JSON from AI.\n{ex.Message}\nSANITIZED JSON:\n{json}\nRAW:\n{raw}");
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new Exception("Empty AI response.");

        var s = raw.Trim();

        // Strip ``` fences (```json ... ```)
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline >= 0) s = s[(firstNewline + 1)..];

            var lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) s = s[..lastFence];
        }

        s = s.Trim();

        // Extract first JSON object by brace matching
        var start = s.IndexOf('{');
        if (start < 0) throw new Exception("No '{' found in AI response.");

        int depth = 0;
        for (int i = start; i < s.Length; i++)
        {
            if (s[i] == '{') depth++;
            else if (s[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return s.Substring(start, i - start + 1).Trim();
            }
        }

        throw new Exception("Unclosed JSON object in AI response.");
    }
}
