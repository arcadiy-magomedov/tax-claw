using TaxClaw.Core.Model;
using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Map;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class DocumentMapperTests
{
    [Fact]
    public void Maps_a_dividend_extraction_to_an_income_item()
    {
        var extraction = new ExtractionResult(DocumentType.DividendStatement, new Dictionary<string, string>
        {
            ["issuer"] = "Microsoft",
            ["pay_date"] = "2027-03-10",
            ["gross_amount"] = "100.00",
            ["currency"] = "USD",
            ["withholding_tax"] = "15.00"
        });

        var ret = new TaxReturn(TaxYear.Of(2027));
        TaxReturn updated = new DocumentMapper().Apply(ret, extraction, documentId: "doc-1");

        IncomeItem item = Assert.Single(updated.Incomes);
        Assert.Equal("dividend", item.Kind);
        Assert.Equal(new Money(100.00m, "USD"), item.Amount);
        Assert.Equal(new DateOnly(2027, 3, 10), item.Date);
        Assert.Equal("doc-1", item.DocumentId);
    }

    [Fact]
    public void Maps_a_sell_trade_to_securities_sale_proceeds()
    {
        var extraction = new ExtractionResult(DocumentType.BrokerageTradeConfirmation, new Dictionary<string, string>
        {
            ["trade_date"] = "2027-06-01",
            ["side"] = "SELL",
            ["shares"] = "10",
            ["price_per_share"] = "250.00",
            ["currency"] = "USD"
        });

        TaxReturn updated = new DocumentMapper().Apply(new TaxReturn(TaxYear.Of(2027)), extraction, "doc-s");

        IncomeItem item = Assert.Single(updated.Incomes);
        Assert.Equal("securities_sale", item.Kind);
        Assert.Equal(new Money(2500.00m, "USD"), item.Amount); // 10 × 250 proceeds
        Assert.Equal(new DateOnly(2027, 6, 1), item.Date);
    }

    [Fact]
    public void A_buy_trade_is_not_income()
    {
        var extraction = new ExtractionResult(DocumentType.BrokerageTradeConfirmation, new Dictionary<string, string>
        {
            ["trade_date"] = "2027-06-01",
            ["side"] = "BUY",
            ["shares"] = "10",
            ["price_per_share"] = "250.00",
            ["currency"] = "USD"
        });

        TaxReturn updated = new DocumentMapper().Apply(new TaxReturn(TaxYear.Of(2027)), extraction, "doc-b");

        Assert.Empty(updated.Incomes);
    }

    [Fact]
    public void Unknown_type_is_a_no_op()
    {
        var extraction = new ExtractionResult(DocumentType.Unknown, new Dictionary<string, string>());
        var ret = new TaxReturn(TaxYear.Of(2027));

        TaxReturn updated = new DocumentMapper().Apply(ret, extraction, "doc-x");

        Assert.Empty(updated.Incomes);
    }
}
