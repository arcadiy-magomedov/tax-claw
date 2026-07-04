using System.Text.Json;
using System.Text.RegularExpressions;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Ingest;

/// <summary>
/// Imports an act edition from the official e-Sbírka open-data SPARQL endpoint. The source is
/// natively structured (fragments carry official citation labels + text), so sections are
/// <b>aggregated</b> from fragments — there is no regex-splitting of legislation text.
/// </summary>
public sealed partial class ESbirkaSource : ILawSource
{
    public const string SparqlEndpoint = "https://opendata.eselpoint.gov.cz/sparql";
    private const string ResourceBase = "https://opendata.eselpoint.gov.cz/esel-esb/";

    private readonly Func<LawVersion, CancellationToken, Task<string>> _fetch;

    public ESbirkaSource(Func<LawVersion, CancellationToken, Task<string>> fetchSparqlJson) =>
        _fetch = fetchSparqlJson;

    /// <summary>A source that queries the live e-Sbírka SPARQL endpoint over HTTP.</summary>
    public static ESbirkaSource Http(HttpClient http) => new((v, ct) => FetchAsync(http, v, ct));

    public async Task<IReadOnlyList<LawSection>> LoadAsync(LawVersion version, CancellationToken ct = default) =>
        Aggregate(await _fetch(version, ct), version);

    /// <summary>Aggregates SPARQL <c>(cit, text)</c> fragment rows into one <see cref="LawSection"/> per §.</summary>
    public static IReadOnlyList<LawSection> Aggregate(string sparqlResultJson, LawVersion version)
    {
        using JsonDocument doc = JsonDocument.Parse(sparqlResultJson);
        JsonElement rows = doc.RootElement.GetProperty("results").GetProperty("bindings");

        var bySection = new Dictionary<string, List<string>>();
        var order = new List<string>();

        foreach (JsonElement row in rows.EnumerateArray())
        {
            string cit = Value(row, "cit");
            Match m = SectionKey().Match(cit);
            if (!m.Success)
            {
                continue; // structural header ("Část 1", ...) — not a §
            }

            string text = CleanText(Value(row, "text"));
            if (text.Length == 0)
            {
                continue;
            }

            string section = $"§ {m.Groups[1].Value}";
            if (!bySection.TryGetValue(section, out List<string>? parts))
            {
                parts = [];
                bySection[section] = parts;
                order.Add(section);
            }
            parts.Add(text);
        }

        return order
            .Select(s => new LawSection(s, s, string.Join(" ", bySection[s]), version, version.Eli))
            .ToList();
    }

    private static string Value(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement v) && v.TryGetProperty("value", out JsonElement val)
            ? val.GetString() ?? string.Empty
            : string.Empty;

    private static string CleanText(string raw) => Whitespace().Replace(Tags().Replace(raw, " "), " ").Trim();

    private static async Task<string> FetchAsync(HttpClient http, LawVersion version, CancellationToken ct)
    {
        string query = $$"""
            PREFIX p: <https://slovník.gov.cz/datový/sbírka/pojem/>
            SELECT ?cit ?text WHERE {
              <{{ResourceBase}}{{version.Eli}}> p:má-fragment-znění ?f .
              ?f p:citace-označení-fragmentu-znění-právního-aktu ?cit .
              ?f p:obsahuje-fragment ?tf .
              ?tf p:text-fragmentu ?text .
            }
            """;

        using var request = new HttpRequestMessage(HttpMethod.Post, SparqlEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["query"] = query })
        };
        request.Headers.Accept.ParseAdd("application/sparql-results+json");

        using HttpResponseMessage response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    [GeneratedRegex(@"^§\s*(\d+[a-z]*)", RegexOptions.CultureInvariant)]
    private static partial Regex SectionKey();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
