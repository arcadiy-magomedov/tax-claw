using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>
/// Deterministic-first extractor: runs the cheap "label: value" pass and only calls the LLM
/// extractor when required fields are still missing. Deterministic values are authoritative — the
/// LLM only fills the gaps, so a clean document never depends on the model and traceable values are
/// never overwritten by a guessed one. Both paths keep only schema-declared keys.
/// </summary>
public sealed class FallbackEntityExtractor(IEntityExtractor primary, IEntityExtractor fallback) : IEntityExtractor
{
    public async Task<ExtractionResult> ExtractAsync(ExtractedText text, EntitySchema schema, CancellationToken ct = default)
    {
        ExtractionResult deterministic = await primary.ExtractAsync(text, schema, ct);
        if (SchemaValidator.Validate(deterministic, schema).IsValid)
        {
            return deterministic;
        }

        ExtractionResult llm = await fallback.ExtractAsync(text, schema, ct);

        var merged = new Dictionary<string, string>(llm.Fields, StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> field in deterministic.Fields)
        {
            merged[field.Key] = field.Value;
        }

        return new ExtractionResult(schema.Type, merged);
    }
}
