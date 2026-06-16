using TaxClaw.Calc.Functions;
using TaxClaw.Calc.Scripting;
using TaxClaw.Core.Calc;
using Xunit;

namespace TaxClaw.Calc.Tests;

public class ScriptCompilerTests
{
    private static FunctionSource Source(string body) =>
        new("r38", "2027.1", body, new Provenance(LawRef: "§ 16", FormLine: "r38", Version: "2027.1"));

    [Fact]
    public async Task Compiling_unapproved_source_throws()
    {
        var compiler = new ScriptCompiler(new ApprovalGate());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => compiler.CompileAsync(Source("return 1m;")));
    }

    [Fact]
    public async Task Compiled_function_reads_lines_and_uses_context_math()
    {
        var gate = new ApprovalGate();
        // Body has access to `ctx` (CalcContext); it must use ctx math, not raw operators on doubles.
        var src = Source("return ctx.Subtract(ctx.Line(\"r36\"), ctx.Line(\"r37\"));");
        gate.Approve(src);

        var compiler = new ScriptCompiler(gate);
        ICalcFunction fn = await compiler.CompileAsync(src);

        var ctx = new CalcContext(new Dictionary<string, decimal> { ["r36"] = 120000m, ["r37"] = 20000m });
        decimal value = fn.Evaluate(ctx, out var steps);

        Assert.Equal(100000m, value);
        Assert.NotEmpty(steps);
    }

    [Fact]
    public async Task Long_running_script_is_aborted_by_the_timeout()
    {
        var gate = new ApprovalGate();
        var src = Source("while(true) {} ");
        gate.Approve(src);

        var compiler = new ScriptCompiler(gate, TimeSpan.FromMilliseconds(200));
        ICalcFunction fn = await compiler.CompileAsync(src);

        var ctx = new CalcContext(new Dictionary<string, decimal>());
        Assert.Throws<TimeoutException>(() => fn.Evaluate(ctx, out _));
    }
}
