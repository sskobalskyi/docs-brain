using DocsBrain.Api.Data;
using DocsBrain.Core;
using DocsBrain.Core.Documents;
using DocsBrain.Core.Ingestion;
using Microsoft.Extensions.AI;
using Pgvector;

namespace DocsBrain.Api.Ingestion;

public sealed class IngestionService(
    DocsBrainDbContext db,
    IEnumerable<ITextExtractor> extractors,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    ILogger<IngestionService> logger)
{
    /// <param name="title">Overrides the title, which otherwise is the file name without its extension.</param>
    public async Task<Result<Document>> IngestAsync(string fileName, Stream content, CancellationToken ct, string? title = null)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var extractor = extractors.FirstOrDefault(e => e.Extensions.Contains(extension));
        if (extractor is null)
            return new Error("ingestion.unsupported_type", $"Files of type '{extension}' are not supported.");

        string text;
        try
        {
            text = extractor.Extract(content);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Error("ingestion.unreadable", "The file could not be read.");
        }

        var chunks = TextChunker.Chunk(text);
        if (chunks.Count == 0)
            return new Error("ingestion.empty", "The file contains no text.");

        var document = new Document { Title = title ?? Path.GetFileNameWithoutExtension(fileName), FileName = Path.GetFileName(fileName) };
        db.Documents.Add(document);
        var vectors = await TryEmbedAsync(chunks, ct);
        db.Chunks.AddRange(chunks.Select((t, i) => new DocumentChunk
        {
            DocumentId = document.Id,
            Index = i,
            Text = t,
            Embedding = vectors?[i],
        }));
        await db.SaveChangesAsync(ct);
        return document;
    }

    // Embedding is best effort: without it the chunks are still found by keyword search.
    private async Task<Vector[]?> TryEmbedAsync(IReadOnlyList<string> chunks, CancellationToken ct)
    {
        try
        {
            var result = await embeddings.GenerateAsync(chunks, cancellationToken: ct);
            return result.Select(e => new Vector(e.Vector.ToArray())).ToArray();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Embedding provider unavailable, storing chunks without embeddings.");
            return null;
        }
    }
}
