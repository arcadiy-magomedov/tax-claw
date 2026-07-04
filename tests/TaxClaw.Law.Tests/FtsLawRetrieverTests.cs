using TaxClaw.Law.Corpus;
using TaxClaw.Law.Model;
using TaxClaw.Law.Retrieval;

namespace TaxClaw.Law.Tests;

public class FtsLawRetrieverTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));
    private static readonly LawVersion V2025 = new("586/1992", new DateOnly(2025, 1, 1));

    private static LawSection Section(string s, string text, LawVersion v) => new(s, s, text, v, v.Eli);

    private static async Task<SqliteLawCorpus> Seeded()
    {
        var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync(
        [
            Section("§ 6", "Příjmy ze závislé činnosti jsou plnění zaměstnavatele zaměstnanci", V2027),
            Section("§ 8", "Příjmy z kapitálového majetku dividendy úroky z vkladů", V2027),
            Section("§ 10", "Ostatní příjmy příjem z prodeje cenných papírů", V2027),
        ]);
        return corpus;
    }

    [Fact]
    public async Task Ranks_the_topical_section_first()
    {
        using SqliteLawCorpus corpus = await Seeded();
        var retriever = new FtsLawRetriever(corpus);

        var hits = retriever.Search("prodej cenných papírů", V2027);

        Assert.Equal("§ 10", hits[0].Match.Section);
    }

    [Fact]
    public async Task Matches_are_diacritic_insensitive()
    {
        using SqliteLawCorpus corpus = await Seeded();
        var retriever = new FtsLawRetriever(corpus);

        // query without diacritics still matches "dividendy"
        var hits = retriever.Search("dividendy uroky", V2027);

        Assert.Equal("§ 8", hits[0].Match.Section);
    }

    [Fact]
    public async Task Search_is_edition_scoped()
    {
        using SqliteLawCorpus corpus = await Seeded();
        var retriever = new FtsLawRetriever(corpus);

        Assert.Empty(retriever.Search("dividendy", V2025)); // nothing ingested for the 2025 edition
    }

    [Fact]
    public async Task Empty_query_returns_nothing()
    {
        using SqliteLawCorpus corpus = await Seeded();
        var retriever = new FtsLawRetriever(corpus);

        Assert.Empty(retriever.Search("   ", V2027));
    }
}
