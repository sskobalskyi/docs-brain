using System.Text;

namespace DocsBrain.Core.Ingestion;

public sealed record ChunkingOptions(int MaxChars = 1000, int OverlapChars = 150);

/// <summary>Splits text into paragraph-aligned chunks of at most <see cref="ChunkingOptions.MaxChars"/> characters.</summary>
public static class TextChunker
{
    public static IReadOnlyList<string> Chunk(string text, ChunkingOptions? options = null)
    {
        options ??= new ChunkingOptions();
        if (options.MaxChars < 50 || options.OverlapChars < 0 || options.OverlapChars >= options.MaxChars / 2)
            throw new ArgumentException("MaxChars must be >= 50 and OverlapChars must be below MaxChars / 2.", nameof(options));

        var units = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(p => SplitLong(p, options.MaxChars));

        var chunks = new List<string>();
        var buffer = new StringBuilder();

        foreach (var unit in units)
        {
            if (buffer.Length > 0 && buffer.Length + 2 + unit.Length > options.MaxChars)
            {
                var finished = buffer.ToString();
                chunks.Add(finished);
                buffer.Clear();

                var tail = OverlapTail(finished, options.OverlapChars);
                if (tail.Length > 0 && tail.Length + 2 + unit.Length <= options.MaxChars)
                    buffer.Append(tail);
            }

            if (buffer.Length > 0) buffer.Append("\n\n");
            buffer.Append(unit);
        }

        if (buffer.Length > 0) chunks.Add(buffer.ToString());

        return chunks;
    }

    private static string OverlapTail(string text, int overlap)
    {
        if (overlap == 0 || text.Length <= overlap) return "";
        var tail = text[^overlap..];
        var firstSpace = tail.IndexOfAny([' ', '\n']);
        return firstSpace < 0 ? "" : tail[(firstSpace + 1)..].Trim();
    }

    private static IEnumerable<string> SplitLong(string paragraph, int max)
    {
        var position = 0;
        while (paragraph.Length - position > max)
        {
            var window = paragraph.AsSpan(position, max);
            var cut = window.LastIndexOfAny(' ', '\n', '\t');
            if (cut < max / 2) cut = max;

            yield return paragraph.Substring(position, cut).Trim();
            position += cut;
            while (position < paragraph.Length && char.IsWhiteSpace(paragraph[position])) position++;
        }

        if (position < paragraph.Length) yield return paragraph[position..].Trim();
    }
}
