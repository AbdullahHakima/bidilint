using Xunit;

namespace BidiLint.Tests;

public class NumeralAnalyzerTests
{
    [Fact]
    public void MixedPolicy_ReportsEverySystemAndTheVisibleDigitSpan()
    {
        const string value = "عدد ٥ و 5 و ۵ منتج";
        var finding = Assert.Single(Analyze(value, NumeralPolicy.Mixed));

        Assert.Equal(DiagnosticCodes.MixedNumerals, finding.Code);
        Assert.Equal(DiagnosticSeverity.Warning, finding.Severity);
        Assert.Contains("Western digits (0-9)", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Arabic-Indic digits (٠-٩)", finding.Message, StringComparison.Ordinal);
        Assert.Contains(
            "Extended Arabic-Indic digits (۰-۹)",
            finding.Message,
            StringComparison.Ordinal);
        Assert.Equal(value.IndexOf('٥'), finding.Start);
        Assert.Equal(value.LastIndexOf('۵') + 1 - finding.Start, finding.Length);
        Assert.Null(finding.Bidi);
    }

    [Theory]
    [InlineData("قيمة {0} و ١", "mixed")]
    [InlineData("<span data-count=\"12\">قيمة ١</span>", "mixed")]
    [InlineData("قيمة {0}", "western")]
    [InlineData("قيمة {0}", "arabic-indic")]
    [InlineData("قيمة {0}", "extended-arabic-indic")]
    public void MaskedMarkupAndPlaceholders_DoNotCreateDigitSystems(
        string value,
        string policyName)
    {
        Assert.Empty(Analyze(value, ParsePolicy(policyName)));
    }

    [Fact]
    public void WesternPolicy_FlagsArabicAndExtendedDigits()
    {
        var finding = Assert.Single(Analyze("قيمة ٥ و ۵", NumeralPolicy.Western));

        Assert.Contains("Numeral policy is Western", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Arabic-Indic digits", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Extended Arabic-Indic digits", finding.Message, StringComparison.Ordinal);
        Assert.Contains("use Western digits (0-9)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArabicIndicPolicy_FlagsExtendedDigits()
    {
        var finding = Assert.Single(Analyze("قیمت ۵", NumeralPolicy.ArabicIndic));

        Assert.Contains("Numeral policy is Arabic-Indic", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Extended Arabic-Indic digits", finding.Message, StringComparison.Ordinal);
        Assert.Contains("use Arabic-Indic digits (٠-٩)", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtendedArabicIndicPolicy_AllowsExtendedDigits()
    {
        Assert.Empty(Analyze("قیمت ۵۶", NumeralPolicy.ExtendedArabicIndic));
    }

    [Fact]
    public void AllowPolicy_DisablesTheCheck()
    {
        Assert.Empty(Analyze("عدد ٥ و 5 و ۵", NumeralPolicy.Allow));
    }

    [Fact]
    public void MixedPolicy_DoesNotTreatOneSystemAsMixed()
    {
        Assert.Empty(Analyze("قيمة ١٢٣", NumeralPolicy.Mixed));
        Assert.Empty(Analyze("value 123", NumeralPolicy.Mixed));
        Assert.Empty(Analyze("قیمت ۱۲۳", NumeralPolicy.Mixed));
    }

    private static IReadOnlyList<ValueFinding> Analyze(string value, NumeralPolicy policy)
    {
        var content = ContentMask.Build(
            value,
            LintConfig.Default.IsolationWrappers,
            out _);
        return NumeralAnalyzer.Analyze(content, policy);
    }

    private static NumeralPolicy ParsePolicy(string policy) => policy switch
    {
        "allow" => NumeralPolicy.Allow,
        "western" => NumeralPolicy.Western,
        "arabic-indic" => NumeralPolicy.ArabicIndic,
        "extended-arabic-indic" => NumeralPolicy.ExtendedArabicIndic,
        _ => NumeralPolicy.Mixed,
    };
}
