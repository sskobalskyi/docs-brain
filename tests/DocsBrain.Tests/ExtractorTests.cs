using DocsBrain.Api.Ingestion;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocsBrain.Tests;

public class ExtractorTests
{
    [Fact]
    public void Plain_text_is_read_as_utf8()
    {
        using var stream = new MemoryStream("Привіт, світе"u8.ToArray());

        Assert.Equal("Привіт, світе", new PlainTextExtractor().Extract(stream));
    }

    [Fact]
    public void Docx_paragraphs_are_extracted()
    {
        using var stream = new MemoryStream();
        using (var word = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new Run(new Text("First paragraph"))),
                new Paragraph(new Run(new Text("Second paragraph")))));
            main.Document.Save();
        }
        stream.Position = 0;

        var text = new DocxTextExtractor().Extract(stream);

        Assert.Equal("First paragraph\n\nSecond paragraph", text);
    }

    [Fact]
    public void Pdf_text_is_extracted()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        page.AddText("Quarterly revenue grew", 12, new UglyToad.PdfPig.Core.PdfPoint(25, 700),
            builder.AddStandard14Font(Standard14Font.Helvetica));
        using var stream = new MemoryStream(builder.Build());

        Assert.Contains("Quarterly revenue grew", new PdfTextExtractor().Extract(stream));
    }
}
