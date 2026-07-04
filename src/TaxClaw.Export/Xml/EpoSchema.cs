namespace TaxClaw.Export.Xml;

/// <summary>
/// The EPO XSD used to validate an exported declaration, plus a human-readable description of where
/// it came from.
/// </summary>
/// <remarks>
/// The authoritative schema for form 25 5405 (DPFDP5) is <c>dpfdp5_epo2.xsd</c>, published — and
/// versioned per tax year — by the Czech Financial Administration on the MOJE daně documentation
/// portal (Popis struktury souborů / "Popis XML struktur"). It is served from a JavaScript portal
/// rather than a stable file URL and cannot be redistributed here, so tax-claw loads it from a
/// configurable path: drop the downloaded file at <c>~/.tax-claw/schemas/dpfdp5_epo2.xsd</c> and it
/// is used automatically. Absent that, a small built-in stand-in matches the current interim export
/// shape. NOTE: the official schema's real element structure is
/// <c>Pisemnost → DPFDP5 → Veta_*</c>, which the exporter will emit once the real form-line model
/// lands; until then, validating the interim shape against the official XSD is expected to fail.
/// </remarks>
public sealed record EpoSchema(string Text, string Source)
{
    /// <summary>Matches the interim <c>Declaration/Line</c> export shape; replaced by the official XSD when configured.</summary>
    public const string StandInXsd =
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

    /// <summary>
    /// Resolves the schema to validate against: the official XSD at <paramref name="xsdPath"/> when
    /// that file exists, otherwise the built-in stand-in.
    /// </summary>
    public static EpoSchema Resolve(string? xsdPath = null) =>
        !string.IsNullOrWhiteSpace(xsdPath) && File.Exists(xsdPath)
            ? new EpoSchema(File.ReadAllText(xsdPath), xsdPath)
            : new EpoSchema(StandInXsd, "built-in stand-in schema");
}
