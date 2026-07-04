using System.Text;
using TaxClaw.Core.Model;
using TaxClaw.Documents;
using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Extract;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class DocumentPipelineTests
{
    private sealed class PlainTextExtractor : ITextExtractor
    {
        public Task<ExtractedText?> TryExtractAsync(SourceDocument doc, CancellationToken ct = default) =>
            Task.FromResult<ExtractedText?>(new ExtractedText(Encoding.UTF8.GetString(doc.Bytes), false));
    }

    private sealed class ThrowingRecognizer : IRecognizer
    {
        public Task<ExtractedText> RecognizeAsync(SourceDocument doc, CancellationToken ct = default) =>
            throw new InvalidOperationException("should not be called");
    }

    private static DocumentPipeline BuildPipeline() => new(
        new TextLayerDetector(new PlainTextExtractor(), new ThrowingRecognizer()),
        new KeywordClassifier(),
        new LabelledLineExtractor());

    [Fact]
    public async Task Processes_a_dividend_document_end_to_end()
    {
        const string text =
            "Dividend statement\nissuer: Microsoft\npay_date: 2027-03-10\n" +
            "gross_amount: 100.00\ncurrency: USD\nwithholding_tax: 15.00";
        var doc = SourceDocument.FromBytes("div.txt", Encoding.UTF8.GetBytes(text));

        var pipeline = BuildPipeline();
        DocumentResult result = await pipeline.ProcessAsync(doc, new TaxReturn(TaxYear.Of(2027)), "doc-1");

        Assert.Equal(DocumentType.DividendStatement, result.Type);
        Assert.True(result.Validation.IsValid);
        Assert.Single(result.Return.Incomes);
    }

    [Fact]
    public async Task Surfaces_validation_gaps_without_mapping()
    {
        const string text = "Dividend statement\nissuer: Microsoft"; // missing required fields
        var doc = SourceDocument.FromBytes("div.txt", Encoding.UTF8.GetBytes(text));

        DocumentResult result = await BuildPipeline()
            .ProcessAsync(doc, new TaxReturn(TaxYear.Of(2027)), "doc-2");

        Assert.False(result.Validation.IsValid);
        Assert.Contains("gross_amount", result.Validation.MissingFields);
        Assert.Empty(result.Return.Incomes); // not mapped while invalid
    }
}
