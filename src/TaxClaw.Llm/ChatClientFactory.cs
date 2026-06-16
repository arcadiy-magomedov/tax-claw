using System.ClientModel;
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

        _ => throw new NotSupportedException($"Unknown LLM provider '{options.Provider}'.")
    };

    private string RequireApiKey() =>
        string.IsNullOrWhiteSpace(options.ApiKey)
            ? throw new ArgumentException($"Provider '{options.Provider}' requires an API key.")
            : options.ApiKey;

    private string RequireEndpoint() =>
        string.IsNullOrWhiteSpace(options.Endpoint)
            ? throw new ArgumentException($"Provider '{options.Provider}' requires an endpoint.")
            : options.Endpoint;
}
