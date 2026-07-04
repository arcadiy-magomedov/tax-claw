using System.Text;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class TextLayerDetectorTests
{
    private sealed class PlainTextExtractor : ITextExtractor
    {
        public Task<ExtractedText?> TryExtractAsync(SourceDocument doc, CancellationToken ct = default)
        {
            string text = Encoding.UTF8.GetString(doc.Bytes);
            return Task.FromResult<ExtractedText?>(
                text.Trim().Length > 0 ? new ExtractedText(text, UsedRecognition: false) : null);
        }
    }

    private sealed class StubRecognizer : IRecognizer
    {
        public Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default) =>
            Task.FromResult(new ExtractedText("recognized", UsedRecognition: true));
    }

    [Fact]
    public async Task Uses_text_layer_when_present()
    {
        var detector = new TextLayerDetector(new PlainTextExtractor(), new StubRecognizer());
        var doc = SourceDocument.FromBytes("a.txt", Encoding.UTF8.GetBytes("hello"));

        var result = await detector.ExtractAsync(doc);

        Assert.False(result.UsedRecognition);
        Assert.Equal("hello", result.Text);
    }

    [Fact]
    public async Task Falls_back_to_recognition_when_no_text_layer()
    {
        var detector = new TextLayerDetector(new PlainTextExtractor(), new StubRecognizer());
        var doc = SourceDocument.FromBytes("scan.png", Array.Empty<byte>());

        var result = await detector.ExtractAsync(doc);

        Assert.True(result.UsedRecognition);
        Assert.Equal("recognized", result.Text);
    }
}
