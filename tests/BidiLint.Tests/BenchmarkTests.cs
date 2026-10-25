using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace BidiLint.Tests;

// The 100k-entry benchmark fixture is generated deterministically so the
// repository does not carry 500 generated files. The timed section covers
// discovery, JSON parsing, comparison, and linting; fixture generation is
// excluded. The CI gate is intentionally looser than the developer target:
// 100,000 entries across 500 files must finish within five seconds on a
// GitHub-hosted runner, while the 1.0 target remains three seconds locally.
[CollectionDefinition("Performance", DisableParallelization = true)]
public sealed class PerformanceTestGroup
{
}

[Collection("Performance")]
public class BenchmarkTests
{
    private const int ResourceSets = 250;
    private const int EntriesPerCulture = 200;
    private const int ExpectedEntries = ResourceSets * EntriesPerCulture * 2;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private readonly ITestOutputHelper _output;

    public BenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HundredThousandEntriesAcrossFiveHundredFiles_CompletesWithinBudget()
    {
        using var workspace = new TestWorkspace();
        for (var set = 0; set < ResourceSets; set++)
        {
            var directory = $"benchmark/set{set:D3}/locales";
            workspace.Write(
                $"{directory}/en/messages.json",
                BuildMessages(set, arabic: false));
            workspace.Write(
                $"{directory}/ar/messages.json",
                BuildMessages(set, arabic: true));
        }

        var stopwatch = Stopwatch.StartNew();
        var load = LocaleLoader.Load([workspace.Root], "en", workspace.Root);
        var result = Linter.Lint(load, "en");
        stopwatch.Stop();

        Assert.Empty(load.Diagnostics);
        Assert.Equal(500, result.Documents.Count);
        Assert.Equal(ExpectedEntries, result.KeyCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);

        _output.WriteLine(
            "Benchmark: {0:N0} entries in {1:N0} files completed in {2:N0} ms (budget {3:N0} ms).",
            result.KeyCount,
            result.Documents.Count,
            stopwatch.ElapsedMilliseconds,
            Budget.TotalMilliseconds);

        Assert.True(
            stopwatch.Elapsed < Budget,
            $"100,000 entries across 500 files took {stopwatch.ElapsedMilliseconds:N0} ms; budget is {Budget.TotalMilliseconds:N0} ms.");
    }

    private static string BuildMessages(int set, bool arabic)
    {
        var builder = new StringBuilder(EntriesPerCulture * 32);
        builder.Append('{');
        for (var index = 0; index < EntriesPerCulture; index++)
        {
            if (index > 0) builder.Append(',');
            builder
                .Append("\"key")
                .Append(set.ToString("D3", CultureInfo.InvariantCulture))
                .Append('.')
                .Append(index.ToString("D3", CultureInfo.InvariantCulture))
                .Append("\":\"")
                .Append(arabic ? "رسالة" : "Message")
                .Append('"');
        }

        builder.Append('}');
        return builder.ToString();
    }
}
