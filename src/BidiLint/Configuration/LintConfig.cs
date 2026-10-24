namespace BidiLint;

internal enum FailOn
{
    Error,
    Warning,
    None,
}

internal enum PlaceholderDialect
{
    Auto,
    Braced,
    None,
}

internal enum NumeralPolicy
{
    Mixed,
    Allow,
    Western,
    ArabicIndic,
}

internal enum IsolationStyle
{
    Isolate,
    External,
}

// A null Severity means "use the rule's default". Disabled is distinct from
// null because an object can carry options such as allow without overriding
// the severity.
internal sealed record RuleConfig(
    DiagnosticSeverity? Severity,
    bool Disabled,
    IReadOnlyList<string> Allow)
{
    public static RuleConfig Default { get; } = new(null, false, Array.Empty<string>());
}

internal sealed record ConfiguredFile(
    string FullPath,
    string Culture,
    string ResourceSet);

internal sealed record IgnoreRule(
    string? Rule,
    string? PathPattern,
    string? Culture,
    string? KeyPattern,
    string Reason,
    DateOnly? Expires);

internal sealed record AllowedLatinTerm(
    string Term,
    bool CaseSensitive,
    string? Reason,
    IReadOnlySet<string> Cultures)
{
    public bool AppliesTo(string culture) =>
        Cultures.Count == 0 || Cultures.Contains(culture);

    public bool Matches(string run) =>
        string.Equals(
            Term,
            run,
            CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}

internal sealed record OutputConfig(
    OutputFormat? Format = null,
    bool? Color = null,
    bool? Quiet = null,
    int? MaxWarnings = null);

// Immutable, validated config. CLI values are applied after this object is
// loaded, so the precedence is always CLI > config > built-in defaults.
internal sealed class LintConfig
{
    public static LintConfig Default { get; } = new();

    public string? ConfigPath { get; init; }
    public string? ConfigDirectory { get; init; }
    public string BaseCulture { get; init; } = "en";
    public IReadOnlyList<string> Include { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Exclude { get; init; } = Array.Empty<string>();
    public IReadOnlySet<string> LayoutNames { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ConfiguredFile> Files { get; init; } = Array.Empty<ConfiguredFile>();
    public IReadOnlyDictionary<string, RuleConfig> Rules { get; init; } =
        new Dictionary<string, RuleConfig>(StringComparer.Ordinal);
    public IReadOnlyList<IgnoreRule> Ignore { get; init; } = Array.Empty<IgnoreRule>();
    public IReadOnlyList<AllowedLatinTerm> AllowLatin { get; init; } =
        Array.Empty<AllowedLatinTerm>();
    public IReadOnlySet<string> RtlCultures { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public PlaceholderDialect PlaceholderDialect { get; init; } = PlaceholderDialect.Auto;
    public NumeralPolicy NumeralPolicy { get; init; } = NumeralPolicy.Mixed;
    public IsolationStyle IsolationStyle { get; init; } = IsolationStyle.Isolate;
    public string? BaselinePath { get; init; }
    public FailOn FailOn { get; init; } = FailOn.Error;
    public OutputConfig Output { get; init; } = new();
}

internal sealed record ConfigResolution(
    LintConfig Config,
    IReadOnlyList<string> Errors);
