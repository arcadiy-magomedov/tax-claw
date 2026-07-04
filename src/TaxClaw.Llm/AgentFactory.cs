using System.ClientModel;
using System.Diagnostics;
using Azure.AI.OpenAI;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using TaxClaw.Privacy;

namespace TaxClaw.Llm;

/// <summary>
/// Builds a Microsoft Agent Framework <see cref="AIAgent"/> for the configured provider. This is the
/// single seam the rest of the app depends on, keeping every provider behind one stable abstraction.
/// </summary>
/// <remarks>
/// For chat-completion providers (ollama/openai/azure) the agent is <c>chatClient.AsAIAgent(...)</c>,
/// which wraps the <see cref="IChatClient"/> and drives function invocation. For GitHub Copilot the
/// agent is built with the official <c>Microsoft.Agents.AI.GitHub.Copilot</c> provider, so the
/// decimal-math tools actually fire on the default provider — the previous hand-rolled adapter never
/// bridged tools, which silently disabled the "no mental float math" guardrail.
/// </remarks>
public sealed class AgentFactory(LlmOptions options) : IAgentFactory
{
    public AIAgent CreateAgent(string instructions, IList<AITool> tools) =>
        options.Provider.ToLowerInvariant() switch
        {
            "ollama" => ApplyPrivacy(
                    new OllamaApiClient(new Uri(options.Endpoint ?? "http://localhost:11434"), options.Model),
                    "ollama", options.RedactPii)
                .AsAIAgent(instructions: instructions, tools: tools),

            "openai" => ApplyPrivacy(
                    new OpenAIClient(RequireApiKey()).GetChatClient(options.Model).AsIChatClient(),
                    "openai", options.RedactPii)
                .AsAIAgent(instructions: instructions, tools: tools),

            "azure" => ApplyPrivacy(
                    new AzureOpenAIClient(new Uri(RequireEndpoint()), new ApiKeyCredential(RequireApiKey()))
                        .GetChatClient(options.Model).AsIChatClient(),
                    "azure", options.RedactPii)
                .AsAIAgent(instructions: instructions, tools: tools),

            "copilot" => CreateCopilotAgent(instructions, tools),

            _ => throw new NotSupportedException($"Unknown LLM provider '{options.Provider}'.")
        };

    /// <summary>
    /// Wraps a cloud provider's client with PII redaction; local providers (ollama) are never
    /// wrapped. NOTE: the GitHub Copilot path goes through the MAF provider (not an
    /// <see cref="IChatClient"/>), so redaction is not applied there yet — that needs a MAF
    /// middleware and is a documented follow-up.
    /// </summary>
    public static IChatClient ApplyPrivacy(IChatClient client, string provider, bool redactPii)
    {
        bool isLocal = string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase);
        return redactPii && !isLocal
            ? new PiiRedactingChatClient(client, new RegexPiiDetector())
            : client;
    }

    /// <summary>Returns a model catalog for providers that can enumerate models, else null.</summary>
    public IModelCatalog? CreateCatalog() => options.Provider.ToLowerInvariant() switch
    {
        "copilot" => new CopilotModelCatalog(ResolveCopilotToken()),
        _ => null
    };

    /// <summary>
    /// Builds the GitHub Copilot agent. The session is configured with the target model, the
    /// system prompt, and tax-claw's tools (as function declarations the SDK invokes in-process).
    /// Built-in Copilot CLI tools (shell, file, etc.) are denied; only our tools run.
    /// </summary>
    private AIAgent CreateCopilotAgent(string instructions, IList<AITool> tools)
    {
        var clientOptions = new CopilotClientOptions();
        string? token = ResolveCopilotToken();
        if (token is not null)
        {
            clientOptions.GitHubToken = token;
        }

        var config = new SessionConfig
        {
            Model = options.Model,
            Tools = tools.OfType<AIFunctionDeclaration>().ToList(),
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = instructions
            },
            OnPermissionRequest = ApproveOwnToolsOnly(tools)
        };
        if (!string.IsNullOrWhiteSpace(options.ReasoningEffort))
        {
            config.ReasoningEffort = options.ReasoningEffort;
        }

        return new CopilotClient(clientOptions).AsAIAgent(config, ownsClient: true);
    }

    // The Copilot runtime routes every tool call — including tax-claw's own function tools — through
    // OnPermissionRequest. We approve exactly our declared tools (they arrive as "custom-tool"
    // requests) and reject everything else, so the model can run our decimal-math tools while the
    // built-in Copilot CLI capabilities (shell, file read/write, url, memory) stay disabled.
    // PermissionDecision is [Experimental(GHCP001)]; the diagnostic is suppressed narrowly here.
#pragma warning disable GHCP001
    private static Func<PermissionRequest, PermissionInvocation, Task<PermissionDecision>> ApproveOwnToolsOnly(
        IEnumerable<AITool> tools)
    {
        var ownToolNames = tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        return (request, _) => Task.FromResult(
            request is PermissionRequestCustomTool customTool && ownToolNames.Contains(customTool.ToolName)
                ? PermissionDecision.ApproveOnce()
                : PermissionDecision.Reject("Only tax-claw's own tools are permitted."));
    }
#pragma warning restore GHCP001

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
