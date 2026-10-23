using DocsBrain.Api.Data;
using DocsBrain.Api.Ingestion;
using DocsBrain.Api.Tenancy;
using DocsBrain.Core;
using DocsBrain.Core.Documents;
using DocsBrain.Core.Ingestion;
using DocsBrain.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace DocsBrain.Api.Documents;

/// <summary>
/// The only writer of shared documents. It opens its own context for the reserved shared tenant, so the request's
/// tenant can never receive a shared row, and it is reachable only through the service-role endpoints.
/// </summary>
public sealed class SharedCorpus(
    DbContextOptions<DocsBrainDbContext> options,
    IEnumerable<ITextExtractor> extractors,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    ILoggerFactory loggers)
{
    private DocsBrainDbContext Open() => new(options, new FixedTenantContext(SharedTenant.Id));

    public async Task<IReadOnlyList<DocumentResponse>> ListAsync(CancellationToken ct)
    {
        await using var db = Open();
        return await db.Documents
            .OrderBy(d => d.Title)
            .Select(d => new DocumentResponse(d.Id, d.Title, d.FileName, d.CreatedAt))
            .ToListAsync(ct);
    }

    /// <summary>Stores the document under <paramref name="title"/>, replacing any shared document with that title.</summary>
    public async Task<Result<Document>> ReplaceAsync(string title, string fileName, Stream content, CancellationToken ct)
    {
        await using var db = Open();
        var ingestion = new IngestionService(db, extractors, embeddings, loggers.CreateLogger<IngestionService>());

        // The new document is stored first, so a file that cannot be read leaves the old version in place.
        var result = await ingestion.IngestAsync(fileName, content, ct, title);
        if (!result.IsSuccess) return result;

        var previous = await db.Documents.Where(d => d.Title == title && d.Id != result.Value.Id).ToListAsync(ct);
        db.Documents.RemoveRange(previous);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = Open();
        var deleted = await db.Documents.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }
}
