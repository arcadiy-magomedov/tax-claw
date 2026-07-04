using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Extract;

/// <summary>
/// Tries to pull an existing text layer from a document (e.g. PdfPig for text PDFs). Returns null
/// when there is no usable text, signalling the recognition fallback.
/// </summary>
public interface ITextExtractor
{
    Task<ExtractedText?> TryExtractAsync(SourceDocument doc, CancellationToken ct = default);
}
