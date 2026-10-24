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
}
