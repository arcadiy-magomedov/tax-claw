using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace TaxClaw.Llm;

/// <summary>Builds the provider-agnostic <see cref="AIAgent"/> and, where available, a model catalog.</summary>
public interface IAgentFactory
{
    /// <summary>Creates an agent seeded with the system prompt and the supplied tools.</summary>
    AIAgent CreateAgent(string instructions, IList<AITool> tools);

    /// <summary>
    /// Returns a plain single-shot <see cref="IChatClient"/> for the configured provider, used by
    /// the document classify/extract/recognize adapters (not the chat agent). Cloud providers are
    /// PII-redacted; Copilot is backed by a bare agent via <see cref="AgentChatClient"/>.
    /// </summary>
    IChatClient CreateChatClient();

    /// <summary>Returns a model catalog for providers that can enumerate models, else null.</summary>
    IModelCatalog? CreateCatalog();
}
