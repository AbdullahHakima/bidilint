namespace BidiLint;

public static class Reporter
{
    public static void Print(LintResult result, string baseCulture)
    {
        foreach (var group in result.Findings.GroupBy(f => f.Culture).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"{group.Key}:");
            foreach (var f in group.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                var mark = f.Severity == FindingSeverity.Error ? "error  " : "warning";
                Console.WriteLine($"  {mark} {f.Code} · {f.Key}");
                Console.WriteLine($"         {f.Message}");
            }
        }

        var errors = result.Findings.Count(f => f.Severity == FindingSeverity.Error);
        var warnings = result.Findings.Count - errors;
        Console.WriteLine();
        Console.WriteLine(
            $"BidiLint {ToolInfo.Version}: {result.Files.Count} file(s), base {baseCulture}, " +
            $"{result.KeyCount} keys — {errors} error(s), {warnings} warning(s) in {result.ElapsedMilliseconds} ms.");
    }
}
