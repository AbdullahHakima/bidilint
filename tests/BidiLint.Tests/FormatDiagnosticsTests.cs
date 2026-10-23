using Xunit;

namespace BidiLint.Tests;

public class FormatDiagnosticsTests
{
    [Fact]
    public void MalformedAppleStrings_ReportAPosition()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("Resources/en.lproj/Localizable.strings", "\"key\" = \"value\"");

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics);
        Assert.Equal(DiagnosticCodes.MalformedFile, diagnostic.Code);
        Assert.EndsWith("Localizable.strings", diagnostic.FilePath, StringComparison.Ordinal);
        Assert.NotNull(diagnostic.Span);
        Assert.Empty(load.Documents);
    }

    [Fact]
    public void MalformedAndroidXml_ReportsAPosition()
    {
        using var workspace = new TestWorkspace();
        workspace.Write(
            "app/src/main/res/values/strings.xml",
            "<resources><string name=\"key\">value</resources>");

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics);
        Assert.Equal(DiagnosticCodes.MalformedFile, diagnostic.Code);
        Assert.NotNull(diagnostic.Span);
        Assert.Empty(load.Documents);
    }

    [Fact]
    public void MalformedPoString_ReportsAPosition()
    {
        using var workspace = new TestWorkspace();
        workspace.Write(
            "locale/ar/LC_MESSAGES/messages.po",
            "msgid \"key\"\nmsgstr \"unterminated\n");

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics);
        Assert.Equal(DiagnosticCodes.MalformedFile, diagnostic.Code);
        Assert.NotNull(diagnostic.Span);
        Assert.Empty(load.Documents);
    }

    [Fact]
    public void NoLocaleFiles_ListsCandidateFiles()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("app.json", "{ \"name\": \"not a locale\" }");

        var result = workspace.Lint();

        var diagnostic = Assert.Single(result.Diagnostics, item =>
            item.Code == DiagnosticCodes.NoLocaleFiles);
        Assert.Contains("app.json", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateAppleKeys_AreReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write(
            "Resources/en.lproj/Localizable.strings",
            "\"key\" = \"first\";\n\"key\" = \"second\";\n");

        var load = workspace.Load();

        var diagnostic = Assert.Single(load.Diagnostics);
        Assert.Equal(DiagnosticCodes.DuplicateKey, diagnostic.Code);
        var document = Assert.Single(load.Documents);
        Assert.Equal("first", document.Entries["key"].Value);
    }
}
