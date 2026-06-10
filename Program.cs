using Microsoft.OpenApi;
using SemanticKnowledgeApi.Models;
using SemanticKnowledgeApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Semantic Knowledge API",
        Version = "v1",
        Description = "A .NET 8 educational RAG prototype using Azure AI Search and OpenAI embeddings."
    });
});
builder.Services.AddSingleton<DocumentLoader>();
builder.Services.AddSingleton<TextChunker>();
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<AzureSearchService>();
builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<LlmService>();
builder.Services.AddSingleton<RagService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "SemanticKnowledgeApi"
}));

app.MapPost("/index", async (RagService ragService, CancellationToken cancellationToken) =>
{
    try
    {
        await ragService.IndexKnowledgeBaseAsync(cancellationToken);

        return Results.Ok(new
        {
            message = "Knowledge base indexed successfully."
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to index knowledge base: {ex.Message}");
    }
});

app.MapPost("/ask", async (AskRequest request, RagService ragService, CancellationToken cancellationToken) =>
{
    try
    {
        var response = await ragService.AskAsync(request, cancellationToken);

        return Results.Ok(response);
    }
    catch (InvalidOperationException ex) when (ex.Message == "Knowledge base is not indexed yet. Run POST /index first.")
    {
        return Results.Problem(ex.Message);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to answer question: {ex.Message}");
    }
});

app.Run();
