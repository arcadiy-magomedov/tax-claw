namespace TaxClaw.Calc.Form;

/// <summary>A single line and the line ids it depends on (its inputs in the DAG).</summary>
public sealed record FormLineDefinition(string Id, IReadOnlyList<string> DependsOn);
