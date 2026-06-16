using System.ClientModel;
using System.Diagnostics;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace TaxClaw.Llm;

/// <summary>
/// Builds an <see cref="IChatClient"/> for the configured provider. This is the single seam
/// the rest of the app depends on, keeping every provider behind one stable abstraction.
/// </summary>
public sealed class ChatClientFactory(LlmOptions options) : IChatClientFactory
{
    public IChatClient Create() => options.Provider.ToLowerInvariant() switch
    {
        "ollama" => new OllamaApiClient(
            new Uri(options.Endpoint ?? "http://localhost:11434"),
            options.Model),

        "openai" => new OpenAIClient(RequireApiKey())
            .GetChatClient(options.Model)
            .AsIChatClient(),

        "azure" => new AzureOpenAIClient(
                new Uri(RequireEndpoint()),
                new ApiKeyCredential(RequireApiKey()))
            .GetChatClient(options.Model)
            .AsIChatClient(),

        // Routes to GitHub Copilot models (e.g. "claude-opus-4.8") via the official Copilot SDK.
        "copilot" => new CopilotChatClient(options.Model, ResolveCopilotToken()),

        _ => throw new NotSupportedException($"Unknown LLM provider '{options.Provider}'.")
    };

    /// <summary>
    /// Resolves a GitHub token for Copilot: explicit ApiKey, then the GITHUB_COPILOT_TOKEN env var,
    /// then `gh auth token`. Returns null to let the SDK fall back to the logged-in Copilot user.
    /// </summary>
    private string? ResolveCopilotToken()
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        string? fromEnv = Environment.GetEnvironmentVariable("GITHUB_COPILOT_TOKEN");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        return TryGetGitHubCliToken();
    }

    private static string? TryGetGitHubCliToken()
    {
        try
        {
            var psi = new ProcessStartInfo("gh", "auth token")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private string RequireApiKey() =>
        string.IsNullOrWhiteSpace(options.ApiKey)
            ? throw new ArgumentException($"Provider '{options.Provider}' requires an API key.")
            : options.ApiKey;

    private string RequireEndpoint() =>
        string.IsNullOrWhiteSpace(options.Endpoint)
            ? throw new ArgumentException($"Provider '{options.Provider}' requires an endpoint.")
            : options.Endpoint;
}
