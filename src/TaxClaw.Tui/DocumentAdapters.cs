using System.Text;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Model;

namespace TaxClaw.Tui;

/// <summary>
/// Reads a text layer for text/CSV/tabular exports (UTF-8). Returns null for PDFs/images so the
/// recognizer path is used — a real PdfPig/text-PDF extractor slots in here later.
/// </summary>
public sealed class PlainTextExtractor : ITextExtractor
{
    public Task<ExtractedText?> TryExtractAsync(SourceDocument doc, CancellationToken ct = default)
    {
        if (doc.Kind is not (MediaKind.Text or MediaKind.Tabular))
        {
            return Task.FromResult<ExtractedText?>(null);
        }
        string text = Encoding.UTF8.GetString(doc.Bytes);
        return Task.FromResult<ExtractedText?>(
            text.Trim().Length > 0 ? new ExtractedText(text, UsedRecognition: false) : null);
    }
}

/// <summary>
/// Placeholder recognizer: OCR/Vision is not wired yet (lands with Plan 7's privacy-aware
/// recognizer). Signals clearly rather than pretending to read a scan.
/// </summary>
public sealed class UnavailableRecognizer : IRecognizer
{
    public Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "This looks like a scan/PDF/image; OCR/Vision recognition isn't configured yet. "
            + "Provide a text or CSV export for now.");
}
