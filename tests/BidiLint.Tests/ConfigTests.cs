using Xunit;

namespace BidiLint.Tests;

public class ConfigTests
{
    [Fact]
    public void JsoncCommentsTrailingCommasAndNestedValuesAreAccepted()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            {
              // Component comments are documentation, not config.
              "$schema": "https://example.test/schema.json",
              "baseCulture": "ar",
              "include": ["locales/**/*.json",],
              "rules": {
                "MissingKey": "warning",
                "ExtraKey": { "severity": "info" },
                "UnisolatedLatin": { "allow": ["BidiLint", ".NET"] }
              },
              "allowLatin": [
                "ExactBrand",
                { "term": "ios", "match": "case-insensitive", "reason": "Product name." }
              ],
              "baseline": "suppressions/baseline.json",
              "output": { "format": "json", "maxWarnings": 0 }
            }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Empty(resolution.Errors);
        Assert.Equal("ar", resolution.Config.BaseCulture);
        Assert.Equal(new[] { "locales/**/*.json" }, resolution.Config.Include);
        Assert.Equal(DiagnosticSeverity.Warning, resolution.Config.Rules["MissingKey"].Severity);
        Assert.Equal(DiagnosticSeverity.Info, resolution.Config.Rules["ExtraKey"].Severity);
        Assert.Equal(new[] { "BidiLint", ".NET" }, resolution.Config.Rules["UnisolatedLatin"].Allow);
        Assert.Equal(2, resolution.Config.AllowLatin.Count);
        Assert.Equal(OutputFormat.Json, resolution.Config.Output.Format);
        Assert.Equal(0, resolution.Config.Output.MaxWarnings);
        Assert.Equal(
            Path.Combine(workspace.Root, "suppressions", "baseline.json"),
            resolution.Config.BaselinePath);
    }

    [Fact]
    public void UnknownRootKeySuggestsTheClosestKey()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            { "baseCulture": "en", "exlude": ["generated/**"] }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Single(resolution.Errors);
        Assert.Contains("unknown key \"exlude\"", resolution.Errors[0], StringComparison.Ordinal);
        Assert.Contains("exclude", resolution.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownRuleSuggestsTheClosestRule()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            { "rules": { "MissingKay": "off" } }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Single(resolution.Errors);
        Assert.Contains("MissingKey", resolution.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoreRequiresReasonAndASelector()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            {
              "ignore": [
                { "rule": "MissingKey" },
                { "reason": "Legacy." }
              ]
            }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Equal(2, resolution.Errors.Count);
        Assert.Contains(resolution.Errors, error => error.Contains("requires a non-empty reason", StringComparison.Ordinal));
        Assert.Contains(resolution.Errors, error => error.Contains("must select at least one", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoveryWalksUpFromTheTargetPath()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            { "baseCulture": "ar", "rules": { "ExtraKey": "info" } }
            """);
        var target = Path.Combine(workspace.Root, "src", "locales");
        Directory.CreateDirectory(target);

        var resolution = ConfigLoader.Resolve(null, false, new[] { target }, workspace.Root);

        Assert.Empty(resolution.Errors);
        Assert.Equal(configPath, resolution.Config.ConfigPath);
        Assert.Equal("ar", resolution.Config.BaseCulture);
        Assert.Equal(DiagnosticSeverity.Info, resolution.Config.Rules["ExtraKey"].Severity);
    }

    [Fact]
    public void DifferentDiscoveredConfigsAreAUsageError()
    {
        using var workspace = new TestWorkspace();
        var firstTarget = Path.Combine(workspace.Root, "first", "locales");
        var secondTarget = Path.Combine(workspace.Root, "second", "locales");
        Directory.CreateDirectory(firstTarget);
        Directory.CreateDirectory(secondTarget);
        workspace.Write("first/bidilint.json", """{ "baseCulture": "ar" }""");
        workspace.Write("second/bidilint.json", """{ "baseCulture": "he" }""");

        var resolution = ConfigLoader.Resolve(
            null,
            false,
            new[] { firstTarget, secondTarget },
            workspace.Root);

        Assert.Single(resolution.Errors);
        Assert.Contains("Multiple bidilint.json", resolution.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitConfigWinsOverDiscovery()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("bidilint.json", """{ "baseCulture": "en" }""");
        var explicitConfig = workspace.Write("configs/other.json", """{ "baseCulture": "he" }""");
        var target = Path.Combine(workspace.Root, "locales");
        Directory.CreateDirectory(target);

        var resolution = ConfigLoader.Resolve(
            explicitConfig,
            false,
            new[] { target },
            workspace.Root);

        Assert.Empty(resolution.Errors);
        Assert.Equal("he", resolution.Config.BaseCulture);
        Assert.Equal(Path.GetFullPath(explicitConfig), resolution.Config.ConfigPath);
    }

    [Fact]
    public void WhitespaceExplicitConfigIsNotSilentlyIgnored()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("bidilint.json", """{ "baseCulture": "ar" }""");

        var resolution = ConfigLoader.Resolve(
            " ",
            false,
            new[] { Path.Combine(workspace.Root, "locales") },
            workspace.Root);

        Assert.Single(resolution.Errors);
        Assert.Contains("Config path must not be empty", resolution.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void NoConfigIgnoresAConfigNextToTheTarget()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("bidilint.json", """{ "baseCulture": "ar" }""");
        var target = Path.Combine(workspace.Root, "locales");
        Directory.CreateDirectory(target);

        var resolution = ConfigLoader.Resolve(null, true, new[] { target }, workspace.Root);

        Assert.Empty(resolution.Errors);
        Assert.Null(resolution.Config.ConfigPath);
        Assert.Equal("en", resolution.Config.BaseCulture);
    }

    [Fact]
    public void InvalidRuleSeverityIsRejected()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            { "rules": { "MissingKey": "fatal" } }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Single(resolution.Errors);
        Assert.Contains("off, error, warning, or info", resolution.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void LoadAndFormatDiagnosticsCannotBeReconfigured()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            { "rules": { "MalformedFile": "off" } }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Single(resolution.Errors);
        Assert.Contains("cannot be configured", resolution.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void IgnoreSelectorsCannotTargetLoadDiagnostics()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            {
              "ignore": [
                { "rule": "MalformedFile", "reason": "Trying to hide a parse error." }
              ]
            }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Single(resolution.Errors);
        Assert.Contains("cannot target a load", resolution.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TermAllowListsAreOnlyAcceptedByTermRules()
    {
        using var workspace = new TestWorkspace();
        var configPath = workspace.Write("bidilint.json", """
            {
              "rules": {
                "ExtraKey": { "allow": ["Not meaningful"] }
              }
            }
            """);

        var resolution = ConfigLoader.Load(configPath);

        Assert.Single(resolution.Errors);
        Assert.Contains("only supported by rules that match Latin terms", resolution.Errors[0], StringComparison.Ordinal);
    }
}
