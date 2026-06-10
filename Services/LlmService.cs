using OpenAI.Chat;

namespace SemanticKnowledgeApi.Services;

public class LlmService
{
    private const string DefaultChatModel = "gpt-4o-mini";

    private readonly ChatClient _client;

    public LlmService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI:ApiKey is missing. Configure it before generating answers.");
        }

        var model = configuration["OpenAI:ChatModel"] ?? DefaultChatModel;

        _client = new ChatClient(model, apiKey);
    }

    public async Task<string> GenerateAnswerAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required to generate an answer.", nameof(prompt));
        }

        var messages = new ChatMessage[]
        {
            new UserChatMessage(prompt)
        };

        var completion = await _client.CompleteChatAsync(messages, options: null, cancellationToken);

        return completion.Value.Content.Count > 0
            ? completion.Value.Content[0].Text
            : string.Empty;
    }
}
