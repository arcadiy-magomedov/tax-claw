using TaxClaw.Memory;
using TaxClaw.Storage;

namespace TaxClaw.Memory.Tests;

public class MemoryContextProviderTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-memctx-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Renders_relevant_memory_with_feedback_first()
    {
        var store = new JsonMemoryStore(new StorageRoot(_root));
        await store.AddAsync(new MemoryEntry("pref", MemoryKind.Preference, MemoryScope.Global(),
            "Reply in Czech.", DateTimeOffset.UnixEpoch));
        await store.AddAsync(new MemoryEntry("fb", MemoryKind.Feedback, MemoryScope.Global(),
            "Treat Microsoft RSUs as § 6.", DateTimeOffset.UnixEpoch.AddDays(1)));

        var provider = new MemoryContextProvider(store);
        string context = await provider.BuildContextAsync(projectId: "2027", documentType: null);

        Assert.Contains("§ 6", context);
        Assert.True(context.IndexOf("§ 6", StringComparison.Ordinal)
                    < context.IndexOf("Reply in Czech", StringComparison.Ordinal),
            "feedback should be rendered before lower-priority preferences");
    }

    [Fact]
    public async Task Empty_memory_yields_empty_context()
    {
        var provider = new MemoryContextProvider(new JsonMemoryStore(new StorageRoot(_root)));
        Assert.Equal(string.Empty, await provider.BuildContextAsync(null, null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
