using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Agent;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class TaxClawAgentTests
{
    [Fact]
    public async Task Send_returns_assistant_text_and_forwards_tools()
    {
        var fake = new FakeChatClient(new ChatResponse(
            new ChatMessage(ChatRole.Assistant, "Hello, I can help with your declaration.")));

        var agent = new TaxClawAgent(fake, Prompts.System, MathTools.CreateTools());

        string reply = await agent.SendAsync("hi");

        Assert.Equal("Hello, I can help with your declaration.", reply);
        Assert.NotNull(fake.LastOptions);
        var toolNames = fake.LastOptions!.Tools!.OfType<AIFunction>().Select(t => t.Name);
        Assert.Contains("add", toolNames);
    }

    [Fact]
    public void System_prompt_states_the_no_float_guardrail()
    {
        Assert.Contains("never", Prompts.System, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tool", Prompts.System, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeChatClient(ChatResponse response) : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            foreach (var update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
