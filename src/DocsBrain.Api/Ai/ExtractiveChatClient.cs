using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace DocsBrain.Api.Ai;

/// <summary>
/// Offline stand-in for an LLM: answers with the sentence of the numbered sources in the prompt that shares
/// the most words with the question, cited as [n]. Deterministic, so tests and CI evals need no model.
/// </summary>
public sealed partial class ExtractiveChatClient : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var text = new StringBuilder();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
            text.Append(update.Text);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text.ToString()));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var answer = BestSentence(prompt) is { } best
            ? $"According to the documents: {best.Sentence} [{best.Source}]"
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

    private static (string Sentence, int Source)? BestSentence(string prompt)
    {
        var question = prompt[(prompt.LastIndexOf("Question:", StringComparison.Ordinal) + "Question:".Length)..];
        var wanted = Words(question);

        (string Sentence, int Source)? best = null;
        var bestScore = 0;
        foreach (Match source in Source().Matches(prompt))
        {
            foreach (var sentence in Sentences().Split(source.Groups[2].Value))
            {
                var text = sentence.Trim();
                if (text.Length == 0 || text.StartsWith('#')) continue;

                var score = Words(text).Count(wanted.Contains);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = (text, int.Parse(source.Groups[1].Value));
                }
            }
        }

        return best;
    }

    private static HashSet<string> Words(string text) => Tokenizer.Tokens(text).Where(w => w.Length > 2).ToHashSet();

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }

    [GeneratedRegex(@"\[(\d+)\] [^:\n]*: (.*?)(?=\n\n\[\d+\] |\n\nQuestion:)", RegexOptions.Singleline)]
    private static partial Regex Source();

    [GeneratedRegex(@"(?<=[.!?])\s+|\n+")]
    private static partial Regex Sentences();
}
