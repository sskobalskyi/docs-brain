namespace DocsBrain.Core.Ingestion;

public interface ITextExtractor
{
    /// <summary>Lower-case file extensions including the dot, for example <c>.pdf</c>.</summary>
    IReadOnlyCollection<string> Extensions { get; }

    string Extract(Stream content);
}
