using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using SemanticKnowledgeApi.Models;

namespace SemanticKnowledgeApi.Services;

public class AzureSearchService
{
    private const string VectorProfileName = "rag-vector-profile";
    private const string VectorAlgorithmName = "rag-hnsw";
    private const int EmbeddingDimensions = 1536;

    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly string _indexName;

    public AzureSearchService(IConfiguration configuration)
    {
        var endpoint = configuration["AzureSearch:Endpoint"];
        var apiKey = configuration["AzureSearch:ApiKey"];
        _indexName = configuration["AzureSearch:IndexName"] ?? "semantic-rag-kb";

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("AzureSearch:Endpoint is missing.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AzureSearch:ApiKey is missing.");
        }

        var credential = new AzureKeyCredential(apiKey);

        _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
        _searchClient = new SearchClient(new Uri(endpoint), _indexName, credential);
    }

    public async Task CreateIndexIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _indexClient.GetIndexAsync(_indexName, cancellationToken);
            return;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var index = BuildIndex();
            await _indexClient.CreateIndexAsync(index, cancellationToken);
        }
    }

    public async Task UploadChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var documents = chunks
            .Where(chunk => chunk.Embedding.Length > 0)
            .Select(chunk => new
            {
                id = chunk.Id,
                documentName = chunk.DocumentName,
                content = chunk.Content,
                embedding = chunk.Embedding
            })
            .ToList();

        if (documents.Count == 0)
        {
            return;
        }

        await _searchClient.UploadDocumentsAsync(documents, cancellationToken: cancellationToken);
    }

    public async Task ResetIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _indexClient.DeleteIndexAsync(_indexName, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The first indexing run will not have an index to delete yet.
        }

        await _indexClient.CreateIndexAsync(BuildIndex(), cancellationToken);
    }

    public async Task<bool> HasDocumentsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _indexClient.GetIndexAsync(_indexName, cancellationToken);

            var options = new SearchOptions
            {
                Size = 1
            };
            options.Select.Add("id");

            var response = await _searchClient.SearchAsync<SearchDocument>(
                searchText: "*",
                options,
                cancellationToken);

            await foreach (var _ in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
            {
                return true;
            }

            return false;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<List<SearchChunkResult>> SearchAsync(
        float[] embedding,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        if (embedding.Length == 0)
        {
            throw new ArgumentException("Embedding is required for vector search.", nameof(embedding));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "topK must be greater than zero.");
        }

        var vectorQuery = new VectorizedQuery(embedding)
        {
            KNearestNeighborsCount = topK
        };
        vectorQuery.Fields.Add("embedding");

        var options = new SearchOptions
        {
            Size = topK,
            VectorSearch = new VectorSearchOptions()
        };
        options.VectorSearch.Queries.Add(vectorQuery);
        options.Select.Add("documentName");
        options.Select.Add("content");

        var response = await _searchClient.SearchAsync<SearchDocument>(
            searchText: null,
            options,
            cancellationToken);

        var results = new List<SearchChunkResult>();

        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
        {
            results.Add(new SearchChunkResult
            {
                DocumentName = result.Document.TryGetValue("documentName", out var documentName)
                    ? documentName?.ToString() ?? string.Empty
                    : string.Empty,
                Content = result.Document.TryGetValue("content", out var content)
                    ? content?.ToString() ?? string.Empty
                    : string.Empty,
                Score = result.Score ?? 0
            });
        }

        return results
            .OrderByDescending(result => result.Score)
            .ToList();
    }

    private SearchIndex BuildIndex()
    {
        var fields = new List<SearchField>
        {
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
            new SearchableField("documentName") { IsFilterable = true },
            new SearchableField("content"),
            new VectorSearchField("embedding", EmbeddingDimensions, VectorProfileName)
        };

        return new SearchIndex(_indexName, fields)
        {
            VectorSearch = new VectorSearch
            {
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(VectorAlgorithmName)
                },
                Profiles =
                {
                    new VectorSearchProfile(VectorProfileName, VectorAlgorithmName)
                }
            }
        };
    }
}
