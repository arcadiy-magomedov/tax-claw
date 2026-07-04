using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Llm;
using TaxClaw.Privacy;

namespace TaxClaw.Llm.Tests;

public class RedactionWiringTests
{
    [Fact]
    public void Local_provider_is_not_wrapped()
    {
        IChatClient client = AgentFactory.ApplyPrivacy(new NoopChatClient(), "ollama", redactPii: true);
        Assert.IsNotType<PiiRedactingChatClient>(client);
    }

    [Fact]
    public void Cloud_provider_is_wrapped_when_redaction_is_enabled()
    {
        IChatClient client = AgentFactory.ApplyPrivacy(new NoopChatClient(), "openai", redactPii: true);
        Assert.IsType<PiiRedactingChatClient>(client);
    }

    [Fact]
    public void Cloud_provider_is_not_wrapped_when_redaction_is_disabled()
    {
        IChatClient client = AgentFactory.ApplyPrivacy(new NoopChatClient(), "openai", redactPii: false);
        Assert.IsNotType<PiiRedactingChatClient>(client);
    }

    private sealed class NoopChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
