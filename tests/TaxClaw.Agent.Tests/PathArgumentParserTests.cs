using TaxClaw.Agent.Commands;

namespace TaxClaw.Agent.Tests;

public class PathArgumentParserTests
{
    [Fact]
    public void Splits_bare_space_separated_tokens()
    {
        Assert.Equal(["a.pdf", "b.csv"], PathArgumentParser.Tokenize("a.pdf b.csv"));
    }

    [Fact]
    public void Collapses_repeated_whitespace_between_tokens()
    {
        Assert.Equal(["a.pdf", "b.csv"], PathArgumentParser.Tokenize("a.pdf   b.csv"));
    }

    [Fact]
    public void Single_quoted_path_keeps_internal_spaces_and_is_not_escape_processed()
    {
        Assert.Equal(["My File.pdf"], PathArgumentParser.Tokenize("'My File.pdf'"));
        Assert.Equal([@"My\File.pdf"], PathArgumentParser.Tokenize(@"'My\File.pdf'"));
    }

    [Fact]
    public void Double_quoted_path_keeps_internal_spaces_and_unescapes_quotes_and_backslashes()
    {
        Assert.Equal(["My File.pdf"], PathArgumentParser.Tokenize("\"My File.pdf\""));
        Assert.Equal(["My \"File\".pdf"], PathArgumentParser.Tokenize("\"My \\\"File\\\".pdf\""));
    }

    [Fact]
    public void Backslash_escapes_a_single_character_outside_quotes()
    {
        Assert.Equal(["My File.pdf"], PathArgumentParser.Tokenize(@"My\ File.pdf"));
    }

    [Fact]
    public void Mixes_quoted_and_bare_tokens_on_one_line()
    {
        Assert.Equal(
            ["My File.pdf", "b.csv"],
            PathArgumentParser.Tokenize("'My File.pdf' b.csv"));
    }

    [Fact]
    public void Empty_input_yields_no_tokens()
    {
        Assert.Empty(PathArgumentParser.Tokenize("   "));
        Assert.Empty(PathArgumentParser.Tokenize(""));
    }

    [Fact]
    public void Unterminated_quote_reads_to_the_end_of_the_input()
    {
        Assert.Equal(["My File.pdf"], PathArgumentParser.Tokenize("'My File.pdf"));
    }
}
