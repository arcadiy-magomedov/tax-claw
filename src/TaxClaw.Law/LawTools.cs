using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using TaxClaw.Law.Corpus;
using TaxClaw.Law.Model;
using TaxClaw.Law.Retrieval;

namespace TaxClaw.Law;

/// <summary>
/// Exposes the law corpus to the agent as tools. Every answer carries a citation (§ + act +
/// edition + source) so a figure or claim can always be traced to a checkable source in the
/// <b>in-force</b> edition. The active edition is resolved dynamically (from the open project),
/// not captured at construction, so switching projects switches the applicable law.
/// </summary>
public sealed class LawTools(ILawCorpus corpus, ILawRetriever retriever, Func<LawVersion> activeEdition)
{
    private const int SnippetLength = 400;

    [Description("Return the exact full text and citation of a Czech tax-law section (e.g. \"§ 6\") "
        + "for the active edition. Use this when you already know the section number.")]
    public Task<string> LookupLaw([Description("section id, e.g. \"§ 6\"")] string section)
    {
        LawVersion version = activeEdition();
        LawSection? found = corpus.Resolve(Normalize(section), version);
        return Task.FromResult(found is null
            ? $"Section '{section}' not found in edition {version.Eli}."
            : Format(found, found.Text));
    }

    [Description("Search Czech tax legislation for the active edition and return the most relevant "
        + "sections with citations. The query MUST be in Czech legal terms (translate/expand first).")]
    public Task<string> SearchLaw(
        [Description("query in Czech legal terminology")] string queryCz,
        [Description("max sections to return")] int limit = 5)
    {
        LawVersion version = activeEdition();
        IReadOnlyList<LawSearchResult> results = retriever.Search(queryCz, version, limit);
        if (results.Count == 0)
        {
            return Task.FromResult($"No legislation matched '{queryCz}' in edition {version.Eli}.");
        }

        var sb = new StringBuilder();
        foreach (LawSearchResult r in results)
        {
            sb.AppendLine(Format(r.Match, Snippet(r.Match.Text))).AppendLine();
        }
        return Task.FromResult(sb.ToString().TrimEnd());
    }

    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(LookupLaw, "lookup_law"),
        AIFunctionFactory.Create(SearchLaw, "search_law")
    ];

    /// <summary>Tolerate "6", "§6", "§ 6" → "§ 6".</summary>
    private static string Normalize(string section)
    {
        string s = section.Trim().TrimStart('§').Trim();
        return $"§ {s}";
    }

    private static string Snippet(string text) =>
        text.Length <= SnippetLength ? text : text[..SnippetLength].TrimEnd() + " …";

    private static string Format(LawSection s, string body) =>
        $"{s.CitationLabel} (act {s.Version.ActNumber}, edition {s.Version.EffectiveOn:yyyy-MM-dd})\n"
        + $"{body}\nSource: {s.SourceEli}";
}
