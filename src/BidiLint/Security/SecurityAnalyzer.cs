using System.Globalization;
using System.Text;

namespace BidiLint;

// Security checks for controls that can hide or reorder visible text. Unlike
// the bidi rules, these checks run for every value: a Trojan Source override or
// a stray BOM is suspicious in English just as much as in Arabic.
internal static class SecurityAnalyzer
{
    // ContentMask uses U+0001 for hidden markup/placeholder text. The same
    // boundary-neutral character is a useful stand-in when probing whether a
    // directional mark changes the resolved levels of visible text.
    private const char Hidden = '\u0001';
    private const int MaxDirectionalMarksToProbe = 64;

    private const int Lro = 0x202D;
    private const int Rlo = 0x202E;
    private const int Bom = 0xFEFF;
    private const int Zwnj = 0x200C;
    private const int Zwj = 0x200D;
    private const int Lrm = 0x200E;
    private const int Rlm = 0x200F;
    private const int Alm = 0x061C;

    public static bool MightContainFindings(string value)
    {
        foreach (var scalar in UnicodeScalars.Enumerate(value))
        {
            var codePoint = scalar.CodePoint;
            if (codePoint is Lro or Rlo or Bom or Zwnj or Zwj or Lrm or Rlm or Alm)
                return true;
            if (IsDisallowedControl(codePoint)) return true;
        }

        return false;
    }

    public static IReadOnlyList<ValueFinding> Analyze(
        ContentMask content,
        bool rtlCulture)
    {
        var findings = new List<ValueFinding>();
        var scalars = VisibleScalars(content);
        var direction = rtlCulture
            ? BidiParagraphDirection.RightToLeft
            : BidiParagraphDirection.Auto;
        BidiContext? context = null;
        var probedMarks = 0;

        for (var index = 0; index < scalars.Count; index++)
        {
            var scalar = scalars[index];
            var codePoint = scalar.CodePoint;

            switch (codePoint)
            {
                case Lro:
                case Rlo:
                    findings.Add(new ValueFinding(
                        DiagnosticCodes.BidiOverride,
                        DiagnosticSeverity.Error,
                        $"Bidi override {OverrideName(codePoint)} (U+{codePoint:X4}) can reorder or hide text (Trojan Source). Remove it or replace it with an isolate (LRI/RLI/FSI..PDI).",
                        scalar.Offset,
                        scalar.Length));
                    break;

                case Bom:
                    findings.Add(new ValueFinding(
                        DiagnosticCodes.InvisibleControl,
                        DiagnosticSeverity.Error,
                        "Byte order mark (U+FEFF) appears in visible text; remove it. BOMs are invisible and can hide content or break search and comparison.",
                        scalar.Offset,
                        scalar.Length));
                    break;

                case Zwnj:
                case Zwj:
                    if (!IsJoinerInContext(scalars, index, codePoint))
                    {
                        var joinerContext = codePoint == Zwj
                            ? "joining-script text or an emoji sequence"
                            : "joining-script text";
                        findings.Add(new ValueFinding(
                            DiagnosticCodes.InvisibleControl,
                            DiagnosticSeverity.Error,
                            $"{ControlName(codePoint)} (U+{codePoint:X4}) is not next to {joinerContext}; remove it or place it where the shaping context requires it.",
                            scalar.Offset,
                            scalar.Length));
                    }

                    break;

                case Lrm:
                case Rlm:
                case Alm:
                    context ??= BidiContext.Create(content.Text, direction);
                    if (probedMarks >= MaxDirectionalMarksToProbe)
                    {
                        findings.Add(new ValueFinding(
                            DiagnosticCodes.InvisibleControl,
                            DiagnosticSeverity.Error,
                            $"{ControlName(codePoint)} (U+{codePoint:X4}) is one of more than {MaxDirectionalMarksToProbe} directional marks in this value; remove the unnecessary marks.",
                            scalar.Offset,
                            scalar.Length));
                        break;
                    }

                    probedMarks++;
                    if (!DirectionalMarkHasEffect(content, context, scalar.Offset, direction))
                    {
                        findings.Add(new ValueFinding(
                            DiagnosticCodes.InvisibleControl,
                            DiagnosticSeverity.Error,
                            $"{ControlName(codePoint)} (U+{codePoint:X4}) does not change the resolved order of any visible text; remove it or place it where it controls a neutral boundary.",
                            scalar.Offset,
                            scalar.Length));
                    }

                    break;

                default:
                    if (IsDisallowedControl(codePoint))
                    {
                        findings.Add(new ValueFinding(
                            DiagnosticCodes.InvisibleControl,
                            DiagnosticSeverity.Error,
                            $"Control character U+{codePoint:X4} is not allowed in visible text; remove it.",
                            scalar.Offset,
                            scalar.Length));
                    }

                    break;
            }
        }

        return findings;
    }

    private static bool IsJoinerInContext(
        IReadOnlyList<VisibleScalar> scalars,
        int index,
        int joiner)
    {
        var current = scalars[index];
        var previous = index > 0 &&
                       scalars[index - 1].Offset + scalars[index - 1].Length == current.Offset
            ? scalars[index - 1].Value
            : (Rune?)null;
        var next = index + 1 < scalars.Count &&
                   current.Offset + current.Length == scalars[index + 1].Offset
            ? scalars[index + 1].Value
            : (Rune?)null;
        var baseBeforePrevious = index > 1 &&
                                 scalars[index - 2].Offset + scalars[index - 2].Length ==
                                 scalars[index - 1].Offset
            ? scalars[index - 2].Value
            : (Rune?)null;

        return IsJoiningNeighbor(previous, joiner, baseBeforePrevious) ||
               IsJoiningNeighbor(next, joiner, null);
    }

    private static bool IsJoiningNeighbor(Rune? neighbor, int joiner, Rune? baseBeforeMark)
    {
        if (neighbor is not { } value) return false;

        var category = Rune.GetUnicodeCategory(value);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
            return baseBeforeMark is { } baseValue && IsJoiningLetter(baseValue);

        if (category is UnicodeCategory.OtherLetter)
            return IsJoiningScript(UnicodeInfo.GetScript(value));

        // Emoji ZWJ sequences are a legitimate use of U+200D. A ZWNJ inside
        // an emoji sequence is not.
        return joiner == Zwj &&
               category is UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol;
    }

    private static bool IsJoiningLetter(Rune value) =>
        Rune.GetUnicodeCategory(value) == UnicodeCategory.OtherLetter &&
        IsJoiningScript(UnicodeInfo.GetScript(value));

    private static bool IsJoiningScript(UnicodeScript script) =>
        script is
            UnicodeScript.Arabic or
            UnicodeScript.Syriac or
            UnicodeScript.Nko or
            UnicodeScript.Mongolian or
            UnicodeScript.Devanagari or
            UnicodeScript.Bengali or
            UnicodeScript.Gurmukhi or
            UnicodeScript.Gujarati or
            UnicodeScript.Oriya or
            UnicodeScript.Tamil or
            UnicodeScript.Telugu or
            UnicodeScript.Kannada or
            UnicodeScript.Malayalam or
            UnicodeScript.Sinhala or
            UnicodeScript.Myanmar or
            UnicodeScript.Khmer or
            UnicodeScript.Tibetan;

    private static bool DirectionalMarkHasEffect(
        ContentMask content,
        BidiContext original,
        int markOffset,
        BidiParagraphDirection direction)
    {
        var replaced = content.Text.ToCharArray();
        replaced[markOffset] = Hidden;
        var without = BidiContext.Create(new string(replaced), direction);
        if (original.ParagraphLevel != without.ParagraphLevel) return true;

        for (var index = 0; index < content.Text.Length; index++)
        {
            if (index == markOffset || content.IsMasked(index)) continue;
            if (original.LevelAtChar(index) != without.LevelAtChar(index)) return true;
        }

        return false;
    }

    private static List<VisibleScalar> VisibleScalars(ContentMask content)
    {
        var scalars = new List<VisibleScalar>();
        foreach (var scalar in UnicodeScalars.Enumerate(content.Text))
        {
            if (content.ContainsMasked(
                    scalar.Utf16Offset,
                    scalar.Utf16Offset + scalar.Utf16Length))
            {
                continue;
            }
            scalars.Add(new VisibleScalar(scalar.Utf16Offset, scalar.Utf16Length, scalar.Value));
        }

        return scalars;
    }

    private static bool IsDisallowedControl(int codePoint)
    {
        if (codePoint is 0x0009 or 0x000A or 0x000D) return false;
        return codePoint <= 0x001F || (codePoint is >= 0x007F and <= 0x009F);
    }

    private static string OverrideName(int codePoint) => codePoint switch
    {
        Lro => "LRO",
        Rlo => "RLO",
        _ => $"U+{codePoint:X4}",
    };

    private static string ControlName(int codePoint) => codePoint switch
    {
        Lro => "left-to-right override",
        Rlo => "right-to-left override",
        Zwnj => "zero-width non-joiner",
        Zwj => "zero-width joiner",
        Lrm => "left-to-right mark",
        Rlm => "right-to-left mark",
        Alm => "Arabic letter mark",
        _ => $"control U+{codePoint:X4}",
    };

    private readonly record struct VisibleScalar(int Offset, int Length, Rune Value)
    {
        public int CodePoint => Value.Value;
    }
}
