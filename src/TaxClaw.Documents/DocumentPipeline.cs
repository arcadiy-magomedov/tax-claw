using TaxClaw.Core.Model;
using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Map;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents;

/// <summary>The outcome of processing one document through the pipeline.</summary>
public sealed record DocumentResult(
    DocumentType Type,
    double Confidence,
    ExtractionResult Extraction,
    ValidationReport Validation,
    TaxReturn Return);

/// <summary>
/// Orchestrates: extract text → classify → extract entities (schema-bound) → validate →
/// map to the return only when valid. Invalid extractions are returned for the agent to
/// raise as questions, never silently mapped.
/// </summary>
public sealed class DocumentPipeline(
    TextLayerDetector extractor,
    IDocumentClassifier classifier,
    IEntityExtractor entityExtractor)
{
    private readonly DocumentMapper _mapper = new();

    public async Task<DocumentResult> ProcessAsync(
        SourceDocument doc, TaxReturn current, string documentId, CancellationToken ct = default)
    {
        ExtractedText text = await extractor.ExtractAsync(doc, ct);
        Classification classification = await classifier.ClassifyAsync(text, ct);

        EntitySchema schema = DocumentSchemas.For(classification.Type);
        ExtractionResult extraction = await entityExtractor.ExtractAsync(text, schema, ct);
        ValidationReport validation = SchemaValidator.Validate(extraction, schema);

        TaxReturn updated = validation.IsValid
            ? _mapper.Apply(current, extraction, documentId)
            : current;

        return new DocumentResult(
            classification.Type, classification.Confidence, extraction, validation, updated);
    }
}
