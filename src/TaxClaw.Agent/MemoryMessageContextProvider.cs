using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TaxClaw.Memory;

namespace TaxClaw.Agent;

/// <summary>
/// MAF-native memory injection. Before each agent run it surfaces the remembered context (facts,
/// preferences, and user corrections scoped to the active project) as a system message. Attaching it
/// through <c>AIAgentBuilder.UseAIContextProviders</c> means it enriches <b>every</b> provider — including
/// GitHub Copilot, which is not an <see cref="IChatClient"/> — uniformly, and decouples memory from the
/// TUI loop (the agent pulls memory itself rather than the caller stitching it into each message).
/// </summary>
public sealed class MemoryMessageContextProvider(
    MemoryContextProvider memory, Func<string?> activeProjectId) : MessageAIContextProvider
{
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context, CancellationToken cancellationToken = default)
    {
        string remembered = await memory.BuildContextAsync(activeProjectId(), documentType: null, cancellationToken);
        return string.IsNullOrWhiteSpace(remembered)
            ? []
            : [new ChatMessage(ChatRole.System, remembered)];
    }
}
