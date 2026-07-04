using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class SourceDocumentTests
{
    [Theory]
    [InlineData("statement.pdf", MediaKind.Pdf)]
    [InlineData("scan.JPG", MediaKind.Image)]
    [InlineData("photo.heic", MediaKind.Image)]
    [InlineData("export.csv", MediaKind.Tabular)]
    [InlineData("notes.txt", MediaKind.Text)]
    public void Media_kind_is_inferred_from_extension(string name, MediaKind expected)
    {
        var doc = SourceDocument.FromBytes(name, new byte[] { 1, 2, 3 });
        Assert.Equal(expected, doc.Kind);
    }
}
