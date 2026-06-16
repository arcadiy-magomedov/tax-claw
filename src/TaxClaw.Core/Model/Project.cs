namespace TaxClaw.Core.Model;

public enum ProjectStatus
{
    Draft,
    CollectingDocuments,
    Calculated,
    Reviewed,
    Exported,
    Filed
}

/// <summary>A single tax declaration for one tax year (e.g. "Declaration 2027").</summary>
public sealed record Project
{
    public required string Id { get; init; }
    public required TaxYear Year { get; init; }
    public required Profile ProfileSnapshot { get; init; }
    public ProjectStatus Status { get; init; } = ProjectStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; }
}
