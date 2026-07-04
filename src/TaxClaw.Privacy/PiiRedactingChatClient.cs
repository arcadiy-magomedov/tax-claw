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
        var redacted = messages.Select(m => Redact(m, map)).ToList();

        ChatResponse response = await inner.GetResponseAsync(redacted, options, cancellationToken);

        foreach (ChatMessage message in response.Messages)
        {
            RestoreInPlace(message, map);
        }
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var map = new PseudonymMap();
        var redacted = messages.Select(m => Redact(m, map)).ToList();

        await foreach (ChatResponseUpdate update in inner.GetStreamingResponseAsync(redacted, options, cancellationToken))
        {
            yield return new ChatResponseUpdate(update.Role ?? ChatRole.Assistant, map.Restore(update.Text));
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();

    private ChatMessage Redact(ChatMessage message, PseudonymMap map)
    {
        string redactedText = message.Text;
        foreach (PiiSpan span in detector.Detect(message.Text))
        {
            redactedText = redactedText.Replace(span.Value, map.Tokenize(span.Kind, span.Value));
        }
        return new ChatMessage(message.Role, redactedText);
    }

    private static void RestoreInPlace(ChatMessage message, PseudonymMap map)
    {
        for (int i = 0; i < message.Contents.Count; i++)
        {
            if (message.Contents[i] is TextContent text)
            {
                message.Contents[i] = new TextContent(map.Restore(text.Text));
            }
        }
    }
}
