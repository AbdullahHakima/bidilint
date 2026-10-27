namespace BidiLint;

public enum LocaleFormat
{
    Json,
    Resx,
    Arb,
    Android,
    AppleStrings,
    Po,
}

// One parsed file. Entries keep their source span so diagnostics can point at
// the exact value rather than only at the culture.
public sealed class LocaleDocument
{
    public required string Culture { get; init; }
    public required string FilePath { get; init; }
    public required LocaleFormat Format { get; init; }
    public required string ResourceSet { get; init; }
    public required SourceText Source { get; init; }

    public Dictionary<string, LocaleEntry> Entries { get; } = new(StringComparer.Ordinal);
}

public sealed class LocaleEntry
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required LocaleDocument Document { get; init; }
    public SourceSpan? Span { get; init; }
    public SourceSpan? KeySpan { get; init; }

    // Optional mapping from decoded value offsets to the raw source token.
    // Adapters that can provide it get sub-value diagnostic ranges.
    internal ValueSourceMap? ValueMap { get; init; }

    // Set only for entries that are one branch of a flattened plural group.
    // The key stays the adapter's public flattened key; this metadata lets the
    // linter pair branches without changing the report schema.
    internal PluralFormInfo? Plural { get; init; }
}

internal enum PluralFormKind
{
    Numeric,
    Category,
}

internal sealed record PluralFormInfo(
    string GroupKey,
    string Form,
    PluralFormKind Kind);

public sealed record SuppressedDiagnostic(
    Diagnostic Diagnostic,
    string Source,
    string? Reason,
    string? Fingerprint);

public sealed class LintResult
{
    public List<LocaleDocument> Documents { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public List<SuppressedDiagnostic> Suppressed { get; } = new();
    public int KeyCount { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public string UnicodeVersion { get; set; } = UnicodeInfo.Version;
    public string BaseCulture { get; set; } = "en";
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();
    public string? ConfigPath { get; set; }
    public bool BaselineUpdated { get; set; }
    public string? BaselinePath { get; set; }
    public int BaselineEntryCount { get; set; }

    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
    public int WarningCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
    public int InfoCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);
    public int SuppressedCount => Suppressed.Count;
}

public sealed class LoadResult
{
    public List<LocaleDocument> Documents { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public List<string> CandidateFiles { get; } = new();
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

    public bool HasInputErrors => Diagnostics.Any(d =>
        d.Code is DiagnosticCodes.PathNotFound
            or DiagnosticCodes.UnreadablePath
            or DiagnosticCodes.UnsupportedFile
            or DiagnosticCodes.CultureNotFound);
}
