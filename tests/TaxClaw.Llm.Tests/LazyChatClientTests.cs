using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Llm;

namespace TaxClaw.Llm.Tests;

public class LazyChatClientTests
{
    [Fact]
    public async Task Does_not_create_the_inner_client_until_first_use()
    {
        int created = 0;
        var lazy = new LazyChatClient(() => { created++; return new StubChatClient(); });

        Assert.Equal(0, created);

        await lazy.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);
        Assert.Equal(1, created);

        await lazy.GetResponseAsync([new ChatMessage(ChatRole.User, "again")]);
        Assert.Equal(1, created); // cached, not recreated
    }

    [Fact]
    public void GetService_does_not_force_creation()
    {
        int created = 0;
        var lazy = new LazyChatClient(() => { created++; return new StubChatClient(); });

        Assert.Null(lazy.GetService(typeof(object)));
        Assert.Equal(0, created);
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
