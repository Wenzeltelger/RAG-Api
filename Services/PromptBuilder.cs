using System.Text;
using SemanticKnowledgeApi.Models;

namespace SemanticKnowledgeApi.Services;

public class PromptBuilder
{
    public string BuildPrompt(string question, IEnumerable<SearchChunkResult> sources)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("Question is required to build a prompt.", nameof(question));
        }

        var prompt = new StringBuilder();

        prompt.AppendLine("System instructions:");
        prompt.AppendLine("You are a helpful assistant for a RAG prototype.");
        prompt.AppendLine("Answer only using the provided context.");
        prompt.AppendLine("Do not invent information.");
        prompt.AppendLine("If there is not enough information in the context, say so clearly.");
        prompt.AppendLine("Keep the answer helpful and concise.");
        prompt.AppendLine();

        prompt.AppendLine("Retrieved context:");

        var sourceList = sources.ToList();
        if (sourceList.Count == 0)
        {
            prompt.AppendLine("No context was retrieved.");
        }
        else
        {
            for (var i = 0; i < sourceList.Count; i++)
            {
                var source = sourceList[i];
                prompt.AppendLine($"Source {i + 1}: {source.DocumentName}");
                prompt.AppendLine(source.Content);
                prompt.AppendLine();
            }
        }

        prompt.AppendLine("User question:");
        prompt.AppendLine(question);

        return prompt.ToString();
    }
}
