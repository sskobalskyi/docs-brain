using System.Text;
using DocsBrain.Core.Ingestion;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace DocsBrain.Api.Ingestion;

public sealed class PlainTextExtractor : ITextExtractor
{
    public IReadOnlyCollection<string> Extensions { get; } = [".txt", ".md"];

    public string Extract(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

public sealed class PdfTextExtractor : ITextExtractor
{
    public IReadOnlyCollection<string> Extensions { get; } = [".pdf"];

    public string Extract(Stream content)
    {
        using var pdf = PdfDocument.Open(content);
        return string.Join("\n\n", pdf.GetPages().Select(p => p.Text));
    }
}

public sealed class DocxTextExtractor : ITextExtractor
{
    public IReadOnlyCollection<string> Extensions { get; } = [".docx"];

    public string Extract(Stream content)
    {
        using var word = WordprocessingDocument.Open(content, false);
        var body = word.MainDocumentPart?.Document?.Body;
        return body is null ? "" : string.Join("\n\n", body.Elements<Paragraph>().Select(p => p.InnerText));
    }
}
