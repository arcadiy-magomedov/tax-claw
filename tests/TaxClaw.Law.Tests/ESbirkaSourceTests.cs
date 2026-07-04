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
