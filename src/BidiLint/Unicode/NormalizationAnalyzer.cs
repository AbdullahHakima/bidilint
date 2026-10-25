using System.Globalization;
using System.Text;

namespace BidiLint;

// Unicode normalization checks that are independent of bidi direction. They
// run on visible text, so markup and placeholders cannot produce findings.
internal static class NormalizationAnalyzer
{
    private const char Tatweel = '\u0640';

    public static bool MightContainFindings(string value)
    {
        foreach (var scalar in UnicodeScalars.Enumerate(value))
        {
            if (scalar.Value.Value == Tatweel || IsPresentationForm(scalar.Value))
                return true;
        }

        try
        {
            return !value.IsNormalized(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // Invalid UTF-16 is reported by the format layer; do not let the
            // normalization probe crash the rest of the lint run.
            return true;
        }
    }

    public static IReadOnlyList<ValueFinding> Analyze(
        string value,
        ContentMask content)
    {
        var findings = new List<ValueFinding>();
        var sawPresentationForm = AddPresentationForms(value, content, findings);
        AddNonNfcText(value, content, findings, sawPresentationForm);
        AddTatweel(value, content, findings);
        return findings;
    }

    private static bool AddPresentationForms(
        string value,
        ContentMask content,
        List<ValueFinding> findings)
    {
        var found = false;
        var index = 0;
        while (index < value.Length)
        {
            if (content.IsMasked(index) ||
                !TryDecode(value, index, out var rune, out var consumed) ||
                !IsPresentationForm(rune))
            {
                index++;
                continue;
            }

            found = true;
            var start = index;
            var end = index + consumed;
            while (end < value.Length &&
                   !content.IsMasked(end) &&
                   TryDecode(value, end, out var next, out var nextLength) &&
                   IsPresentationForm(next))
            {
                end += nextLength;
            }

            var first = value[start..(start + consumed)];
            var label = end - start == consumed
                ? "Arabic presentation form"
                : "Arabic presentation-form sequence starting with";
            findings.Add(new ValueFinding(
                DiagnosticCodes.PresentationForm,
                DiagnosticSeverity.Warning,
                $"{label} U+{rune.Value:X4} ({DiagnosticText.Escape(first)}) is a compatibility character. Replace it with standard Arabic letters so normalization, search, and shaping stay consistent.",
                start,
                end - start));
            index = end;
        }

        return found;
    }

    private static void AddNonNfcText(
        string value,
        ContentMask content,
        List<ValueFinding> findings,
        bool sawPresentationForm)
    {
        var added = false;
        int[] starts;
        try
        {
            starts = StringInfo.ParseCombiningCharacters(value);
        }
        catch (ArgumentException)
        {
            // Invalid UTF-16 is reported by the format layer; normalization is
            // best-effort and must not take down the lint run.
            return;
        }

        for (var elementIndex = 0; elementIndex < starts.Length; elementIndex++)
        {
            var start = starts[elementIndex];
            var end = elementIndex + 1 < starts.Length ? starts[elementIndex + 1] : value.Length;
            if (end <= start || content.ContainsMasked(start, end)) continue;

            var element = value[start..end];
            if (ContainsPresentationForm(element)) continue;

            string normalized;
            try
            {
                normalized = element.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (string.Equals(normalized, element, StringComparison.Ordinal)) continue;
            var mixed = value.Contains(normalized, StringComparison.Ordinal);
            var detail = mixed
                ? "This value mixes composed and decomposed forms."
                : "Store normalized text for stable search, comparison, and rendering.";
            findings.Add(new ValueFinding(
                DiagnosticCodes.Normalization,
                DiagnosticSeverity.Warning,
                $"Text element \"{DiagnosticText.Escape(element)}\" is not Unicode NFC; the normalized form is \"{DiagnosticText.Escape(normalized)}\". {detail}",
                start,
                end - start));
            added = true;
        }

        if (added || sawPresentationForm) return;

        var wholeNormalized = TryNormalize(value);
        if (wholeNormalized is null || string.Equals(wholeNormalized, value, StringComparison.Ordinal))
            return;

        var difference = FirstDifference(value, wholeNormalized);
        if (difference.Length <= 0 ||
            content.ContainsMasked(difference.Start, difference.Start + difference.Length))
        {
            return;
        }

        findings.Add(new ValueFinding(
            DiagnosticCodes.Normalization,
            DiagnosticSeverity.Warning,
            $"Value is not Unicode NFC; \"{DiagnosticText.Escape(difference.Original)}\" normalizes to \"{DiagnosticText.Escape(difference.Normalized)}\". Store normalized text for stable search, comparison, and rendering.",
            difference.Start,
            difference.Length));
    }

    private static void AddTatweel(
        string value,
        ContentMask content,
        List<ValueFinding> findings)
    {
        var index = 0;
        while (index < value.Length)
        {
            if (content.IsMasked(index) || value[index] != Tatweel)
            {
                index++;
                continue;
            }

            var start = index;
            while (index < value.Length && !content.IsMasked(index) && value[index] == Tatweel)
                index++;

            findings.Add(new ValueFinding(
                DiagnosticCodes.Tatweel,
                DiagnosticSeverity.Warning,
                "Arabic tatweel (U+0640) is present; it elongates glyphs but is not a letter. Remove it unless the kashida is intentional, or lower rules.Tatweel.",
                start,
                index - start));
        }
    }

    private static bool ContainsPresentationForm(string text)
    {
        foreach (var scalar in UnicodeScalars.Enumerate(text))
        {
            if (IsPresentationForm(scalar.Value)) return true;
        }

        return false;
    }

    private static string? TryNormalize(string value)
    {
        try
        {
            return value.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static (int Start, int Length, string Original, string Normalized) FirstDifference(
        string value,
        string normalized)
    {
        var prefix = 0;
        var limit = Math.Min(value.Length, normalized.Length);
        while (prefix < limit && value[prefix] == normalized[prefix]) prefix++;

        var suffix = 0;
        while (suffix < limit - prefix &&
               value[value.Length - 1 - suffix] == normalized[normalized.Length - 1 - suffix])
        {
            suffix++;
        }

        var original = value[prefix..(value.Length - suffix)];
        var replacement = normalized[prefix..(normalized.Length - suffix)];
        return original.Length == 0
            ? (0, value.Length, value, normalized)
            : (prefix, original.Length, original, replacement);
    }

    private static bool IsPresentationForm(Rune rune) =>
        rune.Value is >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFE;

    private static bool TryDecode(
        string value,
        int index,
        out Rune rune,
        out int consumed) =>
        Rune.DecodeFromUtf16(value.AsSpan(index), out rune, out consumed) ==
        System.Buffers.OperationStatus.Done;
}
