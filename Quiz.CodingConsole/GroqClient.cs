using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Quiz.CodingConsole;

public sealed class GroqClient
{
    private readonly HttpClient _http = new();
    private readonly string _model;

    public GroqClient(string apiKey, string model)
    {
        _model = model;
        _http.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var req = new GroqChatCompletionRequest(
            model: _model,
            messages: new List<GroqMessage>
            {
                new("system", systemPrompt),
                new("user", userPrompt),
            },
            temperature: 0.0
        );

        var json = JsonSerializer.Serialize(req);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync("chat/completions", content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Groq error: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");

        var parsed = JsonSerializer.Deserialize<GroqChatCompletionResponse>(body)
                     ?? throw new Exception("Failed to parse Groq response.");

        return (parsed.choices.FirstOrDefault()?.message.content ?? "").Trim();
    }
}