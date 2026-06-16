using TaxClaw.Agent.Commands;
using TaxClaw.Core.Model;
using Xunit;

namespace TaxClaw.Agent.Tests;

public class CommandRouterTests
{
    [Fact]
    public void Parses_new_project_command()
    {
        var command = CommandRouter.Parse("/new 2027");
        var newProject = Assert.IsType<NewProjectCommand>(command);
        Assert.Equal(TaxYear.Of(2027), newProject.Year);
    }

    [Fact]
    public void Parses_quit_command()
    {
        Assert.IsType<QuitCommand>(CommandRouter.Parse("/quit"));
    }

    [Fact]
    public void Plain_text_becomes_a_chat_command()
    {
        var command = CommandRouter.Parse("how are RSUs taxed?");
        var chat = Assert.IsType<ChatCommand>(command);
        Assert.Equal("how are RSUs taxed?", chat.Message);
    }

    [Fact]
    public void New_without_a_valid_year_becomes_an_error()
    {
        var command = CommandRouter.Parse("/new notayear");
        Assert.IsType<UnknownCommand>(command);
    }

    [Fact]
    public void Parses_model_command_with_an_id()
    {
        var command = CommandRouter.Parse("/model claude-opus-4.8");
        var model = Assert.IsType<ModelCommand>(command);
        Assert.Equal("claude-opus-4.8", model.ModelId);
    }

    [Fact]
    public void Bare_model_command_lists_models()
    {
        var command = CommandRouter.Parse("/model");
        var model = Assert.IsType<ModelCommand>(command);
        Assert.Null(model.ModelId);
    }
}
