using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class LlmDocumentClassifierTests
{
    private static async Task<Classification> Classify(string modelReply) =>
        await new LlmDocumentClassifier(new CannedChatClient(modelReply))
            .ClassifyAsync(new ExtractedText("some document", false));

    [Fact]
    public async Task Parses_type_and_confidence_from_json()
    {
        Classification c = await Classify("""{"type":"DividendStatement","confidence":0.9}""");
        Assert.Equal(DocumentType.DividendStatement, c.Type);
        Assert.Equal(0.9, c.Confidence, 3);
    }

    [Fact]
    public async Task Unrecognized_label_falls_back_to_unknown()
    {
        Classification c = await Classify("""{"type":"GroceryReceipt","confidence":0.9}""");
        Assert.Equal(DocumentType.Unknown, c.Type);
        Assert.True(c.Confidence <= 0.4);
    }

    [Fact]
    public async Task Non_json_reply_is_scanned_for_a_type_name()
    {
        Classification c = await Classify("This is a RsuVestingStatement, clearly.");
        Assert.Equal(DocumentType.RsuVestingStatement, c.Type);
    }

    [Fact]
    public async Task Confidence_is_clamped()
    {
        Classification c = await Classify("""{"type":"DividendStatement","confidence":5}""");
        Assert.Equal(1.0, c.Confidence, 3);
    }

    private sealed class CannedChatClient(string reply) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, reply);
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
