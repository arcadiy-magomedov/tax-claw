using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Entities;

/// <summary>The per-document-type extraction schemas (what entities to pull).</summary>
public static class DocumentSchemas
{
    public static EntitySchema For(DocumentType type) => type switch
    {
        DocumentType.RsuVestingStatement => new(type,
        [
            new("vest_date", true, "Date shares vested (yyyy-MM-dd)"),
            new("shares", true, "Number of shares vested"),
            new("fmv_per_share", true, "Fair market value per share at vest"),
            new("currency", true, "Currency of FMV, e.g. USD"),
            new("tax_withheld", false, "Tax already withheld, if any")
        ]),
        DocumentType.DividendStatement => new(type,
        [
            new("issuer", true, "Dividend-paying entity"),
            new("pay_date", true, "Payment date (yyyy-MM-dd)"),
            new("gross_amount", true, "Gross dividend amount"),
            new("currency", true, "Currency, e.g. USD"),
            new("withholding_tax", true, "Tax withheld at source")
        ]),
        DocumentType.EmploymentIncomeStatement => new(type,
        [
            new("gross_income", true, "Gross employment income"),
            new("tax_advances", true, "Income tax advances withheld"),
            new("currency", true, "Currency, e.g. CZK")
        ]),
        DocumentType.BrokerageTradeConfirmation => new(type,
        [
            new("trade_date", true, "Trade date (yyyy-MM-dd)"),
            new("side", true, "BUY or SELL"),
            new("shares", true, "Number of shares"),
            new("price_per_share", true, "Price per share"),
            new("currency", true, "Currency, e.g. USD")
        ]),
        _ => new(DocumentType.Unknown, [])
    };
}
