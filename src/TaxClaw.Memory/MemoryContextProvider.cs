using System.Text;

namespace TaxClaw.Memory;

/// <summary>
/// Renders the memory relevant to the current context into a text block the agent prepends to its
/// system context. Feedback/corrections come first so they visibly override default behavior.
/// </summary>
public sealed class MemoryContextProvider(IMemoryStore store, int maxEntries = 20)
{
    public async Task<string> BuildContextAsync(string? projectId, string? documentType, CancellationToken ct = default)
    {
        var entries = await store.QueryAsync(projectId, documentType, ct);
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Remembered context (user corrections take priority over your defaults):");
        foreach (MemoryEntry e in entries.Take(maxEntries))
        {
            sb.AppendLine($"- [{e.Kind}] {e.Text}");
        }
        return sb.ToString().TrimEnd();
    }
}
