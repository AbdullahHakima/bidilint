using System.Text.Json;
using System.Text.Json.Serialization;

namespace BidiLint;

public sealed record ReporterOptions(bool Quiet = false, bool Color = false);

public static class Reporter
{
    public static void Print(LintResult result) => Print(result, Console.Out);

    public static void Print(LintResult result, TextWriter writer) =>
        Print(result, writer, new ReporterOptions());

    public static void Print(LintResult result, TextWriter writer, ReporterOptions options)
    {
        var printed = false;
        foreach (var diagnostic in result.Diagnostics.OrderBy(d => d, DiagnosticOrder.ByLocation))
        {
            if (options.Quiet && diagnostic.Severity != DiagnosticSeverity.Error) continue;

            var path = DisplayPath(result.WorkingDirectory, diagnostic.FilePath);
            var location = FormatLocation(path, diagnostic.Span);
            var key = diagnostic.Key is null ? "" : $"  {diagnostic.Key}";
            writer.WriteLine(
                $"{location}  {SeverityText(diagnostic.Severity, options.Color)}  {diagnostic.Code}{key}");
            writer.WriteLine($"  {diagnostic.Message}");

            foreach (var related in diagnostic.Related ?? Array.Empty<RelatedDiagnostic>())
            {
                var relatedPath = DisplayPath(result.WorkingDirectory, related.FilePath);
                var relatedLocation = FormatLocation(relatedPath, related.Span);
                writer.WriteLine($"  {related.Message} {relatedLocation}");
            }

            printed = true;
        }

        if (printed) writer.WriteLine();

        if (result.BaselineUpdated)
        {
            var path = DisplayPath(result.WorkingDirectory, result.BaselinePath);
            writer.WriteLine(
                $"Baseline written: {path} ({result.BaselineEntryCount} entr{(result.BaselineEntryCount == 1 ? "y" : "ies")}).");
        }

        var suppressed = result.SuppressedCount == 0
            ? ""
            : $", {result.SuppressedCount} suppressed";
        var infos = result.InfoCount == 0 ? "" : $", {result.InfoCount} info";
        writer.WriteLine(
            $"BidiLint {ToolInfo.Version}: {result.Documents.Count} file(s), base {result.BaseCulture}, " +
            $"{result.KeyCount} entries — {result.ErrorCount} error(s), {result.WarningCount} warning(s)" +
            $"{infos}{suppressed} in {result.ElapsedMilliseconds} ms.");
    }

    // Machine-readable report for tooling (editors, CI annotations).
    public static string RenderJson(LintResult result)
    {
        var report = new JsonReport(
            SchemaVersion: "1.0",
            Tool: "BidiLint",
            Version: ToolInfo.Version,
            UnicodeVersion: result.UnicodeVersion,
            BaseCulture: result.BaseCulture,
            ConfigPath: OptionalPath(result.WorkingDirectory, result.ConfigPath),
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
            Infos: result.InfoCount,
            Suppressed: result.SuppressedCount,
            ElapsedMilliseconds: result.ElapsedMilliseconds,
            Baseline: result.BaselinePath is null
                ? null
                : new JsonBaseline(
                    DisplayPath(result.WorkingDirectory, result.BaselinePath),
                    result.BaselineUpdated,
                    result.BaselineEntryCount,
                    result.Suppressed.Count(item => item.Source == "baseline")),
            Findings: result.Diagnostics
                .OrderBy(d => d, DiagnosticOrder.ByLocation)
                .Select(diagnostic => ToJsonFinding(diagnostic, result.WorkingDirectory))
                .ToList(),
            SuppressedFindings: result.Suppressed
                .Select(item => new JsonSuppressed(
                    item.Source,
                    item.Reason,
                    item.Fingerprint,
                    ToJsonFinding(item.Diagnostic, result.WorkingDirectory)))
                .ToList());

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static JsonFinding ToJsonFinding(Diagnostic diagnostic, string workingDirectory) =>
        new(
            SeverityName(diagnostic.Severity),
            diagnostic.Code,
            diagnostic.Culture,
            diagnostic.Key,
            diagnostic.Message,
            diagnostic.FilePath is null
                ? null
                : DisplayPath(workingDirectory, diagnostic.FilePath),
            ToRange(diagnostic.Span),
            diagnostic.Related?
                .Select(related => new JsonRelated(
                    related.Message,
                    DisplayPath(workingDirectory, related.FilePath),
                    ToRange(related.Span),
                    related.Key))
                .ToList(),
            diagnostic.HelpUri);

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

    private static string? OptionalPath(string workingDirectory, string? path) =>
        path is null ? null : DisplayPath(workingDirectory, path);

    private static string DisplayPath(string workingDirectory, string? path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return Path.GetRelativePath(workingDirectory, path).Replace('\\', '/');
    }

    private static string SeverityText(DiagnosticSeverity severity, bool color)
    {
        var text = severity switch
        {
            DiagnosticSeverity.Error => "error  ",
            DiagnosticSeverity.Warning => "warning",
            _ => "info   ",
        };

        if (!color) return text;
        var code = severity switch
        {
            DiagnosticSeverity.Error => "31",
            DiagnosticSeverity.Warning => "33",
            _ => "36",
        };
        return $"\u001b[{code}m{text}\u001b[0m";
    }

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
        string UnicodeVersion,
        string BaseCulture,
        string? ConfigPath,
        List<JsonFile> Files,
        int KeyCount,
        int Errors,
        int Warnings,
        int Infos,
        int Suppressed,
        long ElapsedMilliseconds,
        JsonBaseline? Baseline,
        List<JsonFinding> Findings,
        List<JsonSuppressed> SuppressedFindings);

    private sealed record JsonFile(
        string Culture,
        string Path,
        int Keys,
        string Format,
        string ResourceSet);

    private sealed record JsonBaseline(
        string Path,
        bool Updated,
        int Entries,
        int Suppressed);

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

    private sealed record JsonSuppressed(
        string Source,
        string? Reason,
        string? Fingerprint,
        JsonFinding Finding);

    private sealed record JsonRelated(
        string Message,
        string File,
        JsonRange? Range,
        string? Key);

    private sealed record JsonRange(JsonPosition Start, JsonPosition End);

    private sealed record JsonPosition(int Line, int Column);
}
