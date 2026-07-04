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

        public Task<IReadOnlyList<LawVersion>> ListEditionsAsync(string actNumber, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LawVersion>>(
                sections.Select(s => s.Version).Where(v => v.ActNumber == actNumber).Distinct().ToList());
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

    [Fact]
    public async Task OpenForYearAsync_selects_and_loads_the_year_edition()
    {
        var v2026 = new LawVersion("586/1992", new DateOnly(2026, 4, 1));
        var v2027 = new LawVersion("586/1992", new DateOnly(2027, 1, 1));
        var source = new FakeSource(
            new LawSection("§ 6", "§ 6", "znění 2026", v2026, v2026.Eli),
            new LawSection("§ 6", "§ 6", "Příjmy ze závislé činnosti 2027", v2027, v2027.Eli));
        using var session = new LawSession();

        await session.OpenForYearAsync(source, "586/1992", 2027);

        Assert.Equal(v2027, session.CurrentEdition); // latest effective by 2027-12-31
        Assert.Contains("2027", await session.Tools.LookupLaw("§ 6"));
    }
}
