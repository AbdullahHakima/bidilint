using System.Globalization;

namespace BidiLint;

// A deliberately small BCP-47 validator. It exists to stop arbitrary files
// such as app.json from being treated as locales, not to replace a full
// language-tag library.
public sealed record CultureTag(
    string Name,
    string Language,
    string? Script,
    string? Region,
    bool IsRightToLeft)
{
    private static readonly HashSet<string> RtlLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ar", "he", "fa", "ur", "ps", "sd", "ug", "yi", "dv", "ckb", "prs",
        "haz", "glk", "mzn", "lrc", "azb", "nqo", "syr", "sam", "mid", "mand",
        "mani", "ks", "bal", "brh", "khw", "skr", "rhg",
    };

    private static readonly HashSet<string> RtlScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Arab", "Hebr", "Syrc", "Thaa", "Nkoo", "Samr", "Mand", "Adlm", "Rohg",
    };

    private static readonly Lazy<HashSet<string>> KnownLanguages = new(BuildKnownLanguages);

    public static bool TryParse(string? value, out CultureTag? tag)
    {
        tag = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var candidate = value.Trim().Replace('_', '-');
        var dot = candidate.LastIndexOf('.');
        if (dot > 0)
        {
            var encoding = candidate[(dot + 1)..];
            if (encoding.StartsWith("UTF", StringComparison.OrdinalIgnoreCase) ||
                encoding.StartsWith("ISO", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[..dot];
            }
        }

        var parts = candidate.Split('-');
        if (parts.Length == 0 || parts.Any(string.IsNullOrEmpty)) return false;

        var language = parts[0].ToLowerInvariant();
        if (language.Length is < 2 or > 8 || !language.All(IsAsciiLetter)) return false;
        if (!KnownLanguages.Value.Contains(language)) return false;

        string? script = null;
        string? region = null;
        var index = 1;

        if (index < parts.Length && parts[index].Length == 4 && parts[index].All(IsAsciiLetter))
        {
            script = NormalizeScript(parts[index]);
            index++;
        }

        if (index < parts.Length && IsRegion(parts[index]))
        {
            region = parts[index].ToUpperInvariant();
            index++;
        }

        var suffixStart = index;
        var inExtension = false;
        for (; index < parts.Length; index++)
        {
            var part = parts[index];
            if (!part.All(char.IsAsciiLetterOrDigit)) return false;
            if (part.Length is < 1 or > 8) return false;

            // A singleton starts an extension; it must be followed by at least
            // one subtag. Variants are 5-8 characters or 4 starting with a digit.
            if (part.Length == 1)
            {
                if (index + 1 >= parts.Length) return false;
                inExtension = true;
                continue;
            }

            if (inExtension && part.Length is >= 2 and <= 8) continue;
            if (part.Length is 5 or 6 or 7 or 8) continue;
            if (part.Length == 4 && char.IsAsciiDigit(part[0])) continue;
            return false;
        }

        var nameParts = new List<string> { language };
        if (script is not null) nameParts.Add(script);
        if (region is not null) nameParts.Add(region);
        for (var i = suffixStart; i < parts.Length; i++)
        {
            nameParts.Add(parts[i].ToLowerInvariant());
        }

        var name = string.Join('-', nameParts);
        var rtl = RtlLanguages.Contains(language) ||
                  (script is not null && RtlScripts.Contains(script));
        tag = new CultureTag(name, language, script, region, rtl);
        return true;
    }

    public static bool IsRtl(string culture) =>
        TryParse(culture, out var tag) && tag!.IsRightToLeft;

    private static bool IsRegion(string part) =>
        (part.Length == 2 && part.All(IsAsciiLetter)) ||
        (part.Length == 3 && part.All(char.IsAsciiDigit));

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static string NormalizeScript(string script) =>
        char.ToUpperInvariant(script[0]) + script[1..].ToLowerInvariant();

    private static HashSet<string> BuildKnownLanguages()
    {
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "en", "ar", "he", "fa", "ur", "ps", "sd", "ug", "yi", "dv",
            "fr", "de", "es", "it", "pt", "nl", "pl", "ru", "uk", "ro",
            "cs", "sk", "sl", "hr", "sr", "bs", "mk", "sq", "bg", "el",
            "tr", "az", "kk", "ky", "uz", "tk", "tg", "mn", "hy", "ka", "ku",
            "hi", "bn", "gu", "mr", "pa", "ta", "te", "kn", "ml", "si",
            "ne", "as", "or", "my", "km", "lo", "th", "vi", "id", "ms",
            "zh", "ja", "ko", "sv", "da", "fi", "nb", "no", "is", "et",
            "lv", "lt", "af", "sw", "am", "ha", "yo", "ig", "zu", "so",
            "ckb", "prs", "haz", "glk", "mzn", "lrc", "azb", "nqo", "syr",
            "sam", "mid", "mand", "mani", "ks", "bal", "brh", "khw", "skr", "rhg",
        };

        try
        {
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                if (string.IsNullOrEmpty(culture.Name)) continue;
                var language = culture.Name.Split('-')[0];
                if (language.Length is >= 2 and <= 8) languages.Add(language);
            }
        }
        catch (CultureNotFoundException)
        {
            // The static list above keeps discovery useful in invariant mode.
        }

        return languages;
    }
}
