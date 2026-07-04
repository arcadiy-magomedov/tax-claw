using TaxClaw.Memory;

namespace TaxClaw.Memory.Tests;

public class MemoryScopeTests
{
    [Fact]
    public void Global_scope_is_relevant_to_every_context()
    {
        var scope = MemoryScope.Global();
        Assert.True(scope.IsRelevantTo(projectId: "2027", documentType: "dividend"));
        Assert.True(scope.IsRelevantTo(projectId: null, documentType: null));
    }

    [Fact]
    public void Project_scope_matches_only_its_project()
    {
        var scope = MemoryScope.Project("2027");
        Assert.True(scope.IsRelevantTo("2027", null));
        Assert.False(scope.IsRelevantTo("2026", null));
    }

    [Fact]
    public void DocumentType_scope_matches_only_its_type()
    {
        var scope = MemoryScope.DocumentType("dividend");
        Assert.True(scope.IsRelevantTo("2027", "dividend"));
        Assert.False(scope.IsRelevantTo("2027", "rsu_vesting"));
    }
}
