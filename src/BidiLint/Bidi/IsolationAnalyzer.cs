using System.Text;

namespace BidiLint;

// Isolation and control-character analysis for one decoded locale value.
// Findings carry decoded-value offsets; the linter maps them back to source
// spans and falls back to the whole value when a format has no source map.
internal static class IsolationAnalyzer
{
    public static IReadOnlyList<ValueFinding> Analyze(
        string value,
        string culture,
        bool rtlCulture,
        LintConfig config,
        StringScan scan)
    {
        var content = ContentMask.Build(value, config.IsolationWrappers, out var markup);
        return Analyze(value, culture, rtlCulture, config, scan, content, markup);
    }

    public static IReadOnlyList<ValueFinding> Analyze(
        string value,
        string culture,
        bool rtlCulture,
        LintConfig config,
        StringScan scan,
        ContentMask content,
        MarkupScanResult markup)
    {
        var findings = new List<ValueFinding>();

        // Controls inside markup or placeholders are not rendered, so the
        // structural scan uses the masked text while keeping every offset.
        var structure = BidiStructure.Scan(content.Text);
        AddControlFindings(value, structure, findings);
        AddDepthFindings(value, structure, findings);

        var visibleHasRtlScript = scan.HasRtlScript;
        if (visibleHasRtlScript && content.Text != value)
            visibleHasRtlScript = BidiScanner.Scan(content.Text).HasRtlScript;

        if (!visibleHasRtlScript && !rtlCulture) return findings;

        var context = BidiContext.Create(
            content.Text,
            rtlCulture ? BidiParagraphDirection.RightToLeft : BidiParagraphDirection.Auto);
        var regions = BuildRegions(value, structure, markup);

        if (visibleHasRtlScript && config.IsolationStyle == IsolationStyle.Isolate)
        {
            AddLatinFindings(
                value,
                culture,
                config,
                content,
                structure,
                context,
                regions,
                findings);
        }

        AddNumberFindings(value, content, structure, context, regions, findings);
        return findings;
    }

    private static void AddControlFindings(
        string value,
        BidiStructure structure,
        List<ValueFinding> findings)
    {
        var seen = new HashSet<(string Code, int Start, string Message)>();

        void Add(string code, DiagnosticSeverity severity, string message, int start, int length = 1)
        {
            if (seen.Add((code, start, message)))
                findings.Add(new ValueFinding(code, severity, message, start, length));
        }

        foreach (var index in structure.UnclosedIsolates)
        {
            var control = DescribeControl(value[index]);
            Add(
                DiagnosticCodes.UnbalancedIsolates,
                DiagnosticSeverity.Error,
                $"Unclosed {control}; it runs to the end of the value. Add a matching PDI (U+2069) after the isolated text.",
                index);
        }

        foreach (var index in structure.StrayPdis)
        {
            Add(
                DiagnosticCodes.UnbalancedIsolates,
                DiagnosticSeverity.Error,
                "Stray PDI (U+2069) has no matching isolate opener. Add LRI/RLI/FSI before the isolated text or remove the PDI.",
                index);
        }

        foreach (var index in structure.UnclosedEmbeddings)
        {
            var control = DescribeControl(value[index]);
            Add(
                DiagnosticCodes.BidiControl,
                DiagnosticSeverity.Error,
                $"Unclosed {control}; it runs to the end of the value and can reorder later text. Add a matching PDF (U+202C) or replace the embedding with LRI..PDI.",
                index);
        }

        foreach (var index in structure.StrayPdfs)
        {
            Add(
                DiagnosticCodes.BidiControl,
                DiagnosticSeverity.Error,
                "Stray PDF (U+202C) has no matching LRE/RLE/LRO/RLO. Remove it or add the matching opener.",
                index);
        }

        foreach (var index in structure.IneffectivePdfs)
        {
            Add(
                DiagnosticCodes.BidiControl,
                DiagnosticSeverity.Error,
                "PDF (U+202C) is ignored while an isolate is open; close the isolate with PDI before ending the embedding, or remove the PDF.",
                index);
        }

        foreach (var index in structure.OverflowControls)
        {
            var control = DescribeControl(value[index]);
            Add(
                DiagnosticCodes.BidiControl,
                DiagnosticSeverity.Error,
                $"{control} exceeds the Unicode explicit-direction limit of 125 levels; controls after this point no longer take effect.",
                index);
        }
    }

    private static void AddDepthFindings(
        string value,
        BidiStructure structure,
        List<ValueFinding> findings)
    {
        foreach (var issue in structure.DepthIssues)
        {
            findings.Add(new ValueFinding(
                DiagnosticCodes.IsolateDepth,
                DiagnosticSeverity.Warning,
                $"Isolate nesting reached {issue.Depth} levels; the recommended maximum is {BidiStructure.RecommendedIsolateDepth}. Flatten the markup or reuse an outer isolate.",
                issue.Opener,
                1));
        }

        foreach (var empty in structure.EmptyIsolates)
        {
            var control = DescribeControl(value[empty.Opener]);
            findings.Add(new ValueFinding(
                DiagnosticCodes.IsolateDepth,
                DiagnosticSeverity.Warning,
                $"Empty {control}..PDI isolate; it contains only whitespace or invisible controls. Remove it or move visible text inside.",
                empty.Opener,
                1));
        }
    }

    private static void AddLatinFindings(
        string value,
        string culture,
        LintConfig config,
        ContentMask content,
        BidiStructure structure,
        BidiContext context,
        List<IsolationRegion> regions,
        List<ValueFinding> findings)
    {
        var candidates = LatinCandidateScanner.Find(value, content, structure);

        foreach (var candidate in candidates)
        {
            if (IsAllowed(candidate.Text, culture, config)) continue;

            var explanation = context.Explain(value, candidate.Start, candidate.Length);

            if (candidate.Kind == LatinCandidateKind.CrossedBoundary)
            {
                var enclosing = FindInnermostRegion(
                    candidate.Start,
                    candidate.End,
                    regions);
                if (enclosing is not null)
                {
                    var enclosingDirection = enclosing.Direction == IsolationDirection.Automatic
                        ? ResolveAutomaticDirection(
                            value,
                            enclosing.Start,
                            enclosing.End,
                            content,
                            structure)
                        : enclosing.Direction;
                    if (enclosingDirection == IsolationDirection.LeftToRight) continue;
                }

                var boundary = FindControl(candidate);
                var control = boundary >= 0
                    ? DescribeControl(value[boundary])
                    : "an isolate boundary";
                findings.Add(new ValueFinding(
                    DiagnosticCodes.UnisolatedLatin,
                    DiagnosticSeverity.Warning,
                    $"Latin run \"{DiagnosticText.Escape(candidate.Text)}\" crosses {control} at value offset {boundary}; one left-to-right token is split by the isolate boundary. Move the boundary outside the token or isolate the whole token.",
                    candidate.Start,
                    candidate.Length,
                    explanation));
                continue;
            }

            var region = FindInnermostRegion(candidate.Start, candidate.End, regions);
            if (region is null)
            {
                findings.Add(new ValueFinding(
                    DiagnosticCodes.UnisolatedLatin,
                    DiagnosticSeverity.Warning,
                    $"Latin run \"{DiagnosticText.Escape(candidate.Text)}\" is not isolated from the surrounding RTL text (resolved level {context.LevelAtChar(candidate.Start)}). Wrap it in LRI/FSI..PDI, <bdi>, <code>, or dir=\"ltr\".",
                    candidate.Start,
                    candidate.Length,
                    explanation));
                continue;
            }

            var direction = region.Direction == IsolationDirection.Automatic
                ? ResolveAutomaticDirection(value, region.Start, region.End, content, structure)
                : region.Direction;
            if (direction != IsolationDirection.RightToLeft) continue;

            var reason = region.Direction == IsolationDirection.Automatic
                ? $"its automatic direction resolves right-to-left because the isolate begins with RTL text"
                : "which forces right-to-left order";
            var repair = region.Direction == IsolationDirection.Automatic
                ? "move the run into its own LRI or add dir=\"ltr\" to the wrapper"
                : region.Label.Contains('<')
                    ? "add dir=\"ltr\" or move the run into its own LTR wrapper"
                    : "use LRI around this run";
            findings.Add(new ValueFinding(
                DiagnosticCodes.UnisolatedLatin,
                DiagnosticSeverity.Warning,
                $"Latin run \"{DiagnosticText.Escape(candidate.Text)}\" is inside {region.Label}, but {reason}. The run resolves at level {context.LevelAtChar(candidate.Start)}; {repair}.",
                candidate.Start,
                candidate.Length,
                explanation));
        }
    }

    private static List<IsolationRegion> BuildRegions(
        string value,
        BidiStructure structure,
        MarkupScanResult markup)
    {
        var regions = new List<IsolationRegion>();
        foreach (var isolate in structure.Isolates)
        {
            var end = isolate.Closed ? isolate.Closer : value.Length;
            if (end <= isolate.Opener + 1) continue;

            var direction = isolate.Kind switch
            {
                BidiScanner.Lri => IsolationDirection.LeftToRight,
                BidiScanner.Rli => IsolationDirection.RightToLeft,
                _ => IsolationDirection.Automatic,
            };
            regions.Add(new IsolationRegion(
                isolate.Opener + 1,
                end,
                DescribeControl(isolate.Kind),
                direction));
        }

        foreach (var wrapper in markup.Wrappers)
        {
            if (wrapper.End <= wrapper.Start) continue;
            var label = wrapper.Direction == IsolationDirection.RightToLeft
                ? $"<{wrapper.Tag} dir=\"rtl\">"
                : $"<{wrapper.Tag}>";
            regions.Add(new IsolationRegion(
                wrapper.Start,
                wrapper.End,
                label,
                wrapper.Direction));
        }

        return regions;
    }

    private static IsolationRegion? FindInnermostRegion(
        int start,
        int end,
        List<IsolationRegion> regions)
    {
        IsolationRegion? best = null;
        foreach (var region in regions)
        {
            if (start < region.Start || end > region.End) continue;

            if (best is null ||
                region.Start > best.Start ||
                (region.Start == best.Start && region.End < best.End))
            {
                best = region;
            }
        }

        return best;
    }

    private static bool IsInsideLtrRegion(
        string value,
        int start,
        int end,
        ContentMask content,
        BidiStructure structure,
        List<IsolationRegion> regions)
    {
        var region = FindInnermostRegion(start, end, regions);
        if (region is null) return false;

        var direction = region.Direction == IsolationDirection.Automatic
            ? ResolveAutomaticDirection(value, region.Start, region.End, content, structure)
            : region.Direction;
        return direction is IsolationDirection.LeftToRight or IsolationDirection.Automatic;
    }

    private static IsolationDirection ResolveAutomaticDirection(
        string value,
        int start,
        int end,
        ContentMask content,
        BidiStructure structure)
    {
        var index = start;
        while (index < end)
        {
            if (content.IsMasked(index))
            {
                index++;
                continue;
            }

            if (!TryDecode(value, index, out var rune, out var consumed))
            {
                index++;
                continue;
            }

            if (rune.Value is BidiScanner.Lri or BidiScanner.Rli or BidiScanner.Fsi)
            {
                var nested = structure.Isolates.FirstOrDefault(info => info.Opener == index);
                if (nested is not null)
                {
                    index = nested.Closed ? nested.Closer + 1 : end;
                    continue;
                }
            }

            if (rune.Value == BidiScanner.Pdi ||
                (rune.Value <= char.MaxValue &&
                 BidiScanner.IsLegacyControl((char)rune.Value)))
            {
                index += consumed;
                continue;
            }

            var bidiClass = UnicodeInfo.GetBidiClass(rune);
            if (bidiClass is BidiClass.R or BidiClass.AL)
                return IsolationDirection.RightToLeft;
            if (bidiClass == BidiClass.L)
                return IsolationDirection.LeftToRight;

            index += consumed;
        }

        return IsolationDirection.Automatic;
    }

    private static void AddNumberFindings(
        string value,
        ContentMask content,
        BidiStructure structure,
        BidiContext context,
        List<IsolationRegion> regions,
        List<ValueFinding> findings)
    {
        var tokens = FindNumberTokens(value, content);
        if (tokens.Count < 2) return;

        var group = new List<NumberToken>();
        foreach (var token in tokens)
        {
            if (group.Count > 0 &&
                !OnlyNeutralBetween(value, group[^1].End, token.Start, content))
            {
                ProcessNumberGroup(
                    value,
                    group,
                    content,
                    structure,
                    context,
                    regions,
                    findings);
                group = new List<NumberToken>();
            }

            group.Add(token);
        }

        ProcessNumberGroup(value, group, content, structure, context, regions, findings);
    }

    private static void ProcessNumberGroup(
        string value,
        List<NumberToken> group,
        ContentMask content,
        BidiStructure structure,
        BidiContext context,
        List<IsolationRegion> regions,
        List<ValueFinding> findings)
    {
        if (group.Count < 2) return;

        var first = group[0];
        var last = group[^1];
        if (IsInsideLtrRegion(
                value,
                first.Start,
                last.End,
                content,
                structure,
                regions))
        {
            return;
        }

        var positions = new int[group.Count];
        for (var index = 0; index < group.Count; index++)
        {
            positions[index] = context.FirstVisualPosition(
                group[index].Start,
                group[index].End);
            if (positions[index] < 0) return;
        }

        var reordered = false;
        for (var index = 1; index < positions.Length; index++)
        {
            if (positions[index] < positions[index - 1])
            {
                reordered = true;
                break;
            }
        }

        if (!reordered) return;

        var logical = string.Join(", ", group.Select(token => DiagnosticText.Escape(token.Text)));
        var visual = string.Join(
            ", ",
            group
                .Select((token, index) => (token.Text, Position: positions[index]))
                .OrderBy(item => item.Position)
                .Select(item => item.Text));
        var paragraph = context.ParagraphLevel % 2 == 1 ? "RTL" : "LTR";
        var explanation = new BidiExplanation(
            DiagnosticText.Escape(value[first.Start..last.End]),
            context.ParagraphLevel,
            group
                .Select(token => new BidiLevelRun(
                    token.Start - first.Start,
                    token.Length,
                    context.LevelAtChar(token.Start)))
                .ToList(),
            DiagnosticText.Escape(visual));
        findings.Add(new ValueFinding(
            DiagnosticCodes.NumberAdjacency,
            DiagnosticSeverity.Warning,
            $"Numbers {logical} are separated only by neutral characters and resolve visually as {visual} from left to right in this {paragraph} paragraph. Wrap the sequence in an LRI/FSI if the logical order must stay visible.",
            first.Start,
            last.End - first.Start,
            explanation));
    }

    private static List<NumberToken> FindNumberTokens(string value, ContentMask content)
    {
        var tokens = new List<NumberToken>();
        var index = 0;
        while (index < value.Length)
        {
            if (content.IsMasked(index))
            {
                index++;
                continue;
            }

            if (!TryDecode(value, index, out var rune, out var consumed) ||
                !Rune.IsDigit(rune))
            {
                index += consumed == 0 ? 1 : consumed;
                continue;
            }

            var start = index;
            var lastDigitEnd = index + consumed;
            var cursor = lastDigitEnd;
            while (cursor < value.Length)
            {
                if (content.IsMasked(cursor)) break;
                if (!TryDecode(value, cursor, out var current, out var currentConsumed))
                    break;

                if (Rune.IsDigit(current))
                {
                    lastDigitEnd = cursor + currentConsumed;
                    cursor += currentConsumed;
                    continue;
                }

                if (IsNumberSeparator((char)current.Value) &&
                    cursor + currentConsumed < value.Length &&
                    !content.IsMasked(cursor + currentConsumed) &&
                    TryDecode(value, cursor + currentConsumed, out var next, out _) &&
                    Rune.IsDigit(next))
                {
                    cursor += currentConsumed;
                    continue;
                }

                break;
            }

            tokens.Add(new NumberToken(
                start,
                lastDigitEnd - start,
                value[start..lastDigitEnd]));
            index = lastDigitEnd;
        }

        return tokens;
    }

    private static bool OnlyNeutralBetween(
        string value,
        int start,
        int end,
        ContentMask content)
    {
        if (end <= start) return false;

        var index = start;
        while (index < end)
        {
            if (content.IsMasked(index)) return false;

            if (!TryDecode(value, index, out var rune, out var consumed))
                return false;

            if (rune.Value <= char.MaxValue &&
                BidiScanner.IsBidiControl((char)rune.Value))
            {
                return false;
            }

            var bidiClass = UnicodeInfo.GetBidiClass(rune);
            if (bidiClass is not (
                BidiClass.ES or
                BidiClass.ET or
                BidiClass.CS or
                BidiClass.ON or
                BidiClass.WS))
            {
                return false;
            }

            index += consumed;
        }

        return true;
    }

    private static bool IsAllowed(string text, string culture, LintConfig config)
    {
        if (config.AllowLatin.Any(term => term.AppliesTo(culture) && term.Matches(text)))
            return true;

        return config.Rules.TryGetValue(DiagnosticCodes.UnisolatedLatin, out var rule) &&
               rule.Allow.Any(term => string.Equals(term, text, StringComparison.Ordinal));
    }

    private static int FindControl(LatinCandidate candidate)
    {
        for (var index = candidate.Start; index < candidate.End; index++)
        {
            if (BidiScanner.IsBidiControl(candidate.Text[index - candidate.Start]))
                return index;
        }

        return -1;
    }

    private static bool TryDecode(
        string value,
        int index,
        out Rune rune,
        out int consumed) =>
        Rune.DecodeFromUtf16(value.AsSpan(index), out rune, out consumed) ==
        System.Buffers.OperationStatus.Done;

    private static bool IsNumberSeparator(char c) =>
        c is '.' or ',' or '\u066B' or '\u066C' or '-' or '/' or ':';

    private static string DescribeControl(char c) =>
        $"{BidiScanner.ControlName(c)} (U+{(int)c:X4})";

    private sealed record IsolationRegion(
        int Start,
        int End,
        string Label,
        IsolationDirection Direction);

    private sealed record NumberToken(int Start, int Length, string Text)
    {
        public int End => Start + Length;
    }
}
