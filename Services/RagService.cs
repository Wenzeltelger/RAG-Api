using SemanticKnowledgeApi.Models;

namespace SemanticKnowledgeApi.Services;

public class RagService
{
    private readonly DocumentLoader _documentLoader;
    private readonly TextChunker _textChunker;
    private readonly EmbeddingService _embeddingService;
    private readonly AzureSearchService _azureSearchService;
    private readonly PromptBuilder _promptBuilder;
    private readonly LlmService _llmService;

    public RagService(
        DocumentLoader documentLoader,
        TextChunker textChunker,
        EmbeddingService embeddingService,
        AzureSearchService azureSearchService,
        PromptBuilder promptBuilder,
        LlmService llmService)
    {
        _documentLoader = documentLoader;
        _textChunker = textChunker;
        _embeddingService = embeddingService;
        _azureSearchService = azureSearchService;
        _promptBuilder = promptBuilder;
        _llmService = llmService;
    }

    public async Task IndexKnowledgeBaseAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _documentLoader.LoadDocumentsAsync();
        var chunks = _textChunker.ChunkDocuments(documents);

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            chunk.Embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, cancellationToken);
        }

        await _azureSearchService.ResetIndexAsync(cancellationToken);
        await _azureSearchService.UploadChunksAsync(chunks, cancellationToken);
    }

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ArgumentException("Question is required.", nameof(request));
        }

        if (!await _azureSearchService.HasDocumentsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Knowledge base is not indexed yet. Run POST /index first.");
        }

        var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Question, cancellationToken);
        var sources = await _azureSearchService.SearchAsync(questionEmbedding, topK: 3, cancellationToken);
        var prompt = _promptBuilder.BuildPrompt(request.Question, sources);
        var answer = await _llmService.GenerateAnswerAsync(prompt, cancellationToken);

        return new AskResponse
        {
            Answer = answer,
            Sources = sources,
            Prompt = prompt
        };
    }
}
