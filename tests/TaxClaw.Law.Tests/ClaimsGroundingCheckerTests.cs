using TaxClaw.Law.Corpus;
using TaxClaw.Law.Grounding;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Tests;

public class ClaimsGroundingCheckerTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));

    private static async Task<ClaimsGroundingChecker> Seeded()
    {
        var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync([new LawSection("§ 6", "§ 6", "employment income", V2027, V2027.Eli)]);
        return new ClaimsGroundingChecker(new LawGroundingGate(corpus));
    }

    [Fact]
    public async Task Clean_answer_with_a_resolvable_citation_is_unchanged()
    {
        ClaimsGroundingChecker checker = await Seeded();
        const string answer = "RSU vesting is employment income under § 6.";

        Assert.True(checker.Verify(answer, V2027).IsGrounded);
        Assert.Equal(answer, checker.Annotate(answer, V2027));
    }

    [Fact]
    public async Task Answer_without_citations_is_not_flagged()
    {
        ClaimsGroundingChecker checker = await Seeded();
        const string answer = "Let me look that up.";

        Assert.True(checker.Verify(answer, V2027).IsGrounded);
    }

    [Fact]
    public async Task Hallucinated_or_stale_citation_is_flagged_and_annotated()
    {
        ClaimsGroundingChecker checker = await Seeded();
        const string answer = "This is taxed under § 6 and exempt under § 4242.";

        GroundingReport report = checker.Verify(answer, V2027);
        Assert.False(report.IsGrounded);
        Assert.Equal(["§ 4242"], report.UnresolvedCitations); // § 6 resolves, § 4242 does not

        string annotated = checker.Annotate(answer, V2027);
        Assert.Contains("§ 4242", annotated);
        Assert.Contains("not found", annotated, StringComparison.OrdinalIgnoreCase);
    }
}
