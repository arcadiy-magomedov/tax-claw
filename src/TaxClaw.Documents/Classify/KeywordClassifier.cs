using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Classify;

/// <summary>Scores each document type by counting signal terms; ties broken by first definition.</summary>
public sealed class KeywordClassifier : IDocumentClassifier
{
    private static readonly (DocumentType Type, string[] Terms)[] Signals =
    [
        (DocumentType.RsuVestingStatement, ["rsu", "vesting", "vested", "fmv", "restricted stock"]),
        (DocumentType.DividendStatement, ["dividend", "withholding"]),
        (DocumentType.EmploymentIncomeStatement, ["zdanitelných příjmech", "závislé činnosti", "employment income"]),
        (DocumentType.BrokerageTradeConfirmation, ["trade confirmation", "sell", "buy", "settlement"])
    ];

    public Task<Classification> ClassifyAsync(ExtractedText text, CancellationToken ct = default)
    {
        string haystack = text.Text.ToLowerInvariant();

        DocumentType best = DocumentType.Unknown;
        int bestHits = 0;

        foreach ((DocumentType type, string[] terms) in Signals)
        {
            int hits = terms.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (hits > bestHits)
            {
                bestHits = hits;
                best = type;
            }
        }

        double confidence = bestHits == 0 ? 0.0 : System.Math.Min(1.0, 0.5 + 0.25 * bestHits);
        return Task.FromResult(new Classification(best, confidence));
    }
}
