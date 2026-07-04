using Microsoft.Extensions.AI;
using TaxClaw.Law;
using TaxClaw.Law.Corpus;
using TaxClaw.Law.Model;
using TaxClaw.Law.Retrieval;

namespace TaxClaw.Law.Tests;

public class LawToolsTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));

    private static async Task<LawTools> Seeded(Func<LawVersion>? edition = null)
    {
        var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync(
        [
            new LawSection("§ 6", "§ 6", "Příjmy ze závislé činnosti jsou plnění zaměstnavatele", V2027, V2027.Eli),
            new LawSection("§ 10", "§ 10", "Ostatní příjmy příjem z prodeje cenných papírů", V2027, V2027.Eli),
        ]);
        return new LawTools(corpus, new FtsLawRetriever(corpus), edition ?? (() => V2027));
    }

    [Fact]
    public async Task Lookup_returns_text_with_citation()
    {
        LawTools tools = await Seeded();
        string result = await tools.LookupLaw("§ 6");

        Assert.Contains("závislé", result);
        Assert.Contains("§ 6", result);
        Assert.Contains("586/1992", result);
    }

    [Fact]
    public async Task Lookup_tolerates_bare_or_prefixed_section_ids()
    {
        LawTools tools = await Seeded();
        Assert.Contains("závislé", await tools.LookupLaw("6"));
        Assert.Contains("závislé", await tools.LookupLaw("§6"));
    }

    [Fact]
    public async Task Lookup_reports_a_missing_section()
    {
        LawTools tools = await Seeded();
        Assert.Contains("not found", await tools.LookupLaw("§ 999"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_returns_relevant_section_with_citation()
    {
        LawTools tools = await Seeded();
        string result = await tools.SearchLaw("prodej cenných papírů");

        Assert.Contains("§ 10", result);
        Assert.Contains("edition 2027-01-01", result);
    }

    [Fact]
    public async Task Active_edition_is_resolved_dynamically()
    {
        var current = V2027;
        LawTools tools = await Seeded(() => current);

        Assert.Contains("závislé", await tools.LookupLaw("§ 6"));   // 2027 edition seeded
        current = new LawVersion("586/1992", new DateOnly(2099, 1, 1)); // switch to an un-ingested edition
        Assert.Contains("not found", await tools.LookupLaw("§ 6"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTools_exposes_lookup_and_search()
    {
        LawTools tools = await Seeded();
        var names = tools.CreateTools().OfType<AIFunction>().Select(f => f.Name).ToHashSet();

        Assert.Contains("lookup_law", names);
        Assert.Contains("search_law", names);
    }
}
