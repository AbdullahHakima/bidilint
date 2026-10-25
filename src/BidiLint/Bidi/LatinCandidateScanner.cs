using System.Text;
using System.Text.RegularExpressions;

namespace BidiLint;

internal enum LatinCandidateKind
{
    Brand,
    Url,
    Email,
    Path,
    Code,
    Version,
    FileName,
    CrossedBoundary,
}

internal sealed record LatinCandidate(
    int Start,
    int Length,
    string Text,
    LatinCandidateKind Kind)
{
    public int End => Start + Length;
}

internal static class LatinCandidateScanner
{
    private const int UrlPriority = 100;
    private const int EmailPriority = 95;
    private const int PathPriority = 90;
    private const int FilePriority = 85;
    private const int VersionPriority = 80;
    private const int CodePriority = 30;
    private const int BrandPriority = 40;

    private static readonly Regex UrlPattern = new(
        """(?i)\b(?:https?|ftp|file)://[^\s<>"'\u0001`]+""",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex WwwPattern = new(
        """(?i)\bwww\.[^\s<>"'\u0001`]+""",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex EmailPattern = new(
        """(?i)\b[A-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?(?:\.[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?)+\b""",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex PathPattern = new(
        """(?i)(?:\b[A-Za-z]:[\\/]|\.{1,2}[\\/]|~[\\/]|(?<![\p{L}\p{Nd}])/)[^\s<>"'\u0001|]+|\b[A-Za-z0-9._~-]+[\\/][A-Za-z0-9._~%+@-]+(?:[\\/][A-Za-z0-9._~%+@-]+)*""",
        RegexOptions.Compiled);

    private static readonly Regex FilePattern = new(
        """(?i)\b[A-Za-z0-9_~-][A-Za-z0-9._~-]*\.(?:json|jsonc|resx|arb|po|pot|strings|xml|xaml|cs|csproj|sln|ts|tsx|js|jsx|css|scss|html|htm|md|txt|yml|yaml|toml|ini|config|sh|ps1|bat|cmd|dll|exe|png|jpe?g|gif|svg|webp|pdf|zip|tar|gz|log|csv|sql|java|kt|kts|swift|go|rs|py|rb|php|cc|cpp|hpp|lock)\b""",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex VersionPattern = new(
        """(?i)\b(?:v(?:ersion)?\s?)?\d{1,4}(?:\.\d{1,4}){1,3}(?:[-+][A-Za-z0-9][A-Za-z0-9.-]*)?\b""",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex CodePattern = new(
        """(?i)(?:\b[A-Za-z_][A-Za-z0-9_]*(?:(?:\.|::|->|\$)[A-Za-z0-9_]+|#)*|\.NET\b|#include\b)""",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    public static IReadOnlyList<LatinCandidate> Find(
        string value,
        ContentMask content,
        BidiStructure structure)
    {
        var candidates = new List<ScoredCandidate>();
        AddRegexCandidates(value, content.Text, UrlPattern, LatinCandidateKind.Url, UrlPriority, candidates);
        AddRegexCandidates(value, content.Text, WwwPattern, LatinCandidateKind.Url, UrlPriority, candidates);
        AddRegexCandidates(value, content.Text, EmailPattern, LatinCandidateKind.Email, EmailPriority, candidates);
        AddRegexCandidates(value, content.Text, PathPattern, LatinCandidateKind.Path, PathPriority, candidates);
        AddRegexCandidates(value, content.Text, FilePattern, LatinCandidateKind.FileName, FilePriority, candidates);
        AddRegexCandidates(value, content.Text, VersionPattern, LatinCandidateKind.Version, VersionPriority, candidates);
        AddRegexCandidates(value, content.Text, CodePattern, LatinCandidateKind.Code, CodePriority, candidates);
        AddBrandCandidates(value, content.Text, candidates);

        var selected = SelectNonOverlapping(candidates);
        AddCrossBoundaryCandidates(value, content, selected, structure);
        selected.Sort((left, right) => left.Start.CompareTo(right.Start));
        return selected;

    }

    private static void AddRegexCandidates(
        string value,
        string masked,
        Regex pattern,
        LatinCandidateKind kind,
        int priority,
        List<ScoredCandidate> candidates)
    {
        foreach (Match match in pattern.Matches(masked))
        {
            if (match.Length < 2) continue;
            if (TryCreateCandidate(value, match.Index, match.Length, kind, out var candidate))
                candidates.Add(new ScoredCandidate(candidate, priority));
        }
    }

    private static void AddBrandCandidates(
        string value,
        string masked,
        List<ScoredCandidate> candidates)
    {
        var index = 0;
        while (index < masked.Length)
        {
            if (!TryDecode(masked, index, out var rune, out var consumed))
            {
                index++;
                continue;
            }

            if (!IsLatinLetter(rune))
            {
                index += consumed;
                continue;
            }

            var start = index;
            if (start > 0 && masked[start - 1] == '.') start--;

            var end = start;
            if (end < masked.Length && masked[end] == '.') end++;

            var lastWasSeparator = false;
            while (end < masked.Length)
            {
                if (!TryDecode(masked, end, out rune, out consumed)) break;

                if (IsLatinLetter(rune) || Rune.IsDigit(rune) || IsCodePunctuation(rune))
                {
                    end += consumed;
                    lastWasSeparator = false;
                    continue;
                }

                if (rune.Value is '.' or '\'' && !lastWasSeparator)
                {
                    var next = end + consumed;
                    if (HasLatinAfter(masked, next))
                    {
                        end = next;
                        continue;
                    }

                    break;
                }

                if (rune.Value == ':' &&
                    end + 1 < masked.Length &&
                    masked[end + 1] == ':' &&
                    HasLatinAfter(masked, end + 2))
                {
                    end += 2;
                    lastWasSeparator = false;
                    continue;
                }

                if (rune.Value is ' ' or '-' or '/')
                {
                    var next = end + consumed;
                    if (rune.Value == ' ' && next < masked.Length && masked[next] == ' ')
                        break;

                    if (HasLatinAfter(masked, next))
                    {
                        end = next;
                        lastWasSeparator = true;
                        continue;
                    }

                    break;
                }

                break;
            }

            if (end > start &&
                TryCreateCandidate(value, start, end - start, LatinCandidateKind.Brand, out var candidate))
            {
                candidates.Add(new ScoredCandidate(candidate, BrandPriority));
            }

            index = Math.Max(index + consumed, end);
        }
    }

    private static bool HasLatinAfter(string value, int index)
    {
        if (index >= value.Length) return false;
        return TryDecode(value, index, out var rune, out _) && IsLatinLetter(rune);
    }

    private static bool IsCodePunctuation(Rune rune) =>
        rune.Value is '+' or '#' or '_' or '$';

    private static bool TryCreateCandidate(
        string value,
        int start,
        int length,
        LatinCandidateKind kind,
        out LatinCandidate candidate)
    {
        candidate = null!;
        if (start < 0 || length < 1 || start + length > value.Length) return false;

        var end = start + length;
        while (end > start && IsTrailingPunctuation(value[end - 1]))
            end--;
        while (start < end && IsLeadingPunctuation(value[start]) && end - start > 1)
            start++;

        if (end - start < 2) return false;

        var text = value[start..end];
        if (text.Any(c => c == '\u0001' || BidiScanner.IsBidiControl(c)))
            return false;

        var hasLatin = false;
        foreach (var scalar in UnicodeScalars.Enumerate(text))
        {
            if (!Rune.IsLetter(scalar.Value)) continue;
            if (!IsLatinLetter(scalar.Value)) return false;
            hasLatin = true;
        }

        if (!hasLatin) return false;

        candidate = new LatinCandidate(start, end - start, text, kind);
        return true;
    }

    private static List<LatinCandidate> SelectNonOverlapping(
        List<ScoredCandidate> candidates)
    {
        var selected = new List<ScoredCandidate>();
        foreach (var item in candidates
                     .OrderBy(item => item.Candidate.Start)
                     .ThenByDescending(item => item.Priority)
                     .ThenByDescending(item => item.Candidate.Length))
        {
            var overlaps = false;
            foreach (var existing in selected)
            {
                if (item.Candidate.Start < existing.Candidate.End &&
                    existing.Candidate.Start < item.Candidate.End)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps) selected.Add(item);
        }

        return selected
            .Select(item => item.Candidate)
            .OrderBy(candidate => candidate.Start)
            .ToList();
    }

    private static void AddCrossBoundaryCandidates(
        string value,
        ContentMask content,
        List<LatinCandidate> selected,
        BidiStructure structure)
    {
        if (!structure.HasIsolateControls) return;

        for (var index = 0; index < value.Length; index++)
        {
            if (content.IsMasked(index)) continue;
            if (!BidiScanner.IsIsolateControl(value[index])) continue;

            var left = index;
            while (left > 0 && IsTokenContinuation(value[left - 1])) left--;
            var right = index + 1;
            while (right < value.Length && IsTokenContinuation(value[right])) right++;

            if (left == index || right == index + 1) continue;
            var leftText = value[left..index];
            var rightText = value[(index + 1)..right];
            if (!ContainsLatin(leftText) || !ContainsLatin(rightText)) continue;

            var start = left;
            var end = right;
            selected.RemoveAll(candidate =>
                candidate.Start < end && start < candidate.End);

            var text = value[start..end];
            selected.Add(new LatinCandidate(
                start,
                end - start,
                text,
                LatinCandidateKind.CrossedBoundary));
        }
    }

    private static bool IsTokenContinuation(char c) =>
        char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '+' or '#' or '$' or '\'' or '/' or ':';

    private static bool ContainsLatin(string value)
    {
        foreach (var scalar in UnicodeScalars.Enumerate(value))
        {
            if (IsLatinLetter(scalar.Value)) return true;
        }

        return false;
    }

    private static bool IsLatinLetter(Rune rune) =>
        Rune.IsLetter(rune) && UnicodeInfo.GetScript(rune) == UnicodeScript.Latin;

    private static bool IsTrailingPunctuation(char c) =>
        c is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}' or '"' or '\'' or '>';

    private static bool IsLeadingPunctuation(char c) =>
        c is ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}' or '"' or '\'' or '>';

    private static bool TryDecode(string value, int index, out Rune rune, out int consumed) =>
        Rune.DecodeFromUtf16(value.AsSpan(index), out rune, out consumed) ==
        System.Buffers.OperationStatus.Done;

    private sealed record ScoredCandidate(LatinCandidate Candidate, int Priority);
}
