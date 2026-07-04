using System.Text.Json;
using System.Text.RegularExpressions;
using TaxClaw.Law.Model;

namespace TaxClaw.Law.Ingest;

/// <summary>
/// Imports act editions from the official e-Sbírka open-data SPARQL endpoint. The source is
/// natively structured (fragments carry official citation labels + text), so sections are
/// <b>aggregated</b> from fragments — there is no regex-splitting of legislation text. The SPARQL
/// runner is injectable so parsing/aggregation is unit-tested offline.
/// </summary>
public sealed partial class ESbirkaSource : ILawSource
{
    public const string SparqlEndpoint = "https://opendata.eselpoint.gov.cz/sparql";
    private const string ResourceBase = "https://opendata.eselpoint.gov.cz/esel-esb/";
    private const string Prefix = "PREFIX p: <https://slovník.gov.cz/datový/sbírka/pojem/>";

    private readonly Func<string, CancellationToken, Task<string>> _runSparql;

    public ESbirkaSource(Func<string, CancellationToken, Task<string>> runSparql) => _runSparql = runSparql;

    /// <summary>A source that runs SPARQL against the live e-Sbírka endpoint over HTTP.</summary>
    public static ESbirkaSource Http(HttpClient http) => new((query, ct) => PostAsync(http, query, ct));

    public async Task<IReadOnlyList<LawSection>> LoadAsync(LawVersion version, CancellationToken ct = default) =>
        Aggregate(await _runSparql(FragmentsQuery(version.Eli), ct), version);

    public async Task<IReadOnlyList<LawVersion>> ListEditionsAsync(string actNumber, CancellationToken ct = default) =>
        ParseEditions(await _runSparql(EditionsQuery(actNumber), ct), actNumber);

    /// <summary>Aggregates SPARQL <c>(cit, text)</c> fragment rows into one <see cref="LawSection"/> per § or article.</summary>
    public static IReadOnlyList<LawSection> Aggregate(string sparqlResultJson, LawVersion version)
    {
        using JsonDocument doc = JsonDocument.Parse(sparqlResultJson);
        JsonElement rows = doc.RootElement.GetProperty("results").GetProperty("bindings");

        var bySection = new Dictionary<string, List<string>>();
        var order = new List<string>();

        foreach (JsonElement row in rows.EnumerateArray())
        {
            if (!TryCitationKey(Value(row, "cit"), out string section))
            {
                continue; // structural header ("Část 1", ...) — not a § or article
            }

            string text = CleanText(Value(row, "text"));
            if (text.Length == 0)
            {
                continue;
            }

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

    /// <summary>
    /// Derives the aggregation key from a fragment citation. Acts are keyed by paragraph
    /// (<c>§ 16</c>); international treaties, whose fragments are cited as
    /// <c>"Příloha  Čl. N bod M písm. x)"</c>, are keyed by article (<c>Čl. N</c>). Anything else
    /// (structural headers) is skipped.
    /// </summary>
    private static bool TryCitationKey(string cit, out string cite)
    {
        Match paragraph = SectionKey().Match(cit);
        if (paragraph.Success)
        {
            cite = $"§ {paragraph.Groups[1].Value}";
            return true;
        }

        Match article = ArticleKey().Match(cit);
        if (article.Success)
        {
            cite = $"Čl. {article.Groups[1].Value}";
            return true;
        }

        cite = string.Empty;
        return false;
    }

    /// <summary>Parses edition URIs (…/{act}/{yyyy-MM-dd}) into <see cref="LawVersion"/>s; skips the sentinel 0000-00-00.</summary>
    public static IReadOnlyList<LawVersion> ParseEditions(string sparqlResultJson, string actNumber)
    {
        using JsonDocument doc = JsonDocument.Parse(sparqlResultJson);
        JsonElement rows = doc.RootElement.GetProperty("results").GetProperty("bindings");

        var editions = new List<LawVersion>();
        var seen = new HashSet<DateOnly>();
        foreach (JsonElement row in rows.EnumerateArray())
        {
            Match m = EditionDate().Match(Value(row, "ed"));
            if (m.Success
                && DateOnly.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd", out DateOnly date)
                && seen.Add(date))
            {
                editions.Add(new LawVersion(actNumber, date));
            }
        }
        return editions;
    }

    private static string FragmentsQuery(string eli) => $$"""
        {{Prefix}}
        SELECT ?cit ?text WHERE {
          <{{ResourceBase}}{{eli}}> p:má-fragment-znění ?f .
          ?f p:citace-označení-fragmentu-znění-právního-aktu ?cit .
          ?f p:obsahuje-fragment ?tf .
          ?tf p:text-fragmentu ?text .
        }
        """;

    private static string EditionsQuery(string actNumber) => $$"""
        {{Prefix}}
        SELECT ?ed WHERE { <{{ResourceBase}}{{ActEli(actNumber)}}> p:má-znění ?ed . }
        """;

    private static string ActEli(string actNumber)
    {
        string[] parts = actNumber.Split('/');
        if (parts.Length != 2)
        {
            throw new FormatException($"ActNumber '{actNumber}' must be 'number/year', e.g. '586/1992'.");
        }
        return $"eli/cz/sb/{parts[1]}/{parts[0]}";
    }

    private static string Value(JsonElement row, string name) =>
        row.TryGetProperty(name, out JsonElement v) && v.TryGetProperty("value", out JsonElement val)
            ? val.GetString() ?? string.Empty
            : string.Empty;

    private static string CleanText(string raw) => Whitespace().Replace(Tags().Replace(raw, " "), " ").Trim();

    private static async Task<string> PostAsync(HttpClient http, string query, CancellationToken ct)
    {
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

    [GeneratedRegex(@"Čl\.\s*(\d+[a-z]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ArticleKey();

    [GeneratedRegex(@"/(\d{4}-\d{2}-\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex EditionDate();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
