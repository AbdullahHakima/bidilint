using System.Text;

namespace BidiLint;

internal readonly record struct UnicodeScalar(Rune Value, int Utf16Offset, int Utf16Length)
{
    public int CodePoint => Value.Value;
}

// Scalar-aware enumeration keeps logical UTF-16 offsets for diagnostics while
// preventing surrogate halves from being treated as characters.
internal static class UnicodeScalars
{
    public static IEnumerable<UnicodeScalar> Enumerate(string value)
    {
        var offset = 0;
        while (offset < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value.AsSpan(offset), out var rune, out var consumed);
            if (status != System.Buffers.OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            yield return new UnicodeScalar(rune, offset, consumed);
            offset += consumed;
        }
    }

    public static string Escape(Rune value) =>
        value.Value <= 0xFFFF
            ? $"\\u{value.Value:X4}"
            : $"\\U{value.Value:X8}";

    public static string Escape(string value) =>
        string.Concat(Enumerate(value).Select(scalar => Escape(scalar.Value)));

    public static int Count(string value)
    {
        var count = 0;
        foreach (var _ in Enumerate(value)) count++;
        return count;
    }
}
