using System.Text.Json;
using Xunit;

namespace BidiLint.Tests;

public class LinterTests
{
    [Fact]
    public void NestedKeys_FlattenToDottedPaths()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "checkout": { "payNow": "Pay now" } }""");

        var load = workspace.Load();

        Assert.True(load.Documents.Single().Entries.ContainsKey("checkout.payNow"));
    }

    [Fact]
    public void NonCultureJson_IsIgnored()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("package.json", """{ "name": "x" }""");
        workspace.Write("app.json", """{ "name": "x" }""");
        workspace.Write("en.json", """{ "a": "A" }""");

        var load = workspace.Load();

        Assert.Single(load.Documents);
        Assert.Equal("en", load.Documents[0].Culture);
    }

    [Fact]
    public void MalformedJson_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A" }""");
        workspace.Write("ar.json", """{ "a": "أ" """);

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics, d => d.Code == DiagnosticCodes.MalformedFile);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("ar", diagnostic.Culture);
        Assert.NotNull(diagnostic.Span);
    }

    [Fact]
    public void DuplicateJsonKey_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A", "a": "B" }""");

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics, d => d.Code == DiagnosticCodes.DuplicateKey);
        Assert.Equal("a", diagnostic.Key);
        Assert.Equal("A", load.Documents.Single().Entries["a"].Value);
    }

    [Fact]
    public void FlatteningCollision_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a.b": "literal", "a": { "b": "nested" } }""");

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics, d => d.Code == DiagnosticCodes.KeyCollision);
        Assert.Equal("a.b", diagnostic.Key);
        Assert.Equal("literal", load.Documents.Single().Entries["a.b"].Value);
    }

    [Fact]
    public void MissingKey_IsAnErrorAndLinksToBase()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("ar.json", """{ "a": "أ" }""");

        var result = workspace.Lint();
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == DiagnosticCodes.MissingKey);

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("b", diagnostic.Key);
        Assert.NotNull(diagnostic.Related);
        Assert.Contains("en.json", diagnostic.Related![0].FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtraKey_IsAWarning()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A" }""");
        workspace.Write("ar.json", """{ "a": "أ", "z": "ز" }""");

        var result = workspace.Lint();
        var diagnostic = Assert.Single(result.Diagnostics, d => d.Code == DiagnosticCodes.ExtraKey);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void PlaceholderMismatch_IsAnError()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "d": "Save {0}% for {1} days" }""");
        workspace.Write("ar.json", """{ "d": "وفّر {0}% لفترة محدودة" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d =>
            d.Code == DiagnosticCodes.PlaceholderMismatch &&
            d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void EmptyValue_IsAWarning()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "A" }""");
        workspace.Write("ar.json", """{ "a": "" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.EmptyValue);
    }

    [Fact]
    public void UnisolatedLatin_InArabic_IsFlagged()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "Built with ASP.NET Core" }""");
        workspace.Write("ar.json", """{ "s": "مبني باستخدام ASP.NET Core الحديث" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void HebrewBidi_IsChecked()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "Built with ASP.NET Core" }""");
        workspace.Write("he.json", """{ "s": "נבנה עם ASP.NET Core" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.UnisolatedLatin && d.Culture == "he");
    }

    [Fact]
    public void RtlContentInBaseCulture_IsStillChecked()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "مبني باستخدام ASP.NET Core" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void SupplementaryRtlContentInBaseCulture_IsStillChecked()
    {
        using var workspace = new TestWorkspace();
        var adlam = char.ConvertFromUtf32(0x1E900);
        workspace.Write("en.json", "{ \"s\": \"Hello " + adlam + "\" }");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void YiddishBidi_IsChecked()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "n": "5" }""");
        workspace.Write("yi.json", """{ "n": "٥ 5" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d =>
            d.Code == DiagnosticCodes.MixedNumerals && d.Culture == "yi");
    }

    [Fact]
    public void AzArabBidi_IsChecked()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "n": "5" }""");
        workspace.Write("az-Arab.json", """{ "n": "٥ 5" }""");

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, d =>
            d.Code == DiagnosticCodes.MixedNumerals && d.Culture == "az-Arab");
    }

    [Fact]
    public void KuLatn_IsNotTreatedAsRtl()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "n": "5" }""");
        workspace.Write("ku-Latn.json", """{ "n": "٥ 5" }""");

        var result = workspace.Lint();

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == DiagnosticCodes.MixedNumerals);
    }

    [Fact]
    public void LatinProblems_InEnglish_AreNotBidiFindings()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "ASP.NET Core" }""");
        workspace.Write("ar.json", """{ "s": "ASP.NET Core" }""");

        var result = workspace.Lint();

        Assert.DoesNotContain(result.Diagnostics, d => d.Code == DiagnosticCodes.UnisolatedLatin);
    }

    [Fact]
    public void CleanPair_HasNoFindings()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "Home", "n": "{0} products" }""");
        workspace.Write("ar.json", """{ "a": "الرئيسية", "n": "عدد المنتجات {0}" }""");

        var result = workspace.Lint();

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void MixedNumerals_UsesTheExactJsonDigitSpan()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "n": "Value" }""");
        var ar = """{ "n": "قيمة ٥ و 12" }""";
        workspace.Write("ar.json", ar);

        var result = workspace.Lint();

        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.MixedNumerals);
        Assert.Equal(ar.IndexOf('٥'), diagnostic.Span!.Value.Start.Offset);
        Assert.Equal(ar.IndexOf("12", StringComparison.Ordinal) + 2, diagnostic.Span.Value.End.Offset);
        Assert.Contains("Western digits", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("Arabic-Indic digits", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtendedArabicIndicPolicy_IsAppliedFromConfig()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "n": "Value" }""");
        workspace.Write("ar.json", """{ "n": "قیمت ۵" }""");
        var configPath = workspace.Write("bidilint.json", """
            { "numeralPolicy": "extended-arabic-indic" }
            """);

        var resolution = ConfigLoader.Load(configPath);
        var result = Linter.Lint(workspace.Load(), "en", resolution.Config);

        Assert.Empty(resolution.Errors);
        Assert.Equal(NumeralPolicy.ExtendedArabicIndic, resolution.Config.NumeralPolicy);
        Assert.DoesNotContain(result.Diagnostics, item => item.Code == DiagnosticCodes.MixedNumerals);
    }

    [Fact]
    public void NormalizationFinding_UsesTheExactJsonValueSpan()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "Plain" }""");
        var ar = "{\"a\":\"Cafe\u0301\"}";
        workspace.Write("ar.json", ar);

        var result = workspace.Lint();

        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.Normalization);
        var start = ar.IndexOf("e\u0301", StringComparison.Ordinal);
        Assert.Equal(start, diagnostic.Span!.Value.Start.Offset);
        Assert.Equal(start + 2, diagnostic.Span.Value.End.Offset);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void TatweelFinding_UsesTheExactJsonValueSpan()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "Plain" }""");
        var ar = """{ "a": "مرحـبا" }""";
        workspace.Write("ar.json", ar);

        var result = workspace.Lint();

        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.Tatweel);
        Assert.Equal(ar.IndexOf('ـ'), diagnostic.Span!.Value.Start.Offset);
        Assert.Equal(1, diagnostic.Span.Value.End.Offset - diagnostic.Span.Value.Start.Offset);
    }

    [Fact]
    public void NormalizationRuleSeverityAndTatweelDisable_AreApplied()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "a": "Plain" }""");
        workspace.Write("ar.json", """{ "a": "Cafe\u0301 مرحـبا" }""");
        var configPath = workspace.Write("bidilint.json", """
            {
              "rules": {
                "Normalization": "error",
                "Tatweel": "off"
              }
            }
            """);

        var resolution = ConfigLoader.Load(configPath);
        var (_, stdout, _) = workspace.RunCli(".", "--format", "json");
        using var document = JsonDocument.Parse(stdout);
        var findings = document.RootElement.GetProperty("findings").EnumerateArray().ToList();

        var normalization = Assert.Single(
            findings,
            item => item.GetProperty("code").GetString() == DiagnosticCodes.Normalization);
        Assert.Equal("error", normalization.GetProperty("severity").GetString());
        Assert.DoesNotContain(
            findings,
            item => item.GetProperty("code").GetString() == DiagnosticCodes.Tatweel);
        Assert.Empty(resolution.Errors);
    }

    [Fact]
    public void UnisolatedLatin_UsesTheExactJsonValueSpan()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "Text" }""");
        var ar = """{ "s": "نص ASP.NET هنا" }""";
        workspace.Write("ar.json", ar);

        var result = workspace.Lint();

        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
        var rawStart = ar.IndexOf("ASP.NET", StringComparison.Ordinal);
        Assert.Equal(rawStart, diagnostic.Span!.Value.Start.Offset);
        Assert.Equal(rawStart + "ASP.NET".Length, diagnostic.Span.Value.End.Offset);
    }

    [Fact]
    public void BidiControlFinding_UsesTheExactEscapedJsonSpan()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "Text" }""");
        var ar = """{ "s": "نص \u2066abc" }""";
        workspace.Write("ar.json", ar);

        var result = workspace.Lint();

        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.UnbalancedIsolates);
        var rawStart = ar.IndexOf("\\u2066", StringComparison.Ordinal);
        Assert.Equal(rawStart, diagnostic.Span!.Value.Start.Offset);
        Assert.Equal(6, diagnostic.Span.Value.End.Offset - rawStart);
    }

    [Fact]
    public void BidiControlFinding_UsesTheExactEscapedAppleStringsSpan()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Resources/en.lproj/Localizable.strings", """
            "s" = "Text";
            """);
        var ar = """
            "s" = "\U0646\U0635 \U202Eabc";
            """;
        workspace.Write("Resources/ar.lproj/Localizable.strings", ar);

        var result = workspace.Lint();

        var diagnostic = Assert.Single(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.BidiControl);
        var rawStart = ar.IndexOf("\\U202E", StringComparison.Ordinal);
        Assert.Equal(rawStart, diagnostic.Span!.Value.Start.Offset);
        Assert.Equal(6, diagnostic.Span.Value.End.Offset - rawStart);
    }

    [Fact]
    public void EnglishValueWithAnUnclosedIsolate_IsStillReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "Text \u2066value" }""");

        var result = workspace.Lint();

        Assert.Contains(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.UnbalancedIsolates);
    }

    [Fact]
    public void ConfiguredIsolationWrapper_IsAppliedByTheLinter()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "s": "Text" }""");
        workspace.Write("ar.json", """{ "s": "نص <x-bidi>ASP.NET</x-bidi> هنا" }""");
        var config = new LintConfig
        {
            IsolationWrappers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "x-bidi",
            },
        };

        var result = Linter.Lint(workspace.Load(), "en", config);

        Assert.DoesNotContain(
            result.Diagnostics,
            item => item.Code == DiagnosticCodes.UnisolatedLatin);
    }
}
