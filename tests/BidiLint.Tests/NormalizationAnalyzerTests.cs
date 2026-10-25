using Xunit;

namespace BidiLint.Tests;

public class NormalizationAnalyzerTests
{
    [Fact]
    public void ArabicPresentationForm_IsAWarningWithAnExactSpan()
    {
        const string value = "نص \uFEFB هنا";
        var finding = Assert.Single(Analyze(value), item =>
            item.Code == DiagnosticCodes.PresentationForm);

        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal(value.IndexOf('\uFEFB'), finding.Start);
        Assert.Equal(1, finding.Length);
        Assert.Contains("U+FEFB", finding.Message, StringComparison.Ordinal);
        Assert.Null(finding.Bidi);
    }

    [Fact]
    public void PresentationFormInMarkup_IsIgnored()
    {
        const string value = "نص <span title=\"\uFEFB\">حرف</span>";
        Assert.DoesNotContain(
            Analyze(value),
            item => item.Code == DiagnosticCodes.PresentationForm);
    }

    [Fact]
    public void NonNfcTextInMarkupAndPlaceholders_IsIgnored()
    {
        Assert.DoesNotContain(
            Analyze("نص <span title=\"Cafe\u0301\">حرف</span>"),
            item => item.Code == DiagnosticCodes.Normalization);
        Assert.DoesNotContain(
            Analyze("قيمة {name Cafe\u0301}"),
            item => item.Code == DiagnosticCodes.Normalization);
    }

    [Fact]
    public void NonNfcText_IsAWarningThatNamesTheNormalizedForm()
    {
        const string value = "Cafe\u0301";
        var finding = Assert.Single(Analyze(value), item =>
            item.Code == DiagnosticCodes.Normalization);

        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal(3, finding.Start);
        Assert.Equal(2, finding.Length);
        Assert.Contains("\"e\\u{0301}\"", finding.Message, StringComparison.Ordinal);
        Assert.Contains("\"é\"", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MixedComposedAndDecomposedText_IsExplained()
    {
        const string value = "Café Cafe\u0301";
        var finding = Assert.Single(Analyze(value), item =>
            item.Code == DiagnosticCodes.Normalization);

        Assert.Contains("mixes composed and decomposed", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NfcText_IsClean()
    {
        Assert.DoesNotContain(
            Analyze("Café"),
            item => item.Code == DiagnosticCodes.Normalization);
    }

    [Fact]
    public void Tatweel_IsAWarningWithTheRunSpan()
    {
        const string value = "مرحـبا";
        var finding = Assert.Single(Analyze(value), item =>
            item.Code == DiagnosticCodes.Tatweel);

        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Equal(3, finding.Start);
        Assert.Equal(1, finding.Length);
        Assert.Contains("U+0640", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TatweelInsideAPlaceholder_IsIgnored()
    {
        Assert.DoesNotContain(
            Analyze("قيمة {nameـ}"),
            item => item.Code == DiagnosticCodes.Tatweel);
    }

    [Fact]
    public void MightContainFindings_IsTrueForNormalizationCandidates()
    {
        Assert.True(NormalizationAnalyzer.MightContainFindings("Cafe\u0301"));
        Assert.True(NormalizationAnalyzer.MightContainFindings("\uFEFB"));
        Assert.True(NormalizationAnalyzer.MightContainFindings("مرحـبا"));
        Assert.False(NormalizationAnalyzer.MightContainFindings("Café"));
    }

    private static IReadOnlyList<ValueFinding> Analyze(string value)
    {
        var content = ContentMask.Build(
            value,
            LintConfig.Default.IsolationWrappers,
            out _);
        return NormalizationAnalyzer.Analyze(value, content);
    }
}
