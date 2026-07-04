using System.Text.Json;
using TaxClaw.Storage;

namespace TaxClaw.Memory;

/// <summary>
/// Persists memory as a single JSON array under the data root. Queries return entries whose scope
/// is relevant to the given context, ordered by priority then recency.
/// </summary>
public sealed class JsonMemoryStore(StorageRoot root) : IMemoryStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private string File => Path.Combine(root.Path, "memory", "entries.json");

    public async Task AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var all = (await LoadAllAsync(ct)).ToList();
        all.Add(entry);

        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        await using var stream = System.IO.File.Create(File);
        await JsonSerializer.SerializeAsync(stream, all, Json, ct);
    }

    public async Task<IReadOnlyList<MemoryEntry>> QueryAsync(string? projectId, string? documentType, CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return all
            .Where(e => e.Scope.IsRelevantTo(projectId, documentType))
            .OrderByDescending(e => e.Kind.Priority())
            .ThenByDescending(e => e.CreatedAt)
            .ToList();
    }

    private async Task<IReadOnlyList<MemoryEntry>> LoadAllAsync(CancellationToken ct)
    {
        if (!System.IO.File.Exists(File))
        {
            return [];
        }

        await using var stream = System.IO.File.OpenRead(File);
        return await JsonSerializer.DeserializeAsync<List<MemoryEntry>>(stream, Json, ct) ?? [];
    }
}
