using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TaxClaw.Agent;

namespace TaxClaw.Agent.Tests;

public class AgentContextTests
{
    [Fact]
    public async Task Turn_context_reaches_the_underlying_client()
    {
        var fake = new CapturingChatClient();
        await using var agent = new TaxClawAgent(
            fake.AsAIAgent(instructions: Prompts.System, tools: MathTools.CreateTools()));

        await agent.SendAsync("hi", turnContext: "Remembered: reply in Czech.");

        Assert.Contains(fake.LastMessages, m => m.Text.Contains("reply in Czech"));
    }

    [Fact]
    public async Task Without_turn_context_the_message_is_sent_as_is()
    {
        var fake = new CapturingChatClient();
        await using var agent = new TaxClawAgent(
            fake.AsAIAgent(instructions: Prompts.System, tools: MathTools.CreateTools()));

        string reply = await agent.SendAsync("hello");

        Assert.Equal("ok", reply);
        Assert.Contains(fake.LastMessages, m => m.Text.Contains("hello"));
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public IList<ChatMessage> LastMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
