using System.Text;

namespace BidiLint;

// Decoded file text plus line maps. Positions use 1-based lines and columns;
// columns count UTF-16 code units, matching .NET and editor conventions.
public sealed class SourceText
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly int[] _byteLineStarts;
    private readonly int[] _charLineStarts;

    private SourceText(byte[] bytes, string text)
    {
        Bytes = bytes;
        Text = text;
        _byteLineStarts = FindLineStarts(bytes, (byte)'\n');
        _charLineStarts = FindLineStarts(text);
    }

    public byte[] Bytes { get; }
    public string Text { get; }

    public static SourceText FromUtf8Bytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var offset = bytes.AsSpan().StartsWith(Utf8Bom) ? 3 : 0;
        var content = offset == 0 ? bytes : bytes[offset..];
        var text = StrictUtf8.GetString(content);
        return new SourceText(content, text);
    }

    public static SourceText FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new SourceText(StrictUtf8.GetBytes(text), text);
    }

    public SourcePosition PositionAtByteOffset(int byteOffset)
    {
        var offset = Math.Clamp(byteOffset, 0, Bytes.Length);
        var lineIndex = FindLineIndex(_byteLineStarts, offset);
        var lineStart = _byteLineStarts[lineIndex];
        var column = StrictUtf8.GetCharCount(Bytes, lineStart, offset - lineStart) + 1;
        var charOffset = _charLineStarts[lineIndex] + column - 1;
        return new SourcePosition(lineIndex + 1, column, charOffset);
    }

    // JsonException reports a zero-based line and a byte offset within it.
    public SourcePosition PositionAtLineByteOffset(int zeroBasedLine, int bytePositionInLine)
    {
        var lineIndex = Math.Clamp(zeroBasedLine, 0, _byteLineStarts.Length - 1);
        var offset = _byteLineStarts[lineIndex] + Math.Max(0, bytePositionInLine);
        return PositionAtByteOffset(offset);
    }

    public SourcePosition PositionAtCharOffset(int charOffset)
    {
        var offset = Math.Clamp(charOffset, 0, Text.Length);
        var lineIndex = FindLineIndex(_charLineStarts, offset);
        var column = offset - _charLineStarts[lineIndex] + 1;
        return new SourcePosition(lineIndex + 1, column, offset);
    }

    public SourcePosition PositionAt(int line, int column)
    {
        var lineIndex = Math.Clamp(line - 1, 0, _charLineStarts.Length - 1);
        var offset = _charLineStarts[lineIndex] + Math.Max(0, column - 1);
        return PositionAtCharOffset(offset);
    }

    public string GetLine(int line)
    {
        var lineIndex = Math.Clamp(line - 1, 0, _charLineStarts.Length - 1);
        var start = _charLineStarts[lineIndex];
        var end = lineIndex + 1 < _charLineStarts.Length
            ? _charLineStarts[lineIndex + 1] - 1
            : Text.Length;
        if (end > start && Text[end - 1] == '\r') end--;
        return Text[start..Math.Clamp(end, start, Text.Length)];
    }

    private static int FindLineIndex(int[] lineStarts, int offset)
    {
        var index = Array.BinarySearch(lineStarts, offset);
        return index >= 0 ? index : Math.Max(0, ~index - 1);
    }

    private static int[] FindLineStarts(byte[] bytes, byte newline)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == newline) starts.Add(i + 1);
        }
        return starts.ToArray();
    }

    private static int[] FindLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') starts.Add(i + 1);
        }
        return starts.ToArray();
    }
}
