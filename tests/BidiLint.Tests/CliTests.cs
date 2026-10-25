using System.Text.Json;
using Xunit;

namespace BidiLint.Tests;

public class CliTests
{
    [Fact]
    public void Help_ReturnsZero()
    {
        var (code, stdout, _) = Run(["--help"]);

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Contains("Usage: bidilint", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_ReturnsZero()
    {
        var (code, stdout, _) = Run(["--version"]);

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Contains(ToolInfo.Version, stdout, StringComparison.Ordinal);
        Assert.Contains($"Unicode {UnicodeInfo.Version}", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownOption_ReturnsUsageError()
    {
        var (code, _, stderr) = Run(["--nope"]);

        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("Unknown option", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPath_ReturnsInputError()
    {
        var (code, stdout, _) = Run(["does-not-exist"]);

        Assert.Equal(ExitCodes.Input, code);
        Assert.Contains("PathNotFound", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyDirectory_ReturnsNoInput()
    {
        using var workspace = new TestWorkspace();

        var (code, stdout, _) = Run(["."], workspace);

        Assert.Equal(ExitCodes.NoInput, code);
        Assert.Contains("NoLocaleFiles", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingBaseCulture_ReturnsNoInput()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("ar.json", """{ "a": "أ" }""");

        var (code, stdout, _) = Run(["."], workspace);

        Assert.Equal(ExitCodes.NoInput, code);
        Assert.Contains("BaseCultureMissing", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Errors_FailByDefault()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("ar.json", """{ "a": "أ" }""");

        var (code, stdout, _) = Run(["."], workspace);

        Assert.Equal(ExitCodes.Findings, code);
        Assert.Contains("MissingKey", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Warnings_PassUnlessStrict()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A" }""");
        workspace.Write("ar.json", """{ "a": "أ", "z": "ز" }""");

        var (normalCode, normalOutput, _) = Run(["."], workspace);
        var (strictCode, strictOutput, _) = Run([".", "--strict"], workspace);

        Assert.Equal(ExitCodes.Ok, normalCode);
        Assert.Equal(ExitCodes.Findings, strictCode);
        Assert.Contains("ExtraKey", normalOutput, StringComparison.Ordinal);
        Assert.Contains("ExtraKey", strictOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanInput_ReturnsZero()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A" }""");
        workspace.Write("ar.json", """{ "a": "أ" }""");

        var (code, _, _) = Run(["."], workspace);

        Assert.Equal(ExitCodes.Ok, code);
    }

    [Fact]
    public void MalformedJson_IsAnError()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A" }""");
        workspace.Write("ar.json", """{ "a": "أ" """);

        var (code, stdout, _) = Run(["."], workspace);

        Assert.Equal(ExitCodes.Findings, code);
        Assert.Contains("MalformedFile", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonFormat_IsMachineReadable()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A" }""");
        workspace.Write("ar.json", """{ "a": "أ", "z": "ز" }""");

        var (code, stdout, _) = Run([".", "--format", "json"], workspace);

        Assert.Equal(ExitCodes.Ok, code);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal("BidiLint", document.RootElement.GetProperty("tool").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("errors").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("warnings").GetInt32());
        Assert.Equal("ExtraKey", document.RootElement.GetProperty("findings")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void MultiplePaths_AreCombined()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");

        var (code, _, _) = Run(
            ["locales/en.json", "locales/ar.json"],
            workspace);

        Assert.Equal(ExitCodes.Ok, code);
    }

    [Fact]
    public void DoubleDash_AllowsADashPrefixedPath()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("-locales/en.json", """{ "a": "A" }""");
        workspace.Write("-locales/ar.json", """{ "a": "أ" }""");

        var (code, _, _) = Run(["--", "-locales"], workspace);

        Assert.Equal(ExitCodes.Ok, code);
    }

    private static (int Code, string Stdout, string Stderr) Run(
        string[] args,
        TestWorkspace? workspace = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = Cli.Run(
            args,
            stdout,
            stderr,
            workspace?.Root ?? Directory.GetCurrentDirectory());
        return (code, stdout.ToString(), stderr.ToString());
    }
}
