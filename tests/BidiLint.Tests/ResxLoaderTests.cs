using BidiLint;
using Xunit;

namespace BidiLint.Tests;

public class ResxLoaderTests : IDisposable
{
    private readonly string _dir;

    public ResxLoaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bidilint-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string name, string content) => File.WriteAllText(Path.Combine(_dir, name), content);

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
        Write("Strings.resx", BaseResx);
        Write("Strings.ar.resx", ArResx);

        var files = LocaleLoader.LoadFolder(_dir, "en");

        Assert.Equal(2, files.Count);
        var en = files.Single(f => f.Culture == "en");
        Assert.Equal("Pay {0}", en.Entries["checkout.payNow"]);
    }

    [Fact]
    public void NonStringDataIsSkipped()
    {
        Write("Strings.resx", BaseResx);

        var files = LocaleLoader.LoadFolder(_dir, "en");

        var en = files.Single();
        Assert.False(en.Entries.ContainsKey("logo"));
        Assert.Equal(2, en.Entries.Count);
    }

    [Fact]
    public void SatelliteResxGetsItsCulture()
    {
        Write("Strings.resx", BaseResx);
        Write("Strings.ar.resx", ArResx);

        var files = LocaleLoader.LoadFolder(_dir, "en");

        var ar = files.Single(f => f.Culture == "ar");
        Assert.Equal("المتجر", ar.Entries["app.title"]);
    }

    [Fact]
    public void JsonAndResxMergeWithJsonWinning()
    {
        Write("Strings.resx", BaseResx);
        Write("en.json", """{ "app": { "title": "From JSON" }, "only": { "json": "x" } }""");

        var files = LocaleLoader.LoadFolder(_dir, "en");

        var en = files.Single(f => f.Culture == "en");
        Assert.Equal("From JSON", en.Entries["app.title"]);
        Assert.Equal("x", en.Entries["only.json"]);
        Assert.Equal("Pay {0}", en.Entries["checkout.payNow"]);
    }

    [Fact]
    public void LintRunsAcrossResxFiles()
    {
        Write("Strings.resx", BaseResx);
        Write("Strings.ar.resx", ArResx);

        var files = LocaleLoader.LoadFolder(_dir, "en");
        var result = Linter.Lint(files, files.Single(f => f.Culture == "en"));

        Assert.Contains(result.Findings,
            f => f.Code == "MissingKey" && f.Culture == "ar" && f.Key == "checkout.payNow");
    }
}
