using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Classify;

/// <summary>The classifier's verdict and how confident it is (0..1).</summary>
public readonly record struct Classification(DocumentType Type, double Confidence);

/// <summary>
/// Decides a document's type. The keyword implementation is a cheap first pass; an LLM-backed
/// classifier can implement this same seam for ambiguous documents.
/// </summary>
public interface IDocumentClassifier
{
    Task<Classification> ClassifyAsync(ExtractedText text, CancellationToken ct = default);
}
