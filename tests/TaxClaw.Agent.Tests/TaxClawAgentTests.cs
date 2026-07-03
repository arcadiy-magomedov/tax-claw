using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TaxClaw.Agent;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class TaxClawAgentTests
{
    [Fact]
    public async Task Send_returns_assistant_text()
    {
        var fake = new ScriptedChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello, I can help with your declaration.")));
        await using var agent = new TaxClawAgent(
            fake.AsAIAgent(instructions: Prompts.System, tools: MathTools.CreateTools()));

        string reply = await agent.SendAsync("hi");

        Assert.Equal("Hello, I can help with your declaration.", reply);
    }

    [Fact]
    public async Task Agent_invokes_the_add_tool_when_the_model_requests_it()
    {
        // Turn 1: the model asks to call add(2, 3). Turn 2: after the framework runs the tool and
        // feeds the result back, the model answers. This proves tool-calling actually fires through
        // the MAF agent — the guardrail the old hand-rolled Copilot adapter silently broke.
        var call = new FunctionCallContent("call-1", "add",
            new Dictionary<string, object?> { ["a"] = 2m, ["b"] = 3m });
        var fake = new ScriptedChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "The sum is 5.")));
        await using var agent = new TaxClawAgent(
            fake.AsAIAgent(instructions: Prompts.System, tools: MathTools.CreateTools()));

        string reply = await agent.SendAsync("add 2 and 3");

        Assert.Equal("The sum is 5.", reply);
        FunctionResultContent result = fake.LastRequest
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        Assert.Equal("call-1", result.CallId);
        Assert.Contains("5", result.Result?.ToString());
    }

    [Fact]
    public void System_prompt_states_the_no_float_guardrail()
    {
        Assert.Contains("never", Prompts.System, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tool", Prompts.System, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A fake chat client that replays a fixed script of responses and records the last request.</summary>
    private sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
    {
        private int _call;

        public IReadOnlyList<ChatMessage> LastRequest { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastRequest = messages.ToList();
            ChatResponse response = responses[System.Math.Min(_call, responses.Length - 1)];
            _call++;
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
            {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
