using System.Text.Json;
using System.Text.Json.Serialization;

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

    // Machine-readable report for tooling (editors, CI annotations).
    public static string RenderJson(LintResult result, string baseCulture)
    {
        var errors = result.Findings.Count(f => f.Severity == FindingSeverity.Error);
        var report = new JsonReport(
            Tool: "BidiLint",
            Version: ToolInfo.Version,
            BaseCulture: baseCulture,
            Files: result.Files.Select(f => new JsonFile(f.Culture, f.FilePath, f.Entries.Count)).ToList(),
            KeyCount: result.KeyCount,
            Errors: errors,
            Warnings: result.Findings.Count - errors,
            ElapsedMilliseconds: result.ElapsedMilliseconds,
            Findings: result.Findings.Select(f => new JsonFinding(
                f.Severity == FindingSeverity.Error ? "error" : "warning",
                f.Code, f.Culture, f.Key, f.Message)).ToList());
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    private sealed record JsonReport(
        string Tool, string Version, string BaseCulture,
        List<JsonFile> Files, int KeyCount, int Errors, int Warnings, long ElapsedMilliseconds,
        List<JsonFinding> Findings);

    private sealed record JsonFile(string Culture, string Path, int Keys);

    private sealed record JsonFinding(
        [property: JsonPropertyName("severity")] string Severity,
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("culture")] string Culture,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("message")] string Message);
}
