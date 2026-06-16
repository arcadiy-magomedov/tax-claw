using TaxClaw.Calc.Scripting;
using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class ApprovalGateTests
{
    private static FunctionSource Source(string body) =>
        new("r38", "2027.1", body, new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));

    [Fact]
    public void Hash_is_stable_for_identical_source()
    {
        Assert.Equal(Source("return 1m;").Hash, Source("return 1m;").Hash);
    }

    [Fact]
    public void Hash_differs_when_body_changes()
    {
        Assert.NotEqual(Source("return 1m;").Hash, Source("return 2m;").Hash);
    }

    [Fact]
    public void Unapproved_source_is_not_approved()
    {
        var gate = new ApprovalGate();
        Assert.False(gate.IsApproved(Source("return 1m;")));
    }

    [Fact]
    public void Approving_marks_exactly_that_source_hash()
    {
        var gate = new ApprovalGate();
        var src = Source("return 1m;");

        gate.Approve(src);

        Assert.True(gate.IsApproved(src));
        Assert.False(gate.IsApproved(Source("return 999m;")));
    }
}
