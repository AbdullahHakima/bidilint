using System.Text.Json;

namespace BidiLint;

// Loads locale files from a folder tree. Two shapes are supported:
//   JSON — one file per culture, named by culture ("ar.json", "en-US.json");
//          nested objects flatten to dotted keys.
//   resx — "Strings.ar.resx" per culture, plus a neutral "Strings.resx"
//          which counts as the base culture.
// A culture found in both shapes is merged (JSON wins on duplicate keys —
// it is the shape the dashboard-style tools edit most).
public static class LocaleLoader
{
    public static List<LocaleFile> LoadFolder(string root, string baseCulture = "en")
    {
        var byCulture = new Dictionary<string, LocaleFile>(StringComparer.OrdinalIgnoreCase);

        LocaleFile For(string culture, string path)
        {
            if (!byCulture.TryGetValue(culture, out var file))
            {
                file = new LocaleFile { Culture = culture, FilePath = path };
                byCulture[culture] = file;
            }
            return file;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                     .Where(p => !IsSkipped(p))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var culture = Path.GetFileNameWithoutExtension(path);
            if (!LooksLikeCulture(culture)) continue;

            var file = For(culture, path);
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    Flatten(doc.RootElement, "", file.Entries, overwrite: false);
            }
            catch (JsonException)
            {
                // A file that does not parse is skipped here; malformed JSON is a
                // build problem, not a bidi one. (A parse check can join later.)
            }
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.resx", SearchOption.AllDirectories)
                     .Where(p => !IsSkipped(p))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var culture = ResxLoader.CultureOf(path, baseCulture);
            if (culture is null) continue;

            var file = For(culture, path);
            foreach (var (key, value) in ResxLoader.Load(path))
            {
                if (!file.Entries.ContainsKey(key))
                    file.Entries[key] = value;
            }
        }

        return byCulture.Values.OrderBy(f => f.Culture, StringComparer.Ordinal).ToList();
    }

    private static bool IsSkipped(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(seg => seg is "bin" or "obj" or ".git" or "node_modules");

    // Cultures look like "en", "ar", "en-US", "ar-EG". This keeps package.json
    // and friends out without a config file.
    internal static bool LooksLikeCulture(string name) =>
        name.Length is >= 2 and <= 5 &&
        name.All(c => char.IsLetter(c) || c == '-') &&
        char.IsLetter(name[0]);

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> into, bool overwrite)
    {
        foreach (var prop in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? prop.Name : $"{prefix}.{prop.Name}";
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    Flatten(prop.Value, key, into, overwrite);
                    break;
                case JsonValueKind.String:
                    if (overwrite || !into.ContainsKey(key))
                        into[key] = prop.Value.GetString() ?? "";
                    break;
                case JsonValueKind.Array:
                    // Arrays of strings are common for lists of labels; index them.
                    var i = 0;
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String &&
                            (overwrite || !into.ContainsKey($"{key}[{i}]")))
                            into[$"{key}[{i}]"] = item.GetString() ?? "";
                        i++;
                    }
                    break;
            }
        }
    }
}
