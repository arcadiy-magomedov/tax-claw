using System.Security.Cryptography;
using System.Text;

namespace TaxClaw.Law.Model;

/// <summary>
/// One section (§) of an act edition: the section id ("§ 6"), the official citation label, the
/// aggregated text, the edition it belongs to, and the source ELI. <see cref="Hash"/> pins the exact
/// wording so a citation can never silently apply to changed text.
/// </summary>
public sealed record LawSection(
    string Section,
    string CitationLabel,
    string Text,
    LawVersion Version,
    string SourceEli)
{
    public string Hash { get; } = ComputeHash(Section, Text);

    private static string ComputeHash(string section, string text)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{section}\n{text}"));
        return Convert.ToHexStringLower(bytes);
    }
}
