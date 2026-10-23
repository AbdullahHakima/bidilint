using Xunit;

namespace BidiLint.Tests;

public class ResxLoaderTests
{
    private const string BaseResx = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="app.title" xml:space="preserve"><value>Storefront</value></data>
          <data name="checkout.payNow" xml:space="preserve"><value>Pay {0}</value></data>
          <data name="logo" type="System.Resources.ResXFileRef, System.Windows.Forms"><value>logo.png</value></data>
        </root>
        """;

    private const string ArResx = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="app.title" xml:space="preserve"><value>المتجر</value></data>
        </root>
        """;

    [Fact]
    public void NeutralResxCountsAsBaseCulture()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", BaseResx);
        workspace.Write("Strings.ar.resx", ArResx);

        var load = workspace.Load();

        Assert.Equal(2, load.Documents.Count);
        var en = Assert.Single(load.Documents, document => document.Culture == "en");
        Assert.Equal("Strings", en.ResourceSet);
        Assert.Equal("Pay {0}", en.Entries["checkout.payNow"].Value);
    }

    [Fact]
    public void NonStringDataIsSkipped()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", BaseResx);

        var load = workspace.Load();

        var en = load.Documents.Single();
        Assert.False(en.Entries.ContainsKey("logo"));
        Assert.Equal(2, en.Entries.Count);
    }

    [Fact]
    public void SatelliteResxGetsItsCulture()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", BaseResx);
        workspace.Write("Strings.ar.resx", ArResx);

        var load = workspace.Load();

        var ar = Assert.Single(load.Documents, document => document.Culture == "ar");
        Assert.Equal("المتجر", ar.Entries["app.title"].Value);
    }

    [Fact]
    public void DottedNeutralResxIsItsOwnResourceSet()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("App.Resources.resx", BaseResx);

        var load = workspace.Load();

        var en = load.Documents.Single();
        Assert.Equal("en", en.Culture);
        Assert.Equal("App.Resources", en.ResourceSet);
    }

    [Fact]
    public void ResourceSets_AreComparedSeparately()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", """
            <root><data name="title"><value>Store</value></data></root>
            """);
        workspace.Write("Strings.ar.resx", """
            <root><data name="title"><value>متجر</value></data></root>
            """);
        workspace.Write("Errors.resx", """
            <root><data name="title"><value>Error</value></data></root>
            """);
        workspace.Write("Errors.ar.resx", """
            <root><data name="title"><value>خطأ</value></data></root>
            """);

        var result = workspace.Lint();

        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.KeyCollision or DiagnosticCodes.MissingKey);
    }

    [Fact]
    public void MalformedResx_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", BaseResx);
        workspace.Write("Strings.ar.resx", """<root><data name="a">""");

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics, d => d.Code == DiagnosticCodes.MalformedFile);
        Assert.Equal("ar", diagnostic.Culture);
    }

    [Fact]
    public void DuplicateResxKey_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", """
            <root>
              <data name="a"><value>first</value></data>
              <data name="a"><value>second</value></data>
            </root>
            """);

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics, d => d.Code == DiagnosticCodes.DuplicateKey);
        Assert.Equal("a", diagnostic.Key);
        Assert.Equal("first", load.Documents.Single().Entries["a"].Value);
    }

    [Fact]
    public void JsonAndResxRemainSeparateResourceSets()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", BaseResx);
        workspace.Write("Strings.ar.resx", ArResx);
        workspace.Write("en.json", """{ "app": { "title": "From JSON" } }""");
        workspace.Write("ar.json", """{ "app": { "title": "من JSON" } }""");

        var load = workspace.Load();

        Assert.Equal(4, load.Documents.Count);
        Assert.Contains(load.Documents, document =>
            document.Format == LocaleFormat.Json && document.Culture == "en");
        Assert.Contains(load.Documents, document =>
            document.Format == LocaleFormat.Resx && document.Culture == "en");
        Assert.DoesNotContain(load.Diagnostics, d => d.Code == DiagnosticCodes.KeyCollision);
    }

    [Fact]
    public void LintRunsAcrossResxFiles()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Strings.resx", BaseResx);
        workspace.Write("Strings.ar.resx", ArResx);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics,
            diagnostic => diagnostic.Code == DiagnosticCodes.MissingKey &&
                          diagnostic.Culture == "ar" &&
                          diagnostic.Key == "checkout.payNow");
    }
}
