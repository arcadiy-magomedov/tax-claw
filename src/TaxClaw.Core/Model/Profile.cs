namespace TaxClaw.Core.Model;

/// <summary>
/// Cross-project personal information. Captured once and re-confirmed (and snapshotted)
/// into every new project. Treated as sensitive (PII) by later plans.
/// </summary>
public sealed record Profile
{
    public required string FullName { get; init; }
    public string? RodneCislo { get; init; }
    public string? Address { get; init; }
    public string? Employer { get; init; }
    public string? BankAccount { get; init; }
}
