namespace TaxClaw.Llm;

/// <summary>A selectable model: its provider-specific id and a human-friendly display name.</summary>
public readonly record struct ModelOption(string Id, string Name);

/// <summary>
/// Lists the models a provider exposes, for the TUI's <c>/model</c> command. Implemented only by
/// providers that can enumerate models (e.g. Copilot); others have no uniform catalog.
/// </summary>
public interface IModelCatalog
{
    Task<IReadOnlyList<ModelOption>> ListAsync(CancellationToken ct = default);
}
