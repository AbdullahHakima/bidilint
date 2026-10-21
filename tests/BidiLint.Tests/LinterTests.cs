using BidiLint;
using Xunit;

namespace BidiLint.Tests;

public class LinterTests : IDisposable
{
    private readonly string _root;

    public LinterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "bidilint-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private LintResult LintPair(string enJson, string arJson)
    {
        File.WriteAllText(Path.Combine(_root, "en.json"), enJson);
        File.WriteAllText(Path.Combine(_root, "ar.json"), arJson);
        var files = LocaleLoader.LoadFolder(_root);
        var baseFile = files.Single(f => f.Culture == "en");
        return Linter.Lint(files, baseFile);
    }

    [Fact]
    public void NestedKeys_FlattenToDottedPaths()
    {
        File.WriteAllText(Path.Combine(_root, "en.json"), """{ "checkout": { "payNow": "Pay now" } }""");
        var files = LocaleLoader.LoadFolder(_root);
        Assert.True(files.Single().Entries.ContainsKey("checkout.payNow"));
    }

    [Fact]
    public void NonCultureJson_IsIgnored()
    {
        File.WriteAllText(Path.Combine(_root, "package.json"), """{ "name": "x" }""");
        File.WriteAllText(Path.Combine(_root, "en.json"), """{ "a": "A" }""");
        var files = LocaleLoader.LoadFolder(_root);
        Assert.Single(files);
    }

    [Fact]
    public void MissingKey_IsAnError()
    {
        var result = LintPair("""{ "a": "A", "b": "B" }""", """{ "a": "أ" }""");
        var f = Assert.Single(result.Findings, x => x.Code == "MissingKey");
        Assert.Equal(FindingSeverity.Error, f.Severity);
        Assert.Equal("b", f.Key);
    }

    [Fact]
    public void ExtraKey_IsAWarning()
    {
        var result = LintPair("""{ "a": "A" }""", """{ "a": "أ", "z": "ز" }""");
        var f = Assert.Single(result.Findings, x => x.Code == "ExtraKey");
        Assert.Equal(FindingSeverity.Warning, f.Severity);
    }

    [Fact]
    public void PlaceholderMismatch_IsAnError()
    {
        var result = LintPair(
            """{ "d": "Save {0}% for {1} days" }""",
            """{ "d": "وفّر {0}% لفترة محدودة" }""");
        Assert.Contains(result.Findings, x => x.Code == "PlaceholderMismatch" && x.Severity == FindingSeverity.Error);
    }

    [Fact]
    public void EmptyValue_IsAWarning()
    {
        var result = LintPair("""{ "a": "A" }""", """{ "a": "" }""");
        Assert.Contains(result.Findings, x => x.Code == "EmptyValue");
    }

    [Fact]
    public void UnisolatedLatin_InArabic_IsFlagged()
    {
        var result = LintPair(
            """{ "s": "Built with ASP.NET Core" }""",
            """{ "s": "مبني باستخدام ASP.NET Core الحديث" }""");
        Assert.Contains(result.Findings, x => x.Code == "UnisolatedLatin");
    }

    [Fact]
    public void LatinProblems_InEnglish_AreNotBidiFindings()
    {
        // Bidi checks only run on RTL cultures; English text is out of scope.
        var result = LintPair("""{ "s": "ASP.NET Core" }""", """{ "s": "ASP.NET Core" }""");
        Assert.DoesNotContain(result.Findings, x => x.Code == "UnisolatedLatin");
    }

    [Fact]
    public void CleanPair_HasNoFindings()
    {
        var result = LintPair(
            """{ "a": "Home", "n": "{0} products" }""",
            """{ "a": "الرئيسية", "n": "عدد المنتجات {0}" }""");
        Assert.Empty(result.Findings);
    }
}
