using System.Globalization;
using TaxClaw.Core.Model;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Map;

/// <summary>
/// Turns a validated extraction into canonical <see cref="IncomeItem"/>s on the return, attaching
/// the source document id for provenance. Parsing is culture-invariant and decimal-based. Amounts
/// keep their source currency and date — FX conversion (USD→CZK at the ČNB rate) is a downstream
/// calc step, not a mapping concern.
/// </summary>
public sealed class DocumentMapper
{
    public TaxReturn Apply(TaxReturn ret, ExtractionResult extraction, string documentId) =>
        extraction.Type switch
        {
            DocumentType.DividendStatement => ret.WithIncome(new IncomeItem(
                "dividend",
                Money(extraction, "gross_amount", "currency"),
                Date(extraction, "pay_date"),
                documentId)),

            DocumentType.RsuVestingStatement => ret.WithIncome(new IncomeItem(
                "rsu_vesting",
                Money(extraction, "fmv_per_share", "currency").Multiply(Decimal(extraction, "shares")),
                Date(extraction, "vest_date"),
                documentId)),

            DocumentType.EmploymentIncomeStatement => ret.WithIncome(new IncomeItem(
                "employment",
                Money(extraction, "gross_income", "currency"),
                new DateOnly(ret.Year.Year, 12, 31),
                documentId)),

            // A SELL is a §10 disposal — record the proceeds; the taxable gain (proceeds − basis)
            // and the exemption time test are computed downstream. A BUY is basis, not income.
            DocumentType.BrokerageTradeConfirmation when IsSell(extraction) => ret.WithIncome(new IncomeItem(
                "securities_sale",
                Money(extraction, "price_per_share", "currency").Multiply(Decimal(extraction, "shares")),
                Date(extraction, "trade_date"),
                documentId)),

            _ => ret
        };

    private static bool IsSell(ExtractionResult e) =>
        string.Equals(e.Get("side")?.Trim(), "SELL", StringComparison.OrdinalIgnoreCase);

    private static Money Money(ExtractionResult e, string amountField, string currencyField) =>
        new(Decimal(e, amountField), e.Get(currencyField) ?? "CZK");

    private static decimal Decimal(ExtractionResult e, string field) =>
        decimal.Parse(e.Get(field) ?? "0", CultureInfo.InvariantCulture);

    private static DateOnly Date(ExtractionResult e, string field) =>
        DateOnly.ParseExact(e.Get(field) ?? "0001-01-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
