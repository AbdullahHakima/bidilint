using System.Text.Json;

namespace BidiLint;

// Loads JSON locale files. A file belongs to a culture when its name is the
// culture ("ar.json", "en-US.json"); nested objects flatten to dotted keys.
public static class LocaleLoader
{
    public static List<LocaleFile> LoadFolder(string root)
    {
        var files = new List<LocaleFile>();
        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                     .Where(p => !IsSkipped(p))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var culture = Path.GetFileNameWithoutExtension(path);
            if (!LooksLikeCulture(culture)) continue;

            var file = new LocaleFile { Culture = culture, FilePath = path };
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    Flatten(doc.RootElement, "", file.Entries);
            }
            catch (JsonException)
            {
                // A file that does not parse is skipped here; malformed JSON is a
                // build problem, not a bidi one. (A parse check can join later.)
                continue;
            }
            files.Add(file);
        }
        return files;
    }

    private static bool IsSkipped(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(seg => seg is "bin" or "obj" or ".git" or "node_modules");

    // Cultures look like "en", "ar", "en-US", "ar-EG". This keeps package.json
    // and friends out without a config file.
    private static bool LooksLikeCulture(string name) =>
        name.Length is >= 2 and <= 5 &&
        name.All(c => char.IsLetter(c) || c == '-') &&
        char.IsLetter(name[0]);

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> into)
    {
        foreach (var prop in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? prop.Name : $"{prefix}.{prop.Name}";
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flatten(prop.Value, key, into);
                    break;
                case JsonValueKind.String:
                    into[key] = prop.Value.GetString() ?? "";
                    break;
                case JsonValueKind.Array:
                    // Arrays of strings are common for lists of labels; index them.
                    var i = 0;
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            into[$"{key}[{i}]"] = item.GetString() ?? "";
                        i++;
                    }
                    break;
            }
        }
    }
}
