namespace SemanticKnowledgeApi.Services;

public class DocumentLoader
{
    public async Task<List<(string DocumentName, string Content)>> LoadDocumentsAsync(string dataFolderPath = "data")
    {
        var fullPath = Path.GetFullPath(dataFolderPath);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"The data folder was not found: {fullPath}");
        }

        var documents = new List<(string DocumentName, string Content)>();
        var textFiles = Directory
            .EnumerateFiles(fullPath, "*.txt", SearchOption.TopDirectoryOnly)
            .Where(filePath => !IsHidden(filePath))
            .ToList();

        foreach (var filePath in textFiles)
        {
            var content = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            documents.Add((Path.GetFileName(filePath), content));
        }

        if (documents.Count == 0)
        {
            throw new InvalidOperationException($"No non-empty .txt files were found in the data folder: {fullPath}");
        }

        return documents;
    }

    private static bool IsHidden(string filePath)
    {
        return File.GetAttributes(filePath).HasFlag(FileAttributes.Hidden);
    }
}
