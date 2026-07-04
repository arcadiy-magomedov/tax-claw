using System.Text;
using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;

namespace TaxClaw.Export;

/// <summary>
/// Renders the return as a human-readable markdown summary: every populated line with its value,
/// the calculation steps, and the legislation citation. This is export milestone 1.
/// </summary>
public sealed class SummaryExporter : IReturnExporter<string>
{
    public string Export(TaxReturn taxReturn)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Tax declaration summary — {taxReturn.Year}");
        sb.AppendLine();
        sb.AppendLine("> This is a computed draft and is **not tax advice**. Review every figure before filing.");
        sb.AppendLine();

        foreach ((string lineId, decimal value) in taxReturn.Lines.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"## Line {lineId}: {value}");
            CalculationTrace? trace = taxReturn.GetTrace(lineId);
            if (trace is not null)
            {
                foreach (CalculationStep step in trace.Steps)
                {
                    sb.AppendLine($"- {step.Description}: {step.Detail}");
                }
                sb.AppendLine($"- Source: {trace.Provenance}");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
