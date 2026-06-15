using Microsoft.Extensions.AI;

namespace TaxClaw.Agent;

/// <summary>
/// A thin, multi-turn chat agent over <see cref="IChatClient"/> with automatic function
/// invocation. Kept intentionally small; the richer Microsoft Agent Framework agent (memory,
/// MCP, middleware) replaces this in a later plan via the same <see cref="IChatClient"/> seam.
/// </summary>
public sealed class TaxClawAgent
{
    private readonly IChatClient _client;
    private readonly List<ChatMessage> _history;
    private readonly ChatOptions _options;

    public TaxClawAgent(IChatClient baseClient, string systemPrompt, IList<AITool> tools)
    {
        _client = baseClient.AsBuilder().UseFunctionInvocation().Build();
        _history = [new ChatMessage(ChatRole.System, systemPrompt)];
        _options = new ChatOptions { Tools = tools };
    }

    public async Task<string> SendAsync(string userMessage, CancellationToken ct = default)
    {
        _history.Add(new ChatMessage(ChatRole.User, userMessage));
        ChatResponse response = await _client.GetResponseAsync(_history, _options, ct);
        _history.AddRange(response.Messages);
        return response.Text;
    }
}
