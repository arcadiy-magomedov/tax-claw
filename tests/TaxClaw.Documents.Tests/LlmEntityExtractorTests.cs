using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class LlmEntityExtractorTests
{
    private static readonly EntitySchema DividendSchema = DocumentSchemas.For(DocumentType.DividendStatement);

    private static async Task<ExtractionResult> Extract(string modelReply) =>
        await new LlmEntityExtractor(new CannedChatClient(modelReply))
            .ExtractAsync(new ExtractedText("ignored", false), DividendSchema);

    [Fact]
    public async Task Parses_a_clean_json_object()
    {
        ExtractionResult r = await Extract(
            """{"issuer":"Microsoft","pay_date":"2027-03-10","gross_amount":"100.00","currency":"USD","withholding_tax":"15.00"}""");

        Assert.Equal("Microsoft", r.Get("issuer"));
        Assert.Equal("USD", r.Get("currency"));
    }

    [Fact]
    public async Task Handles_fenced_json_and_numeric_values()
    {
        ExtractionResult r = await Extract(
            "Here you go:\n```json\n{\"issuer\":\"MSFT\",\"gross_amount\":100.0}\n```");

        Assert.Equal("MSFT", r.Get("issuer"));
        Assert.Equal("100.0", r.Get("gross_amount")); // JSON number coerced to string
    }

    [Fact]
    public async Task Keeps_only_schema_keys_and_drops_nulls()
    {
        ExtractionResult r = await Extract(
            """{"issuer":"MSFT","hacker":"rm -rf","currency":null}""");

        Assert.Equal("MSFT", r.Get("issuer"));
        Assert.False(r.Fields.ContainsKey("hacker")); // non-schema key dropped (injection guard)
        Assert.Null(r.Get("currency"));               // null dropped
    }

    [Fact]
    public async Task Non_json_reply_yields_empty_result()
    {
        ExtractionResult r = await Extract("I couldn't read the document.");
        Assert.Empty(r.Fields);
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
