using Microsoft.Extensions.AI;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class VisionRecognizerTests
{
    [Fact]
    public async Task Returns_transcribed_text_flagged_as_recognized()
    {
        var client = new CapturingChatClient("issuer: Microsoft\ngross_amount: 100");
        var recognizer = new VisionRecognizer(client);
        var doc = SourceDocument.FromBytes("scan.png", new byte[] { 1, 2, 3 });

        ExtractedText result = await recognizer.RecognizeAsync(doc);

        Assert.True(result.UsedRecognition);
        Assert.Contains("Microsoft", result.Text);
    }

    [Fact]
    public async Task Sends_the_image_bytes_with_the_right_media_type()
    {
        var client = new CapturingChatClient("text");
        var doc = SourceDocument.FromBytes("scan.png", new byte[] { 9, 8, 7 });

        await new VisionRecognizer(client).RecognizeAsync(doc);

        var contents = client.LastMessages.Single().Contents;
        Assert.Contains(contents, c => c is TextContent);
        DataContent image = Assert.Single(contents.OfType<DataContent>());
        Assert.Equal("image/png", image.MediaType);
    }

    private sealed class CapturingChatClient(string reply) : IChatClient
    {
        public IEnumerable<ChatMessage> LastMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastMessages = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
