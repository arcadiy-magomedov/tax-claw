using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Extract;

/// <summary>Prefers a text layer; falls back to recognition when none is found.</summary>
public sealed class TextLayerDetector(ITextExtractor textExtractor, IRecognizer recognizer)
{
    public async Task<ExtractedText> ExtractAsync(SourceDocument doc, CancellationToken ct = default)
    {
        ExtractedText? direct = await textExtractor.TryExtractAsync(doc, ct);
        return direct ?? await recognizer.RecognizeAsync(doc, ct);
    }
}
