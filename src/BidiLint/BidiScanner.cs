using System.Text;
using System.Text.RegularExpressions;

namespace BidiLint;

// Character-level facts about one string, computed once and shared by the
// checks. It identifies scripts and control characters; BidiResolver owns
// UAX #9 levels and visual ordering.
public sealed record StringScan
{
    public bool HasArabic { get; init; }
    public bool HasHebrew { get; init; }
    public bool HasRtlScript { get; init; }
    public bool HasWesternDigits { get; init; }
    public bool HasArabicIndicDigits { get; init; }
    public bool HasBidiControls { get; init; }
    public bool HasDirectionalMarks { get; init; }

    // Latin runs that are NOT inside a bidi isolate span (FSI/LRI/RLI .. PDI).
    public List<string> UnisolatedLatinRuns { get; } = new();

    // Isolate openers never closed by a PDI, and PDIs with no opener.
    public int UnclosedIsolates { get; init; }
    public int StrayClosers { get; init; }
}

public static class BidiScanner
{
    public const char Lri = '\u2066';
    public const char Rli = '\u2067';
    public const char Fsi = '\u2068';
    public const char Pdi = '\u2069';

    public const char Lre = '\u202A';
    public const char Rle = '\u202B';
    public const char Pdf = '\u202C';
    public const char Lro = '\u202D';
    public const char Rlo = '\u202E';

    public const char Lrm = '\u200E';
    public const char Rlm = '\u200F';
    public const char Alm = '\u061C';

    public static bool IsIsolateControl(char c) => c is >= Lri and <= Pdi;

    public static bool IsLegacyControl(char c) => c is >= Lre and <= Rlo;

    public static bool IsDirectionalMark(char c) => c is Lrm or Rlm or Alm;

    public static bool IsBidiControl(char c) =>
        IsIsolateControl(c) || IsLegacyControl(c) || IsDirectionalMark(c);

    public static string ControlName(char c) => c switch
    {
        Lri => "LRI",
        Rli => "RLI",
        Fsi => "FSI",
        Pdi => "PDI",
        Lre => "LRE",
        Rle => "RLE",
        Pdf => "PDF",
        Lro => "LRO",
        Rlo => "RLO",
        Lrm => "LRM",
        Rlm => "RLM",
        Alm => "ALM",
        _ => "bidi control",
    };

    // A Latin run: brand-like tokens ("ASP.NET Core", "C#", ".NET", "Visual Studio").
    // Tokens join across a dot or a single space when another token follows, so a
    // multi-word product name reads as one run.
    private static readonly Regex LatinRun = new(
        @"\.?[A-Za-z][A-Za-z0-9+#]*(?:[ .][A-Za-z][A-Za-z0-9+#]*)*",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    private static readonly Regex Placeholder = new(
        @"\{[^}]+\}",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    public static bool IsArabicChar(char c) =>
        !char.IsSurrogate(c) && IsArabicChar(new Rune(c));

    public static bool IsArabicChar(Rune value) =>
        UnicodeInfo.GetScript(value) == UnicodeScript.Arabic && UnicodeInfo.IsRtlCharacter(value);

    public static bool IsHebrewChar(char c) =>
        !char.IsSurrogate(c) && IsHebrewChar(new Rune(c));

    public static bool IsHebrewChar(Rune value) =>
        UnicodeInfo.GetScript(value) == UnicodeScript.Hebrew && UnicodeInfo.IsRtlCharacter(value);

    public static bool IsRtlChar(char c) =>
        !char.IsSurrogate(c) && IsRtlChar(new Rune(c));

    public static bool IsRtlChar(Rune value) => UnicodeInfo.IsRtlCharacter(value);

    public static StringScan Scan(string value)
    {
        var hasArabic = false;
        var hasHebrew = false;
        var hasRtlScript = false;
        var hasBidiControls = false;
        var hasDirectionalMarks = false;
        foreach (var scalar in UnicodeScalars.Enumerate(value))
        {
            if (!UnicodeInfo.IsRtlCharacter(scalar.Value)) continue;

            hasRtlScript = true;
            var script = UnicodeInfo.GetScript(scalar.Value);
            if (script == UnicodeScript.Arabic) hasArabic = true;
            if (script == UnicodeScript.Hebrew) hasHebrew = true;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (IsBidiControl(c)) hasBidiControls = true;
            if (IsDirectionalMark(c)) hasDirectionalMarks = true;
        }

        var scan = new StringScan
        {
            HasArabic = hasArabic,
            HasHebrew = hasHebrew,
            HasRtlScript = hasRtlScript,
            HasWesternDigits = value.Any(c => c is >= '0' and <= '9'),
            HasArabicIndicDigits = value.Any(c =>
                (c >= '\u0660' && c <= '\u0669') || (c >= '\u06F0' && c <= '\u06F9')),
            HasBidiControls = hasBidiControls,
            HasDirectionalMarks = hasDirectionalMarks,
        };

        // Track isolate depth per character index, so the caller can tell whether
        // a Latin run sits inside an isolate span.
        var depth = new int[value.Length + 1];
        var d = 0;
        var stray = 0;
        for (var i = 0; i < value.Length; i++)
        {
            depth[i] = d;
            var c = value[i];
            if (c is Lri or Rli or Fsi) d++;
            else if (c is Pdi)
            {
                if (d == 0) stray++;
                else d--;
            }
        }
        depth[value.Length] = d;

        foreach (Match m in LatinRun.Matches(value))
        {
            var run = m.Value.Trim();
            if (!IsSignificantRun(run)) continue;
            if (depth[m.Index] == 0)
                scan.UnisolatedLatinRuns.Add(run);
        }

        return scan with { UnclosedIsolates = d, StrayClosers = stray };
    }

    // Single plain letters ("a", "x") are usually harmless; runs that look like
    // names, code or versions are the ones that flip order in an RTL line.
    private static bool IsSignificantRun(string run) => run.Length >= 2;

    public static List<string> Placeholders(string value) =>
        Placeholder.Matches(value)
            .Select(m => m.Value)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    internal static IEnumerable<(int Start, int Length)> PlaceholderSpans(string value) =>
        Placeholder.Matches(value).Select(m => (m.Index, m.Length));
}
