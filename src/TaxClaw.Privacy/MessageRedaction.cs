using Microsoft.Extensions.AI;

namespace TaxClaw.Privacy;

/// <summary>
/// Redacts/restores PII across a list of chat messages using a shared <see cref="PseudonymMap"/>.
/// Reused by both the <see cref="PiiRedactingChatClient"/> (IChatClient layer) and the agent-run
/// redaction middleware (which covers every provider, including ones reached through an agent
/// framework rather than a raw IChatClient).
/// </summary>
public static class MessageRedaction
{
    /// <summary>Returns a copy of the messages with detected PII replaced by tokens from the map.</summary>
    public static IList<ChatMessage> Redact(IEnumerable<ChatMessage> messages, IPiiDetector detector, PseudonymMap map)
    {
        var result = new List<ChatMessage>();
        foreach (ChatMessage message in messages)
        {
            string text = message.Text;
            foreach (PiiSpan span in detector.Detect(text))
            {
                text = text.Replace(span.Value, map.Tokenize(span.Kind, span.Value));
            }
            result.Add(text == message.Text ? message : new ChatMessage(message.Role, text));
        }
        return result;
    }

    /// <summary>Restores tokens back to their original values in-place across the given messages.</summary>
    public static void Restore(IEnumerable<ChatMessage> messages, PseudonymMap map)
    {
        foreach (ChatMessage message in messages)
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
}
