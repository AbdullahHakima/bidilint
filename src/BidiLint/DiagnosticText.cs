using System.Globalization;
using System.Text;

namespace BidiLint;

// Printable evidence for diagnostics. Invisible bidi controls and other
// control/format scalars are escaped so a log never renders a finding with the
// same characters that caused it.
internal static class DiagnosticText
{
    public static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var scalar in UnicodeScalars.Enumerate(text))
        {
            var rune = scalar.Value;
            switch (rune.Value)
            {
                case '\r':
                    builder.Append("\\r");
                    continue;
                case '\n':
                    builder.Append("\\n");
                    continue;
                case '\t':
                    builder.Append("\\t");
                    continue;
            }

            if (IsInvisible(rune))
            {
                builder.Append("\\u{");
                builder.Append(rune.Value.ToString("X4", CultureInfo.InvariantCulture));
                builder.Append('}');
            }
            else
            {
                builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }

    private static bool IsInvisible(Rune rune) =>
        (rune.IsBmp && BidiScanner.IsBidiControl((char)rune.Value)) ||
        Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.Control or
            UnicodeCategory.Format or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark;
}
