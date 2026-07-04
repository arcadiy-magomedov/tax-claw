using TaxClaw.Core.Calc;
using TaxClaw.Core.Model;
using TaxClaw.Export.Xml;

namespace TaxClaw.Export.Tests;

public class XmlExporterTests
{
    private static TaxReturn SampleReturn()
    {
        var trace = new CalculationTrace("r38", new[] { new CalculationStep("x", "y") }, "100000",
            new Provenance(FormLine: "r38", Version: "2027.1"));
        return new TaxReturn(TaxYear.Of(2027)).WithLine("r38", 100000m, trace);
    }

    private const string Xsd =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="Declaration">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="Line" maxOccurs="unbounded">
                  <xs:complexType>
                    <xs:attribute name="id" type="xs:string" use="required"/>
                    <xs:attribute name="value" type="xs:decimal" use="required"/>
                  </xs:complexType>
                </xs:element>
              </xs:sequence>
              <xs:attribute name="year" type="xs:int" use="required"/>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;

    [Fact]
    public void Generated_xml_contains_the_year_and_lines()
    {
        string xml = new XmlExporter().Export(SampleReturn());
        Assert.Contains("year=\"2027\"", xml);
        Assert.Contains("id=\"r38\"", xml);
        Assert.Contains("value=\"100000\"", xml);
    }

    [Fact]
    public void Generated_xml_validates_against_the_schema()
    {
        string xml = new XmlExporter().Export(SampleReturn());
        var report = XsdValidator.Validate(xml, Xsd);
        Assert.True(report.IsValid, string.Join("; ", report.Errors));
    }

    [Fact]
    public void Malformed_xml_fails_validation()
    {
        const string bad = "<Declaration year=\"2027\"><Line id=\"r38\"/></Declaration>"; // missing value attr
        var report = XsdValidator.Validate(bad, Xsd);
        Assert.False(report.IsValid);
    }
}
