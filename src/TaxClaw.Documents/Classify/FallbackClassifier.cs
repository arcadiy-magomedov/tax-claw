using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Classify;

/// <summary>
/// Deterministic-first classifier: trusts the cheap keyword pass when it is confident, and only
/// consults the (non-deterministic, paid) LLM classifier for ambiguous documents. This keeps the
/// common case reproducible and cost-free, matching tax-claw's "deterministic where possible"
/// principle. The more confident verdict wins when the fallback is consulted.
/// </summary>
public sealed class FallbackClassifier(
    IDocumentClassifier primary, IDocumentClassifier fallback, double minConfidence = 0.5) : IDocumentClassifier
{
    public async Task<Classification> ClassifyAsync(ExtractedText text, CancellationToken ct = default)
    {
        Classification p = await primary.ClassifyAsync(text, ct);
        if (p.Type != DocumentType.Unknown && p.Confidence >= minConfidence)
        {
            return p;
        }

        Classification f = await fallback.ClassifyAsync(text, ct);
        return f.Confidence > p.Confidence ? f : p;
    }
}
