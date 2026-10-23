using DocsBrain.Api.Data;
using DocsBrain.Api.Ingestion;
using DocsBrain.Core.Documents;
using DocsBrain.Core.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace DocsBrain.Api.Documents;

public sealed record CreateDocumentRequest(string Title);

public sealed record DocumentResponse(Guid Id, string Title, string? FileName, DateTimeOffset CreatedAt)
{
    public static DocumentResponse From(Document d) => new(d.Id, d.Title, d.FileName, d.CreatedAt);
}

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").RequireAuthorization();

        // Only the tenant's own documents: the shared corpus is searched, not browsed.
        group.MapGet("/", async (DocsBrainDbContext db, ITenantContext tenant, CancellationToken ct) =>
            await db.Documents
                .Where(d => d.TenantId == tenant.TenantId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DocumentResponse(d.Id, d.Title, d.FileName, d.CreatedAt))
                .ToListAsync(ct));

        group.MapGet("/{id:guid}", async (Guid id, DocsBrainDbContext db, CancellationToken ct) =>
            await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct) is { } doc
                ? Results.Ok(DocumentResponse.From(doc))
                : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Document not found."));

        group.MapGet("/{id:guid}/chunks", async (Guid id, DocsBrainDbContext db, CancellationToken ct) =>
            await db.Documents.AnyAsync(d => d.Id == id, ct)
                ? Results.Ok(await db.Chunks.Where(c => c.DocumentId == id).OrderBy(c => c.Index).Select(c => c.Text).ToListAsync(ct))
                : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Document not found."));

        group.MapPost("/upload", async (IFormFile file, IngestionService ingestion, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await ingestion.IngestAsync(file.FileName, stream, ct);
            return result.IsSuccess
                ? Results.Created($"/api/documents/{result.Value.Id}", DocumentResponse.From(result.Value))
                : Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
        }).DisableAntiforgery();

        group.MapPost("/", async (CreateDocumentRequest request, DocsBrainDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Title is required.");

            var doc = new Document { Title = request.Title.Trim() };
            db.Documents.Add(doc);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/documents/{doc.Id}", DocumentResponse.From(doc));
        });
    }
}
