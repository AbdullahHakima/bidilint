namespace BidiLint;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public readonly record struct SourcePosition(int Line, int Column, int Offset);

public readonly record struct SourceSpan(SourcePosition Start, SourcePosition End)
{
    public static SourceSpan At(SourcePosition position) => new(position, position);
}

public sealed record RelatedDiagnostic(
    string Message,
    string FilePath,
    SourceSpan? Span = null,
    string? Key = null);

public sealed record TextEdit(SourceSpan Span, string NewText);

public sealed record DiagnosticFix(string Title, IReadOnlyList<TextEdit> Edits);

// A compact slice of the resolved level map. Offsets index the decoded
// logical run before DiagnosticText escaping; escaping only changes display.
public sealed record BidiLevelRun(int Start, int Length, int Level);

// Evidence for a bidi finding: the logical run with invisible controls
// escaped, the paragraph level, and the resolved levels that apply to it.
// Visual is populated only when the rule can state the reordered token order.
public sealed record BidiExplanation(
    string Logical,
    int ParagraphLevel,
    IReadOnlyList<BidiLevelRun> Levels,
    string? Visual = null);

public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? FilePath = null,
    SourceSpan? Span = null,
    string? Culture = null,
    string? Key = null,
    IReadOnlyList<RelatedDiagnostic>? Related = null,
    string? HelpUri = null,
    IReadOnlyList<DiagnosticFix>? Fixes = null,
    BidiExplanation? Bidi = null);

public static class DiagnosticCodes
{
    public const string MalformedFile = nameof(MalformedFile);
    public const string DuplicateKey = nameof(DuplicateKey);
    public const string KeyCollision = nameof(KeyCollision);
    public const string NonStringValue = nameof(NonStringValue);
    public const string UnsupportedFile = nameof(UnsupportedFile);
    public const string CultureNotFound = nameof(CultureNotFound);
    public const string PathNotFound = nameof(PathNotFound);
    public const string UnreadablePath = nameof(UnreadablePath);
    public const string MissingKey = nameof(MissingKey);
    public const string ExtraKey = nameof(ExtraKey);
    public const string EmptyValue = nameof(EmptyValue);
    public const string PlaceholderMismatch = nameof(PlaceholderMismatch);
    public const string PlaceholderSyntax = nameof(PlaceholderSyntax);
    public const string PlaceholderType = nameof(PlaceholderType);
    public const string UnisolatedLatin = nameof(UnisolatedLatin);
    public const string MixedNumerals = nameof(MixedNumerals);
    public const string PresentationForm = nameof(PresentationForm);
    public const string Normalization = nameof(Normalization);
    public const string Tatweel = nameof(Tatweel);
    public const string UnbalancedIsolates = nameof(UnbalancedIsolates);
    public const string BidiControl = nameof(BidiControl);
    public const string BidiOverride = nameof(BidiOverride);
    public const string InvisibleControl = nameof(InvisibleControl);
    public const string IsolateDepth = nameof(IsolateDepth);
    public const string NumberAdjacency = nameof(NumberAdjacency);
    public const string BaseCultureMissing = nameof(BaseCultureMissing);
    public const string NoLocaleFiles = nameof(NoLocaleFiles);
    public const string StaleSuppression = nameof(StaleSuppression);
    public const string StaleBaseline = nameof(StaleBaseline);
}

public static class DiagnosticOrder
{
    public static readonly IComparer<Diagnostic> ByLocation =
        Comparer<Diagnostic>.Create(Compare);

    private static int Compare(Diagnostic? left, Diagnostic? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        var result = string.Compare(left.FilePath, right.FilePath, StringComparison.Ordinal);
        if (result != 0) return result;

        result = StartLine(left).CompareTo(StartLine(right));
        if (result != 0) return result;

        result = StartColumn(left).CompareTo(StartColumn(right));
        if (result != 0) return result;

        result = right.Severity.CompareTo(left.Severity);
        if (result != 0) return result;

        result = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
        if (result != 0) return result;

        result = string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        if (result != 0) return result;

        result = string.Compare(left.Culture, right.Culture, StringComparison.Ordinal);
        if (result != 0) return result;

        return string.Compare(left.Message, right.Message, StringComparison.Ordinal);
    }

    private static int StartLine(Diagnostic diagnostic) =>
        diagnostic.Span?.Start.Line ?? 0;

    private static int StartColumn(Diagnostic diagnostic) =>
        diagnostic.Span?.Start.Column ?? 0;
}
