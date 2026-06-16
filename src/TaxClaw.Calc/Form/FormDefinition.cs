namespace TaxClaw.Calc.Form;

/// <summary>
/// An official form modeled as a dependency graph of lines. Parsed from the form's filling
/// instructions (pokyny) in the document-pipeline plan; here it is a plain in-memory DAG.
/// </summary>
public sealed class FormDefinition(string formCode, string version, IReadOnlyList<FormLineDefinition> lines)
{
    public string FormCode { get; } = formCode;
    public string Version { get; } = version;
    public IReadOnlyList<FormLineDefinition> Lines { get; } = lines;

    /// <summary>Lines in an order where every dependency precedes its dependents.</summary>
    public IReadOnlyList<FormLineDefinition> EvaluationOrder() => TopologicalSort.Order(Lines);
}
