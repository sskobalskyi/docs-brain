using DocsBrain.Core.Ingestion;

namespace DocsBrain.Tests;

public class TextChunkerTests
{
    private static readonly ChunkingOptions Small = new(MaxChars: 100, OverlapChars: 20);

    [Fact]
    public void Blank_text_yields_no_chunks()
    {
        Assert.Empty(TextChunker.Chunk("  \n\n  "));
    }

    [Fact]
    public void Short_text_is_a_single_chunk()
    {
        Assert.Equal(["Hello world."], TextChunker.Chunk("Hello world."));
    }

    [Fact]
    public void No_chunk_exceeds_the_limit_and_no_words_are_lost()
    {
        var words = Enumerable.Range(0, 200).Select(i => $"word{i}").ToArray();
        var text = string.Join(' ', words.Take(100)) + "\n\n" + string.Join(' ', words.Skip(100));

        var chunks = TextChunker.Chunk(text, Small);

        Assert.All(chunks, c => Assert.True(c.Length <= Small.MaxChars, $"chunk of {c.Length} chars"));
        var seen = chunks.SelectMany(c => c.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries)).ToHashSet();
        Assert.All(words, w => Assert.Contains(w, seen));
    }

    [Fact]
    public void Consecutive_paragraph_chunks_share_an_overlap()
    {
        var paragraphs = Enumerable.Range(0, 6).Select(i => $"Paragraph {i} " + string.Join(' ', Enumerable.Repeat("lorem", 8)));

        var chunks = TextChunker.Chunk(string.Join("\n\n", paragraphs), Small);

        Assert.True(chunks.Count > 1);
        for (var i = 1; i < chunks.Count; i++)
        {
            var firstLine = chunks[i].Split("\n\n")[0];
            Assert.Contains(firstLine, chunks[i - 1]);
        }
    }

    [Fact]
    public void Invalid_options_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => TextChunker.Chunk("text", new ChunkingOptions(MaxChars: 100, OverlapChars: 80)));
    }
}
