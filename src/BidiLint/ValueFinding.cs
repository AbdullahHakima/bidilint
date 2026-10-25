namespace BidiLint;

// Internal finding shape shared by the value analyzers. Offsets refer to the
// decoded locale value; the linter maps them to source spans.
internal sealed record ValueFinding(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    int Start,
    int Length,
    BidiExplanation? Bidi = null);
