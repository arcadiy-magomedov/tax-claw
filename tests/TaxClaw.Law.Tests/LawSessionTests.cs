using TaxClaw.Law;
using TaxClaw.Law.Ingest;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Tests;

public class LawSessionTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));

    private sealed class FakeSource(params LawSection[] sections) : ILawSource
    {
        public Task<IReadOnlyList<LawSection>> LoadAsync(LawVersion version, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LawSection>>(sections.Where(s => s.Version == version).ToList());
    }

    [Fact]
    public async Task Before_load_tools_report_no_law_and_annotate_is_noop()
    {
        using var session = new LawSession();

        Assert.Null(session.CurrentEdition);
        Assert.Contains("No law is loaded", await session.Tools.LookupLaw("§ 6"));
        Assert.Equal("cites § 4242", session.Annotate("cites § 4242")); // no edition → nothing to check
    }

    [Fact]
    public async Task After_load_tools_resolve_and_grounding_annotates()
    {
        var source = new FakeSource(new LawSection("§ 6", "§ 6", "Příjmy ze závislé činnosti", V2027, V2027.Eli));
        using var session = new LawSession();

        await session.LoadAsync(source, V2027);

        Assert.Equal(V2027, session.CurrentEdition);
        Assert.Equal(1, session.SectionCount);
        Assert.Contains("závislé", await session.Tools.LookupLaw("§ 6"));
        Assert.Equal("taxed under § 6", session.Annotate("taxed under § 6")); // grounded → unchanged
        Assert.Contains("not found", session.Annotate("exempt under § 999"),  // ungrounded → flagged
            StringComparison.OrdinalIgnoreCase);
    }
}
