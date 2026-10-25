namespace BidiLint;

internal enum ValueQuoteSyntax
{
    Json,
    AppleStrings,
}

// Maps decoded value offsets back to the raw source token when the format has
// an unambiguous string encoding. Formats without that information leave the
// map null and diagnostics fall back to the whole value span.
internal sealed class ValueSourceMap
{
    private readonly SourceText _source;
    private readonly int[] _rawOffsets;

    private ValueSourceMap(SourceText source, int[] rawOffsets)
    {
        _source = source;
        _rawOffsets = rawOffsets;
    }

    public SourceSpan Map(int start, int length)
    {
        if (start < 0 || length < 0 || start + length >= _rawOffsets.Length)
            throw new ArgumentOutOfRangeException(nameof(start));

        return new SourceSpan(
            _source.PositionAtCharOffset(_rawOffsets[start]),
            _source.PositionAtCharOffset(_rawOffsets[start + length]));
    }

    public static ValueSourceMap? TryCreate(
        SourceText source,
        SourceSpan rawSpan,
        string value,
        ValueQuoteSyntax syntax)
    {
        if (rawSpan.Start.Offset < 0 ||
            rawSpan.End.Offset > source.Text.Length ||
            rawSpan.End.Offset < rawSpan.Start.Offset)
        {
            return null;
        }

        var raw = source.Text.AsSpan(
            rawSpan.Start.Offset,
            rawSpan.End.Offset - rawSpan.Start.Offset);
        if (raw.Length > 0 && raw[0] == '"')
            return TryCreateQuoted(source, rawSpan.Start.Offset, raw, value, syntax);

        if (syntax == ValueQuoteSyntax.AppleStrings &&
            raw.Length == value.Length &&
            raw.SequenceEqual(value.AsSpan()))
        {
            var offsets = new int[value.Length + 1];
            for (var i = 0; i <= value.Length; i++)
                offsets[i] = rawSpan.Start.Offset + i;
            return new ValueSourceMap(source, offsets);
        }

        return null;
    }

    private static ValueSourceMap? TryCreateQuoted(
        SourceText source,
        int rawStart,
        ReadOnlySpan<char> raw,
        string value,
        ValueQuoteSyntax syntax)
    {
        var offsets = new List<int>(value.Length + 1);
        var rawIndex = 1;
        var decodedIndex = 0;

        while (rawIndex < raw.Length)
        {
            var current = raw[rawIndex];
            if (current == '"')
            {
                if (decodedIndex != value.Length) return null;
                offsets.Add(rawStart + rawIndex);
                return new ValueSourceMap(source, offsets.ToArray());
            }

            if (current != '\\')
            {
                if (decodedIndex >= value.Length || value[decodedIndex] != current)
                    return null;

                offsets.Add(rawStart + rawIndex);
                decodedIndex++;
                rawIndex++;
                continue;
            }

            var escapeStart = rawIndex;
            rawIndex++;
            if (rawIndex >= raw.Length) return null;

            var escaped = raw[rawIndex++];
            char decoded;
            switch (escaped)
            {
                case '"': decoded = '"'; break;
                case '\\': decoded = '\\'; break;
                case '/': decoded = '/'; break;
                case 'b': decoded = '\b'; break;
                case 'f': decoded = '\f'; break;
                case 'n': decoded = '\n'; break;
                case 'r': decoded = '\r'; break;
                case 't': decoded = '\t'; break;
                case 'u':
                case 'U':
                    if (rawIndex + 4 > raw.Length ||
                        !int.TryParse(
                            raw.Slice(rawIndex, 4),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var code))
                    {
                        return null;
                    }

                    decoded = (char)code;
                    rawIndex += 4;
                    break;
                default:
                    if (syntax == ValueQuoteSyntax.Json) return null;
                    decoded = escaped;
                    break;
            }

            if (decodedIndex >= value.Length || value[decodedIndex] != decoded)
                return null;

            offsets.Add(rawStart + escapeStart);
            decodedIndex++;
        }

        return null;
    }
}
