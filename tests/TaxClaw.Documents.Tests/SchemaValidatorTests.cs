using TaxClaw.Documents.Entities;
using TaxClaw.Documents.Model;

namespace TaxClaw.Documents.Tests;

public class SchemaValidatorTests
{
    [Fact]
    public void Validation_passes_when_all_required_fields_present()
    {
        EntitySchema schema = DocumentSchemas.For(DocumentType.DividendStatement);
        var result = new ExtractionResult(DocumentType.DividendStatement, new Dictionary<string, string>
        {
            ["issuer"] = "Microsoft",
            ["pay_date"] = "2027-03-10",
            ["gross_amount"] = "100.00",
            ["currency"] = "USD",
            ["withholding_tax"] = "15.00"
        });

        var report = SchemaValidator.Validate(result, schema);

        Assert.True(report.IsValid);
        Assert.Empty(report.MissingFields);
    }

    [Fact]
    public void Validation_reports_missing_required_fields()
    {
        EntitySchema schema = DocumentSchemas.For(DocumentType.DividendStatement);
        var result = new ExtractionResult(DocumentType.DividendStatement, new Dictionary<string, string>
        {
            ["issuer"] = "Microsoft"
        });

        var report = SchemaValidator.Validate(result, schema);

        Assert.False(report.IsValid);
        Assert.Contains("gross_amount", report.MissingFields);
        Assert.Contains("currency", report.MissingFields);
    }
}
