using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocsBrain.Api.Chat;

public sealed record ChatRequest(string Question);

public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        // A POST (not EventSource) so the bearer token can be sent; the body is a text/event-stream.
        app.MapPost("/api/chat", async (ChatRequest request, HttpContext http, ChatService chat, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                await Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Question is required.").ExecuteAsync(http);
                return;
            }

            http.Response.ContentType = "text/event-stream";
            http.Response.Headers.CacheControl = "no-cache";

            await foreach (var chatEvent in chat.AskAsync(request.Question.Trim(), ct))
            {
                await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(chatEvent, Json)}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }
        })
        .RequireAuthorization()
        .RequireRateLimiting("chat");
    }
}
