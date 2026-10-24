using System.Text.Json;
using Xunit;

namespace BidiLint.Tests;

public class ReporterTests
{
    [Fact]
    public void JsonReportHasTheToolingFields()
    {
        var root = CreateRoot();
        try
        {
            var source = SourceText.FromText("{\n  \"a.b\": \"x\"\n}\n");
            var en = Document(root, "en.json", "en", source);
            var enEntry = new LocaleEntry
            {
                Key = "a.b",
                Value = "x",
                Document = en,
                Span = new SourceSpan(source.PositionAt(2, 10), source.PositionAt(2, 13)),
            };
            en.Entries.Add(enEntry.Key, enEntry);

            var arPath = Path.Combine(root, "ar.json");
            var result = new LintResult
            {
                KeyCount = 1,
                BaseCulture = "en",
                WorkingDirectory = root,
            };
            result.Documents.Add(en);
            result.Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.MissingKey,
                DiagnosticSeverity.Error,
                "Key exists in en but is missing here.",
                arPath,
                Culture: "ar",
                Key: "a.b",
                Related: new[]
                {
                    new RelatedDiagnostic("Base value.", en.FilePath, enEntry.Span, "a.b"),
                }));

            var json = Reporter.RenderJson(result);

            using var doc = JsonDocument.Parse(json);
            var rootElement = doc.RootElement;
            Assert.Equal("1.0", rootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("BidiLint", rootElement.GetProperty("tool").GetString());
            Assert.Equal(ToolInfo.Version, rootElement.GetProperty("version").GetString());
            Assert.Equal(UnicodeInfo.Version, rootElement.GetProperty("unicodeVersion").GetString());
            Assert.Equal("en", rootElement.GetProperty("baseCulture").GetString());
            Assert.Equal(1, rootElement.GetProperty("errors").GetInt32());
            Assert.Equal(0, rootElement.GetProperty("warnings").GetInt32());

            var file = rootElement.GetProperty("files")[0];
            Assert.Equal("en", file.GetProperty("culture").GetString());
            Assert.Equal("json", file.GetProperty("format").GetString());
            Assert.Equal("json", file.GetProperty("resourceSet").GetString());

            var finding = rootElement.GetProperty("findings")[0];
            Assert.Equal("error", finding.GetProperty("severity").GetString());
            Assert.Equal("MissingKey", finding.GetProperty("code").GetString());
            Assert.Equal("ar", finding.GetProperty("culture").GetString());
            Assert.Equal("a.b", finding.GetProperty("key").GetString());
            Assert.Equal("ar.json", finding.GetProperty("file").GetString());

            var related = finding.GetProperty("related")[0];
            Assert.Equal("en.json", related.GetProperty("file").GetString());
            Assert.Equal(2, related.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TextReportIncludesLocationAndKey()
    {
        var root = CreateRoot();
        try
        {
            var source = SourceText.FromText("{\"a\":\"x\"}");
            var ar = Document(root, "ar.json", "ar", source);
            var result = new LintResult { BaseCulture = "en", WorkingDirectory = root };
            result.Documents.Add(ar);
            result.Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.EmptyValue,
                DiagnosticSeverity.Warning,
                "Value is empty or whitespace.",
                ar.FilePath,
                new SourceSpan(source.PositionAt(1, 6), source.PositionAt(1, 9)),
                "ar",
                "a"));

            using var writer = new StringWriter();
            Reporter.Print(result, writer);
            var text = writer.ToString();

            Assert.Contains("ar.json:1:6", text, StringComparison.Ordinal);
            Assert.Contains("warning", text, StringComparison.Ordinal);
            Assert.Contains("EmptyValue", text, StringComparison.Ordinal);
            Assert.Contains(ToolInfo.Version, text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ColorAndQuietAreAppliedToTextOutput()
    {
        var root = CreateRoot();
        try
        {
            var source = SourceText.FromText("{\"a\":\"x\"}");
            var ar = Document(root, "ar.json", "ar", source);
            var result = new LintResult { BaseCulture = "en", WorkingDirectory = root };
            result.Documents.Add(ar);
            result.Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.EmptyValue,
                DiagnosticSeverity.Warning,
                "Value is empty or whitespace.",
                ar.FilePath,
                new SourceSpan(source.PositionAt(1, 6), source.PositionAt(1, 9)),
                "ar",
                "a"));

            using var colored = new StringWriter();
            Reporter.Print(result, colored, new ReporterOptions(Color: true));
            Assert.Contains("\u001b[33m", colored.ToString(), StringComparison.Ordinal);

            using var plain = new StringWriter();
            Reporter.Print(result, plain, new ReporterOptions(Color: false));
            Assert.DoesNotContain("\u001b[", plain.ToString(), StringComparison.Ordinal);

            using var quiet = new StringWriter();
            Reporter.Print(result, quiet, new ReporterOptions(Quiet: true));
            Assert.DoesNotContain("EmptyValue", quiet.ToString(), StringComparison.Ordinal);
            Assert.Contains("1 warning(s)", quiet.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "bidilint-reporter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static LocaleDocument Document(string root, string name, string culture, SourceText source) =>
        new()
        {
            Culture = culture,
            FilePath = Path.Combine(root, name),
            Format = LocaleFormat.Json,
            ResourceSet = "json",
            Source = source,
        };
}
