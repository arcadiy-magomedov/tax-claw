using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using TaxClaw.Export;

namespace TaxClaw.Export.Tests;

public class PdfExporterTests
{
    private static TaxReturn SampleReturn()
    {
        var trace = new CalculationTrace("r38", new[] { new CalculationStep("x", "y") }, "100000",
            new Provenance(FormLine: "r38", Version: "2027.1"));
        return new TaxReturn(TaxYear.Of(2027)).WithLine("r38", 100000m, trace);
    }

    [Fact]
    public void Produces_a_non_empty_pdf_with_a_pdf_header()
    {
        byte[] pdf = new PdfExporter().Export(SampleReturn());

        Assert.True(pdf.Length > 1000);
        // PDF files start with "%PDF-".
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }
}
