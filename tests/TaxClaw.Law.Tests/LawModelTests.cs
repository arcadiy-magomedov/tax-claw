using TaxClaw.Law.Model;

namespace TaxClaw.Law.Tests;

public class LawModelTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));

    [Fact]
    public void LawVersion_builds_eli_from_act_and_date()
    {
        Assert.Equal("eli/cz/sb/1992/586/2027-01-01", V2027.Eli);
    }

    [Fact]
    public void LawVersion_rejects_malformed_act_number()
    {
        Assert.Throws<FormatException>(() => new LawVersion("586", new DateOnly(2027, 1, 1)).Eli);
    }

    [Fact]
    public void LawSection_hash_is_stable_and_wording_sensitive()
    {
        var a = new LawSection("§ 6", "§ 6", "income from employment", V2027, V2027.Eli);
        var b = new LawSection("§ 6", "§ 6", "income from employment", V2027, V2027.Eli);
        var c = new LawSection("§ 6", "§ 6", "income from employment (amended)", V2027, V2027.Eli);

        Assert.Equal(a.Hash, b.Hash);
        Assert.NotEqual(a.Hash, c.Hash);
    }

    [Fact]
    public void SelectForTaxYear_picks_latest_edition_effective_by_year_end()
    {
        LawVersion[] editions =
        [
            new("586/1992", new DateOnly(2025, 1, 1)),
            new("586/1992", new DateOnly(2026, 4, 1)),
            new("586/1992", new DateOnly(2027, 1, 1)),
        ];

        Assert.Equal(new DateOnly(2026, 4, 1), LawVersionSet.SelectForTaxYear(editions, 2026)!.EffectiveOn);
        Assert.Equal(new DateOnly(2027, 1, 1), LawVersionSet.SelectForTaxYear(editions, 2027)!.EffectiveOn);
        Assert.Null(LawVersionSet.SelectForTaxYear(editions, 2024));
    }

    [Fact]
    public void VersionSet_returns_the_pinned_edition_per_act()
    {
        var set = new LawVersionSet([V2027]);

        Assert.Equal(V2027, set.EditionFor("586/1992"));
        Assert.Null(set.EditionFor("589/1992"));
    }
}
