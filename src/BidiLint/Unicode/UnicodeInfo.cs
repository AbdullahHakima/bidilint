using System.Text;

namespace BidiLint;

// Unicode-property lookup over the generated 17.0.0 tables. Dropping the
// tables into place keeps the tool independent of the runtime's Unicode
// version and makes an upgrade an explicit, reviewable change.
internal static class UnicodeInfo
{
    public const string Version = UnicodeTables.Version;

    public static BidiClass GetBidiClass(Rune rune) => FindBidiClass(rune.Value);

    public static UnicodeScript GetScript(Rune rune) => FindScript(rune.Value);

    public static bool IsRtlBidiClass(BidiClass value) => value is BidiClass.R or BidiClass.AL;

    public static bool TryGetBracketPair(Rune rune, out BracketPair pair) =>
        TryFind(UnicodeTables.BracketPairs, rune.Value, item => item.CodePoint, out pair);

    public static bool TryGetMirror(Rune rune, out Rune mirror)
    {
        if (TryFind(UnicodeTables.MirrorPairs, rune.Value, item => item.CodePoint, out var pair))
        {
            mirror = new Rune(pair.MirroredCodePoint);
            return true;
        }

        mirror = default;
        return false;
    }

    private static BidiClass FindBidiClass(int codePoint) =>
        Find(UnicodeTables.BidiRanges, codePoint, range => range.Start, range => range.End, range => range.Value);

    private static UnicodeScript FindScript(int codePoint) =>
        Find(UnicodeTables.ScriptRanges, codePoint, range => range.Start, range => range.End, range => range.Value);

    private static TResult Find<T, TResult>(
        T[] values,
        int codePoint,
        Func<T, int> start,
        Func<T, int> end,
        Func<T, TResult> select)
        where T : struct
    {
        var low = 0;
        var high = values.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var item = values[middle];
            if (codePoint < start(item))
            {
                high = middle - 1;
            }
            else if (codePoint > end(item))
            {
                low = middle + 1;
            }
            else
            {
                return select(item);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(codePoint), codePoint, "No Unicode range covers this scalar.");
    }

    private static bool TryFind<T>(
        T[] values,
        int codePoint,
        Func<T, int> key,
        out T result)
        where T : struct
    {
        var low = 0;
        var high = values.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var item = values[middle];
            var current = key(item);
            if (codePoint < current)
            {
                high = middle - 1;
            }
            else if (codePoint > current)
            {
                low = middle + 1;
            }
            else
            {
                result = item;
                return true;
            }
        }

        result = default;
        return false;
    }
}
