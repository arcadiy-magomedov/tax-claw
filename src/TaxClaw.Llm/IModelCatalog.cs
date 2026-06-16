namespace TaxClaw.Llm;

/// <summary>
/// A selectable model: its provider id, display name, the reasoning-effort levels it supports
/// (empty when the model has no reasoning effort), its default effort, and its fixed context window.
/// </summary>
public sealed record ModelOption(
    string Id,
    string Name,
    IReadOnlyList<string> SupportedReasoningEfforts,
    string? DefaultReasoningEffort,
    long? MaxContextWindowTokens)
{
    public bool SupportsReasoningEffort => SupportedReasoningEfforts.Count > 0;
}

/// <summary>
/// Lists the models a provider exposes, for the TUI's <c>/model</c> command. Implemented only by
/// providers that can enumerate models (e.g. Copilot); others have no uniform catalog.
/// </summary>
public interface IModelCatalog
{
    Task<IReadOnlyList<ModelOption>> ListAsync(CancellationToken ct = default);
}
