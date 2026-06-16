namespace TaxClaw.Core.Calc;

/// <summary>
/// Where a figure or rule comes from. Every computed number must carry one of these so any
/// result can be traced back to legislation, a form line, and the exact pinned version.
/// </summary>
public sealed record Provenance(
    string? LawRef = null,
    string? FormLine = null,
    string? DocumentId = null,
    string? Version = null,
    string? Hash = null)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (LawRef is not null) parts.Add(LawRef);
        if (FormLine is not null) parts.Add($"line {FormLine}");
        if (DocumentId is not null) parts.Add($"doc {DocumentId}");
        if (Version is not null) parts.Add($"v{Version}");
        return parts.Count == 0 ? "(no source)" : string.Join(", ", parts);
    }
}
