using Xunit;

namespace BidiLint.Tests;

public class SampleTests
{
    [Fact]
    public void StorefrontSample_HasExpectedFindings()
    {
        var directory = Path.Combine(TestPaths.RepositoryRoot, "samples", "storefront");
        var load = LocaleLoader.Load([directory], "en", TestPaths.RepositoryRoot);
        var result = Linter.Lint(load, "en");

        Assert.Equal(4, result.ErrorCount);
        Assert.Equal(5, result.WarningCount);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.MissingKey);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.PlaceholderMismatch);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.UnbalancedIsolates);
    }

    [Fact]
    public void ResxSampleLoadsBothCultures()
    {
        var directory = Path.Combine(TestPaths.RepositoryRoot, "samples", "resx-storefront");
        var load = LocaleLoader.Load([directory], "en", TestPaths.RepositoryRoot);

        Assert.Equal(2, load.Documents.Count);
        Assert.Contains(load.Documents, document => document.Culture == "en");
        Assert.Contains(load.Documents, document => document.Culture == "ar");
    }

    [Fact]
    public void StorefrontOutputMatchesTheReadme()
    {
        var directory = Path.Combine(TestPaths.RepositoryRoot, "samples", "storefront");
        var load = LocaleLoader.Load([directory], "en", TestPaths.RepositoryRoot);
        var result = Linter.Lint(load, "en");
        result.ElapsedMilliseconds = 72;

        using var writer = new StringWriter();
        Reporter.Print(result, writer);
        var expected = Normalize(writer.ToString()).TrimEnd();
        var readme = Normalize(File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, "README.md")));

        Assert.Contains(expected, readme, StringComparison.Ordinal);
    }

    private static string Normalize(string text) => text.ReplaceLineEndings("\n");
}
