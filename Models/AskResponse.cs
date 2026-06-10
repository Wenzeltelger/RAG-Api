namespace SemanticKnowledgeApi.Models;

public class AskResponse
{
    public string Answer { get; set; } = string.Empty;

    public List<SearchChunkResult> Sources { get; set; } = [];

    public string Prompt { get; set; } = string.Empty;
}
