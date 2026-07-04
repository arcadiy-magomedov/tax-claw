using System.Globalization;
using System.Xml.Linq;
using TaxClaw.Core.Model;

namespace TaxClaw.Export.Xml;

/// <summary>
/// Projects the return to the portal XML shape (one element per populated line). This is export
/// milestone 3; the element/attribute names track the EPO schema for form 25 5405 once obtained.
/// </summary>
public sealed class XmlExporter : IReturnExporter<string>
{
    public string Export(TaxReturn taxReturn)
    {
        var doc = new XDocument(
            new XElement("Declaration",
                new XAttribute("year", taxReturn.Year.Year),
                taxReturn.Lines
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => new XElement("Line",
                        new XAttribute("id", kv.Key),
                        new XAttribute("value", kv.Value.ToString(CultureInfo.InvariantCulture))))));

        return doc.Declaration is null
            ? doc.ToString()
            : doc.Declaration + Environment.NewLine + doc;
    }
}
