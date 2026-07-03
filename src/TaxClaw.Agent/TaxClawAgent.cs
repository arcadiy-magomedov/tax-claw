using System.Text.Json;
using Microsoft.Agents.AI;

namespace TaxClaw.Agent;

/// <summary>
/// A thin, multi-turn chat agent over a Microsoft Agent Framework <see cref="AIAgent"/>. The agent
/// drives tool (function) invocation itself; tax-claw keeps this wrapper small and owns only the
/// conversation <see cref="AgentSession"/> and model-swap semantics.
/// </summary>
public sealed class TaxClawAgent : IAsyncDisposable
{
    private AIAgent _agent;
    private AgentSession? _session;

    public TaxClawAgent(AIAgent agent) => _agent = agent;

    public async Task<string> SendAsync(string userMessage, CancellationToken ct = default)
    {
        _session ??= await _agent.CreateSessionAsync(ct);
        AgentResponse response = await _agent.RunAsync(userMessage, _session, cancellationToken: ct);
        return response.Text;
    }

    /// <summary>
    /// Swaps the underlying agent (e.g. to change model) while preserving the conversation via MAF
    /// session serialization. If the serialized state is not portable to the new agent (e.g. a
    /// cross-provider switch), the conversation restarts with a fresh session. The previous agent is
    /// disposed so its resources — including any spawned runtime process — are released.
    /// </summary>
    public async Task UseAgentAsync(AIAgent newAgent, CancellationToken ct = default)
    {
        if (_session is not null)
        {
            try
            {
                JsonElement state = await _agent.SerializeSessionAsync(_session, cancellationToken: ct);
                _session = await newAgent.DeserializeSessionAsync(state, cancellationToken: ct);
            }
            catch
            {
                _session = null;
            }
        }

        AIAgent previous = _agent;
        _agent = newAgent;
        await DisposeAgentAsync(previous);
    }

    public ValueTask DisposeAsync() => DisposeAgentAsync(_agent);

    private static async ValueTask DisposeAgentAsync(AIAgent agent)
    {
        switch (agent)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
