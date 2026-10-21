using System.Text.Json;
using BidiLint;
using Xunit;

namespace BidiLint.Tests;

public class ReporterTests
{
    [Fact]
    public void JsonReportHasTheToolingFields()
    {
        var result = new LintResult { KeyCount = 3, ElapsedMilliseconds = 5 };
        var en = new LocaleFile { Culture = "en", FilePath = "en.json" };
        en.Entries["a.b"] = "x";
        result.Files.Add(en);
        result.Findings.Add(new Finding(
            FindingSeverity.Error, "MissingKey", "ar", "a.b", "Key exists in en but is missing here."));

        var json = Reporter.RenderJson(result, "en");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("BidiLint", root.GetProperty("tool").GetString());
        Assert.Equal("0.2.0", root.GetProperty("version").GetString());
        Assert.Equal("en", root.GetProperty("baseCulture").GetString());
        Assert.Equal(1, root.GetProperty("errors").GetInt32());
        Assert.Equal(0, root.GetProperty("warnings").GetInt32());

        var finding = root.GetProperty("findings")[0];
        Assert.Equal("error", finding.GetProperty("severity").GetString());
        Assert.Equal("MissingKey", finding.GetProperty("code").GetString());
        Assert.Equal("ar", finding.GetProperty("culture").GetString());
    }
}
