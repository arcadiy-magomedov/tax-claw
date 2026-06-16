using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;

namespace TaxClaw.Calc.Scripting;

/// <summary>
/// Compiles an approved <see cref="FunctionSource"/> body into an <see cref="ICalcFunction"/>.
/// The body is a C# expression/statements with a single global, <c>ctx</c> (a
/// <see cref="CalcContext"/>), returning a <see cref="decimal"/>. Execution is bounded by a
/// timeout. This is an in-process guard; OS-level sandboxing is layered on in the document plan.
/// </summary>
public sealed class ScriptCompiler(ApprovalGate gate, TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(2);

    public sealed class Globals
    {
        public required CalcContext ctx { get; init; }
    }

    public async Task<ICalcFunction> CompileAsync(FunctionSource source)
    {
        if (!gate.IsApproved(source))
        {
            throw new InvalidOperationException(
                $"Refusing to compile unapproved function for line '{source.LineId}'.");
        }

        ScriptOptions options = ScriptOptions.Default
            .WithImports("System")
            .AddReferences(typeof(CalcContext).Assembly);

        // Compile once up front so syntax errors surface immediately.
        Script<decimal> script = CSharpScript.Create<decimal>(source.Body, options, typeof(Globals));
        script.Compile();

        await Task.CompletedTask;
        return new CompiledCalcFunction(script, source, _timeout);
    }

    private sealed class CompiledCalcFunction(Script<decimal> script, FunctionSource source, TimeSpan timeout)
        : ICalcFunction
    {
        public decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps)
        {
            var globals = new Globals { ctx = context };

            // Offload to the thread pool so a CPU-bound body cannot block the caller, and bound it
            // with a wait. CSharpScript runs synchronously up to its first await, so a tight
            // `while(true)` would otherwise never yield; Task.Run + Wait(timeout) makes the timeout
            // reliable. A runaway body is abandoned (it cannot be force-killed in-process) — the
            // real, killable sandbox is the OS-level process introduced in the document plan.
            Task<ScriptState<decimal>> run = Task.Run(() => script.RunAsync(globals));

            if (!run.Wait(timeout))
            {
                throw new TimeoutException(
                    $"Calc function for line '{source.LineId}' exceeded {timeout.TotalMilliseconds} ms.");
            }

            decimal value = run.GetAwaiter().GetResult().ReturnValue;
            steps = new[]
            {
                new CalculationStep(
                    $"evaluate {source.LineId} ({source.Provenance})",
                    value.ToString())
            };
            return value;
        }
    }
}
