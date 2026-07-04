using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class FallbackCompositesTests
{
    private static readonly ExtractedText AnyText = new("irrelevant", UsedRecognition: false);

    private sealed class FakeClassifier(Classification result) : IDocumentClassifier
    {
        public bool Called { get; private set; }
        public Task<Classification> ClassifyAsync(ExtractedText text, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeExtractor(ExtractionResult result) : IEntityExtractor
    {
        public bool Called { get; private set; }
        public Task<ExtractionResult> ExtractAsync(ExtractedText text, EntitySchema schema, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task Classifier_trusts_confident_primary_and_skips_the_llm()
    {
        var primary = new FakeClassifier(new Classification(DocumentType.RsuVestingStatement, 0.8));
        var fallback = new FakeClassifier(new Classification(DocumentType.DividendStatement, 0.99));

        Classification result = await new FallbackClassifier(primary, fallback).ClassifyAsync(AnyText);

        Assert.Equal(DocumentType.RsuVestingStatement, result.Type);
        Assert.False(fallback.Called);
    }

    [Fact]
    public async Task Classifier_consults_llm_when_primary_is_unknown()
    {
        var primary = new FakeClassifier(new Classification(DocumentType.Unknown, 0.0));
        var fallback = new FakeClassifier(new Classification(DocumentType.DividendStatement, 0.9));

        Classification result = await new FallbackClassifier(primary, fallback).ClassifyAsync(AnyText);

        Assert.Equal(DocumentType.DividendStatement, result.Type);
        Assert.True(fallback.Called);
    }

    [Fact]
    public async Task Classifier_keeps_primary_when_it_is_still_more_confident()
    {
        var primary = new FakeClassifier(new Classification(DocumentType.DividendStatement, 0.4)); // below threshold
        var fallback = new FakeClassifier(new Classification(DocumentType.Unknown, 0.0));

        Classification result = await new FallbackClassifier(primary, fallback).ClassifyAsync(AnyText);

        Assert.Equal(DocumentType.DividendStatement, result.Type);
        Assert.True(fallback.Called); // consulted, but did not win
    }

    private static EntitySchema TwoFieldSchema() => new(
        DocumentType.DividendStatement,
        [new EntityField("issuer", Required: true, "who paid"),
         new EntityField("gross_amount", Required: true, "gross")]);

    [Fact]
    public async Task Extractor_returns_deterministic_result_and_skips_the_llm_when_valid()
    {
        EntitySchema schema = TwoFieldSchema();
        var primary = new FakeExtractor(new ExtractionResult(schema.Type,
            new Dictionary<string, string> { ["issuer"] = "Microsoft", ["gross_amount"] = "100.00" }));
        var fallback = new FakeExtractor(new ExtractionResult(schema.Type, new Dictionary<string, string>()));

        ExtractionResult result = await new FallbackEntityExtractor(primary, fallback).ExtractAsync(AnyText, schema);

        Assert.Equal("100.00", result.Get("gross_amount"));
        Assert.False(fallback.Called);
    }

    [Fact]
    public async Task Extractor_fills_gaps_from_llm_but_keeps_deterministic_values()
    {
        EntitySchema schema = TwoFieldSchema();
        var primary = new FakeExtractor(new ExtractionResult(schema.Type,
            new Dictionary<string, string> { ["issuer"] = "Microsoft" })); // missing gross_amount
        var fallback = new FakeExtractor(new ExtractionResult(schema.Type,
            new Dictionary<string, string> { ["issuer"] = "WRONG", ["gross_amount"] = "100.00" }));

        ExtractionResult result = await new FallbackEntityExtractor(primary, fallback).ExtractAsync(AnyText, schema);

        Assert.True(fallback.Called);
        Assert.Equal("Microsoft", result.Get("issuer"));      // deterministic value wins
        Assert.Equal("100.00", result.Get("gross_amount"));   // gap filled by llm
    }
}
