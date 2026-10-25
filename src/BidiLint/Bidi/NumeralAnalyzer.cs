namespace BidiLint;

// Numeral-system analysis for one visible locale value. Markup and
// placeholders are already masked by ContentMask, so digits in "{0}" do not
// count as translator-visible Western digits.
internal static class NumeralAnalyzer
{
    public static IReadOnlyList<ValueFinding> Analyze(
        ContentMask content,
        NumeralPolicy policy)
    {
        if (policy == NumeralPolicy.Allow) return Array.Empty<ValueFinding>();

        var systems = NumeralSystem.None;
        var firstDigit = -1;
        var lastDigitEnd = -1;
        foreach (var scalar in UnicodeScalars.Enumerate(content.Text))
        {
            var system = BidiScanner.GetNumeralSystem(scalar.Value);
            if (system == NumeralSystem.None) continue;
            systems |= system;
            if (firstDigit < 0) firstDigit = scalar.Utf16Offset;
            lastDigitEnd = scalar.Utf16Offset + scalar.Utf16Length;
        }

        if (systems == NumeralSystem.None) return Array.Empty<ValueFinding>();
        if (!Violates(systems, policy)) return Array.Empty<ValueFinding>();

        return new[]
        {
            new ValueFinding(
                DiagnosticCodes.MixedNumerals,
                DiagnosticSeverity.Warning,
                Message(systems, policy),
                firstDigit,
                lastDigitEnd - firstDigit),
        };
    }

    private static bool Violates(NumeralSystem systems, NumeralPolicy policy) =>
        policy switch
        {
            NumeralPolicy.Allow => false,
            NumeralPolicy.Mixed => PopCount(systems) > 1,
            NumeralPolicy.Western =>
                (systems & ~NumeralSystem.Western) != 0,
            NumeralPolicy.ArabicIndic =>
                (systems & ~NumeralSystem.ArabicIndic) != 0,
            NumeralPolicy.ExtendedArabicIndic =>
                (systems & ~NumeralSystem.ExtendedArabicIndic) != 0,
            _ => false,
        };

    private static int PopCount(NumeralSystem systems)
    {
        var value = (int)systems;
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    private static string Message(NumeralSystem systems, NumeralPolicy policy)
    {
        if (policy == NumeralPolicy.Mixed)
        {
            return $"Found {DescribeAll(systems)} in one string; pick one numeral system per string.";
        }

        var allowed = policy switch
        {
            NumeralPolicy.Western => NumeralSystem.Western,
            NumeralPolicy.ArabicIndic => NumeralSystem.ArabicIndic,
            NumeralPolicy.ExtendedArabicIndic => NumeralSystem.ExtendedArabicIndic,
            _ => NumeralSystem.None,
        };
        var found = systems & ~allowed;
        return $"Numeral policy is {PolicyName(policy)}, but {DescribeAll(found)} were found; use {Describe(allowed)}.";
    }

    private static string PolicyName(NumeralPolicy policy) => policy switch
    {
        NumeralPolicy.Western => "Western",
        NumeralPolicy.ArabicIndic => "Arabic-Indic",
        NumeralPolicy.ExtendedArabicIndic => "Extended Arabic-Indic",
        NumeralPolicy.Allow => "allow",
        _ => "mixed",
    };

    private static string DescribeAll(NumeralSystem systems)
    {
        var labels = new List<string>();
        if ((systems & NumeralSystem.Western) != 0) labels.Add(Describe(NumeralSystem.Western));
        if ((systems & NumeralSystem.ArabicIndic) != 0) labels.Add(Describe(NumeralSystem.ArabicIndic));
        if ((systems & NumeralSystem.ExtendedArabicIndic) != 0)
            labels.Add(Describe(NumeralSystem.ExtendedArabicIndic));

        return labels.Count switch
        {
            0 => "no recognized digits",
            1 => labels[0],
            2 => $"{labels[0]} and {labels[1]}",
            _ => $"{labels[0]}, {labels[1]}, and {labels[2]}",
        };
    }

    private static string Describe(NumeralSystem system) => system switch
    {
        NumeralSystem.Western => "Western digits (0-9)",
        NumeralSystem.ArabicIndic => "Arabic-Indic digits (٠-٩)",
        NumeralSystem.ExtendedArabicIndic => "Extended Arabic-Indic digits (۰-۹)",
        _ => "digits",
    };
}
