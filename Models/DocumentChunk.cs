namespace SemanticKnowledgeApi.Models;

public class DocumentChunk
{
    public string Id { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public float[] Embedding { get; set; } = [];
}
