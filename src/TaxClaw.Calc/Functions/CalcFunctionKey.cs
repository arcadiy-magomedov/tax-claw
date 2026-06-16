namespace TaxClaw.Calc.Functions;

/// <summary>Identifies a calc function by the form line it computes and the pinned version.</summary>
public readonly record struct CalcFunctionKey(string LineId, string Version);
