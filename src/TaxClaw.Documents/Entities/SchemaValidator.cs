namespace TaxClaw.Documents.Entities;

/// <summary>Checks that every required schema field is present and non-blank.</summary>
public static class SchemaValidator
{
    public static ValidationReport Validate(ExtractionResult result, EntitySchema schema)
    {
        var missing = schema.RequiredFields
            .Where(f => string.IsNullOrWhiteSpace(result.Get(f.Name)))
            .Select(f => f.Name)
            .ToList();

        return new ValidationReport(missing.Count == 0, missing);
    }
}
