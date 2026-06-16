namespace TaxClaw.Core.Model;

/// <summary>
/// User preferences persisted across runs (e.g. the chosen LLM provider, model, and reasoning
/// effort). Env-var configuration still overrides these at startup.
/// </summary>
public sealed record Preferences
{
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public string? ReasoningEffort { get; init; }
}
