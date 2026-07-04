namespace TaxClaw.Calc.Form;

/// <summary>
/// A single line and the line ids it depends on (its inputs in the DAG). <see cref="LawRef"/> ties
/// the line to the provision that grounds it (the grounding contract), and <see cref="Description"/>
/// is the human-readable label. Both are optional so purely structural test fixtures stay terse.
/// </summary>
public sealed record FormLineDefinition(
    string Id,
    IReadOnlyList<string> DependsOn,
    string Description = "",
    string? LawRef = null);
