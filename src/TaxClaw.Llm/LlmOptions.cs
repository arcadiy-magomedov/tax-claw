namespace TaxClaw.Llm;

/// <summary>Provider-agnostic LLM configuration, bound from the "Llm" config section.</summary>
public sealed class LlmOptions
{
    /// <summary>One of: "copilot", "ollama", "openai", "azure". Defaults to GitHub Copilot.</summary>
    public string Provider { get; set; } = "copilot";

    public string Model { get; set; } = "claude-opus-4.8";

    /// <summary>
    /// Reasoning/thinking effort for models that support it: "low", "medium", "high", "xhigh".
    /// Null uses the model's default. Ignored by models without reasoning effort.
    /// </summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>Required for "azure"; optional override for "ollama" (defaults to localhost).</summary>
    public string? Endpoint { get; set; }

    /// <summary>Required for "openai"/"azure"; optional GitHub token override for "copilot".</summary>
    public string? ApiKey { get; set; }

    /// <summary>When true, wrap cloud providers with PII redaction. Ignored for local providers (ollama).</summary>
    public bool RedactPii { get; set; } = true;
}
