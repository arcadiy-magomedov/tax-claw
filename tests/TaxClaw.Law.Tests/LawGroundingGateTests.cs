using TaxClaw.Law.Corpus;
using TaxClaw.Law.Grounding;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Tests;

public class LawGroundingGateTests
{
    private static readonly LawVersion V2027 = new("586/1992", new DateOnly(2027, 1, 1));
    private static readonly LawVersion V2025 = new("586/1992", new DateOnly(2025, 1, 1));

    private static async Task<LawGroundingGate> Seeded()
    {
        var corpus = new SqliteLawCorpus();
        await corpus.IngestAsync(
        [
            new LawSection("§ 6", "§ 6", "employment", V2027, V2027.Eli),
            new LawSection("§ 38f", "§ 38f", "foreign tax credit", V2027, V2027.Eli),
        ]);
        return new LawGroundingGate(corpus);
    }

    [Fact]
    public async Task Grounded_when_the_cited_section_resolves()
    {
        LawGroundingGate gate = await Seeded();
        Assert.True(gate.IsGrounded("§ 6", V2027));
        Assert.True(gate.IsGrounded("income per § 6 odst. 1", V2027));
    }

    [Fact]
    public async Task Not_grounded_when_empty_absent_or_wrong_edition()
    {
        LawGroundingGate gate = await Seeded();
        Assert.False(gate.IsGrounded(null, V2027));
        Assert.False(gate.IsGrounded("no citation here", V2027));
        Assert.False(gate.IsGrounded("§ 999", V2027));  // section absent
        Assert.False(gate.IsGrounded("§ 6", V2025));     // present, wrong edition
    }

    [Fact]
    public async Task Multi_reference_requires_all_to_resolve()
    {
        LawGroundingGate gate = await Seeded();
        Assert.True(gate.IsGrounded("§ 6 and § 38f", V2027));
        Assert.False(gate.IsGrounded("§ 6 and § 999", V2027)); // one unresolved fails the whole citation
    }

    [Fact]
    public async Task EnsureGrounded_throws_on_ungrounded_reference()
    {
        LawGroundingGate gate = await Seeded();
        Assert.Throws<UngroundedLawReferenceException>(() => gate.EnsureGrounded("§ 999", V2027));
        gate.EnsureGrounded("§ 6", V2027); // does not throw
    }
}
