using TaxClaw.Law.Corpus;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Tests;

public class SqliteLawCorpusTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));
    private static readonly LawVersion V2025 = new("586/1992", new DateOnly(2025, 1, 1));

    private static LawSection Section(string s, string text, LawVersion v) => new(s, s, text, v, v.Eli);

    [Fact]
    public async Task Resolves_a_section_for_its_edition()
    {
        using var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync([Section("§ 6", "Příjmy ze závislé činnosti", V2027)]);

        LawSection? p = corpus.Resolve("§ 6", V2027);

        Assert.NotNull(p);
        Assert.Contains("závislé", p!.Text);
        Assert.Equal("eli/cz/sb/1992/586/2027-01-01", p.SourceEli);
    }

    [Fact]
    public async Task Missing_section_or_wrong_edition_returns_null()
    {
        using var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync([Section("§ 6", "text", V2027)]);

        Assert.Null(corpus.Resolve("§ 999", V2027));  // absent section
        Assert.Null(corpus.Resolve("§ 6", V2025));     // present, wrong edition
    }

    [Fact]
    public async Task Ingest_is_idempotent_and_edition_scoped()
    {
        using var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync([Section("§ 16", "old", V2027)]);
        await corpus.IngestAsync([Section("§ 16", "Sazba daně činí", V2027)]); // replaces same key
        await corpus.IngestAsync([Section("§ 16", "starší znění", V2025)]);    // different edition kept

        Assert.Equal("Sazba daně činí", corpus.Resolve("§ 16", V2027)!.Text);
        Assert.Equal("starší znění", corpus.Resolve("§ 16", V2025)!.Text);
    }
}
