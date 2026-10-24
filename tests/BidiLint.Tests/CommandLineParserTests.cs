using Xunit;

namespace BidiLint.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void ParsesPathsAndOptions()
    {
        var result = CommandLineParser.Parse(
            ["locales", "--base", "ar", "--format", "json", "--strict"]);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Options);
        Assert.Equal(new[] { "locales" }, result.Options!.Paths);
        Assert.Equal("ar", result.Options.BaseCulture);
        Assert.Equal(OutputFormat.Json, result.Options.Format);
        Assert.True(result.Options.Strict);
    }

    [Fact]
    public void ParsesEqualsSyntaxAndMultiplePaths()
    {
        var result = CommandLineParser.Parse(
            ["a", "b", "--base=ar-EG", "--format=json"]);

        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "a", "b" }, result.Options!.Paths);
        Assert.Equal("ar-EG", result.Options.BaseCulture);
        Assert.Equal(OutputFormat.Json, result.Options.Format);
    }

    [Fact]
    public void DoubleDashStopsOptionParsing()
    {
        var result = CommandLineParser.Parse(["--", "--strange", "-x"]);

        Assert.Empty(result.Errors);
        Assert.Equal(new[] { "--strange", "-x" }, result.Options!.Paths);
    }

    [Fact]
    public void HelpIsRecognizedInAnyPosition()
    {
        var result = CommandLineParser.Parse(["locales", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Null(result.Options);
    }

    [Fact]
    public void MissingValueIsAnError()
    {
        var result = CommandLineParser.Parse(["locales", "--base"]);

        Assert.Contains(result.Errors, error => error.Contains("--base", StringComparison.Ordinal));
        Assert.Null(result.Options);
    }

    [Fact]
    public void UnknownFormatIsAnError()
    {
        var result = CommandLineParser.Parse(["locales", "--format", "xml"]);

        Assert.Contains(result.Errors, error => error.Contains("Unknown format", StringComparison.Ordinal));
        Assert.Null(result.Options);
    }

    [Fact]
    public void WhitespaceConfigPathIsAnError()
    {
        var result = CommandLineParser.Parse(["locales", "--config", " "]);

        Assert.Contains(result.Errors, error => error.Contains("--config", StringComparison.Ordinal));
        Assert.Null(result.Options);
    }

    [Fact]
    public void NoPathIsAnError()
    {
        var result = CommandLineParser.Parse([]);

        Assert.Contains(result.Errors, error => error.Contains("No path", StringComparison.Ordinal));
        Assert.Null(result.Options);
    }

    [Fact]
    public void InvalidBaseCultureIsAnError()
    {
        var result = CommandLineParser.Parse(["locales", "--base", "app"]);

        Assert.Contains(result.Errors, error => error.Contains("base culture", StringComparison.OrdinalIgnoreCase));
        Assert.Null(result.Options);
    }
}
