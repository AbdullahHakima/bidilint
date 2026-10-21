using System.Xml.Linq;

namespace BidiLint;

// Loads .resx resource files. The culture comes from the file name:
// "Strings.ar.resx" is Arabic; a neutral "Strings.resx" is the base culture
// (the developer's own language — what every satellite file translates).
public static class ResxLoader
{
    // Returns null when the file is not a usable string-resource file.
    // onEntries receives the dotted-key entries of one file.
    public static string? CultureOf(string path, string baseCulture)
    {
        var name = Path.GetFileNameWithoutExtension(path); // "Strings.ar" or "Strings"
        var dot = name.LastIndexOf('.');
        if (dot < 0) return baseCulture; // neutral file
        var suffix = name[(dot + 1)..];
        return LocaleLoader.LooksLikeCulture(suffix) ? suffix : null;
    }

    public static Dictionary<string, string> Load(string path)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (System.Xml.XmlException)
        {
            return entries; // not well-formed XML — a build problem, not a bidi one
        }

        foreach (var data in doc.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
        {
            var name = data.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;
            // Skip non-string resources (images, file references, typed data).
            if (data.Attribute("type") is not null) continue;
            var value = data.Element("value")?.Value;
            if (value is null) continue;
            entries[name] = value;
        }
        return entries;
    }
}
