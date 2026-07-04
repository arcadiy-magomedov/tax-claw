using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>Extracted field values for a document, keyed by schema field name.</summary>
public sealed record ExtractionResult(DocumentType Type, IReadOnlyDictionary<string, string> Fields)
{
    public string? Get(string field) => Fields.TryGetValue(field, out var v) ? v : null;
}

/// <summary>Outcome of validating an extraction against its schema.</summary>
public sealed record ValidationReport(bool IsValid, IReadOnlyList<string> MissingFields);
