namespace BidiLint;

internal static class LocalePath
{
    private static readonly HashSet<string> LayoutNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "locales", "locale", "i18n", "lang", "languages", "translations",
        "messages", "intl", "resources", "po",
    };

    public static string[] Segments(string workingDirectory, string path) =>
        Path.GetRelativePath(workingDirectory, path)
            .Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

    public static string ResourceSet(string[] segments, int removeIndex)
    {
        var parts = new List<string>(segments.Length);
        for (var i = 0; i < segments.Length; i++)
        {
            if (i != removeIndex) parts.Add(segments[i]);
        }
        return string.Join('/', parts);
    }

    public static bool IsLayoutName(string value) => LayoutNames.Contains(value);

    public static int FindLastSegment(string[] segments, Func<string, bool> predicate)
    {
        for (var i = segments.Length - 2; i >= 0; i--)
        {
            if (predicate(segments[i])) return i;
        }
        return -1;
    }
}
