using Microsoft.Extensions.AI;
using TaxClaw.Mcp;

namespace TaxClaw.Skills.Tests;

public class McpToolDescriptorTests
{
    [Fact]
    public void Server_publishes_the_math_tools_for_mcp_clients()
    {
        IList<AITool> tools = TaxClawMcpServer.PublishedTools();
        var names = tools.OfType<AIFunction>().Select(t => t.Name).ToHashSet();

        Assert.Contains("add", names);
        Assert.Contains("round_to_unit", names);
    }
}
