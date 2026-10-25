using Xunit;

namespace BidiLint.Tests;

// End-to-end checks over the checked-in 0.6 RTL corpus. The corpus is split
// into independent resource sets so Hebrew, Persian, and Urdu are compared
// against their own English base instead of being merged into one document.
public class RtlCorpusTests
{
    private static readonly string CorpusRoot = Path.Combine(
        TestPaths.RepositoryRoot,
        "tests",
        "fixtures",
        "rtl-corpus");

    private static readonly HashSet<string> RuleCodes = new(StringComparer.Ordinal)
    {
        DiagnosticCodes.UnisolatedLatin,
        DiagnosticCodes.MixedNumerals,
        DiagnosticCodes.UnbalancedIsolates,
        DiagnosticCodes.BidiControl,
        DiagnosticCodes.BidiOverride,
        DiagnosticCodes.IsolateDepth,
        DiagnosticCodes.NumberAdjacency,
        DiagnosticCodes.PresentationForm,
        DiagnosticCodes.Normalization,
        DiagnosticCodes.Tatweel,
        DiagnosticCodes.InvisibleControl,
    };

    private static readonly Dictionary<string, (string Positive, string Benign)> RuleCases =
        new(StringComparer.Ordinal)
        {
            [DiagnosticCodes.UnisolatedLatin] = ("unisolatedLatin.positive", "unisolatedLatin.benign"),
            [DiagnosticCodes.MixedNumerals] = ("mixedNumerals.positive", "mixedNumerals.benign"),
            [DiagnosticCodes.UnbalancedIsolates] = ("unbalancedIsolate.positive", "unbalancedIsolate.benign"),
            [DiagnosticCodes.BidiControl] = ("legacyControl.positive", "legacyControl.benign"),
            [DiagnosticCodes.BidiOverride] = ("security.override.positive", "security.override.benign"),
            [DiagnosticCodes.IsolateDepth] = ("isolateDepth.positive", "isolateDepth.benign"),
            [DiagnosticCodes.NumberAdjacency] = ("numberAdjacency.positive", "numberAdjacency.benign"),
            [DiagnosticCodes.PresentationForm] = ("presentationForm.positive", "presentationForm.benign"),
            [DiagnosticCodes.Normalization] = ("normalization.positive", "normalization.benign"),
            [DiagnosticCodes.Tatweel] = ("tatweel.positive", "tatweel.benign"),
            [DiagnosticCodes.InvisibleControl] = ("security.bom.positive", "security.bom.benign"),
        };

    [Fact]
    public void CorpusCoversEvery06RuleAndBenignCounterpart()
    {
        var result = LintCorpus();

        Assert.Equal(8, result.Documents.Count);
        Assert.Equal(88, result.KeyCount);
        Assert.Equal(8, result.ErrorCount);
        Assert.Equal(14, result.WarningCount);
        Assert.Equal(0, result.InfoCount);
        Assert.All(result.Diagnostics, diagnostic => Assert.Contains(diagnostic.Code, RuleCodes));
        Assert.All(result.Diagnostics, diagnostic => Assert.NotNull(diagnostic.Span));
        Assert.All(result.Diagnostics, diagnostic => Assert.NotNull(diagnostic.FilePath));

        var entries = result.Documents
            .SelectMany(document => document.Entries.Values)
            .ToList();
        var positiveKeys = entries
            .Where(entry => entry.Key.EndsWith(".positive", StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var benignKeys = entries
            .Where(entry => entry.Key.EndsWith(".benign", StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(positiveKeys);
        Assert.NotEmpty(benignKeys);
        foreach (var key in positiveKeys)
        {
            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Key == key && RuleCodes.Contains(diagnostic.Code));
        }

        foreach (var key in benignKeys)
        {
            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Key == key && RuleCodes.Contains(diagnostic.Code));
        }

        foreach (var (code, keys) in RuleCases)
        {
            Assert.Contains(
                result.Diagnostics,
                diagnostic => diagnostic.Code == code && diagnostic.Key == keys.Positive);
            Assert.DoesNotContain(
                result.Diagnostics,
                diagnostic => diagnostic.Key == keys.Benign && RuleCodes.Contains(diagnostic.Code));
        }

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnisolatedLatin && diagnostic.Culture == "he");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnisolatedLatin && diagnostic.Culture == "fa");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnisolatedLatin && diagnostic.Culture == "ur");

        var latin = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnisolatedLatin
            && diagnostic.Key == "unisolatedLatin.positive");
        Assert.NotNull(latin.Bidi);
        Assert.Equal("BidiLint", latin.Bidi!.Logical);
        Assert.Equal(1, latin.Bidi.ParagraphLevel);

        var numbers = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.NumberAdjacency
            && diagnostic.Key == "numberAdjacency.positive");
        Assert.NotNull(numbers.Bidi);
        Assert.Equal("3, 2, 1", numbers.Bidi!.Visual);
    }

    [Fact]
    public void GoldenTextAndJsonOutputsMatch()
    {
        var result = LintCorpus();
        result.ElapsedMilliseconds = 0;

        using var writer = new StringWriter();
        Reporter.Print(result, writer);
        AssertGolden("rtl-corpus.txt", writer.ToString());
        AssertGolden("rtl-corpus.json", Reporter.RenderJson(result));
    }

    private static LintResult LintCorpus()
    {
        var load = LocaleLoader.Load([CorpusRoot], "en", CorpusRoot);
        Assert.Empty(load.Diagnostics);
        return Linter.Lint(load, "en");
    }

    private static void AssertGolden(string name, string actual)
    {
        var path = Path.Combine(TestPaths.RepositoryRoot, "tests", "golden", name);
        Assert.True(File.Exists(path), $"Missing golden file: {path}");
        var expected = Normalize(File.ReadAllText(path));
        Assert.Equal(expected, Normalize(actual).TrimEnd());
    }

    private static string Normalize(string value) =>
        value.ReplaceLineEndings("\n").TrimEnd();
}
