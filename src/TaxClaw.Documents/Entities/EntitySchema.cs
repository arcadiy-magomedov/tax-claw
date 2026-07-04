using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>A single field the pipeline expects to extract from a document.</summary>
public sealed record EntityField(string Name, bool Required, string Description);

/// <summary>
/// The set of fields to extract for a document type. Extraction is bound to this schema, which is
/// what stops free-form document text from being treated as instructions.
/// </summary>
public sealed record EntitySchema(DocumentType Type, IReadOnlyList<EntityField> Fields)
{
    public IEnumerable<EntityField> RequiredFields => Fields.Where(f => f.Required);
}
