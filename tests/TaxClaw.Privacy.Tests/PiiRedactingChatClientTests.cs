using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Privacy;

namespace TaxClaw.Privacy.Tests;

public class PiiRedactingChatClientTests
{
    [Fact]
    public async Task Outbound_pii_is_tokenized_before_reaching_the_inner_client()
    {
        var inner = new CapturingChatClient("ok");
        var client = new PiiRedactingChatClient(inner, new RegexPiiDetector());

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Refund to CZ6508000000192000145399 for 900101/1234.")]);

        Assert.DoesNotContain("CZ6508000000192000145399", inner.LastUserText);
        Assert.DoesNotContain("900101/1234", inner.LastUserText);
    }

    [Fact]
    public async Task Inbound_token_is_restored_to_the_original_value()
    {
        var inner = new EchoTokenChatClient();
        var client = new PiiRedactingChatClient(inner, new RegexPiiDetector());

        ChatResponse response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Confirm CZ6508000000192000145399")]);

        Assert.Contains("CZ6508000000192000145399", response.Text);
    }

    private sealed class CapturingChatClient(string reply) : IChatClient
    {
        public string? LastUserText { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastUserText = messages.Last(m => m.Role == ChatRole.User).Text;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastUserText = messages.Last(m => m.Role == ChatRole.User).Text;
            yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class EchoTokenChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            string text = messages.Last(m => m.Role == ChatRole.User).Text;
            string token = text.Contains("[[IBAN_1]]") ? "[[IBAN_1]]" : "(none)";
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"Confirmed {token}")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Confirmed [[IBAN_1]]");
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
