using System.Text.RegularExpressions;
using DocsBrain.Api.Tenancy;

namespace DocsBrain.Api.Documents;

public static partial class SharedEndpoints
{
    /// <summary>Manages the corpus every tenant can search. Only callers with the service role get in.</summary>
    public static void MapSharedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shared/documents").RequireAuthorization(AuthSetup.SharedWriterPolicy);

        group.MapGet("/", (SharedCorpus corpus, CancellationToken ct) => corpus.ListAsync(ct));

        // Idempotent: uploading a title again replaces the previous version.
        group.MapPut("/{title}", async (string title, IFormFile file, SharedCorpus corpus, CancellationToken ct) =>
        {
            if (!ValidTitle().IsMatch(title))
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Title must be 1 to 200 letters, digits, spaces or . _ - ( ).");

            await using var stream = file.OpenReadStream();
            var result = await corpus.ReplaceAsync(title, title + Path.GetExtension(file.FileName), stream, ct);
            return result.IsSuccess
                ? Results.Created($"/api/shared/documents/{result.Value.Id}", DocumentResponse.From(result.Value))
                : Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: result.Error.Message,
                    extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
        }).DisableAntiforgery();

        group.MapDelete("/{id:guid}", async (Guid id, SharedCorpus corpus, CancellationToken ct) =>
            await corpus.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Document not found."));
    }

    [GeneratedRegex(@"^[\p{L}\p{N} ._()-]{1,200}$")]
    private static partial Regex ValidTitle();
}
