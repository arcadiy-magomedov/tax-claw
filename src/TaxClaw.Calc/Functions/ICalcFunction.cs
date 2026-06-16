using TaxClaw.Core.Calc;

namespace TaxClaw.Calc.Functions;

/// <summary>
/// Computes one form line deterministically. Implementations are either hand-written (for tests)
/// or compiled from approved, agent-generated source. They return the value and the human-readable
/// steps that become part of the <see cref="CalculationTrace"/>.
/// </summary>
public interface ICalcFunction
{
    decimal Evaluate(CalcContext context, out IReadOnlyList<CalculationStep> steps);
}
