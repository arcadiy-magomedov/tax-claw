using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Extract;

/// <summary>
/// Recovers text from scans/photos/image-PDFs via OCR (Tesseract/OS Vision) or a Vision-LLM.
/// Faked in tests; the concrete implementation respects the privacy mode (Plan 7).
/// </summary>
public interface IRecognizer
{
    Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default);
}
