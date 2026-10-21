using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace DocsBrain.Api.Ai;

/// <summary>
/// Offline stand-in for an LLM: answers with the start of the first numbered source in the prompt.
/// Deterministic, so tests and CI evals do not depend on a model.
/// </summary>
public sealed partial class ExtractiveChatClient : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var text = new System.Text.StringBuilder();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
            text.Append(update.Text);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text.ToString()));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var match = FirstSource().Match(prompt);
        var answer = match.Success
            ? $"According to the documents: {Truncate(match.Groups[1].Value.Trim(), 300)} [1]"
            : "I could not find this in the documents.";

        foreach (var word in answer.Split(' '))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, word + " ");
            await Task.Yield();
        }

        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new UsageContent(new UsageDetails { TotalTokenCount = (prompt.Length + answer.Length) / 4 })],
        };
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "...";

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    [GeneratedRegex(@"\[1\] [^:\n]*: (.*?)(?:\n\n\[2\]|\n\nQuestion:)", RegexOptions.Singleline)]
    private static partial Regex FirstSource();
}
