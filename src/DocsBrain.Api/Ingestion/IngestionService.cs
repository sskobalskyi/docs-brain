using DocsBrain.Api.Data;
using DocsBrain.Core;
using DocsBrain.Core.Documents;
using DocsBrain.Core.Ingestion;

namespace DocsBrain.Api.Ingestion;

public sealed class IngestionService(DocsBrainDbContext db, IEnumerable<ITextExtractor> extractors)
{
    public async Task<Result<Document>> IngestAsync(string fileName, Stream content, CancellationToken ct)
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

        var document = new Document { Title = Path.GetFileNameWithoutExtension(fileName), FileName = Path.GetFileName(fileName) };
        db.Documents.Add(document);
        db.Chunks.AddRange(chunks.Select((t, i) => new DocumentChunk { DocumentId = document.Id, Index = i, Text = t }));
        await db.SaveChangesAsync(ct);
        return document;
    }
}
