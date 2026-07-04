using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>
/// Deterministic extractor for "label: value" text. Keeps only keys declared in the schema —
/// everything else is discarded, so document content is treated strictly as data.
/// </summary>
public sealed class LabelledLineExtractor : IEntityExtractor
{
    public Task<ExtractionResult> ExtractAsync(ExtractedText text, EntitySchema schema, CancellationToken ct = default)
    {
        var allowed = schema.Fields.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in text.Text.Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            if (allowed.Contains(key) && value.Length > 0)
            {
                fields[key] = value;
            }
        }

        return Task.FromResult(new ExtractionResult(schema.Type, fields));
    }
}
