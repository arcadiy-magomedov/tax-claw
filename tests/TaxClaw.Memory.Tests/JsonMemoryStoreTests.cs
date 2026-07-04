using TaxClaw.Memory;
using TaxClaw.Storage;

namespace TaxClaw.Memory.Tests;

public class JsonMemoryStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-mem-" + Guid.NewGuid().ToString("N"));

    private JsonMemoryStore NewStore() => new(new StorageRoot(_root));

    [Fact]
    public async Task Persists_and_reloads_entries()
    {
        var store = NewStore();
        await store.AddAsync(new MemoryEntry("p", MemoryKind.Preference,
            MemoryScope.Global(), "Prefer Czech replies.", DateTimeOffset.UnixEpoch));

        var reloaded = NewStore();
        var all = await reloaded.QueryAsync(projectId: null, documentType: null);

        Assert.Single(all);
        Assert.Equal("Prefer Czech replies.", all[0].Text);
    }

    [Fact]
    public async Task Query_filters_by_relevance_to_context()
    {
        var store = NewStore();
        await store.AddAsync(new MemoryEntry("g", MemoryKind.Fact, MemoryScope.Global(), "g", DateTimeOffset.UnixEpoch));
        await store.AddAsync(new MemoryEntry("p27", MemoryKind.Fact, MemoryScope.Project("2027"), "p27", DateTimeOffset.UnixEpoch));
        await store.AddAsync(new MemoryEntry("p26", MemoryKind.Fact, MemoryScope.Project("2026"), "p26", DateTimeOffset.UnixEpoch));

        var hits = await store.QueryAsync(projectId: "2027", documentType: null);

        var ids = hits.Select(h => h.Id).ToHashSet();
        Assert.Contains("g", ids);
        Assert.Contains("p27", ids);
        Assert.DoesNotContain("p26", ids);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
