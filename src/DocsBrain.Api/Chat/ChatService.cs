using System.Runtime.CompilerServices;
using System.Text;
using DocsBrain.Api.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace DocsBrain.Api.Chat;

public sealed class ChatOptions
{
    public long DailyTokenBudget { get; set; } = 50_000;
    public int RequestsPerMinute { get; set; } = 20;
    public int MaxSources { get; set; } = 5;
}

public sealed record ChatSource(int Number, Guid DocumentId, string Title, int ChunkIndex, string Text);

/// <summary>One server-sent event. <c>Type</c> is <c>sources</c>, <c>token</c> or <c>done</c>.</summary>
public sealed record ChatEvent(
    string Type,
    string? Text = null,
    IReadOnlyList<ChatSource>? Sources = null,
    string? Mode = null,
    string? Reason = null,
    long? Tokens = null);

public static class ChatModes
{
    public const string Answer = "answer";
    public const string RetrievalOnly = "retrieval_only";
    public const string NoResults = "no_results";
}

public sealed class ChatService(
    SearchService search,
    IChatClient chat,
    UsageStore usage,
    IOptions<ChatOptions> options,
    ILogger<ChatService> logger)
{
    private const string SystemPrompt =
        "You are Docs-Brain, an assistant that answers using only the provided sources. " +
        "Cite the sources you use as [1], [2]. If the sources do not contain the answer, say that you do not know. " +
        "Answer in the language of the question.";

    public async IAsyncEnumerable<ChatEvent> AskAsync(string question, [EnumeratorCancellation] CancellationToken ct)
    {
        var settings = options.Value;
        var hits = await search.SearchAsync(question, settings.MaxSources, ct);
        if (hits.Count == 0)
        {
            yield return new ChatEvent("done", Mode: ChatModes.NoResults);
            yield break;
        }

        var sources = hits.Select((h, i) => new ChatSource(i + 1, h.DocumentId, h.Title, h.ChunkIndex, h.Text)).ToList();
        yield return new ChatEvent("sources", Sources: sources);

        // Retrieval-only fallback: the sources are already useful without a generated answer.
        if (await usage.GetTodayAsync(ct) >= settings.DailyTokenBudget)
        {
            yield return new ChatEvent("done", Mode: ChatModes.RetrievalOnly, Reason: "budget_exceeded");
            yield break;
        }

        var prompt = BuildPrompt(question, sources);
        var answer = new StringBuilder();
        long reportedTokens = 0;
        var failed = false;

        var updates = chat.GetStreamingResponseAsync(prompt, cancellationToken: ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await updates.MoveNextAsync()) break;
                    update = updates.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Chat provider failed, falling back to retrieval-only.");
                    failed = true;
                    break;
                }

                foreach (var usageContent in update.Contents.OfType<UsageContent>())
                    reportedTokens = usageContent.Details.TotalTokenCount ?? reportedTokens;

                if (update.Text is { Length: > 0 } text)
                {
                    answer.Append(text);
                    yield return new ChatEvent("token", Text: text);
                }
            }
        }
        finally
        {
            await updates.DisposeAsync();
        }

        if (failed)
        {
            yield return new ChatEvent("done", Mode: ChatModes.RetrievalOnly, Reason: "llm_unavailable");
            yield break;
        }

        var tokens = reportedTokens > 0 ? reportedTokens : (prompt.Sum(m => m.Text.Length) + answer.Length) / 4;
        await usage.AddAsync(tokens, ct);
        yield return new ChatEvent("done", Mode: ChatModes.Answer, Tokens: tokens);
    }

    private static List<ChatMessage> BuildPrompt(string question, IEnumerable<ChatSource> sources)
    {
        var user = new StringBuilder("Sources:\n");
        foreach (var s in sources)
            user.Append('[').Append(s.Number).Append("] ").Append(s.Title).Append(": ").Append(s.Text).Append("\n\n");
        user.Append("Question: ").Append(question);

        return [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, user.ToString())];
    }
}
