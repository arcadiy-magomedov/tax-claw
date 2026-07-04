using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TaxClaw.Llm;

namespace TaxClaw.Llm.Tests;

public class RedactionWiringTests
{
    private const string Iban = "CZ6508000000192000145399";

    [Fact]
    public async Task Cloud_agent_redacts_pii_before_it_reaches_the_model()
    {
        var capture = new CapturingChatClient();
        AIAgent agent = AgentFactory.WithRedaction(
            capture.AsAIAgent(), "openai", redactPii: true);

        await RunAsync(agent, $"Send refund to {Iban}");

        Assert.DoesNotContain(Iban, capture.SeenText);
    }

    [Fact]
    public async Task Cloud_agent_restores_pii_in_the_response()
    {
        var capture = new CapturingChatClient();
        AIAgent agent = AgentFactory.WithRedaction(
            capture.AsAIAgent(), "openai", redactPii: true);

        AgentResponse response = await RunAsync(agent, $"Send refund to {Iban}");

        // The client echoes the (tokenized) user text; after middleware restore the original is back.
        Assert.Contains(Iban, response.Text);
    }

    [Fact]
    public async Task Local_agent_is_not_redacted()
    {
        var capture = new CapturingChatClient();
        AIAgent agent = AgentFactory.WithRedaction(
            capture.AsAIAgent(), "ollama", redactPii: true);

        await RunAsync(agent, $"Send refund to {Iban}");

        Assert.Contains(Iban, capture.SeenText);
    }

    [Fact]
    public async Task Cloud_agent_is_not_redacted_when_disabled()
    {
        var capture = new CapturingChatClient();
        AIAgent agent = AgentFactory.WithRedaction(
            capture.AsAIAgent(), "openai", redactPii: false);

        await RunAsync(agent, $"Send refund to {Iban}");

        Assert.Contains(Iban, capture.SeenText);
    }

    private static async Task<AgentResponse> RunAsync(AIAgent agent, string message)
    {
        AgentSession session = await agent.CreateSessionAsync();
        return await agent.RunAsync(message, session);
    }

    /// <summary>Records the text the model actually received and echoes it back as the reply.</summary>
    private sealed class CapturingChatClient : IChatClient
    {
        public string SeenText { get; private set; } = "";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            SeenText = string.Join(" ", messages.Select(m => m.Text));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, SeenText)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            SeenText = string.Join(" ", messages.Select(m => m.Text));
            yield return new ChatResponseUpdate(ChatRole.Assistant, SeenText);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
