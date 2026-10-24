using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BidiLint;

internal sealed record BaselineEntry(
    string Fingerprint,
    string Rule,
    string Culture,
    string Key,
    int Occurrence)
{
    [JsonIgnore]
    public string[] SortKey =>
    [
        Culture.ToLowerInvariant(),
        Rule,
        Key,
        Occurrence.ToString("D9", CultureInfo.InvariantCulture),
    ];
}

internal static class BaselineFingerprint
{
    public static string Compute(string rule, string culture, string key, int occurrence)
    {
        var canonical = string.Join(
            "\n",
            rule,
            culture.ToLowerInvariant(),
            key,
            occurrence.ToString(CultureInfo.InvariantCulture));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

internal static class BaselineStore
{
    private static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal)
    {
        "version", "findings",
    };

    private static readonly HashSet<string> EntryKeys = new(StringComparer.Ordinal)
    {
        "fingerprint", "rule", "culture", "key", "occurrence",
    };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 32,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IReadOnlyList<BaselineEntry> Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new ConfigException(
                $"Baseline file not found: {fullPath}. Run --update-baseline to create it.");
        }

        string text;
        try
        {
            text = File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException($"Cannot read baseline file {fullPath}: {ex.Message}");
        }

        var errors = new List<string>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, ReadOptions);
        }
        catch (JsonException ex)
        {
            var position = ex.LineNumber is null
                ? ""
                : $" at line {ex.LineNumber + 1}, column {ex.BytePositionInLine + 1}";
            throw new ConfigException($"{fullPath}{position}: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ConfigException($"{fullPath}: baseline root must be a JSON object.");
            }

            var properties = ReadProperties(root, "$", RootKeys, errors);
            if (!properties.TryGetValue("version", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out var version) ||
                version != 1)
            {
                errors.Add("version must be 1.");
            }

            var entries = new List<BaselineEntry>();
            var fingerprints = new HashSet<string>(StringComparer.Ordinal);
            if (!properties.TryGetValue("findings", out var findingsElement) ||
                findingsElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add("findings must be an array.");
            }
            else
            {
                var index = 0;
                foreach (var item in findingsElement.EnumerateArray())
                {
                    var entryPath = $"findings[{index}]";
                    var entryProperties = ReadProperties(
                        item,
                        entryPath,
                        EntryKeys,
                        errors);

                    var fingerprint = ReadRequiredString(entryProperties, "fingerprint", entryPath, errors);
                    var rule = ReadRequiredString(entryProperties, "rule", entryPath, errors);
                    var culture = ReadRequiredString(entryProperties, "culture", entryPath, errors);
                    var key = ReadRequiredString(entryProperties, "key", entryPath, errors);
                    var occurrence = 0;
                    if (!entryProperties.TryGetValue("occurrence", out var occurrenceElement) ||
                        occurrenceElement.ValueKind != JsonValueKind.Number ||
                        !occurrenceElement.TryGetInt32(out occurrence) ||
                        occurrence < 1)
                    {
                        errors.Add($"{entryPath}.occurrence must be a positive integer.");
                    }

                    if (fingerprint is not null && !IsFingerprint(fingerprint))
                        errors.Add($"{entryPath}.fingerprint must be a 64-character SHA-256 hex string.");

                    if (rule is not null)
                    {
                        if (!RuleCatalog.TryGet(rule, out var metadata))
                        {
                            errors.Add($"{entryPath}.rule is not a known rule.");
                        }
                        else if (!metadata.BaselineAllowed)
                        {
                            errors.Add($"{entryPath}.rule is not eligible for baselining.");
                        }
                    }

                    if (culture is not null && !CultureTag.TryParse(culture, out _))
                        errors.Add($"{entryPath}.culture must be a known BCP-47 culture.");

                    if (fingerprint is not null &&
                        rule is not null &&
                        culture is not null &&
                        key is not null &&
                        occurrence > 0)
                    {
                        var expected = BaselineFingerprint.Compute(rule, culture, key, occurrence);
                        if (!string.Equals(fingerprint, expected, StringComparison.Ordinal))
                            errors.Add($"{entryPath}.fingerprint does not match its rule/culture/key/occurrence fields.");

                        if (!fingerprints.Add(fingerprint))
                            errors.Add($"{entryPath}.fingerprint appears more than once.");

                        entries.Add(new BaselineEntry(
                            fingerprint,
                            rule,
                            culture,
                            key,
                            occurrence));
                    }

                    index++;
                }
            }

            if (errors.Count > 0)
            {
                throw new ConfigException(errors
                    .Select(error => $"{fullPath}: {error}")
                    .ToList());
            }

            return entries;
        }
    }

    public static void Write(string path, IEnumerable<BaselineEntry> entries)
    {
        var fullPath = Path.GetFullPath(path);
        var ordered = entries
            .OrderBy(entry => entry.SortKey[0], StringComparer.Ordinal)
            .ThenBy(entry => entry.SortKey[1], StringComparer.Ordinal)
            .ThenBy(entry => entry.SortKey[2], StringComparer.Ordinal)
            .ThenBy(entry => entry.Occurrence)
            .ToList();

        var file = new BaselineFile(1, ordered);
        var json = JsonSerializer.Serialize(file, WriteOptions);
        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(
                fullPath,
                json + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException($"Cannot write baseline file {fullPath}: {ex.Message}");
        }
    }

    private static bool IsFingerprint(string value) =>
        value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character));

    private static Dictionary<string, JsonElement> ReadProperties(
        JsonElement element,
        string path,
        HashSet<string> allowed,
        List<string> errors)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{path} must be an object.");
            return properties;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                errors.Add($"{path}.{property.Name} is defined more than once.");
                continue;
            }

            if (allowed.Contains(property.Name)) continue;
            var suggestion = SuggestionHelper.Closest(property.Name, allowed);
            errors.Add(suggestion is null
                ? $"{path} contains unknown key \"{property.Name}\"."
                : $"{path} contains unknown key \"{property.Name}\". Did you mean \"{suggestion}\"?");
        }

        return properties;
    }

    private static string? ReadRequiredString(
        Dictionary<string, JsonElement> properties,
        string name,
        string path,
        List<string> errors)
    {
        if (!properties.TryGetValue(name, out var element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            errors.Add($"{path}.{name} must be a non-empty string.");
            return null;
        }

        return element.GetString();
    }

    private sealed record BaselineFile(int Version, List<BaselineEntry> Findings);
}
