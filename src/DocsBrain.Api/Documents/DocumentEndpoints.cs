using DocsBrain.Api.Data;
using DocsBrain.Core.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocsBrain.Api.Documents;

public sealed record CreateDocumentRequest(string Title);

public sealed record DocumentResponse(Guid Id, string Title, DateTimeOffset CreatedAt)
{
    public static DocumentResponse From(Document d) => new(d.Id, d.Title, d.CreatedAt);
}

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").RequireAuthorization();

        group.MapGet("/", async (DocsBrainDbContext db, CancellationToken ct) =>
            await db.Documents
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new DocumentResponse(d.Id, d.Title, d.CreatedAt))
                .ToListAsync(ct));

        group.MapGet("/{id:guid}", async (Guid id, DocsBrainDbContext db, CancellationToken ct) =>
            await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct) is { } doc
                ? Results.Ok(DocumentResponse.From(doc))
                : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Document not found."));

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
