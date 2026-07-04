using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace TaxClaw.Privacy;

/// <summary>
/// A chat-client middleware that pseudonymizes PII in outgoing messages and restores it in the
/// response. Wrap a cloud provider's client with this; for a local provider, skip it entirely.
/// Each call uses a fresh <see cref="PseudonymMap"/> so tokens never collide across conversations.
/// </summary>
public sealed class PiiRedactingChatClient(IChatClient inner, IPiiDetector detector) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var map = new PseudonymMap();
        IList<ChatMessage> redacted = MessageRedaction.Redact(messages, detector, map);

        ChatResponse response = await inner.GetResponseAsync(redacted, options, cancellationToken);

        MessageRedaction.Restore(response.Messages, map);
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var map = new PseudonymMap();
        IList<ChatMessage> redacted = MessageRedaction.Redact(messages, detector, map);

        await foreach (ChatResponseUpdate update in inner.GetStreamingResponseAsync(redacted, options, cancellationToken))
        {
            yield return new ChatResponseUpdate(update.Role ?? ChatRole.Assistant, map.Restore(update.Text));
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();
}
