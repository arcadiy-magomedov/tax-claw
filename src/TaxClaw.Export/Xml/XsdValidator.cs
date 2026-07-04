using System.Xml;
using System.Xml.Schema;

namespace TaxClaw.Export.Xml;

/// <summary>The result of validating XML against a schema.</summary>
public sealed record XsdReport(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Validates exported XML against the portal XSD before submission.</summary>
public static class XsdValidator
{
    public static XsdReport Validate(string xml, string xsd)
    {
        var errors = new List<string>();

        var schemas = new XmlSchemaSet();
        using (var schemaReader = XmlReader.Create(new StringReader(xsd)))
        {
            schemas.Add(null, schemaReader);
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas
        };
        settings.ValidationEventHandler += (_, e) => errors.Add(e.Message);

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            errors.Add(ex.Message);
        }

        return new XsdReport(errors.Count == 0, errors);
    }
}
