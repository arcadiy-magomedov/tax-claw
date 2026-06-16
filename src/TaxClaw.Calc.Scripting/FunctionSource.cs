using System.Security.Cryptography;
using System.Text;
using TaxClaw.Core.Calc;

namespace TaxClaw.Calc.Scripting;

/// <summary>
/// Agent-generated calc-function source plus its provenance. The <see cref="Hash"/> pins the exact
/// text + line + version so an approval can never silently apply to changed code.
/// </summary>
public sealed record FunctionSource(string LineId, string Version, string Body, Provenance Provenance)
{
    public string Hash { get; } = ComputeHash(LineId, Version, Body);

    private static string ComputeHash(string lineId, string version, string body)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{lineId}\n{version}\n{body}"));
        return Convert.ToHexStringLower(bytes);
    }
}
