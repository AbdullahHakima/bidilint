namespace BidiLint;

// Produces the source ranges that should be opaque to bidi, numeral, and
// security analysis. ICU branch text stays visible; only the argument syntax,
// selectors, braces, and nested placeholders are masked.
internal static class PlaceholderMask
{
    public static IReadOnlyList<(int Start, int Length)> Spans(string value)
    {
        var message = PlaceholderParser.Parse(
            value,
            LocaleFormat.Json,
            PlaceholderDialect.Auto);
        var spans = new List<(int Start, int Length)>();
        AddTokenSpans(message.Tokens, spans);

        if (spans.Count == 0 && value.Contains('{'))
            AddGenericBraceSpans(value, spans);

        return spans;
    }

    private static void AddTokenSpans(
        IReadOnlyList<PlaceholderToken> tokens,
        List<(int Start, int Length)> spans)
    {
        foreach (var token in tokens)
        {
            if (token.Kind == PlaceholderTokenKind.IcuPound)
            {
                Add(spans, token.Start, token.Length);
                continue;
            }

            if (token.Kind == PlaceholderTokenKind.IcuArgument &&
                token.ArgumentKind != PlaceholderArgumentKind.Simple &&
                token.Branches is { Count: > 0 } branches)
            {
                AddIcuSpans(token, branches, spans);
                continue;
            }

            Add(spans, token.Start, token.Length);
        }
    }

    private static void AddIcuSpans(
        PlaceholderToken token,
        IReadOnlyList<PlaceholderBranch> branches,
        List<(int Start, int Length)> spans)
    {
        var first = branches[0];
        Add(spans, token.Start, first.Start - token.Start);

        var previousEnd = first.Start;
        for (var index = 0; index < branches.Count; index++)
        {
            var branch = branches[index];
            if (index > 0)
                Add(spans, previousEnd, branch.Start - previousEnd);

            Add(spans, branch.Start, 1);
            if (branch.Length > 0)
                Add(spans, branch.Start + branch.Length - 1, 1);

            AddTokenSpans(branch.Tokens, spans);
            previousEnd = branch.Start + branch.Length;
        }

        if (token.Length > 0)
            Add(spans, token.Start + token.Length - 1, 1);
    }

    private static void AddGenericBraceSpans(
        string value,
        List<(int Start, int Length)> spans)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '{') continue;

            if (index + 1 < value.Length && value[index + 1] == '{')
            {
                var triple = index + 2 < value.Length && value[index + 2] == '{';
                var closing = triple ? "}}}" : "}}";
                var close = value.IndexOf(closing, index + (triple ? 3 : 2), StringComparison.Ordinal);
                if (close < 0) return;
                Add(spans, index, close + closing.Length - index);
                index = close + closing.Length - 1;
                continue;
            }

            var end = value.IndexOf('}', index + 1);
            if (end < 0) return;
            Add(spans, index, end - index + 1);
            index = end;
        }
    }

    private static void Add(
        List<(int Start, int Length)> spans,
        int start,
        int length)
    {
        if (length > 0) spans.Add((start, length));
    }
}
