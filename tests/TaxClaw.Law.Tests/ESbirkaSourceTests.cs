using TaxClaw.Law.Ingest;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Tests;

public class ESbirkaSourceTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));

    [Fact]
    public void Aggregate_groups_fragments_into_one_section_per_paragraph()
    {
        const string json = """
        {"results":{"bindings":[
          {"cit":{"value":"Část 1"},"text":{"value":"ČÁST PRVNÍ"}},
          {"cit":{"value":"§ 6 odst. 1"},"text":{"value":"<var>(1)</var> Příjmy ze závislé činnosti jsou"}},
          {"cit":{"value":"§ 6 odst. 2"},"text":{"value":"<var>(2)</var> plnění zaměstnavatele"}},
          {"cit":{"value":"§ 8 odst. 1"},"text":{"value":"Příjmy z kapitálového majetku"}}
        ]}}
        """;

        var sections = ESbirkaSource.Aggregate(json, V2027);

        Assert.Equal(2, sections.Count); // §6, §8; the "Část 1" header is skipped
        LawSection s6 = sections.Single(s => s.Section == "§ 6");
        Assert.Contains("závislé", s6.Text);
        Assert.Contains("plnění zaměstnavatele", s6.Text); // both fragments aggregated
        Assert.DoesNotContain("<var>", s6.Text);           // tags stripped
        Assert.Equal("eli/cz/sb/1992/586/2027-01-01", s6.SourceEli);
    }

    [Fact]
    public void Aggregate_handles_multi_letter_section_suffixes()
    {
        const string json = """
        {"results":{"bindings":[{"cit":{"value":"§ 38ch odst. 1"},"text":{"value":"text"}}]}}
        """;

        var sections = ESbirkaSource.Aggregate(json, V2027);

        Assert.Equal("§ 38ch", sections.Single().Section);
    }

    [Fact]
    public void Aggregate_groups_treaty_fragments_by_article()
    {
        // Real fragment shape of the US–CZ double-taxation treaty (32/1994) from e-Sbírka:
        // treaty fragments are cited as "Příloha  Čl. N …" (articles), not "§ N".
        const string json = """
        {"results":{"bindings":[
          {"cit":{"value":"Příloha  Čl. 1"},"text":{"value":"Článek 1"}},
          {"cit":{"value":"Příloha  Čl. 1 bod 1"},"text":{"value":"<var>1.</var> Tato smlouva se vztahuje na osoby"}},
          {"cit":{"value":"Příloha  Čl. 1 bod 2 písm. a)"},"text":{"value":"jakoukoli výjimku, osvobození, zápočet"}},
          {"cit":{"value":"Příloha  Čl. 4 bod 1"},"text":{"value":"výraz rezident jednoho smluvního státu"}}
        ]}}
        """;

        var treaty = new LawVersion("32/1994", new DateOnly(1994, 1, 1));
        var sections = ESbirkaSource.Aggregate(json, treaty);

        Assert.Equal(2, sections.Count); // Čl. 1, Čl. 4
        LawSection article1 = sections.Single(s => s.Section == "Čl. 1");
        Assert.Contains("Tato smlouva se vztahuje", article1.Text);
        Assert.Contains("zápočet", article1.Text); // both bod fragments aggregated
        Assert.DoesNotContain("<var>", article1.Text);
        Assert.Contains(sections, s => s.Section == "Čl. 4");
    }

    [Fact]
    public async Task LoadAsync_uses_the_injected_fetcher()
    {
        var source = new ESbirkaSource((_, _) =>
            Task.FromResult("""{"results":{"bindings":[{"cit":{"value":"§ 16"},"text":{"value":"Sazba daně"}}]}}"""));

        var sections = await source.LoadAsync(V2027);

        Assert.Equal("§ 16", sections.Single().Section);
    }

    [Fact]
    public void ParseEditions_extracts_dates_and_skips_the_sentinel()
    {
        const string json = """
        {"results":{"bindings":[
          {"ed":{"value":"https://opendata.eselpoint.gov.cz/esel-esb/eli/cz/sb/1992/586/2027-01-01"}},
          {"ed":{"value":"https://opendata.eselpoint.gov.cz/esel-esb/eli/cz/sb/1992/586/2026-04-01"}},
          {"ed":{"value":"https://opendata.eselpoint.gov.cz/esel-esb/eli/cz/sb/1992/586/0000-00-00"}}
        ]}}
        """;

        var editions = ESbirkaSource.ParseEditions(json, "586/1992");

        Assert.Equal(2, editions.Count); // 0000-00-00 is skipped
        Assert.Contains(new LawVersion("586/1992", new DateOnly(2027, 1, 1)), editions);
        Assert.Contains(new LawVersion("586/1992", new DateOnly(2026, 4, 1)), editions);
    }
}
