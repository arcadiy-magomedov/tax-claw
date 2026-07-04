using System.Security.Cryptography;
using System.Text;

namespace TaxClaw.Skills.Model;

/// <summary>
/// Identifies and pins a shareable skill / knowledge pack: what it is, which law and form versions
/// it was built against, who authored it, and a content hash binding the exact artifact bytes.
/// </summary>
public sealed record SkillManifest(
    string Id,
    string Version,
    string LawVersion,
    string? FormVersion,
    string Author,
    string ContentHash)
{
    /// <summary>Order-independent hash over the pack's file contents.</summary>
    public static string ComputeContentHash(IReadOnlyDictionary<string, string> files)
    {
        var sb = new StringBuilder();
        foreach (var kv in files.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.Append(kv.Key).Append('\0').Append(kv.Value).Append('\n');
        }

        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexStringLower(bytes);
    }
}
