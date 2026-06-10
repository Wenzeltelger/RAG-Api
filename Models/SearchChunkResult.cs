namespace SemanticKnowledgeApi.Models;

public class SearchChunkResult
{
    public string DocumentName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public double Score { get; set; }
}
