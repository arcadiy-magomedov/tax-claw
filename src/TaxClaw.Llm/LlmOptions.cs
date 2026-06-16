namespace TaxClaw.Llm;

/// <summary>Provider-agnostic LLM configuration, bound from the "Llm" config section.</summary>
public sealed class LlmOptions
{
    /// <summary>One of: "ollama", "openai", "azure".</summary>
    public string Provider { get; set; } = "ollama";

    public string Model { get; set; } = "llama3.1";

    /// <summary>Required for "azure"; optional override for "ollama" (defaults to localhost).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Required for "openai" and "azure".</summary>
    public string? ApiKey { get; set; }
}
