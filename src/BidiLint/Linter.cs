namespace BidiLint;

// The checks. Structural checks compare every culture against the base file;
// bidi checks run on the RTL cultures' strings.
public static class Linter
{
    private static readonly HashSet<string> RtlCultures = new(StringComparer.OrdinalIgnoreCase)
        { "ar", "he", "fa", "ur", "dv", "ps", "sd", "ug", "yi" };

    public static bool IsRtl(string culture) =>
        RtlCultures.Contains(culture.Split('-')[0]);

    public static LintResult Lint(List<LocaleFile> files, LocaleFile baseFile)
    {
        var result = new LintResult();
        result.Files.AddRange(files);
        result.KeyCount = files.Sum(f => f.Entries.Count);

        foreach (var file in files)
        {
            if (!ReferenceEquals(file, baseFile))
                CompareToBase(result, baseFile, file);

            foreach (var (key, value) in file.Entries.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.Findings.Add(new Finding(FindingSeverity.Warning, "EmptyValue",
                        file.Culture, key, "Value is empty or whitespace."));
                    continue;
                }

                if (!IsRtl(file.Culture)) continue;
                var scan = BidiScanner.Scan(value);
                if (!scan.HasArabic) continue;

                foreach (var run in scan.UnisolatedLatinRuns)
                {
                    result.Findings.Add(new Finding(FindingSeverity.Warning, "UnisolatedLatin",
                        file.Culture, key,
                        $"Latin run \"{run}\" is not wrapped in bidi isolates; it can render in the wrong order inside Arabic text. Wrap it (FSI..PDI) or confirm the UI isolates it."));
                }

                if (scan.HasWesternDigits && scan.HasArabicIndicDigits)
                {
                    result.Findings.Add(new Finding(FindingSeverity.Warning, "MixedNumerals",
                        file.Culture, key,
                        "Mixes Western digits (0-9) and Arabic-Indic digits (٠-٩) in one string; pick one numeral system per string."));
                }

                if (scan.UnclosedIsolates > 0)
                {
                    result.Findings.Add(new Finding(FindingSeverity.Error, "UnbalancedIsolates",
                        file.Culture, key,
                        $"{scan.UnclosedIsolates} bidi isolate opener(s) without a closing PDI; everything after the opener inherits the isolation."));
                }

                if (scan.StrayClosers > 0)
                {
                    result.Findings.Add(new Finding(FindingSeverity.Error, "UnbalancedIsolates",
                        file.Culture, key,
                        $"{scan.StrayClosers} PDI closer(s) without a matching isolate opener."));
                }
            }
        }

        return result;
    }

    private static void CompareToBase(LintResult result, LocaleFile baseFile, LocaleFile file)
    {
        foreach (var key in baseFile.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!file.Entries.TryGetValue(key, out var value))
            {
                result.Findings.Add(new Finding(FindingSeverity.Error, "MissingKey",
                    file.Culture, key, $"Key exists in {baseFile.Culture} but is missing here."));
                continue;
            }

            var basePlaceholders = BidiScanner.Placeholders(baseFile.Entries[key]);
            var placeholders = BidiScanner.Placeholders(value);
            if (!basePlaceholders.SequenceEqual(placeholders))
            {
                result.Findings.Add(new Finding(FindingSeverity.Error, "PlaceholderMismatch",
                    file.Culture, key,
                    $"Placeholders differ from {baseFile.Culture}: expected [{string.Join(", ", basePlaceholders)}], found [{string.Join(", ", placeholders)}]."));
            }
        }

        foreach (var key in file.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!baseFile.Entries.ContainsKey(key))
            {
                result.Findings.Add(new Finding(FindingSeverity.Warning, "ExtraKey",
                    file.Culture, key, $"Key does not exist in {baseFile.Culture}; it may be dead text."));
            }
        }
    }
}
