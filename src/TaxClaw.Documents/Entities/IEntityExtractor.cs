using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>
/// Pulls schema-defined fields out of recognized text. Only fields named in the schema are kept,
/// so arbitrary text in the document cannot inject unexpected keys (prompt-injection guard).
/// An LLM-backed extractor implements the same seam for messy formats.
/// </summary>
public interface IEntityExtractor
{
    Task<ExtractionResult> ExtractAsync(ExtractedText text, EntitySchema schema, CancellationToken ct = default);
}
