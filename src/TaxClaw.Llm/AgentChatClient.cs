using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace TaxClaw.Llm;

/// <summary>
/// Exposes an <see cref="AIAgent"/> as a single-shot <see cref="IChatClient"/>. Each call runs the
/// agent in a fresh session (stateless completion), which is exactly what the document
/// classify/extract adapters need. This lets GitHub Copilot — which has no native
/// <see cref="IChatClient"/> — back those adapters by reusing the already-verified Copilot agent
/// path (session config, tool-permission handling) instead of a second hand-rolled RPC client.
/// </summary>
public sealed class AgentChatClient(AIAgent agent) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        AgentResponse response = await agent.RunAsync(messages, session, cancellationToken: cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, response.Text));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(
            messages, session, cancellationToken: cancellationToken))
        {
            yield return new ChatResponseUpdate(update.Role ?? ChatRole.Assistant, update.Text);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => (agent as IDisposable)?.Dispose();
}
