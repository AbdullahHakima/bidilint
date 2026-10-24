namespace BidiLint;

// One entry per diagnostic code. The catalog is the source of truth for
// default severity, baseline eligibility, help links, and generated docs.
internal sealed record RuleMetadata(
    string Code,
    string Title,
    string Category,
    DiagnosticSeverity DefaultSeverity,
    bool Fixable,
    string SinceVersion,
    bool BaselineAllowed,
    bool AllowsTerms,
    string Description)
{
    private static readonly HashSet<string> ConfigurableCodes = new(StringComparer.Ordinal)
    {
        DiagnosticCodes.MissingKey,
        DiagnosticCodes.ExtraKey,
        DiagnosticCodes.EmptyValue,
        DiagnosticCodes.PlaceholderMismatch,
        DiagnosticCodes.UnisolatedLatin,
        DiagnosticCodes.MixedNumerals,
        DiagnosticCodes.UnbalancedIsolates,
    };

    public bool Configurable => ConfigurableCodes.Contains(Code);

    public string DocsAnchor => Code.ToLowerInvariant();

    public string HelpUri =>
        $"https://github.com/AbdullahHakima/bidilint/blob/main/docs/rules/README.md#{DocsAnchor}";
}

internal static class RuleCatalog
{
    private static readonly IReadOnlyList<RuleMetadata> Entries =
    [
        new(
            DiagnosticCodes.MalformedFile,
            "Malformed file",
            "format",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "The file could not be decoded or parsed. Fixing the syntax is required before linting can continue."),
        new(
            DiagnosticCodes.DuplicateKey,
            "Duplicate key",
            "format",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "The same key is defined more than once in one file. The first value is used, which makes the later value dead and surprising."),
        new(
            DiagnosticCodes.KeyCollision,
            "Key collision",
            "format",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "Flattening or merging produces the same logical key twice. Split or rename the conflicting entries."),
        new(
            DiagnosticCodes.NonStringValue,
            "Non-string value",
            "format",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "A JSON value is not a string and cannot be analyzed as user-visible locale text."),
        new(
            DiagnosticCodes.UnsupportedFile,
            "Unsupported file",
            "discovery",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "An explicitly selected file is not in a supported locale format."),
        new(
            DiagnosticCodes.CultureNotFound,
            "Culture not found",
            "discovery",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "The filename or metadata does not contain a usable BCP-47 culture."),
        new(
            DiagnosticCodes.PathNotFound,
            "Path not found",
            "discovery",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "A target path or configured file does not exist."),
        new(
            DiagnosticCodes.UnreadablePath,
            "Unreadable path",
            "discovery",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "A file or directory could not be read. Check permissions, locks, and broken links."),
        new(
            DiagnosticCodes.MissingKey,
            "Missing key",
            "structure",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "A key exists in the base resource set but not in this translated resource set."),
        new(
            DiagnosticCodes.ExtraKey,
            "Extra key",
            "structure",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "A key exists only in the translated resource set. It may be dead text left behind by a redesign."),
        new(
            DiagnosticCodes.EmptyValue,
            "Empty value",
            "structure",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "The value is empty or contains only whitespace."),
        new(
            DiagnosticCodes.PlaceholderMismatch,
            "Placeholder mismatch",
            "placeholder",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "The placeholders in a translated value differ from the base value."),
        new(
            DiagnosticCodes.UnisolatedLatin,
            "Unisolated Latin run",
            "bidi",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: true,
            "A Latin brand, command, URL, or code term is not isolated inside RTL text."),
        new(
            DiagnosticCodes.MixedNumerals,
            "Mixed numerals",
            "bidi",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "One string uses more than one numeral system, or violates the configured numeral policy."),
        new(
            DiagnosticCodes.UnbalancedIsolates,
            "Unbalanced isolates",
            "bidi",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "A bidi isolate opener has no matching PDI, or a PDI has no opener."),
        new(
            DiagnosticCodes.BaseCultureMissing,
            "Base culture missing",
            "structure",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "The configured base culture does not exist for a resource set, so that set cannot be compared."),
        new(
            DiagnosticCodes.NoLocaleFiles,
            "No locale files",
            "discovery",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.3.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "No supported locale files were discovered under the selected paths."),
        new(
            DiagnosticCodes.StaleSuppression,
            "Stale suppression",
            "configuration",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.5.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "An ignore rule did not match a finding, matched only findings that were already suppressed, or expired."),
        new(
            DiagnosticCodes.StaleBaseline,
            "Stale baseline entry",
            "configuration",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.5.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "A baseline entry no longer matches any eligible finding."),
    ];

    public static IReadOnlyList<RuleMetadata> All => Entries;

    public static bool TryGet(string code, out RuleMetadata metadata)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Code, code, StringComparison.Ordinal))
            {
                metadata = entry;
                return true;
            }
        }

        metadata = null!;
        return false;
    }

    public static RuleMetadata Get(string code) =>
        TryGet(code, out var metadata)
            ? metadata
            : throw new KeyNotFoundException($"Unknown diagnostic code: {code}");

    public static string ToMarkdown()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# BidiLint rule catalog");
        builder.AppendLine();
        builder.AppendLine("This file is generated from `RuleCatalog` and checked by a test.");
        builder.AppendLine();
        builder.AppendLine("| Rule | Default | Category | Fixable | Since | Baseline |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");

        foreach (var rule in Entries.OrderBy(rule => rule.Code, StringComparer.Ordinal))
        {
            builder.Append("| [`");
            builder.Append(rule.Code);
            builder.Append("`](#");
            builder.Append(rule.DocsAnchor);
            builder.Append(") | ");
            builder.Append(SeverityName(rule.DefaultSeverity));
            builder.Append(" | ");
            builder.Append(rule.Category);
            builder.Append(" | ");
            builder.Append(rule.Fixable ? "yes" : "no");
            builder.Append(" | ");
            builder.Append(rule.SinceVersion);
            builder.Append(" | ");
            builder.Append(rule.BaselineAllowed ? "yes" : "no");
            builder.AppendLine(" |");
        }

        foreach (var rule in Entries.OrderBy(rule => rule.Code, StringComparer.Ordinal))
        {
            builder.AppendLine();
            builder.Append("## ");
            builder.Append(rule.Code);
            builder.AppendLine();
            builder.AppendLine();
            builder.Append("**");
            builder.Append(rule.Title);
            builder.Append("** · ");
            builder.Append(SeverityName(rule.DefaultSeverity));
            builder.Append(" · ");
            builder.Append(rule.Category);
            builder.Append(" · since ");
            builder.Append(rule.SinceVersion);
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine(rule.Description);
            builder.AppendLine();
            if (rule.Configurable)
            {
                builder.Append("Suppress with an `ignore` entry, or change the severity under `rules.");
                builder.Append(rule.Code);
                builder.AppendLine("`.");
            }
            else
            {
                builder.AppendLine(
                    "This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.");
            }
        }

        return builder.ToString();
    }

    private static string SeverityName(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "info",
    };
}
