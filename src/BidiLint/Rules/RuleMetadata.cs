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
    string Description,
    bool Security = false)
{
    private static readonly HashSet<string> ConfigurableCodes = new(StringComparer.Ordinal)
    {
        DiagnosticCodes.MissingKey,
        DiagnosticCodes.ExtraKey,
        DiagnosticCodes.EmptyValue,
        DiagnosticCodes.PlaceholderMismatch,
        DiagnosticCodes.UnisolatedLatin,
        DiagnosticCodes.MixedNumerals,
        DiagnosticCodes.PresentationForm,
        DiagnosticCodes.Normalization,
        DiagnosticCodes.Tatweel,
        DiagnosticCodes.UnbalancedIsolates,
        DiagnosticCodes.BidiControl,
        DiagnosticCodes.IsolateDepth,
        DiagnosticCodes.NumberAdjacency,
    };

    public bool Configurable => ConfigurableCodes.Contains(Code);

    // Security rules are deliberately not part of ConfigurableCodes. They can
    // only be waived by a targeted ignore entry with a reason; rules.{code},
    // --update-baseline, and baseline files must not hide them.
    public bool Waivable => Security;

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
            "A Latin brand, command, URL, code term, or other left-to-right run is not isolated inside RTL text, uses the wrong isolate direction, or crosses an isolate boundary."),
        new(
            DiagnosticCodes.MixedNumerals,
            "Mixed numerals",
            "bidi",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "One string uses more than one numeral system, or violates the configured numeral policy. Western, Arabic-Indic, and Extended Arabic-Indic digits are distinguished."),
        new(
            DiagnosticCodes.PresentationForm,
            "Arabic presentation form",
            "unicode",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "Arabic Presentation Forms-A or -B (U+FB50-U+FDFF and U+FE70-U+FEFE) are present. These compatibility characters should be stored as standard Arabic letters so normalization, search, and shaping remain stable."),
        new(
            DiagnosticCodes.Normalization,
            "Non-NFC text",
            "unicode",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "A visible text element is not in Unicode NFC, or the value mixes composed and decomposed forms. Normalize values at the source so search, comparison, and rendering are stable."),
        new(
            DiagnosticCodes.Tatweel,
            "Arabic tatweel",
            "unicode",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "Arabic tatweel (U+0640) is present. It elongates glyphs but is not a letter; keep it only for intentional kashida and configure the rule when that policy is deliberate."),
        new(
            DiagnosticCodes.UnbalancedIsolates,
            "Unbalanced isolates",
            "bidi",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.1.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "A bidi isolate opener has no matching PDI, or a PDI has no opener. The finding points at the unmatched control."),
        new(
            DiagnosticCodes.BidiControl,
            "Bidi control",
            "bidi",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "Legacy bidi embeddings or overrides are unbalanced, ineffective across an isolate, or exceed the Unicode nesting limit. Directional marks are handled by the security rules."),
        new(
            DiagnosticCodes.BidiOverride,
            "Bidi override",
            "security",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "A left-to-right or right-to-left override (LRO/RLO) is visible in the value. Override sequences can reorder or hide text and are the basis of Trojan Source attacks; use isolates instead.",
            Security: true),
        new(
            DiagnosticCodes.InvisibleControl,
            "Invisible control",
            "security",
            DiagnosticSeverity.Error,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: false,
            AllowsTerms: false,
            "Visible text contains a BOM, a disallowed C0/C1 control, a zero-width joiner or non-joiner without a legitimate shaping or emoji context, or a directional mark that does not affect resolved order.",
            Security: true),
        new(
            DiagnosticCodes.IsolateDepth,
            "Isolate depth",
            "bidi",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "Bidi isolates are nested more deeply than the recommended limit, or an isolate contains no visible content."),
        new(
            DiagnosticCodes.NumberAdjacency,
            "Number adjacency",
            "bidi",
            DiagnosticSeverity.Warning,
            Fixable: false,
            SinceVersion: "0.6.0",
            BaselineAllowed: true,
            AllowsTerms: false,
            "Two or more logically adjacent numeric tokens resolve into a different visual order, so the numbers can appear swapped in RTL text."),
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
            if (rule.Security)
            {
                builder.AppendLine(
                    "This is a security rule. Waive a specific finding only with an `ignore` entry that names this rule and includes a reason. It cannot be disabled under `rules` or suppressed by a baseline.");
            }
            else if (rule.Configurable)
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
