namespace BidiLint;

// One loaded locale file, flattened to dotted keys ("checkout.payNow").
public sealed class LocaleFile
{
    public required string Culture { get; init; }
    public required string FilePath { get; init; }
    public Dictionary<string, string> Entries { get; } = new(StringComparer.Ordinal);
}

public enum FindingSeverity
{
    Warning,
    Error,
}

public sealed record Finding(
    FindingSeverity Severity,
    string Code,
    string Culture,
    string Key,
    string Message);

public sealed class LintResult
{
    public List<LocaleFile> Files { get; } = new();
    public List<Finding> Findings { get; } = new();
    public int KeyCount { get; set; }
    public long ElapsedMilliseconds { get; set; }
}
