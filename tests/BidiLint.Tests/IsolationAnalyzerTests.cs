using Xunit;

namespace BidiLint.Tests;

public class IsolationAnalyzerTests
{
    [Theory]
    [InlineData("زر https://example.com/a?b=1 الآن", "https://example.com/a?b=1")]
    [InlineData("راسل support@example.com الآن", "support@example.com")]
    [InlineData("افتح D:\\app\\bin\\tool.exe الآن", "D:\\app\\bin\\tool.exe")]
    [InlineData("استخدم std::vector الآن", "std::vector")]
    [InlineData("الإصدار v2.1.3 متاح", "v2.1.3")]
    [InlineData("افتح README.md الآن", "README.md")]
    [InlineData("مبني باستخدام Visual Studio الآن", "Visual Studio")]
    [InlineData("مبني باستخدام C# الآن", "C#")]
    [InlineData("مبني باستخدام .NET الآن", ".NET")]
    public void LatinForms_AreFlaggedWithoutIsolation(string value, string expectedRun)
    {
        var findings = Analyze(value);

        var finding = Assert.Single(
            findings,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
        Assert.Contains($"\"{expectedRun}\"", finding.Message, StringComparison.Ordinal);
        Assert.True(finding.Length > 0);
    }

    [Theory]
    [InlineData("نص \u2066ASP.NET Core\u2069 هنا")]
    [InlineData("نص \u2068ASP.NET Core\u2069 هنا")]
    [InlineData("نص <bdi>ASP.NET Core</bdi> هنا")]
    [InlineData("نص <bdi dir=\"ltr\">ASP.NET Core</bdi> هنا")]
    [InlineData("نص <bdi dir=\"auto\">ASP.NET Core</bdi> هنا")]
    [InlineData("نص <code>ASP.NET Core</code> هنا")]
    [InlineData("نص <code dir=\"ltr\">ASP.NET Core</code> هنا")]
    [InlineData("نص <span dir=\"ltr\">ASP.NET Core</span> هنا")]
    [InlineData("نص <span dir='ltr'>ASP.NET Core</span> هنا")]
    [InlineData("نص <SPAN DIR=\"LTR\">ASP.NET Core</SPAN> هنا")]
    [InlineData("نص <div dir=\"ltr\">ASP.NET Core</div> هنا")]
    public void CorrectMitigation_IsNotFlagged(string value)
    {
        var findings = Analyze(value);

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void ConfiguredIsolationWrapper_IsNotFlagged()
    {
        var config = new LintConfig
        {
            IsolationWrappers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "x-bidi",
            },
        };

        var findings = Analyze(
            "نص <x-bidi>ASP.NET Core</x-bidi> هنا",
            config: config);

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void Rli_IsTheWrongIsolateDirection()
    {
        var finding = Assert.Single(
            Analyze("نص \u2067ASP.NET Core\u2069 هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("RLI", finding.Message, StringComparison.Ordinal);
        Assert.Contains("right-to-left", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FsiThatStartsWithRtlText_IsTheWrongDirection()
    {
        var finding = Assert.Single(
            Analyze("نص \u2068مرحبا ASP.NET Core\u2069 هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("FSI", finding.Message, StringComparison.Ordinal);
        Assert.Contains("automatic direction", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BdiThatStartsWithRtlText_IsTheWrongDirection()
    {
        var finding = Assert.Single(
            Analyze("نص <bdi>مرحبا ASP.NET Core</bdi> هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("<bdi>", finding.Message, StringComparison.Ordinal);
        Assert.Contains("automatic direction", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("نص <code dir=\"rtl\">ASP.NET Core</code> هنا", "<code dir=\"rtl\">")]
    [InlineData("نص <bdi dir=\"rtl\">ASP.NET Core</bdi> هنا", "<bdi dir=\"rtl\">")]
    [InlineData("نص <span dir=\"rtl\">ASP.NET Core</span> هنا", "<span dir=\"rtl\">")]
    [InlineData("نص \u2067ASP.NET Core\u2069 هنا", "RLI")]
    public void ExplicitRtlDirection_IsReportedWithContext(string value, string expected)
    {
        var finding = Assert.Single(
            Analyze(value),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains(expected, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInnerRtlDirectionOverridesAnOuterLtrWrapper()
    {
        var finding = Assert.Single(
            Analyze("نص <span dir=\"ltr\"><span dir=\"rtl\">ASP.NET Core</span></span> هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("<span dir=\"rtl\">", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredRtlWrapper_IsReported()
    {
        var config = new LintConfig
        {
            IsolationWrappers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "x-bidi",
            },
        };

        var finding = Assert.Single(
            Analyze(
                "نص <x-bidi dir=\"rtl\">ASP.NET Core</x-bidi> هنا",
                config: config),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("<x-bidi dir=\"rtl\">", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossingAPdiBoundary_IsReported()
    {
        var finding = Assert.Single(
            Analyze("نص \u2066ASP\u2069.NET هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("crosses", finding.Message, StringComparison.Ordinal);
        Assert.Contains("PDI", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2069", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossingAnOpeningBoundary_IsReported()
    {
        var finding = Assert.Single(
            Analyze("نص ASP\u2066.NET\u2069 هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("crosses", finding.Message, StringComparison.Ordinal);
        Assert.Contains("LRI", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossingAnInnerBoundaryInsideAnOuterLtrIsolate_IsNotReported()
    {
        var findings = Analyze("نص \u2066ASP\u2066.NET\u2069\u2069 هنا");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Theory]
    [InlineData('\u202A', "LRE")]
    [InlineData('\u202B', "RLE")]
    [InlineData('\u202D', "LRO")]
    [InlineData('\u202E', "RLO")]
    public void UnclosedLegacyControl_IsReported(char control, string name)
    {
        var finding = Assert.Single(
            Analyze($"نص {control}abc"),
            item => item.Code == DiagnosticCodes.BidiControl);

        Assert.Contains($"Unclosed {name}", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StrayPdf_IsReported()
    {
        var finding = Assert.Single(
            Analyze("نص \u202C"),
            item => item.Code == DiagnosticCodes.BidiControl);

        Assert.Contains("Stray PDF", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfInsideAnIsolate_IsReportedAsIneffective()
    {
        var finding = Assert.Single(
            Analyze("نص \u2066ASP\u202C\u2069"),
            item => item.Code == DiagnosticCodes.BidiControl);

        Assert.Contains("ignored while an isolate is open", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BalancedLegacyControlOutsideAnIsolate_IsNotReported()
    {
        var findings = Analyze("نص \u202AABC\u202C هنا");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.BidiControl);
    }

    [Theory]
    [InlineData('\u2066', "LRI")]
    [InlineData('\u2067', "RLI")]
    [InlineData('\u2068', "FSI")]
    public void UnclosedIsolate_IsReportedAtTheOpener(char control, string name)
    {
        var finding = Assert.Single(
            Analyze($"نص {control}ASP.NET"),
            item => item.Code == DiagnosticCodes.UnbalancedIsolates);

        Assert.Contains($"Unclosed {name}", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StrayPdi_IsReportedAtTheCloser()
    {
        var finding = Assert.Single(
            Analyze("نص \u2069"),
            item => item.Code == DiagnosticCodes.UnbalancedIsolates);

        Assert.Contains("Stray PDI", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyIsolate_IsReported()
    {
        var finding = Assert.Single(
            Analyze("نص \u2066 \u2069"),
            item => item.Code == DiagnosticCodes.IsolateDepth);

        Assert.Contains("Empty", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcessiveIsolateDepth_IsReported()
    {
        var value = new string(BidiScanner.Lri, 17) + "x" + new string(BidiScanner.Pdi, 17);

        var finding = Assert.Single(
            Analyze(value),
            item => item.Code == DiagnosticCodes.IsolateDepth);

        Assert.Contains("17 levels", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OverflowingTheUnicodeDepthLimit_IsReported()
    {
        var value = new string(BidiScanner.Lri, 126) +
                    "x" +
                    new string(BidiScanner.Pdi, 126);

        var finding = Assert.Single(
            Analyze(value),
            item => item.Code == DiagnosticCodes.BidiControl);

        Assert.Contains("125 levels", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectionalMarks_DoNotCreateIsolationFindings()
    {
        var findings = Analyze("نص \u200F\u200E\u061C هنا");

        Assert.DoesNotContain(
            findings,
            item => item.Code is DiagnosticCodes.BidiControl
                or DiagnosticCodes.UnisolatedLatin
                or DiagnosticCodes.IsolateDepth
                or DiagnosticCodes.UnbalancedIsolates);
    }

    [Fact]
    public void AdjacentNumbers_InRtlText_AreReported()
    {
        var finding = Assert.Single(
            Analyze("نص 1 2 3 هنا"),
            item => item.Code == DiagnosticCodes.NumberAdjacency);

        Assert.Contains("1, 2, 3", finding.Message, StringComparison.Ordinal);
        Assert.Contains("3, 2, 1", finding.Message, StringComparison.Ordinal);
        Assert.Contains("RTL", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NumbersSeparatedByRtlWords_AreNotReported()
    {
        var findings = Analyze("نص 1 من 2 هنا");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.NumberAdjacency);
    }

    [Fact]
    public void IsolatedNumbers_AreNotReported()
    {
        var findings = Analyze("نص \u20661 2 3\u2069 هنا");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.NumberAdjacency);
    }

    [Fact]
    public void PlaceholdersBetweenNumbers_AreNotReported()
    {
        var findings = Analyze("نص {0} {1} هنا");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.NumberAdjacency);
    }

    [Fact]
    public void ADateRange_IsOneNumberToken()
    {
        var findings = Analyze("نص 2024-2025 هنا");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.NumberAdjacency);
    }

    [Fact]
    public void AdjacentArabicIndicNumbers_AreReported()
    {
        var finding = Assert.Single(
            Analyze("نص ١ ٢ هنا"),
            item => item.Code == DiagnosticCodes.NumberAdjacency);

        Assert.Contains("١, ٢", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PureLatinValueInAnRtlCulture_IsNotUnisolatedLatin()
    {
        var findings = Analyze("ASP.NET Core", culture: "ar");

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Theory]
    [InlineData("نص <code>1 2</code> هنا")]
    [InlineData("نص <bdi>1 2</bdi> هنا")]
    [InlineData("نص <span dir=\"ltr\">1 2</span> هنا")]
    public void NumbersInsideAnLtrWrapper_AreNotReported(string value)
    {
        var findings = Analyze(value);

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.NumberAdjacency);
    }

    [Fact]
    public void DirTextInsideAnotherAttribute_IsNotTreatedAsAWrapper()
    {
        var finding = Assert.Single(
            Analyze("نص <span title=\"dir=ltr\">ASP.NET Core</span> هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("ASP.NET Core", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkupInsideAPlaceholder_DoesNotIsolateVisibleText()
    {
        var finding = Assert.Single(
            Analyze("نص {<bdi>}ASP.NET Core</bdi> هنا"),
            item => item.Code == DiagnosticCodes.UnisolatedLatin);

        Assert.Contains("ASP.NET Core", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowedLatinTerm_IsNotReported()
    {
        var config = new LintConfig
        {
            AllowLatin = new[]
            {
                new AllowedLatinTerm("ASP.NET Core", false, "Product name", new HashSet<string>()),
            },
        };

        var findings = Analyze("نص ASP.NET Core هنا", config: config);

        Assert.DoesNotContain(
            findings,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
    }

    private static IReadOnlyList<ValueFinding> Analyze(
        string value,
        string culture = "ar",
        LintConfig? config = null)
    {
        config ??= LintConfig.Default;
        var scan = BidiScanner.Scan(value);
        return IsolationAnalyzer.Analyze(
            value,
            culture,
            Linter.IsRtl(culture, config),
            config,
            scan);
    }
}
