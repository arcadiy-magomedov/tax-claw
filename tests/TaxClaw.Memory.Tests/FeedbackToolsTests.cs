using Microsoft.Extensions.AI;
using TaxClaw.Memory;
using TaxClaw.Storage;

namespace TaxClaw.Memory.Tests;

public class FeedbackToolsTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-fbtool-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Remember_feedback_persists_scoped_to_the_active_project()
    {
        var store = new JsonMemoryStore(new StorageRoot(_root));
        var tools = new FeedbackTools(store, () => "2027");

        string ack = await tools.RememberFeedback("Treat Microsoft RSUs as § 6.");

        Assert.Contains("remembered", ack, StringComparison.OrdinalIgnoreCase);
        var entries = await store.QueryAsync("2027", null);
        Assert.Contains(entries, e => e.Kind == MemoryKind.Feedback && e.Text.Contains("§ 6"));
    }

    [Fact]
    public async Task Document_type_scope_is_used_when_given()
    {
        var store = new JsonMemoryStore(new StorageRoot(_root));
        var tools = new FeedbackTools(store, () => "2027");

        await tools.RememberFeedback("RSU as § 6", "rsu_vesting");

        Assert.Contains(await store.QueryAsync(null, "rsu_vesting"), e => e.Text.Contains("§ 6"));
    }

    [Fact]
    public void CreateTools_exposes_remember_feedback()
    {
        var tools = new FeedbackTools(new JsonMemoryStore(new StorageRoot(_root)), () => null);
        var names = tools.CreateTools().OfType<AIFunction>().Select(f => f.Name).ToHashSet();
        Assert.Contains("remember_feedback", names);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
