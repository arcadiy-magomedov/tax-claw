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
    public void Parses_law_command()
    {
        var command = CommandRouter.Parse("/law 2027");
        var loadLaw = Assert.IsType<LoadLawCommand>(command);
        Assert.Equal(TaxYear.Of(2027), loadLaw.Year);
    }

    [Fact]
    public void Law_without_a_valid_year_becomes_an_error()
    {
        Assert.IsType<UnknownCommand>(CommandRouter.Parse("/law"));
    }

    [Fact]
    public void Parses_doc_command_with_a_path()
    {
        var command = CommandRouter.Parse("/doc ~/statements/dividend.txt");
        var doc = Assert.IsType<ProcessDocumentCommand>(command);
        Assert.Equal(["~/statements/dividend.txt"], doc.Paths);
    }

    [Fact]
    public void Doc_without_a_path_becomes_an_error()
    {
        Assert.IsType<UnknownCommand>(CommandRouter.Parse("/doc"));
    }

    [Fact]
    public void Parses_doc_command_with_multiple_space_separated_paths()
    {
        var command = CommandRouter.Parse("/doc ~/a.pdf ~/b.csv");
        var doc = Assert.IsType<ProcessDocumentCommand>(command);
        Assert.Equal(["~/a.pdf", "~/b.csv"], doc.Paths);
    }

    [Fact]
    public void Parses_doc_command_with_a_quoted_path_containing_spaces()
    {
        // Simulates dragging a file with spaces in its name from Finder/Explorer.
        var command = CommandRouter.Parse("/doc '~/My Statements/dividend.txt'");
        var doc = Assert.IsType<ProcessDocumentCommand>(command);
        Assert.Equal(["~/My Statements/dividend.txt"], doc.Paths);
    }

    [Fact]
    public void Parses_doc_command_with_a_backslash_escaped_space()
    {
        // Simulates macOS Terminal's drag-and-drop escaping convention.
        var command = CommandRouter.Parse(@"/doc ~/My\ Statements/dividend.txt");
        var doc = Assert.IsType<ProcessDocumentCommand>(command);
        Assert.Equal(["~/My Statements/dividend.txt"], doc.Paths);
    }

    [Fact]
    public void Parses_export_command_with_format_and_path()
    {
        var command = CommandRouter.Parse("/export summary ~/out/summary.md");
        var export = Assert.IsType<ExportCommand>(command);
        Assert.Equal("summary", export.Format);
        Assert.Equal("~/out/summary.md", export.Path);
    }

    [Fact]
    public void Export_without_a_path_becomes_an_error()
    {
        Assert.IsType<UnknownCommand>(CommandRouter.Parse("/export pdf"));
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
