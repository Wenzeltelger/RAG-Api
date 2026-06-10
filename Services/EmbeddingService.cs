using OpenAI.Embeddings;

namespace SemanticKnowledgeApi.Services;

public class EmbeddingService
{
    private const string DefaultEmbeddingModel = "text-embedding-3-small";

    private readonly EmbeddingClient _client;

    public EmbeddingService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI:ApiKey is missing. Configure it before generating embeddings.");
        }

        var model = configuration["OpenAI:EmbeddingModel"] ?? DefaultEmbeddingModel;

        _client = new EmbeddingClient(model, apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required to generate an embedding.", nameof(text));
        }

        var embedding = await _client.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

        return embedding.Value.ToFloats().ToArray();
    }
}
