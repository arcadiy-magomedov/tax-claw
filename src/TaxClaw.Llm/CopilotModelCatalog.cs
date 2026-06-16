using GitHub.Copilot;

namespace TaxClaw.Llm;

/// <summary>
/// Lists GitHub Copilot models via the official SDK. Spins up a short-lived Copilot runtime to
/// enumerate models, then tears it down — used on demand by the TUI's <c>/model</c> command.
/// </summary>
public sealed class CopilotModelCatalog(string? gitHubToken = null) : IModelCatalog
{
    public async Task<IReadOnlyList<ModelOption>> ListAsync(CancellationToken ct = default)
    {
        var options = new CopilotClientOptions();
        if (gitHubToken is not null)
        {
            options.GitHubToken = gitHubToken;
        }

        await using var client = new CopilotClient(options);
        await client.StartAsync(ct);

        var models = await client.ListModelsAsync(ct);
        return models.Select(m => new ModelOption(m.Id, m.Name)).ToList();
    }
}
