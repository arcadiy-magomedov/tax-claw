using Microsoft.Extensions.AI;
using TaxClaw.Agent;

namespace TaxClaw.Mcp;

/// <summary>
/// The tax-claw tool surface intended for the Model Context Protocol, so the same deterministic
/// capabilities can be shared with other MCP-aware agents. <see cref="PublishedTools"/> is the
/// single published list; the MCP transport host (stdio/HTTP via the MCP C# SDK) and consumption of
/// external MCP servers — which MAF supports natively — are wired when a serve/connect mode is
/// added. Kept minimal deliberately: v1 has no external MCP servers (see design §9).
/// </summary>
public static class TaxClawMcpServer
{
    /// <summary>The internal tools made available over MCP. Extend as new safe tool groups are added.</summary>
    public static IList<AITool> PublishedTools() => MathTools.CreateTools();
}
