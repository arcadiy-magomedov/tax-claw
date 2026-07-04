using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using TaxClaw.Agent;
using TaxClaw.Memory;

namespace TaxClaw.Agent.Tests;

public class MemoryMessageContextProviderTests
{
    [Fact]
    public async Task Injects_remembered_context_into_every_invocation()
    {
        var store = new FakeMemoryStore(
            new MemoryEntry("1", MemoryKind.Feedback, MemoryScope.Project("2027"), "reply in Czech", DateTimeOffset.UtcNow));
        var provider = new MemoryMessageContextProvider(new MemoryContextProvider(store), () => "2027");

        var capture = new CapturingChatClient();
        AIAgent agent = capture.AsAIAgent()
            .AsBuilder().UseAIContextProviders(provider).Build(new EmptyServiceProvider());

        await agent.RunAsync("hello", await agent.CreateSessionAsync());

        Assert.Contains(capture.LastMessages, m => m.Text.Contains("reply in Czech"));
    }

    [Fact]
    public async Task Adds_nothing_when_there_is_no_memory()
    {
        var provider = new MemoryMessageContextProvider(new MemoryContextProvider(new FakeMemoryStore()), () => "2027");

        var capture = new CapturingChatClient();
        AIAgent agent = capture.AsAIAgent()
            .AsBuilder().UseAIContextProviders(provider).Build(new EmptyServiceProvider());

        await agent.RunAsync("hello", await agent.CreateSessionAsync());

        Assert.DoesNotContain(capture.LastMessages, m => m.Text.Contains("Remembered context"));
    }

    private sealed class FakeMemoryStore(params MemoryEntry[] entries) : IMemoryStore
    {
        public Task AddAsync(MemoryEntry entry, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<MemoryEntry>> QueryAsync(
            string? projectId, string? documentType, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MemoryEntry>>(entries);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
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
