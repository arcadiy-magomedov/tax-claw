using TaxClaw.Documents.Classify;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class KeywordClassifierTests
{
    private readonly KeywordClassifier _classifier = new();

    [Theory]
    [InlineData("RSU vesting: 100 shares vested, FMV 50 USD", DocumentType.RsuVestingStatement)]
    [InlineData("Dividend payment, withholding tax 15%", DocumentType.DividendStatement)]
    [InlineData("Potvrzení o zdanitelných příjmech ze závislé činnosti", DocumentType.EmploymentIncomeStatement)]
    [InlineData("Trade confirmation: SELL 10 shares", DocumentType.BrokerageTradeConfirmation)]
    public async Task Classifies_by_signal_terms(string text, DocumentType expected)
    {
        var result = await _classifier.ClassifyAsync(new ExtractedText(text, false));
        Assert.Equal(expected, result.Type);
    }

    [Fact]
    public async Task Unrecognized_text_is_low_confidence_unknown()
    {
        var result = await _classifier.ClassifyAsync(new ExtractedText("grocery receipt for milk", false));
        Assert.Equal(DocumentType.Unknown, result.Type);
        Assert.True(result.Confidence < 0.5);
    }
}
