namespace BidiLint;

// The text that is actually visible to a reader. Markup tags, comments,
// entities, and placeholders are replaced with a sentinel so token scanners do
// not treat their identifiers as user-visible Latin text. Lengths and offsets
// are preserved for span reporting.
internal sealed class ContentMask
{
    private const char Hidden = '\u0001';

    private readonly bool[] _masked;

    private ContentMask(string text, bool[] masked)
    {
        Text = text;
        _masked = masked;
    }

    public string Text { get; }

    public bool IsMasked(int index) => index >= 0 && index < _masked.Length && _masked[index];

    public bool ContainsMasked(int start, int end)
    {
        for (var index = Math.Max(0, start); index < Math.Min(end, _masked.Length); index++)
        {
            if (_masked[index]) return true;
        }

        return false;
    }

    public static ContentMask Build(
        string value,
        IReadOnlySet<string> isolationWrappers,
        out MarkupScanResult markup)
    {
        var placeholders = BidiScanner.PlaceholderSpans(value).ToList();

        // Placeholders are opaque to the markup scanner: a tag-looking sequence
        // inside one is data, not an element that isolates later text.
        var markupInput = value.ToCharArray();
        foreach (var (start, length) in placeholders)
            MarkHidden(start, length, markupInput);

        markup = MarkupScanner.Scan(new string(markupInput), isolationWrappers);
        var masked = new bool[value.Length];
        foreach (var range in markup.MaskedRanges)
            Mark(range.Start, range.Length, masked);
        foreach (var (start, length) in placeholders)
            Mark(start, length, masked);

        var chars = value.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            if (masked[index]) chars[index] = Hidden;
        }

        return new ContentMask(new string(chars), masked);
    }

    private static void MarkHidden(int start, int length, char[] chars)
    {
        var end = Math.Min(chars.Length, start + length);
        for (var index = Math.Max(0, start); index < end; index++)
            chars[index] = Hidden;
    }

    private static void Mark(int start, int length, bool[] masked)
    {
        var end = Math.Min(masked.Length, start + length);
        for (var index = Math.Max(0, start); index < end; index++)
            masked[index] = true;
    }
}
