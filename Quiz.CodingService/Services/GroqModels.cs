namespace Quiz.CodingService.Services;

public sealed record GroqChatCompletionRequest(
    string model,
    List<GroqMessage> messages,
    double temperature = 0.0
);

public sealed record GroqMessage(string role, string content);

public sealed class GroqChatCompletionResponse
{
    public List<Choice> choices { get; set; } = new();

    public sealed class Choice
    {
        public Message message { get; set; } = new();
    }

    public sealed class Message
    {
        public string role { get; set; } = "";
        public string content { get; set; } = "";
    }
}
