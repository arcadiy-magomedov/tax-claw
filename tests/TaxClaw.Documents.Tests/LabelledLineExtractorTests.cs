using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class LabelledLineExtractorTests
{
    [Fact]
    public async Task Extracts_fields_from_label_colon_value_lines()
    {
        const string text =
            "issuer: Microsoft\n" +
            "pay_date: 2027-03-10\n" +
            "gross_amount: 100.00\n" +
            "currency: USD\n" +
            "withholding_tax: 15.00";

        var schema = DocumentSchemas.For(DocumentType.DividendStatement);
        var extractor = new LabelledLineExtractor();

        ExtractionResult result = await extractor.ExtractAsync(new ExtractedText(text, false), schema);

        Assert.Equal("Microsoft", result.Get("issuer"));
        Assert.Equal("USD", result.Get("currency"));
    }

    [Fact]
    public async Task Ignores_lines_that_are_not_schema_fields()
    {
        const string text = "ignore me: hacker instructions\nissuer: Microsoft";
        var schema = DocumentSchemas.For(DocumentType.DividendStatement);

        ExtractionResult result = await new LabelledLineExtractor()
            .ExtractAsync(new ExtractedText(text, false), schema);

        Assert.False(result.Fields.ContainsKey("ignore me"));
        Assert.Equal("Microsoft", result.Get("issuer"));
    }
}
