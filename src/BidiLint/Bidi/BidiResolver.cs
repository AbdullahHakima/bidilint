using System.Text;

namespace BidiLint;

internal enum BidiParagraphDirection
{
    Auto,
    LeftToRight,
    RightToLeft,
}

internal sealed record BidiResolution(
    int ParagraphLevel,
    int[] Levels,
    int[] VisualOrder,
    bool[] Removed);

// A UAX #9 implementation for one paragraph. Levels are kept for every input
// scalar; VisualOrder contains only characters that survive X9, matching the
// conformance files and the text that is actually displayed.
internal static class BidiResolver
{
    private const int MaxDepth = 125;

    public static BidiResolution Resolve(string text, BidiParagraphDirection direction = BidiParagraphDirection.Auto)
    {
        var scalars = UnicodeScalars.Enumerate(text).ToArray();
        var classes = scalars.Select(scalar => UnicodeInfo.GetBidiClass(scalar.Value)).ToArray();
        var codePoints = scalars.Select(scalar => scalar.CodePoint).ToArray();
        return Resolve(classes, direction, codePoints);
    }

    internal static BidiResolution Resolve(
        IReadOnlyList<BidiClass> originalClasses,
        BidiParagraphDirection direction = BidiParagraphDirection.Auto,
        IReadOnlyList<int>? codePoints = null)
    {
        var original = originalClasses.ToArray();
        var count = original.Length;
        if (count == 0)
        {
            return new BidiResolution(direction == BidiParagraphDirection.RightToLeft ? 1 : 0, [], [], []);
        }

        var matchingPdi = BuildMatchingPdi(original);
        var paragraphLevel = direction switch
        {
            BidiParagraphDirection.LeftToRight => 0,
            BidiParagraphDirection.RightToLeft => 1,
            _ => FindFirstStrong(original, 0, count, matchingPdi, out var strong) &&
                 strong is BidiClass.R or BidiClass.AL
                ? 1
                : 0,
        };

        var types = original.ToArray();
        var levels = new int[count];
        var removed = new bool[count];
        for (var index = 0; index < count; index++)
        {
            removed[index] = IsRemovedByX9(original[index]);
        }

        AssignExplicitLevels(original, matchingPdi, paragraphLevel, types, levels);

        var kept = Enumerable.Range(0, count).Where(index => !removed[index]).ToArray();
        if (kept.Length == 0)
        {
            return new BidiResolution(paragraphLevel, levels, [], removed);
        }

        var explicitLevels = (int[])levels.Clone();
        var runs = BuildLevelRuns(kept, levels);
        var runByOriginal = new int[count];
        Array.Fill(runByOriginal, -1);
        foreach (var run in runs)
        {
            for (var position = run.Start; position <= run.End; position++)
            {
                runByOriginal[kept[position]] = run.Index;
            }
        }

        var previousKept = new int[count];
        var nextKept = new int[count];
        Array.Fill(previousKept, -1);
        Array.Fill(nextKept, -1);
        for (var position = 0; position < kept.Length; position++)
        {
            if (position > 0) previousKept[kept[position]] = kept[position - 1];
            if (position < kept.Length - 1) nextKept[kept[position]] = kept[position + 1];
        }

        var assigned = new bool[runs.Count];
        for (var runIndex = 0; runIndex < runs.Count; runIndex++)
        {
            if (assigned[runIndex]) continue;

            var sequence = new List<int>();
            var current = runIndex;
            while (!assigned[current])
            {
                assigned[current] = true;
                var run = runs[current];
                for (var position = run.Start; position <= run.End; position++)
                {
                    sequence.Add(kept[position]);
                }

                var last = kept[run.End];
                if (!IsIsolateInitiator(original[last]) || matchingPdi[last] < 0) break;

                var nextRun = runByOriginal[matchingPdi[last]];
                if (nextRun < 0) break;
                current = nextRun;
            }

            var indices = sequence.ToArray();
            var level = explicitLevels[indices[0]];
            var sos = SequenceBoundaryType(
                level,
                previousKept[indices[0]] >= 0 ? explicitLevels[previousKept[indices[0]]] : paragraphLevel);
            var lastIndex = indices[^1];
            var eos = IsIsolateInitiator(original[lastIndex]) || nextKept[lastIndex] < 0
                ? SequenceBoundaryType(level, paragraphLevel)
                : SequenceBoundaryType(level, explicitLevels[nextKept[lastIndex]]);

            ResolveSequence(
                indices,
                level,
                sos,
                eos,
                original,
                types,
                levels,
                codePoints);
        }

        ApplyL1(kept, original, paragraphLevel, levels);
        var visualOrder = ApplyL2(kept, levels);
        return new BidiResolution(paragraphLevel, levels, visualOrder, removed);
    }

    private static void AssignExplicitLevels(
        BidiClass[] original,
        int[] matchingPdi,
        int paragraphLevel,
        BidiClass[] types,
        int[] levels)
    {
        var stack = new List<StatusEntry>
        {
            new(paragraphLevel, OverrideStatus.Neutral, false),
        };
        var overflowIsolateCount = 0;
        var overflowEmbeddingCount = 0;
        var validIsolateCount = 0;

        for (var index = 0; index < original.Length; index++)
        {
            var type = original[index];
            var top = stack[^1];
            switch (type)
            {
                case BidiClass.RLE:
                case BidiClass.LRE:
                case BidiClass.RLO:
                case BidiClass.LRO:
                    {
                        levels[index] = top.Level;
                        var newLevel = type is BidiClass.RLE or BidiClass.RLO
                            ? NextOdd(top.Level)
                            : NextEven(top.Level);
                        if (newLevel <= MaxDepth && overflowIsolateCount == 0 && overflowEmbeddingCount == 0)
                        {
                            var overrideStatus = type switch
                            {
                                BidiClass.RLO => OverrideStatus.RightToLeft,
                                BidiClass.LRO => OverrideStatus.LeftToRight,
                                _ => OverrideStatus.Neutral,
                            };
                            stack.Add(new StatusEntry(newLevel, overrideStatus, false));
                        }
                        else if (overflowIsolateCount == 0)
                        {
                            overflowEmbeddingCount++;
                        }

                        break;
                    }

                case BidiClass.RLI:
                case BidiClass.LRI:
                case BidiClass.FSI:
                    {
                        levels[index] = top.Level;
                        ApplyOverride(ref types[index], top.Override);

                        var isRtl = type switch
                        {
                            BidiClass.RLI => true,
                            BidiClass.LRI => false,
                            _ => IsRtlFirstStrong(original, index, matchingPdi),
                        };
                        var newLevel = isRtl ? NextOdd(top.Level) : NextEven(top.Level);
                        if (newLevel <= MaxDepth && overflowIsolateCount == 0 && overflowEmbeddingCount == 0)
                        {
                            validIsolateCount++;
                            stack.Add(new StatusEntry(newLevel, OverrideStatus.Neutral, true));
                        }
                        else
                        {
                            overflowIsolateCount++;
                        }

                        break;
                    }

                case BidiClass.PDI:
                    {
                        if (overflowIsolateCount > 0)
                        {
                            overflowIsolateCount--;
                        }
                        else if (validIsolateCount > 0)
                        {
                            overflowEmbeddingCount = 0;
                            while (!stack[^1].Isolate)
                            {
                                stack.RemoveAt(stack.Count - 1);
                            }

                            stack.RemoveAt(stack.Count - 1);
                            validIsolateCount--;
                        }

                        top = stack[^1];
                        levels[index] = top.Level;
                        ApplyOverride(ref types[index], top.Override);
                        break;
                    }

                case BidiClass.PDF:
                    {
                        levels[index] = top.Level;
                        if (overflowIsolateCount > 0)
                        {
                            break;
                        }

                        if (overflowEmbeddingCount > 0)
                        {
                            overflowEmbeddingCount--;
                        }
                        else if (!top.Isolate && stack.Count >= 2)
                        {
                            stack.RemoveAt(stack.Count - 1);
                        }

                        break;
                    }

                case BidiClass.B:
                    levels[index] = paragraphLevel;
                    break;

                case BidiClass.BN:
                    levels[index] = top.Level;
                    break;

                default:
                    levels[index] = top.Level;
                    ApplyOverride(ref types[index], top.Override);
                    break;
            }
        }
    }

    private static List<LevelRun> BuildLevelRuns(int[] kept, int[] levels)
    {
        var runs = new List<LevelRun>();
        var start = 0;
        for (var position = 1; position <= kept.Length; position++)
        {
            if (position < kept.Length && levels[kept[position]] == levels[kept[start]]) continue;

            runs.Add(new LevelRun(runs.Count, start, position - 1, levels[kept[start]]));
            start = position;
        }

        return runs;
    }

    private static void ResolveSequence(
        int[] indices,
        int level,
        BidiClass sos,
        BidiClass eos,
        BidiClass[] original,
        BidiClass[] types,
        int[] levels,
        IReadOnlyList<int>? codePoints)
    {
        ResolveWeakTypes(indices, sos, types, original);
        ResolveNeutralTypes(indices, level, sos, eos, original, types, codePoints);
        ResolveImplicitLevels(indices, types, levels);
    }

    private static void ResolveWeakTypes(int[] indices, BidiClass sos, BidiClass[] types, BidiClass[] original)
    {
        // W1
        for (var position = 0; position < indices.Length; position++)
        {
            var index = indices[position];
            if (types[index] != BidiClass.NSM) continue;

            if (position == 0)
            {
                types[index] = sos;
            }
            else
            {
                var previous = indices[position - 1];
                types[index] = IsIsolateInitiator(original[previous]) || original[previous] == BidiClass.PDI
                    ? BidiClass.ON
                    : types[previous];
            }
        }

        // W2
        var lastStrong = sos;
        foreach (var index in indices)
        {
            if (types[index] == BidiClass.EN && lastStrong == BidiClass.AL)
            {
                types[index] = BidiClass.AN;
            }

            if (types[index] is BidiClass.L or BidiClass.R or BidiClass.AL)
            {
                lastStrong = types[index];
            }
        }

        // W3
        foreach (var index in indices)
        {
            if (types[index] == BidiClass.AL) types[index] = BidiClass.R;
        }

        // W4
        for (var position = 1; position < indices.Length - 1; position++)
        {
            var index = indices[position];
            var previous = types[indices[position - 1]];
            var next = types[indices[position + 1]];
            if (types[index] == BidiClass.ES && previous == BidiClass.EN && next == BidiClass.EN)
            {
                types[index] = BidiClass.EN;
            }
            else if (types[index] == BidiClass.CS &&
                     ((previous == BidiClass.EN && next == BidiClass.EN) ||
                      (previous == BidiClass.AN && next == BidiClass.AN)))
            {
                types[index] = previous;
            }
        }

        // W5
        for (var position = 0; position < indices.Length;)
        {
            if (types[indices[position]] != BidiClass.ET)
            {
                position++;
                continue;
            }

            var start = position;
            while (position < indices.Length && types[indices[position]] == BidiClass.ET) position++;
            var end = position;
            var adjacentToEuropeanNumber =
                (start > 0 && types[indices[start - 1]] == BidiClass.EN) ||
                (end < indices.Length && types[indices[end]] == BidiClass.EN);
            if (!adjacentToEuropeanNumber) continue;

            for (var index = start; index < end; index++)
            {
                types[indices[index]] = BidiClass.EN;
            }
        }

        // W6
        foreach (var index in indices)
        {
            if (types[index] is BidiClass.ES or BidiClass.ET or BidiClass.CS)
            {
                types[index] = BidiClass.ON;
            }
        }

        // W7
        lastStrong = sos;
        foreach (var index in indices)
        {
            if (types[index] == BidiClass.EN && lastStrong == BidiClass.L)
            {
                types[index] = BidiClass.L;
            }

            if (types[index] is BidiClass.L or BidiClass.R)
            {
                lastStrong = types[index];
            }
        }
    }

    private static void ResolveNeutralTypes(
        int[] indices,
        int level,
        BidiClass sos,
        BidiClass eos,
        BidiClass[] original,
        BidiClass[] types,
        IReadOnlyList<int>? codePoints)
    {
        ResolveBrackets(indices, level, sos, original, types, codePoints);

        var embedding = (level & 1) == 0 ? BidiClass.L : BidiClass.R;
        for (var position = 0; position < indices.Length;)
        {
            if (!IsNeutralOrIsolate(types[indices[position]]))
            {
                position++;
                continue;
            }

            var start = position;
            while (position < indices.Length && IsNeutralOrIsolate(types[indices[position]])) position++;
            var end = position;

            var before = start == 0 ? sos : StrongForNeutral(types[indices[start - 1]]);
            var after = end == indices.Length ? eos : StrongForNeutral(types[indices[end]]);
            if (before == after)
            {
                for (var index = start; index < end; index++)
                {
                    types[indices[index]] = before;
                }
            }
            else
            {
                for (var index = start; index < end; index++)
                {
                    types[indices[index]] = embedding;
                }
            }
        }
    }

    private static void ResolveBrackets(
        int[] indices,
        int level,
        BidiClass sos,
        BidiClass[] original,
        BidiClass[] types,
        IReadOnlyList<int>? codePoints)
    {
        if (codePoints is null) return;

        var stack = new List<OpenBracket>();
        var pairs = new List<BracketPair>();
        var overflow = false;
        for (var position = 0; position < indices.Length; position++)
        {
            var index = indices[position];
            if (types[index] != BidiClass.ON) continue;

            var codePoint = codePoints[index];
            if (codePoint > 0x10FFFF) continue;
            var rune = new Rune(codePoint);
            if (!UnicodeInfo.TryGetBracketPair(rune, out var bracket)) continue;

            if (bracket.Type == BracketType.Open)
            {
                if (stack.Count >= 63)
                {
                    overflow = true;
                    break;
                }

                stack.Add(new OpenBracket(bracket.PairedCodePoint, position, index));
                continue;
            }

            if (stack.Count == 0) continue;
            for (var stackIndex = stack.Count - 1; stackIndex >= 0; stackIndex--)
            {
                if (!BracketsMatch(stack[stackIndex].PairedCodePoint, codePoint)) continue;
                pairs.Add(new BracketPair(stack[stackIndex].Position, position, stack[stackIndex].Index, index));
                stack.RemoveRange(stackIndex, stack.Count - stackIndex);
                break;
            }
        }

        if (overflow) return;

        pairs.Sort((left, right) => left.OpenPosition.CompareTo(right.OpenPosition));
        var embedding = (level & 1) == 0 ? BidiClass.L : BidiClass.R;
        var opposite = embedding == BidiClass.L ? BidiClass.R : BidiClass.L;

        foreach (var pair in pairs)
        {
            var hasEmbedding = false;
            var hasOpposite = false;
            for (var position = pair.OpenPosition + 1; position < pair.ClosePosition; position++)
            {
                var type = StrongForNeutral(types[indices[position]]);
                if (type == embedding) hasEmbedding = true;
                else if (type == opposite) hasOpposite = true;
            }

            BidiClass resolved;
            if (hasEmbedding)
            {
                resolved = embedding;
            }
            else if (hasOpposite)
            {
                var preceding = sos;
                for (var position = pair.OpenPosition - 1; position >= 0; position--)
                {
                    var type = StrongForNeutral(types[indices[position]]);
                    if (type is BidiClass.L or BidiClass.R)
                    {
                        preceding = type;
                        break;
                    }
                }

                resolved = preceding == opposite ? opposite : embedding;
            }
            else
            {
                continue;
            }

            types[pair.OpenIndex] = resolved;
            types[pair.CloseIndex] = resolved;
            ResolveFollowingMarks(indices, pair.OpenPosition, resolved, original, types);
            ResolveFollowingMarks(indices, pair.ClosePosition, resolved, original, types);
        }
    }

    private static void ResolveFollowingMarks(
        int[] indices,
        int position,
        BidiClass resolved,
        BidiClass[] original,
        BidiClass[] types)
    {
        for (var index = position + 1; index < indices.Length; index++)
        {
            if (original[indices[index]] != BidiClass.NSM) return;
            types[indices[index]] = resolved;
        }
    }

    private static void ResolveImplicitLevels(int[] indices, BidiClass[] types, int[] levels)
    {
        foreach (var index in indices)
        {
            var type = types[index];
            if ((levels[index] & 1) == 0)
            {
                if (type == BidiClass.R) levels[index]++;
                else if (type is BidiClass.AN or BidiClass.EN) levels[index] += 2;
            }
            else if (type is BidiClass.L or BidiClass.AN or BidiClass.EN)
            {
                levels[index]++;
            }
        }
    }

    private static void ApplyL1(int[] kept, BidiClass[] original, int paragraphLevel, int[] levels)
    {
        for (var position = 0; position < kept.Length; position++)
        {
            var type = original[kept[position]];
            if (type is not (BidiClass.S or BidiClass.B)) continue;

            levels[kept[position]] = paragraphLevel;
            for (var previous = position - 1;
                 previous >= 0 && IsWhitespaceOrIsolate(original[kept[previous]]);
                 previous--)
            {
                levels[kept[previous]] = paragraphLevel;
            }
        }

        for (var position = kept.Length - 1;
             position >= 0 && IsWhitespaceOrIsolate(original[kept[position]]);
             position--)
        {
            levels[kept[position]] = paragraphLevel;
        }
    }

    private static int[] ApplyL2(int[] kept, int[] levels)
    {
        var order = (int[])kept.Clone();
        var highest = 0;
        var lowestOdd = int.MaxValue;
        foreach (var index in kept)
        {
            highest = Math.Max(highest, levels[index]);
            if ((levels[index] & 1) != 0) lowestOdd = Math.Min(lowestOdd, levels[index]);
        }

        if (lowestOdd == int.MaxValue) return order;

        for (var level = highest; level >= lowestOdd; level--)
        {
            var start = 0;
            while (start < order.Length)
            {
                if (levels[order[start]] < level)
                {
                    start++;
                    continue;
                }

                var end = start;
                while (end < order.Length && levels[order[end]] >= level) end++;
                Array.Reverse(order, start, end - start);
                start = end;
            }
        }

        return order;
    }

    private static int[] BuildMatchingPdi(BidiClass[] original)
    {
        var matching = new int[original.Length];
        Array.Fill(matching, -1);
        var stack = new Stack<int>();
        for (var index = 0; index < original.Length; index++)
        {
            if (IsIsolateInitiator(original[index]))
            {
                stack.Push(index);
            }
            else if (original[index] == BidiClass.PDI && stack.Count > 0)
            {
                matching[stack.Pop()] = index;
            }
        }

        return matching;
    }

    private static bool FindFirstStrong(
        BidiClass[] original,
        int start,
        int end,
        int[] matchingPdi,
        out BidiClass strong)
    {
        for (var index = start; index < end; index++)
        {
            var type = original[index];
            if (IsIsolateInitiator(type))
            {
                var pdi = matchingPdi[index];
                if (pdi < 0 || pdi >= end) break;
                index = pdi;
                continue;
            }

            if (type is BidiClass.L or BidiClass.R or BidiClass.AL)
            {
                strong = type;
                return true;
            }
        }

        strong = default;
        return false;
    }

    private static bool IsRtlFirstStrong(
        BidiClass[] original,
        int index,
        int[] matchingPdi)
    {
        var end = matchingPdi[index] >= 0 ? matchingPdi[index] : original.Length;
        return FindFirstStrong(original, index + 1, end, matchingPdi, out var strong) &&
               strong is BidiClass.R or BidiClass.AL;
    }

    private static BidiClass SequenceBoundaryType(int level, int adjacentLevel) =>
        (Math.Max(level, adjacentLevel) & 1) == 0 ? BidiClass.L : BidiClass.R;

    private static BidiClass StrongForNeutral(BidiClass type) =>
        type is BidiClass.EN or BidiClass.AN ? BidiClass.R : type;

    private static bool IsRemovedByX9(BidiClass type) =>
        type is BidiClass.RLE or BidiClass.LRE or BidiClass.RLO or BidiClass.LRO or BidiClass.PDF or BidiClass.BN;

    private static bool IsIsolateInitiator(BidiClass type) =>
        type is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;

    private static bool IsNeutralOrIsolate(BidiClass type) =>
        type is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON or
            BidiClass.FSI or BidiClass.LRI or BidiClass.RLI or BidiClass.PDI;

    private static bool IsWhitespaceOrIsolate(BidiClass type) =>
        type is BidiClass.WS or BidiClass.FSI or BidiClass.LRI or BidiClass.RLI or BidiClass.PDI;

    private static bool BracketsMatch(int expected, int actual) =>
        expected == actual ||
        (expected == 0x232A && actual == 0x3009) ||
        (expected == 0x3009 && actual == 0x232A);

    private static int NextOdd(int level) => (level & 1) == 0 ? level + 1 : level + 2;

    private static int NextEven(int level) => (level & 1) == 0 ? level + 2 : level + 1;

    private static void ApplyOverride(ref BidiClass type, OverrideStatus status)
    {
        if (status == OverrideStatus.LeftToRight) type = BidiClass.L;
        else if (status == OverrideStatus.RightToLeft) type = BidiClass.R;
    }

    private enum OverrideStatus
    {
        Neutral,
        LeftToRight,
        RightToLeft,
    }

    private readonly record struct StatusEntry(int Level, OverrideStatus Override, bool Isolate);

    private readonly record struct LevelRun(int Index, int Start, int End, int Level);

    private readonly record struct OpenBracket(int PairedCodePoint, int Position, int Index);

    private readonly record struct BracketPair(int OpenPosition, int ClosePosition, int OpenIndex, int CloseIndex);
}
