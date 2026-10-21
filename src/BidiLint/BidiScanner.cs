using System.Text.RegularExpressions;

namespace BidiLint;

// Character-level facts about one string, computed once and shared by the checks.
// The ranges follow the Unicode blocks Arabic text actually uses; the goal is
// linting, not a full UAX #9 implementation.
public sealed record StringScan
{
    public bool HasArabic { get; init; }
    public bool HasWesternDigits { get; init; }
    public bool HasArabicIndicDigits { get; init; }

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

    // A Latin run: brand-like tokens ("ASP.NET Core", "C#", ".NET", "Visual Studio").
    // Tokens join across a dot or a single space when another token follows, so a
    // multi-word product name reads as one run.
    private static readonly Regex LatinRun = new(
        @"\.?[A-Za-z][A-Za-z0-9+#]*(?:[ .][A-Za-z][A-Za-z0-9+#]*)*",
        RegexOptions.Compiled);

    public static bool IsArabicChar(char c) =>
        (c >= '\u0620' && c <= '\u064A') ||   // Arabic
        (c >= '\u066E' && c <= '\u06D3') ||   // Arabic Supplement letters
        (c >= '\u06FA' && c <= '\u06FF') ||
        (c >= '\u0750' && c <= '\u077F') ||   // Arabic Supplement
        (c >= '\u08A0' && c <= '\u08FF') ||   // Arabic Extended-A
        (c >= '\uFB50' && c <= '\uFDFF') ||   // Presentation Forms-A
        (c >= '\uFE70' && c <= '\uFEFF');     // Presentation Forms-B

    public static StringScan Scan(string value)
    {
        var scan = new StringScan
        {
            HasArabic = value.Any(IsArabicChar),
            HasWesternDigits = value.Any(c => c is >= '0' and <= '9'),
            HasArabicIndicDigits = value.Any(c =>
                (c >= '\u0660' && c <= '\u0669') || (c >= '\u06F0' && c <= '\u06F9')),
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
    private static bool IsSignificantRun(string run)
    {
        if (run.Length >= 2) return true;
        return run.Length == 1 && false; // reserved for future single-letter rules
    }

    private static readonly Regex Placeholder = new(@"\{[^}]+\}", RegexOptions.Compiled);

    public static List<string> Placeholders(string value) =>
        Placeholder.Matches(value).Select(m => m.Value).OrderBy(p => p, StringComparer.Ordinal).ToList();
}
