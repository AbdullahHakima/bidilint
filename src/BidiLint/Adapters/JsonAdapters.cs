namespace BidiLint;

// Culture-named JSON files: en.json, ar-EG.json. This is the 0.1 layout and
// remains the fallback when a file is not inside a recognized culture folder.
internal sealed class JsonFileAdapter : ILocaleAdapter
{
    public LocaleIdentity? Identify(
        string path,
        string workingDirectory,
        string baseCulture,
        IReadOnlySet<string>? layoutNames = null)
    {
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return null;

        var stem = Path.GetFileNameWithoutExtension(path);
        if (!CultureTag.TryParse(stem, out var tag)) return null;

        // The culture is carried by the file name, so the containing directory
        // identifies the logical resource set. Using one global "json" set
        // merges unrelated locale trees and produces false KeyCollision errors.
        var segments = LocalePath.Segments(workingDirectory, path);
        var resourceSet = segments.Length <= 1
            ? "."
            : string.Join('/', segments.Take(segments.Length - 1));

        return new LocaleIdentity(path, tag!.Name, resourceSet, LocaleFormat.Json);
    }

    public LocaleDocument? Load(LocaleIdentity identity, List<Diagnostic> diagnostics)
    {
        var source = JsonLocaleFile.Read(identity.FilePath, identity.Culture, diagnostics);
        if (source is null) return null;

        return JsonLocaleParser.Parse(
            identity.FilePath,
            identity.Culture,
            identity.ResourceSet,
            source,
            diagnostics);
    }
}

// Culture-directory JSON layouts: locales/en/translation.json,
// web/locales/ar/checkout/common.json. The culture segment must be directly
// under a conventional layout folder so an unrelated project directory named
// "en" is not mistaken for a locale.
internal sealed class JsonCultureDirectoryAdapter : ILocaleAdapter
{
    public LocaleIdentity? Identify(
        string path,
        string workingDirectory,
        string baseCulture,
        IReadOnlySet<string>? layoutNames = null)
    {
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = LocalePath.Segments(workingDirectory, path);
        if (segments.Length < 3) return null;

        for (var i = segments.Length - 2; i >= 1; i--)
        {
            if (!CultureTag.TryParse(segments[i], out var tag)) continue;
            if (!LocalePath.IsLayoutName(segments[i - 1], layoutNames)) continue;

            return new LocaleIdentity(
                path,
                tag!.Name,
                LocalePath.ResourceSet(segments, i),
                LocaleFormat.Json);
        }

        return null;
    }

    public LocaleDocument? Load(LocaleIdentity identity, List<Diagnostic> diagnostics)
    {
        var source = JsonLocaleFile.Read(identity.FilePath, identity.Culture, diagnostics);
        if (source is null) return null;

        return JsonLocaleParser.Parse(
            identity.FilePath,
            identity.Culture,
            identity.ResourceSet,
            source,
            diagnostics);
    }
}
