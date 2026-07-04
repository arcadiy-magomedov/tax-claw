namespace TaxClaw.Memory;

public interface IMemoryStore
{
    Task AddAsync(MemoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryEntry>> QueryAsync(string? projectId, string? documentType, CancellationToken ct = default);
}
