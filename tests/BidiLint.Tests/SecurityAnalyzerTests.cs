using Xunit;

namespace BidiLint.Tests;

public class SecurityAnalyzerTests
{
    [Theory]
    [InlineData("\u202Dabc\u202C", "LRO")]
    [InlineData("\u202Eabc\u202C", "RLO")]
    public void OverrideControls_AreReportedEvenWhenBalanced(string value, string name)
    {
        var finding = Assert.Single(
            Analyze(value),
            item => item.Code == DiagnosticCodes.BidiOverride);

        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
        Assert.Contains(name, finding.Message, StringComparison.Ordinal);
        Assert.Contains("Trojan Source", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BalancedEmbedding_IsNotABidiOverride()
    {
        var findings = Analyze("\u202Aabc\u202C");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.BidiOverride);
    }

    [Fact]
    public void MidStringBom_IsReportedAtItsExactOffset()
    {
        var finding = Assert.Single(
            Analyze("ab\uFEFFcd"),
            item => item.Code == DiagnosticCodes.InvisibleControl);

        Assert.Equal(2, finding.Start);
        Assert.Equal(1, finding.Length);
        Assert.Contains("U+FEFF", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData('\u0000')]
    [InlineData('\u0007')]
    [InlineData('\u001B')]
    [InlineData('\u007F')]
    [InlineData('\u0085')]
    public void DisallowedControlCharacters_AreReported(char control)
    {
        var finding = Assert.Single(
            Analyze($"a{control}b"),
            item => item.Code == DiagnosticCodes.InvisibleControl);

        Assert.Contains($"U+{(int)control:X4}", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TabsAndLineBreaks_AreAllowed()
    {
        var findings = Analyze("a\tb\nc\r\nd");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.InvisibleControl);
    }

    [Theory]
    [InlineData("می\u200Cخواهم")]
    [InlineData("می\u200Dخواهم")]
    [InlineData("ب\u200Cه")]
    [InlineData("क्\u200Dष")]
    [InlineData("👨\u200D👩")]
    public void JoinersInLegitimateShapingContext_AreNotReported(string value)
    {
        var findings = Analyze(value);

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.InvisibleControl);
    }

    [Theory]
    [InlineData("a\u200Db")]
    [InlineData("a\u200Cb")]
    [InlineData(" \u200D ")]
    [InlineData(" \u200C ")]
    [InlineData("👨\u200C👩")]
    public void JoinersWithoutALegitimateContext_AreReported(string value)
    {
        var finding = Assert.Single(
            Analyze(value),
            item => item.Code == DiagnosticCodes.InvisibleControl);

        Assert.Contains("U+200", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\u200E")]
    [InlineData("abc\u200E")]
    [InlineData("abc \u200E def")]
    public void IneffectiveDirectionalMarks_AreReported(string value)
    {
        var finding = Assert.Single(
            Analyze(value),
            item => item.Code == DiagnosticCodes.InvisibleControl);

        Assert.Contains("does not change", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EffectiveDirectionalMark_IsNotReported()
    {
        var findings = Analyze("abc \u200F 1");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.InvisibleControl);
    }

    [Fact]
    public void ControlsInsideMarkupAndPlaceholders_AreNotReported()
    {
        var findings = Analyze("<b title=\"\u202E\uFEFF\u200D\">visible</b> {name\u0000}");

        Assert.DoesNotContain(
            findings,
            item => item.Code is DiagnosticCodes.BidiOverride
                or DiagnosticCodes.InvisibleControl);
    }

    private static IReadOnlyList<ValueFinding> Analyze(string value, bool rtlCulture = false)
    {
        var content = ContentMask.Build(
            value,
            LintConfig.Default.IsolationWrappers,
            out _);
        return SecurityAnalyzer.Analyze(content, rtlCulture);
    }
}
