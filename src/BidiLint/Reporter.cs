using System.Text.Json;
using System.Text.Json.Serialization;

namespace BidiLint;

public static class Reporter
{
    public static void Print(LintResult result) => Print(result, Console.Out);

    public static void Print(LintResult result, TextWriter writer)
    {
        foreach (var diagnostic in result.Diagnostics.OrderBy(d => d, DiagnosticOrder.ByLocation))
        {
            var path = DisplayPath(result.WorkingDirectory, diagnostic.FilePath);
            var location = FormatLocation(path, diagnostic.Span);
            var key = diagnostic.Key is null ? "" : $"  {diagnostic.Key}";
            writer.WriteLine($"{location}  {SeverityText(diagnostic.Severity)}  {diagnostic.Code}{key}");
            writer.WriteLine($"  {diagnostic.Message}");

            foreach (var related in diagnostic.Related ?? Array.Empty<RelatedDiagnostic>())
            {
                var relatedPath = DisplayPath(result.WorkingDirectory, related.FilePath);
                var relatedLocation = FormatLocation(relatedPath, related.Span);
                writer.WriteLine($"  {related.Message} {relatedLocation}");
            }
        }

        if (result.Diagnostics.Count > 0) writer.WriteLine();
        writer.WriteLine(
            $"BidiLint {ToolInfo.Version}: {result.Documents.Count} file(s), base {result.BaseCulture}, " +
            $"{result.KeyCount} entries — {result.ErrorCount} error(s), {result.WarningCount} warning(s) in {result.ElapsedMilliseconds} ms.");
    }

    // Machine-readable report for tooling (editors, CI annotations).
    public static string RenderJson(LintResult result)
    {
        var report = new JsonReport(
            SchemaVersion: "1.0",
            Tool: "BidiLint",
            Version: ToolInfo.Version,
            BaseCulture: result.BaseCulture,
            Files: result.Documents
                .Select(document => new JsonFile(
                    document.Culture,
                    DisplayPath(result.WorkingDirectory, document.FilePath),
                    document.Entries.Count,
                    FormatName(document.Format),
                    document.ResourceSet))
                .ToList(),
            KeyCount: result.KeyCount,
            Errors: result.ErrorCount,
            Warnings: result.WarningCount,
            ElapsedMilliseconds: result.ElapsedMilliseconds,
            Findings: result.Diagnostics
                .OrderBy(d => d, DiagnosticOrder.ByLocation)
                .Select(diagnostic => new JsonFinding(
                    SeverityName(diagnostic.Severity),
                    diagnostic.Code,
                    diagnostic.Culture,
                    diagnostic.Key,
                    diagnostic.Message,
                    DisplayPath(result.WorkingDirectory, diagnostic.FilePath),
                    ToRange(diagnostic.Span),
                    diagnostic.Related?
                        .Select(related => new JsonRelated(
                            related.Message,
                            DisplayPath(result.WorkingDirectory, related.FilePath),
                            ToRange(related.Span),
                            related.Key))
                        .ToList(),
                    diagnostic.HelpUri))
                .ToList());

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static JsonRange? ToRange(SourceSpan? span) =>
        span is null
            ? null
            : new JsonRange(
                new JsonPosition(span.Value.Start.Line, span.Value.Start.Column),
                new JsonPosition(span.Value.End.Line, span.Value.End.Column));

    private static string FormatLocation(string path, SourceSpan? span) =>
        span is null
            ? path
            : $"{path}:{span.Value.Start.Line}:{span.Value.Start.Column}";

    private static string DisplayPath(string workingDirectory, string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return Path.GetRelativePath(workingDirectory, path).Replace('\\', '/');
    }

    private static string SeverityText(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error  ",
        DiagnosticSeverity.Warning => "warning",
        _ => "info   ",
    };

    private static string SeverityName(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "info",
    };

    private static string FormatName(LocaleFormat format) => format switch
    {
        LocaleFormat.Json => "json",
        LocaleFormat.Resx => "resx",
        LocaleFormat.Arb => "arb",
        LocaleFormat.Android => "android",
        LocaleFormat.AppleStrings => "apple-strings",
        LocaleFormat.Po => "po",
        _ => "unknown",
    };

    private sealed record JsonReport(
        string SchemaVersion,
        string Tool,
        string Version,
        string BaseCulture,
        List<JsonFile> Files,
        int KeyCount,
        int Errors,
        int Warnings,
        long ElapsedMilliseconds,
        List<JsonFinding> Findings);

    private sealed record JsonFile(
        string Culture,
        string Path,
        int Keys,
        string Format,
        string ResourceSet);

    private sealed record JsonFinding(
        string Severity,
        string Code,
        string? Culture,
        string? Key,
        string Message,
        string? File,
        JsonRange? Range,
        List<JsonRelated>? Related,
        string? HelpUri);

    private sealed record JsonRelated(
        string Message,
        string File,
        JsonRange? Range,
        string? Key);

    private sealed record JsonRange(JsonPosition Start, JsonPosition End);

    private sealed record JsonPosition(int Line, int Column);
}
