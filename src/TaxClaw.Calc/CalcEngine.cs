using TaxClaw.Calc.Form;
using TaxClaw.Calc.Functions;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;

namespace TaxClaw.Calc;

/// <summary>
/// Evaluates a form by running the approved function for each line in dependency order, threading
/// computed values forward and recording a <see cref="CalculationTrace"/> for every line.
/// The engine performs no arithmetic of its own — it only orchestrates function execution.
/// </summary>
public sealed class CalcEngine(CalcFunctionRegistry registry)
{
    public TaxReturn Evaluate(FormDefinition form, TaxReturn seed)
    {
        TaxReturn current = seed;
        var computed = new Dictionary<string, decimal>();

        foreach (FormLineDefinition line in form.EvaluationOrder())
        {
            var key = new CalcFunctionKey(line.Id, form.Version);
            if (!registry.TryResolve(key, out var function) || function is null)
            {
                throw new InvalidOperationException(
                    $"No approved calc function registered for line '{line.Id}' (version {form.Version}).");
            }

            var context = new CalcContext(computed);
            decimal value = function.Evaluate(context, out var steps);
            computed[line.Id] = value;

            var trace = new CalculationTrace(
                line.Id,
                steps,
                value.ToString(),
                new Provenance(FormLine: line.Id, Version: form.Version));

            current = current.WithLine(line.Id, value, trace);
        }

        return current;
    }
}
