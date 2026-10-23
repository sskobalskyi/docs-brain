using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace DocsBrain.Api.Chat;

/// <param name="Context">Optional trusted facts from a calling service, at most <c>Chat:MaxContextChars</c> characters.</param>
public sealed record ChatRequest(string Question, string? Context = null);

public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        // A POST (not EventSource) so the bearer token can be sent; the body is a text/event-stream.
        app.MapPost("/api/chat", async (
            ChatRequest request, HttpContext http, ChatService chat, IOptions<ChatOptions> options, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                await Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Question is required.").ExecuteAsync(http);
                return;
            }

            if (request.Context is { } context && context.Length > options.Value.MaxContextChars)
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: $"Context is longer than {options.Value.MaxContextChars} characters.")
                    .ExecuteAsync(http);
                return;
            }

            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            await foreach (var chatEvent in chat.AskAsync(request.Question.Trim(), request.Context, ct))
            {
                await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(chatEvent, Json)}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }
        })
        .RequireAuthorization()
        .RequireRateLimiting("chat");
    }
}
