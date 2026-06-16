using System.Collections.Immutable;
using TaxClaw.Core.Calc;

namespace TaxClaw.Core.Model;

/// <summary>An income item feeding the declaration (employment, RSU vest, dividend, sale).</summary>
public sealed record IncomeItem(
    string Kind,
    Money Amount,
    DateOnly Date,
    string? DocumentId = null);

/// <summary>
/// The format-independent single source of truth for a declaration. Form-specific exports
/// (summary, PDF, XML) are projections of this model. Immutable — each update returns a copy.
/// </summary>
public sealed record TaxReturn
{
    public TaxReturn(TaxYear year)
    {
        Year = year;
        Incomes = [];
        Lines = ImmutableDictionary<string, decimal>.Empty;
        Traces = ImmutableDictionary<string, CalculationTrace>.Empty;
    }

    public TaxYear Year { get; init; }
    public ImmutableList<IncomeItem> Incomes { get; init; }
    public ImmutableDictionary<string, decimal> Lines { get; init; }
    public ImmutableDictionary<string, CalculationTrace> Traces { get; init; }

    public TaxReturn WithIncome(IncomeItem item) =>
        this with { Incomes = Incomes.Add(item) };

    public TaxReturn WithLine(string lineId, decimal value, CalculationTrace trace) =>
        this with
        {
            Lines = Lines.SetItem(lineId, value),
            Traces = Traces.SetItem(lineId, trace)
        };

    public decimal? GetLine(string lineId) =>
        Lines.TryGetValue(lineId, out var v) ? v : null;

    public CalculationTrace? GetTrace(string lineId) =>
        Traces.TryGetValue(lineId, out var t) ? t : null;
}
