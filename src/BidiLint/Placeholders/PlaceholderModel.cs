namespace BidiLint;

internal enum PlaceholderTokenKind
{
    Braced,
    IcuArgument,
    IcuPound,
    Printf,
    DoubleBraced,
    Rails,
    JavaScript,
}

internal enum PlaceholderArgumentKind
{
    Simple,
    Plural,
    Select,
    SelectOrdinal,
}

// One placeholder occurrence. Start and Length are decoded-value offsets, so
// the caller can map a diagnostic back to the raw source token.
internal sealed record PlaceholderToken(
    string Name,
    string Raw,
    PlaceholderTokenKind Kind,
    int Start,
    int Length,
    string? Format = null,
    PlaceholderArgumentKind ArgumentKind = PlaceholderArgumentKind.Simple,
    string? IcuType = null,
    IReadOnlyList<PlaceholderBranch>? Branches = null)
{
    public string Display => Kind == PlaceholderTokenKind.IcuArgument &&
                             ArgumentKind != PlaceholderArgumentKind.Simple
        ? $"{Name} ({ArgumentKind.ToString().ToLowerInvariant()})"
        : Raw;
}

internal sealed record PlaceholderBranch(
    string Selector,
    int Start,
    int Length,
    IReadOnlyList<PlaceholderToken> Tokens);

internal sealed record PlaceholderIssue(string Message, int Start, int Length);

internal sealed record PlaceholderMessage(
    PlaceholderDialect Dialect,
    IReadOnlyList<PlaceholderToken> Tokens,
    IReadOnlyList<PlaceholderIssue> Issues)
{
    public bool HasIssues => Issues.Count > 0;
}

internal enum PlaceholderDifferenceKind
{
    None,
    Mismatch,
    Type,
}

internal sealed record PlaceholderComparison(
    PlaceholderDifferenceKind Kind,
    string Expected,
    string Found,
    PlaceholderToken? BaseToken,
    PlaceholderToken? TargetToken)
{
    public bool IsMatch => Kind == PlaceholderDifferenceKind.None;
}

internal sealed record PlaceholderUse(
    string Name,
    string? Format,
    PlaceholderToken Token);

internal sealed record PlaceholderGroup(
    string Name,
    PlaceholderArgumentKind Kind,
    IReadOnlyList<string> Selectors,
    IReadOnlyList<PlaceholderUse> NestedUses,
    PlaceholderToken Token);
