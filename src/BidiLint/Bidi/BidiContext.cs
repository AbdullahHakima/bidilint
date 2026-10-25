namespace BidiLint;

// Per-value access to the UAX #9 result. BidiResolver indexes levels and visual
// order by Unicode scalar; callers work with UTF-16 offsets and source spans.
internal sealed class BidiContext
{
    private readonly BidiResolution _resolution;
    private readonly int[] _scalarAtChar;
    private readonly int[] _scalarOffsets;
    private readonly int[] _visualPosition;

    private BidiContext(
        BidiResolution resolution,
        int[] scalarAtChar,
        int[] scalarOffsets,
        int[] visualPosition)
    {
        _resolution = resolution;
        _scalarAtChar = scalarAtChar;
        _scalarOffsets = scalarOffsets;
        _visualPosition = visualPosition;
    }

    public int ParagraphLevel => _resolution.ParagraphLevel;
    public int ScalarCount => _resolution.Levels.Length;
    public BidiResolution Resolution => _resolution;

    public static BidiContext Create(
        string text,
        BidiParagraphDirection direction = BidiParagraphDirection.Auto)
    {
        var scalars = UnicodeScalars.Enumerate(text).ToArray();
        var resolution = BidiResolver.Resolve(text, direction);
        var scalarAtChar = new int[text.Length + 1];
        Array.Fill(scalarAtChar, -1);

        var scalarOffsets = new int[scalars.Length];
        for (var index = 0; index < scalars.Length; index++)
        {
            scalarOffsets[index] = scalars[index].Utf16Offset;
            for (var offset = 0; offset < scalars[index].Utf16Length; offset++)
                scalarAtChar[scalars[index].Utf16Offset + offset] = index;
        }

        scalarAtChar[text.Length] = scalars.Length;

        var visualPosition = new int[scalars.Length];
        Array.Fill(visualPosition, -1);
        for (var position = 0; position < resolution.VisualOrder.Length; position++)
            visualPosition[resolution.VisualOrder[position]] = position;

        return new BidiContext(resolution, scalarAtChar, scalarOffsets, visualPosition);
    }

    public int ScalarAtChar(int charOffset)
    {
        if (charOffset <= 0) return 0;
        if (charOffset >= _scalarAtChar.Length) return ScalarCount;

        var scalar = _scalarAtChar[charOffset];
        if (scalar >= 0) return scalar;

        // A char offset can point at the low surrogate of a scalar. Walk back
        // to the high surrogate, which is always indexed.
        for (var offset = charOffset - 1; offset >= 0; offset--)
        {
            if (_scalarAtChar[offset] >= 0) return _scalarAtChar[offset];
        }

        return 0;
    }

    public int ScalarOffset(int scalarIndex) =>
        scalarIndex >= 0 && scalarIndex < _scalarOffsets.Length
            ? _scalarOffsets[scalarIndex]
            : -1;

    public int LevelAtChar(int charOffset)
    {
        var scalar = ScalarAtChar(charOffset);
        return scalar < _resolution.Levels.Length
            ? _resolution.Levels[scalar]
            : ParagraphLevel;
    }

    public int LevelAtScalar(int scalarIndex) =>
        scalarIndex >= 0 && scalarIndex < _resolution.Levels.Length
            ? _resolution.Levels[scalarIndex]
            : ParagraphLevel;

    public bool IsRemovedAtChar(int charOffset) =>
        IsRemovedAtScalar(ScalarAtChar(charOffset));

    public bool IsRemovedAtScalar(int scalarIndex) =>
        scalarIndex >= 0 &&
        scalarIndex < _resolution.Removed.Length &&
        _resolution.Removed[scalarIndex];

    public int VisualPositionAtChar(int charOffset) =>
        VisualPositionAtScalar(ScalarAtChar(charOffset));

    public int VisualPositionAtScalar(int scalarIndex) =>
        scalarIndex >= 0 && scalarIndex < _visualPosition.Length
            ? _visualPosition[scalarIndex]
            : -1;

    public int FirstVisualPosition(int start, int end)
    {
        var first = ScalarAtChar(start);
        var last = ScalarAtChar(end);
        for (var scalar = first; scalar < last; scalar++)
        {
            if (!IsRemovedAtScalar(scalar)) return _visualPosition[scalar];
        }

        return -1;
    }
}
