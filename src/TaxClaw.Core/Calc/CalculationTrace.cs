namespace TaxClaw.Core.Calc;

/// <summary>One human-readable step in deriving a figure.</summary>
public sealed record CalculationStep(string Description, string Detail);

/// <summary>
/// The full derivation of a single form line: ordered steps, the final result, and the
/// provenance. This is what powers "explain how line N was computed".
/// </summary>
public sealed record CalculationTrace(
    string LineId,
    IReadOnlyList<CalculationStep> Steps,
    string Result,
    Provenance Provenance);
