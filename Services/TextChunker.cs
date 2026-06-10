using SemanticKnowledgeApi.Models;

namespace SemanticKnowledgeApi.Services;

public class TextChunker
{
    public List<DocumentChunk> ChunkDocument(
        string documentName,
        string content,
        int chunkSize = 500,
        int overlap = 100)
    {
        if (string.IsNullOrWhiteSpace(documentName))
        {
            throw new ArgumentException("Document name is required.", nameof(documentName));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be greater than zero.");
        }

        if (overlap < 0 || overlap >= chunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(overlap), "Overlap must be zero or greater and smaller than chunk size.");
        }

        var chunks = new List<DocumentChunk>();
        var chunkNumber = 1;
        var sections = SplitByTitleSections(content);

        foreach (var section in sections)
        {
            foreach (var chunkContent in SplitLargeSection(section, chunkSize, overlap))
            {
                if (!string.IsNullOrWhiteSpace(chunkContent))
                {
                    chunks.Add(new DocumentChunk
                    {
                        Id = $"{Path.GetFileNameWithoutExtension(documentName)}-{chunkNumber}",
                        DocumentName = documentName,
                        Content = chunkContent,
                        Embedding = []
                    });

                    chunkNumber++;
                }
            }
        }

        return chunks;
    }

    public List<DocumentChunk> ChunkDocuments(IEnumerable<(string DocumentName, string Content)> documents)
    {
        return documents
            .SelectMany(document => ChunkDocument(document.DocumentName, document.Content))
            .ToList();
    }

    private static List<string> SplitByTitleSections(string content)
    {
        var titleStarts = new List<int>();
        var lineStart = 0;

        while (lineStart < content.Length)
        {
            var remainingContent = content[lineStart..];
            if (remainingContent.StartsWith("Title:", StringComparison.OrdinalIgnoreCase)
                || remainingContent.StartsWith("Título:", StringComparison.OrdinalIgnoreCase))
            {
                titleStarts.Add(lineStart);
            }

            var nextLineBreak = content.IndexOf('\n', lineStart);

            if (nextLineBreak < 0)
            {
                break;
            }

            lineStart = nextLineBreak + 1;
        }

        if (titleStarts.Count <= 1)
        {
            return [content];
        }

        var sections = new List<string>();

        if (!string.IsNullOrWhiteSpace(content[..titleStarts[0]]))
        {
            sections.Add(content[..titleStarts[0]].Trim());
        }

        for (var i = 0; i < titleStarts.Count; i++)
        {
            var sectionStart = titleStarts[i];
            var sectionEnd = i + 1 < titleStarts.Count ? titleStarts[i + 1] : content.Length;
            var section = content[sectionStart..sectionEnd].Trim();

            if (!string.IsNullOrWhiteSpace(section))
            {
                sections.Add(section);
            }
        }

        return sections;
    }

    private static List<string> SplitLargeSection(string section, int chunkSize, int overlap)
    {
        if (section.Length <= chunkSize)
        {
            return [section.Trim()];
        }

        var chunks = new List<string>();
        var start = 0;

        while (start < section.Length)
        {
            start = MoveToNextNonWhiteSpace(section, start);

            if (start >= section.Length)
            {
                break;
            }

            var end = FindBestChunkEnd(section, start, chunkSize);
            var chunk = section[start..end].Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (end >= section.Length)
            {
                break;
            }

            start = FindNextChunkStart(section, start, end, overlap);
        }

        return chunks;
    }

    private static int FindBestChunkEnd(string content, int start, int chunkSize)
    {
        var maxEnd = Math.Min(start + chunkSize, content.Length);

        if (maxEnd == content.Length)
        {
            return maxEnd;
        }

        var minimumEnd = start + (chunkSize / 2);
        var searchLength = maxEnd - minimumEnd;

        if (searchLength <= 0)
        {
            return maxEnd;
        }

        return FindLastSeparator(content, minimumEnd, maxEnd, ["\r\n\r\n", "\n\n"])
            ?? FindLastSeparator(content, minimumEnd, maxEnd, ["\r\n", "\n"])
            ?? FindLastPeriod(content, minimumEnd, maxEnd)
            ?? FindLastSeparator(content, minimumEnd, maxEnd, [" "])
            ?? MoveBackToWordBoundary(content, start, maxEnd);
    }

    private static int FindNextChunkStart(string content, int currentStart, int currentEnd, int overlap)
    {
        var nextStart = Math.Max(currentStart + 1, currentEnd - overlap);

        while (nextStart < currentEnd
            && nextStart > 0
            && !char.IsWhiteSpace(content[nextStart - 1])
            && !char.IsPunctuation(content[nextStart - 1]))
        {
            nextStart++;
        }

        return MoveToNextNonWhiteSpace(content, nextStart);
    }

    private static int? FindLastSeparator(string content, int start, int end, string[] separators)
    {
        var searchArea = content[start..end];

        foreach (var separator in separators)
        {
            var index = searchArea.LastIndexOf(separator, StringComparison.Ordinal);

            if (index >= 0)
            {
                return start + index + separator.Length;
            }
        }

        return null;
    }

    private static int? FindLastPeriod(string content, int start, int end)
    {
        for (var i = end - 1; i >= start; i--)
        {
            if (content[i] == '.' && (i + 1 == content.Length || char.IsWhiteSpace(content[i + 1])))
            {
                return i + 1;
            }
        }

        return null;
    }

    private static int MoveBackToWordBoundary(string content, int start, int end)
    {
        for (var i = end; i > start; i--)
        {
            if (char.IsWhiteSpace(content[i - 1]) || char.IsPunctuation(content[i - 1]))
            {
                return i;
            }
        }

        return end;
    }

    private static int MoveToNextNonWhiteSpace(string content, int start)
    {
        while (start < content.Length && char.IsWhiteSpace(content[start]))
        {
            start++;
        }

        return start;
    }
}
