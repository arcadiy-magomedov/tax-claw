using TaxClaw.Memory;

namespace TaxClaw.Memory.Tests;

public class MemoryEntryTests
{
    [Fact]
    public void Feedback_outranks_fact_which_outranks_preference()
    {
        Assert.True(MemoryKind.Feedback.Priority() > MemoryKind.Fact.Priority());
        Assert.True(MemoryKind.Fact.Priority() > MemoryKind.Preference.Priority());
    }

    [Fact]
    public void Entry_keeps_its_scope_kind_and_text()
    {
        var entry = new MemoryEntry(
            Id: "m1",
            Kind: MemoryKind.Feedback,
            Scope: MemoryScope.DocumentType("rsu_vesting"),
            Text: "Treat Microsoft RSUs as § 6 employment income.",
            CreatedAt: DateTimeOffset.UnixEpoch);

        Assert.Equal(MemoryKind.Feedback, entry.Kind);
        Assert.True(entry.Scope.IsRelevantTo("2027", "rsu_vesting"));
    }
}
