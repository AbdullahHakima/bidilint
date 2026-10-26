using Xunit;

namespace BidiLint.Tests;

public class AdapterTests
{
    [Fact]
    public void AndroidStringsAndPlurals_AreLoadedFromValuesDirectories()
    {
        var root = Fixture("android");
        var load = LocaleLoader.Load([root], "en", root);

        Assert.Empty(load.Diagnostics);
        Assert.Equal(2, load.Documents.Count);
        Assert.All(load.Documents, document => Assert.Equal(LocaleFormat.Android, document.Format));
        Assert.All(load.Documents, document =>
            Assert.Equal("app/src/main/res/strings.xml", document.ResourceSet));

        var en = Assert.Single(load.Documents, document => document.Culture == "en");
        Assert.Equal(5, en.Entries.Count);
        Assert.False(en.Entries.ContainsKey("build_only"));
        Assert.Equal("{0} messages", en.Entries["messages[other]"].Value);

        var ar = Assert.Single(load.Documents, document => document.Culture == "ar");
        Assert.Equal("متجر خياطة", ar.Entries["app_name"].Value);
    }

    [Theory]
    [InlineData("values-b+ar+EG", "ar-EG")]
    [InlineData("values-ar-rEG", "ar-EG")]
    [InlineData("values-pt-rBR", "pt-BR")]
    public void AndroidBcp47AndLegacyQualifiers_AreRecognized(string directory, string expected)
    {
        var root = Fixture("android");
        var path = Path.Combine(root, "res", directory, "strings.xml");

        var identity = new AndroidAdapter().Identify(path, root, "en");

        Assert.NotNull(identity);
        Assert.Equal(expected, identity.Culture);
    }

    [Theory]
    [InlineData("values-night")]
    [InlineData("values-sw600dp")]
    [InlineData("values-ar-night")]
    public void AndroidNonLocaleQualifiers_AreIgnored(string directory)
    {
        var root = Fixture("android");
        var path = Path.Combine(root, "res", directory, "strings.xml");

        Assert.Null(new AndroidAdapter().Identify(path, root, "en"));
    }

    [Fact]
    public void AppleStrings_AreLoadedWithEscapes()
    {
        var root = Fixture("apple");
        var load = LocaleLoader.Load([root], "en", root);

        Assert.Empty(load.Diagnostics);
        Assert.Equal(2, load.Documents.Count);
        Assert.All(load.Documents, document => Assert.Equal(LocaleFormat.AppleStrings, document.Format));
        Assert.All(load.Documents, document =>
            Assert.Equal("Resources/Localizable.strings", document.ResourceSet));

        var en = Assert.Single(load.Documents, document => document.Culture == "en");
        Assert.Equal("He said \"hello\"", en.Entries["quote"].Value);
        Assert.Equal(4, en.Entries.Count);
    }

    [Fact]
    public void PoFiles_AreLoadedFromLcMessagesDirectories()
    {
        var root = Fixture("po");
        var load = LocaleLoader.Load([root], "en", root);

        Assert.Empty(load.Diagnostics);
        Assert.Equal(2, load.Documents.Count);
        Assert.All(load.Documents, document => Assert.Equal(LocaleFormat.Po, document.Format));
        Assert.All(load.Documents, document =>
            Assert.Equal("locale/messages.po", document.ResourceSet));

        var en = Assert.Single(load.Documents, document => document.Culture == "en");
        Assert.Equal("Pay", en.Entries["checkout|pay"].Value);
        Assert.Equal(3, en.Entries.Count);
    }

    [Fact]
    public void PotTemplates_UseMsgidAsTheBaseValue()
    {
        var root = Fixture("po-template");
        var load = LocaleLoader.Load([root], "en", root);

        Assert.Empty(load.Diagnostics);
        Assert.Equal(2, load.Documents.Count);
        Assert.All(load.Documents, document => Assert.Equal("po/messages.po", document.ResourceSet));

        var en = Assert.Single(load.Documents, document => document.Culture == "en");
        Assert.Equal("app.name", en.Entries["app.name"].Value);
        Assert.Equal("cart.items", en.Entries["cart.items"].Value);
    }

    [Fact]
    public void ArbFiles_SkipMetadataAndUseTheLocaleHeader()
    {
        var root = Fixture("arb");
        var load = LocaleLoader.Load([root], "en", root);

        Assert.Empty(load.Diagnostics);
        Assert.Equal(2, load.Documents.Count);
        Assert.All(load.Documents, document => Assert.Equal(LocaleFormat.Arb, document.Format));
        Assert.All(load.Documents, document => Assert.Equal("lib/l10n/app", document.ResourceSet));
        Assert.All(load.Documents, document =>
            Assert.DoesNotContain(document.Entries.Keys, key => key.StartsWith('@')));

        var ar = Assert.Single(load.Documents, document => document.Culture == "ar");
        Assert.Equal("متجر خياطة", ar.Entries["appName"].Value);
        Assert.Equal(3, ar.Entries.Count);
    }

    [Fact]
    public void ArbMetadata_CanDetermineCultureWithoutAFilenameSuffix()
    {
        var root = Fixture("arb-metadata");
        var load = LocaleLoader.Load([root], "en", root);

        var document = Assert.Single(load.Documents);
        Assert.Equal("ar", document.Culture);
        Assert.Equal("messages", document.ResourceSet);
        Assert.Equal("مرحبا", document.Entries["greeting"].Value);
    }

    [Fact]
    public void JsonCultureDirectories_KeepResourceSetsSeparateAndIgnoreAppJson()
    {
        var root = Fixture("json-layout");
        var load = LocaleLoader.Load([root], "en", root);

        Assert.Empty(load.Diagnostics);
        Assert.Equal(4, load.Documents.Count);
        Assert.All(load.Documents, document => Assert.Equal(LocaleFormat.Json, document.Format));
        Assert.DoesNotContain(load.Documents, document =>
            document.FilePath.EndsWith("app.json", StringComparison.OrdinalIgnoreCase));

        var sets = load.Documents.Select(document => document.ResourceSet).Distinct().Order().ToArray();
        Assert.Equal(["locales/common.json", "locales/translation.json"], sets);
        Assert.Contains(load.Documents, document =>
            document.Culture == "ar" &&
            document.ResourceSet == "locales/translation.json" &&
            document.Entries["nav.home"].Value == "الرئيسية");
    }

    [Fact]
    public void CultureNamedJsonFiles_UseTheirDirectoryAsTheResourceSet()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("apps/files/l10n/en.json", """{ "Cancel": "Cancel" }""");
        workspace.Write("apps/files/l10n/ar.json", """{ "Cancel": "إلغاء" }""");
        workspace.Write("apps/settings/l10n/en.json", """{ "Cancel": "Cancel" }""");
        workspace.Write("apps/settings/l10n/ar.json", """{ "Cancel": "إلغاء" }""");

        var load = workspace.Load();
        var lint = Linter.Lint(load, "en");

        Assert.Empty(load.Diagnostics);
        Assert.Equal(
            ["apps/files/l10n", "apps/settings/l10n"],
            load.Documents
                .Select(document => document.ResourceSet)
                .Distinct()
                .Order(StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(lint.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.KeyCollision);
    }

    [Fact]
    public void RootLevelCultureNamedJsonFiles_UseTheRootResourceSet()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "greeting": "Hello" }""");
        workspace.Write("ar.json", """{ "greeting": "مرحبا" }""");

        var load = workspace.Load();
        var lint = Linter.Lint(load, "en");

        Assert.Empty(load.Diagnostics);
        Assert.All(load.Documents, document => Assert.Equal(".", document.ResourceSet));
        Assert.Empty(lint.Diagnostics);
    }

    [Fact]
    public void EachFormat_IsComparedInsideItsOwnResourceSet()
    {
        var root = Fixture("json-layout");
        var result = LocaleLoader.Load([root], "en", root);
        var lint = Linter.Lint(result, "en");

        Assert.DoesNotContain(lint.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.BaseCultureMissing);
        Assert.DoesNotContain(lint.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.MissingKey);
    }

    [Fact]
    public void JsonReport_IncludesTheAdapterFormat()
    {
        var root = Fixture("android");
        var load = LocaleLoader.Load([root], "en", root);
        var result = Linter.Lint(load, "en");

        using var report = System.Text.Json.JsonDocument.Parse(Reporter.RenderJson(result));
        var formats = report.RootElement.GetProperty("files")
            .EnumerateArray()
            .Select(file => file.GetProperty("format").GetString() ?? "")
            .ToArray();

        Assert.Equal(["android", "android"], formats);
    }

    private static string Fixture(string name) =>
        Path.Combine(TestPaths.RepositoryRoot, "tests", "fixtures", name);
}
