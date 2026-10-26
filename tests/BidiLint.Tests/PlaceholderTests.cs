using System.Text.Json;
using Xunit;

namespace BidiLint.Tests;

public class PlaceholderTests
{
    [Fact]
    public void DotNetEscapedBracesAndNamedArguments_AreCompared()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "{{literal}} {0} {name}" }""");
        workspace.Write("ar.json", """{ "message": "{{حرفي}} {0} {name}" }""");

        var result = workspace.Lint();

        AssertNoPlaceholderFindings(result);
    }

    [Fact]
    public void DotNetFormatDifference_IsAPlaceholderType()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "Total: {0:N2}" }""");
        workspace.Write("ar.json", """{ "message": "المجموع: {0}" }""");

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderType);

        Assert.NotNull(finding.Span);
        Assert.NotNull(finding.Related);
        Assert.Contains("expected", finding.Message, StringComparison.Ordinal);
        Assert.Contains("found", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingDotNetArgument_HasBaseAndTargetSpans()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "From {0} to {1}" }""");
        workspace.Write("ar.json", """{ "message": "من {0}" }""");

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);

        Assert.Contains("expected [{0}, {1}]", finding.Message, StringComparison.Ordinal);
        Assert.Contains("found [{0}]", finding.Message, StringComparison.Ordinal);
        Assert.NotNull(finding.Span);
        var related = Assert.Single(finding.Related!);
        Assert.NotNull(related.Span);
    }

    [Fact]
    public void PrintfPositionalArguments_AreOrderIndependent()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "%1$s to %2$s" }""");
        workspace.Write("ar.json", """{ "message": "%2$s إلى %1$s" }""");

        var result = workspace.Lint();

        AssertNoPlaceholderFindings(result);
    }

    [Fact]
    public void PrintfConversionDifference_IsAPlaceholderType()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "%s items" }""");
        workspace.Write("ar.json", """{ "message": "%d عناصر" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderType);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void MissingPrintfArgument_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "Path: %s" }""");
        workspace.Write("ar.json", """{ "message": "المسار:" }""");

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);

        Assert.Contains("expected [%s]", finding.Message, StringComparison.Ordinal);
        Assert.Contains("found []", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IcuPluralAllowsDifferentCldrCategories()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "message": "{count, plural, one {# file} other {# files}}" }
            """);
        workspace.Write("ar.json", """
            { "message": "{count, plural, zero {لا ملفات} one {ملف واحد} two {ملفان} few {# ملفات} many {# ملفاً} other {# ملف}}" }
            """);

        var result = workspace.Lint();

        AssertNoPlaceholderFindings(result);
    }

    [Fact]
    public void IcuPluralExtraCategoryWithNestedPlaceholder_IsAllowed()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "message": "{count, plural, one {# file for {name}} other {# files for {name}}}" }
            """);
        workspace.Write("ar.json", """
            { "message": "{count, plural, zero {لا ملفات لـ {name}} one {# ملف لـ {name}} two {ملفان لـ {name}} few {# ملفات لـ {name}} many {# ملفاً لـ {name}} other {# ملف لـ {name}}}" }
            """);

        var result = workspace.Lint();

        AssertNoPlaceholderFindings(result);
    }

    [Fact]
    public void IcuNestedPlaceholderMissing_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "message": "{count, plural, one {# file for {name}} other {# files for {name}}}" }
            """);
        workspace.Write("ar.json", """
            { "message": "{count, plural, one {ملف} other {# ملفات}}" }
            """);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void IcuSelectRequiresTheSameBranches()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "message": "{gender, select, male {His} female {Her} other {Their}}" }
            """);
        workspace.Write("ar.json", """
            { "message": "{gender, select, male {له} other {لهم}}" }
            """);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void IcuApostropheEscaping_DoesNotCreatePlaceholders()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "message": "{count, plural, one {'{'# file'}'} other {'{'# files'}'}}" }
            """);
        workspace.Write("ar.json", """
            { "message": "{count, plural, one {'{'# ملف'}'} other {'{'# ملفات'}'}}" }
            """);

        var result = workspace.Lint();

        AssertNoPlaceholderFindings(result);
    }

    [Fact]
    public void IcuBranchTextStillGetsBidiAnalysis()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "message": "{count, plural, one {# file} other {# files}}" }
            """);
        workspace.Write("ar.json", """
            { "message": "{count, plural, one {# ملف} other {# Cancel}}" }
            """);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void NextcloudBraceTagsAndPercentBeforeClosingTag_AreMasked()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "message": "You are using {s}{usage}{/s} of {s}{totalSpace}{/s} ({s}{usageRelative}%{/s})" }
            """);
        workspace.Write("ar.json", """
            { "message": "أنت تستعمل {s}{usage}{/s} من {s}{totalSpace}{/s} ({s}{usageRelative}%{/s})" }
            """);

        var result = workspace.Lint();

        AssertNoPlaceholderFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void I18NextSpacing_IsIgnoredButMissingNamesAreReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "Hello {{name}}" }""");
        workspace.Write("ar.json", """{ "message": "مرحبا {{ name }}" }""");

        AssertNoPlaceholderFindings(workspace.Lint());

        using var missing = new TestWorkspace();
        missing.Write("en.json", """{ "message": "Hello {{name}}" }""");
        missing.Write("ar.json", """{ "message": "مرحبا" }""");

        Assert.Contains(missing.Lint().Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void RailsAndJavaScriptPlaceholders_AreCompared()
    {
        using var rails = new TestWorkspace();
        rails.Write("en.json", """{ "message": "Hello %{name}" }""");
        rails.Write("ar.json", """{ "message": "مرحبا %{name}" }""");
        AssertNoPlaceholderFindings(rails.Lint());

        using var javascript = new TestWorkspace();
        javascript.Write("en.json", """{ "message": "Hello ${name}" }""");
        javascript.Write("ar.json", """{ "message": "مرحبا ${name}" }""");
        AssertNoPlaceholderFindings(javascript.Lint());
    }

    [Fact]
    public void UnclosedPlaceholder_IsAPlaceholderSyntaxFinding()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "{0" }""");
        workspace.Write("ar.json", """{ "message": "{0" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderSyntax);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void PlaceholderSyntaxCanBeDisabled()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "message": "{0" }""");
        workspace.Write("ar.json", """{ "message": "{0" }""");
        workspace.Write("bidilint.json", """
            { "rules": { "PlaceholderSyntax": "off" } }
            """);

        var (code, stdout, stderr) = workspace.RunCli(".", "--format", "json");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(0, document.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Theory]
    [InlineData("dotnet", "{0}")]
    [InlineData("icu", "{count, plural, one {#} other {#}}")]
    [InlineData("printf", "%s")]
    [InlineData("i18next", "{{name}}")]
    [InlineData("angular", "{{ name }}")]
    [InlineData("rails", "%{name}")]
    [InlineData("javascript", "${name}")]
    [InlineData("mustache", "{{name}}")]
    public void ExplicitPlaceholderDialectsAreAccepted(string dialect, string message)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", "{\"message\":" + JsonSerializer.Serialize(message) + "}");
        workspace.Write("ar.json", "{\"message\":" + JsonSerializer.Serialize(message) + "}");
        workspace.Write("bidilint.json", $$"""
            { "placeholderDialects": ["{{dialect}}"] }
            """);

        var (code, stdout, stderr) = workspace.RunCli(".", "--format", "json");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(0, document.RootElement.GetProperty("findings").GetArrayLength());
    }

    private static void AssertNoPlaceholderFindings(LintResult result) =>
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch
                or DiagnosticCodes.PlaceholderSyntax
                or DiagnosticCodes.PlaceholderType);
}
