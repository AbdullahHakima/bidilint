using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BidiLint;

internal sealed class ConfigException : Exception
{
    public ConfigException(string message)
        : base(message)
    {
        Errors = new[] { message };
    }

    public ConfigException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

internal static class ConfigLoader
{
    public const string FileName = "bidilint.json";
    public const string SchemaUrl =
        "https://raw.githubusercontent.com/AbdullahHakima/bidilint/main/schema/bidilint.schema.json";

    private static readonly string[] EmptyConfigPathErrors =
    [
        "Config path must not be empty.",
    ];

    private static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal)
    {
        "$schema", "baseCulture", "include", "exclude", "layouts", "files",
        "rules", "ignore", "allowLatin", "rtlCultures", "isolationWrappers",
        "placeholderDialects", "numeralPolicy", "isolationStyle", "baseline",
        "failOn", "output",
    };

    public static IReadOnlySet<string> RootPropertyNames => RootKeys;

    private static readonly HashSet<string> FileKeys = new(StringComparer.Ordinal)
    {
        "path", "culture", "resourceSet",
    };

    private static readonly HashSet<string> RuleKeys = new(StringComparer.Ordinal)
    {
        "severity", "allow",
    };

    private static readonly HashSet<string> IgnoreKeys = new(StringComparer.Ordinal)
    {
        "rule", "path", "culture", "key", "reason", "expires",
    };

    private static readonly HashSet<string> AllowLatinKeys = new(StringComparer.Ordinal)
    {
        "term", "match", "reason", "cultures",
    };

    private static readonly HashSet<string> OutputKeys = new(StringComparer.Ordinal)
    {
        "format", "color", "quiet", "maxWarnings",
    };

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 64,
    };

    public static ConfigResolution Resolve(
        string? explicitPath,
        bool noConfig,
        IEnumerable<string> targetPaths,
        string workingDirectory)
    {
        if (noConfig) return new ConfigResolution(LintConfig.Default, Array.Empty<string>());

        string? configPath;
        if (explicitPath is not null && string.IsNullOrWhiteSpace(explicitPath))
        {
            return new ConfigResolution(LintConfig.Default, EmptyConfigPathErrors);
        }

        if (explicitPath is not null)
        {
            var resolved = ResolveExplicitPath(explicitPath, workingDirectory, out var error);
            if (error is not null)
                return new ConfigResolution(LintConfig.Default, new[] { error });
            configPath = resolved;
        }
        else
        {
            var found = new HashSet<string>(PathComparer);
            foreach (var target in targetPaths)
            {
                var candidate = FindUpwards(target, workingDirectory);
                if (candidate is not null) found.Add(candidate);
            }

            if (found.Count > 1)
            {
                var paths = string.Join(", ", found.OrderBy(path => path, StringComparer.Ordinal));
                return new ConfigResolution(
                    LintConfig.Default,
                    new[]
                    {
                        $"Multiple {FileName} files were found for the target paths ({paths}). Pass --config to choose one.",
                    });
            }

            configPath = found.FirstOrDefault();
        }

        return configPath is null
            ? new ConfigResolution(LintConfig.Default, Array.Empty<string>())
            : Load(configPath);
    }

    public static ConfigResolution Load(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ConfigResolution(LintConfig.Default, new[] { $"Invalid config path: {ex.Message}" });
        }

        if (!File.Exists(fullPath))
        {
            return new ConfigResolution(
                LintConfig.Default,
                new[] { $"Config file not found: {fullPath}" });
        }

        string text;
        try
        {
            text = File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ConfigResolution(
                LintConfig.Default,
                new[] { $"Cannot read config file {fullPath}: {ex.Message}" });
        }

        var errors = new List<string>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            var position = ex.LineNumber is null
                ? ""
                : $" at line {ex.LineNumber + 1}, column {ex.BytePositionInLine + 1}";
            return new ConfigResolution(
                LintConfig.Default,
                new[] { $"{fullPath}{position}: {ex.Message}" });
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new ConfigResolution(
                    LintConfig.Default,
                    new[] { $"{fullPath}: the config root must be a JSON object." });
            }

            var properties = ReadProperties(root, "$", RootKeys, errors);
            var configDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
            var config = ParseRoot(properties, fullPath, configDirectory, errors);
            var prefixed = errors
                .Select(error => $"{fullPath}: {error}")
                .ToList();
            return new ConfigResolution(
                prefixed.Count == 0 ? config : LintConfig.Default,
                prefixed);
        }
    }

    public static string Starter =>
        $$"""
        {
          // BidiLint configuration. JSON comments and trailing commas are allowed.
          "$schema": "{{SchemaUrl}}",
          "baseCulture": "en",
          "include": [],
          "exclude": [],
          "layouts": [],
          "files": [],
          "rules": {
            // "UnisolatedLatin": { "severity": "warning", "allow": ["BidiLint"] }
          },
          "ignore": [
            // { "rule": "ExtraKey", "key": "legacy.*", "reason": "Remove after the 2027-01 migration." }
          ],
          "allowLatin": [
            // { "term": "BidiLint", "match": "exact", "reason": "Brand name." }
          ],
          "rtlCultures": [],
          "isolationWrappers": [],
          "placeholderDialects": ["auto"],
          "numeralPolicy": "mixed",
          "isolationStyle": "isolate",
          "failOn": "error",
          "output": {
            "format": "text",
            "color": true,
            "quiet": false
          }
          // "baseline": "bidilint-baseline.json"
        }

        """;

    public static void WriteStarter(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
            throw new ConfigException($"Config file already exists: {fullPath}");

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, Starter, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static LintConfig ParseRoot(
        Dictionary<string, JsonElement> properties,
        string fullPath,
        string configDirectory,
        List<string> errors)
    {
        var baseCulture = "en";
        if (properties.TryGetValue("baseCulture", out var baseElement))
        {
            var value = ReadString(baseElement, "baseCulture", errors);
            if (value is not null)
            {
                if (CultureTag.TryParse(value, out var tag))
                    baseCulture = tag!.Name;
                else
                    errors.Add("baseCulture must be a known BCP-47 culture such as en or ar-EG.");
            }
        }

        var include = ReadStringArray(properties, "include", errors, normalizeSlashes: true);
        var exclude = ReadStringArray(properties, "exclude", errors, normalizeSlashes: true);
        foreach (var pattern in include) ValidateGlob(pattern, "include", errors);
        foreach (var pattern in exclude) ValidateGlob(pattern, "exclude", errors);
        var layouts = ReadLayoutNames(properties, errors);
        var files = ReadFiles(properties, configDirectory, errors);
        var rules = ReadRules(properties, errors);
        var ignore = ReadIgnore(properties, errors);
        var allowLatin = ReadAllowLatin(properties, errors);
        var rtlCultures = ReadRtlCultures(properties, errors);
        var isolationWrappers = ReadIsolationWrappers(properties, errors);
        var placeholderDialect = ReadPlaceholderDialect(properties, errors);
        var numeralPolicy = ReadNumeralPolicy(properties, errors);
        var isolationStyle = ReadIsolationStyle(properties, errors);
        var baselinePath = ReadBaseline(properties, configDirectory, errors);
        var failOn = ReadFailOn(properties, errors);
        var output = ReadOutput(properties, errors);

        if (properties.TryGetValue("$schema", out var schema) &&
            schema.ValueKind != JsonValueKind.String)
        {
            errors.Add("$schema must be a string URL.");
        }

        return new LintConfig
        {
            ConfigPath = fullPath,
            ConfigDirectory = configDirectory,
            BaseCulture = baseCulture,
            Include = include,
            Exclude = exclude,
            LayoutNames = layouts,
            Files = files,
            Rules = rules,
            Ignore = ignore,
            AllowLatin = allowLatin,
            RtlCultures = rtlCultures,
            IsolationWrappers = isolationWrappers,
            PlaceholderDialect = placeholderDialect,
            NumeralPolicy = numeralPolicy,
            IsolationStyle = isolationStyle,
            BaselinePath = baselinePath,
            FailOn = failOn,
            Output = output,
        };
    }

    private static IReadOnlyList<string> ReadStringArray(
        Dictionary<string, JsonElement> properties,
        string name,
        List<string> errors,
        bool normalizeSlashes = false)
    {
        if (!properties.TryGetValue(name, out var element)) return Array.Empty<string>();
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{name} must be an array of strings.");
            return Array.Empty<string>();
        }

        var values = new List<string>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                errors.Add($"{name}[{index}] must be a non-empty string.");
            }
            else
            {
                var value = item.GetString()!;
                values.Add(normalizeSlashes ? value.Replace('\\', '/') : value);
            }
            index++;
        }

        return values;
    }

    private static IReadOnlyList<string> ReadStringArray(
        JsonElement element,
        string path,
        List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{path} must be an array of strings.");
            return Array.Empty<string>();
        }

        var values = new List<string>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                errors.Add($"{path}[{index}] must be a non-empty string.");
            }
            else
            {
                values.Add(item.GetString()!.Replace('\\', '/'));
            }
            index++;
        }

        return values;
    }

    private static HashSet<string> ReadLayoutNames(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in ReadStringArray(properties, "layouts", errors))
        {
            if (value.Contains('/') || value.Contains('\\') || value is "." or "..")
            {
                errors.Add($"layouts contains \"{value}\"; use a single directory name, not a path.");
                continue;
            }

            names.Add(value);
        }

        return names;
    }

    private static IReadOnlyList<ConfiguredFile> ReadFiles(
        Dictionary<string, JsonElement> properties,
        string configDirectory,
        List<string> errors)
    {
        if (!properties.TryGetValue("files", out var element)) return Array.Empty<ConfiguredFile>();
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add("files must be an array of { path, culture, resourceSet } objects.");
            return Array.Empty<ConfiguredFile>();
        }

        var files = new List<ConfiguredFile>();
        var seenPaths = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var path = $"files[{index}]";
            var fileProperties = ReadProperties(item, path, FileKeys, errors);
            if (!fileProperties.TryGetValue("path", out var pathElement) ||
                !fileProperties.TryGetValue("culture", out var cultureElement) ||
                !fileProperties.TryGetValue("resourceSet", out var resourceSetElement))
            {
                errors.Add($"{path} requires path, culture, and resourceSet.");
                index++;
                continue;
            }

            var relativePath = ReadString(pathElement, $"{path}.path", errors);
            var cultureValue = ReadString(cultureElement, $"{path}.culture", errors);
            var resourceSet = ReadString(resourceSetElement, $"{path}.resourceSet", errors);
            if (relativePath is null || cultureValue is null || resourceSet is null)
            {
                index++;
                continue;
            }

            if (!CultureTag.TryParse(cultureValue, out var culture))
                errors.Add($"{path}.culture must be a known BCP-47 culture.");

            var extension = Path.GetExtension(relativePath);
            if (!IsSupportedExplicitExtension(extension))
                errors.Add($"{path}.path must use .json, .resx, .xml, .strings, .po, .pot, or .arb.");

            if (string.IsNullOrWhiteSpace(resourceSet))
                errors.Add($"{path}.resourceSet must not be empty.");

            try
            {
                var fullPath = Path.GetFullPath(Path.Combine(configDirectory, relativePath));
                if (!seenPaths.Add(fullPath))
                    errors.Add($"{path}.path is configured more than once.");
                else
                    files.Add(new ConfiguredFile(fullPath, culture?.Name ?? cultureValue, resourceSet));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add($"{path}.path is invalid: {ex.Message}");
            }

            index++;
        }

        return files;
    }

    private static Dictionary<string, RuleConfig> ReadRules(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        var rules = new Dictionary<string, RuleConfig>(StringComparer.Ordinal);
        if (!properties.TryGetValue("rules", out var element)) return rules;
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add("rules must be an object keyed by rule code.");
            return rules;
        }

        var knownCodes = RuleCatalog.All.Select(rule => rule.Code).ToArray();
        foreach (var property in element.EnumerateObject())
        {
            var code = property.Name;
            if (!RuleCatalog.TryGet(property.Name, out var metadata))
            {
                var suggestion = SuggestionHelper.Closest(code, knownCodes);
                errors.Add(suggestion is null
                    ? $"rules contains unknown rule \"{code}\"."
                    : $"rules contains unknown rule \"{code}\". Did you mean \"{suggestion}\"?");
                continue;
            }

            if (!metadata.Configurable)
            {
                errors.Add(
                    $"rules.{code} cannot be configured because it is a load, format, or configuration diagnostic.");
                continue;
            }

            if (rules.ContainsKey(code))
            {
                errors.Add($"rules contains duplicate rule \"{code}\".");
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var severity = ReadRuleSeverity(property.Value.GetString(), $"rules.{code}", errors);
                rules.Add(code, new RuleConfig(severity.Severity, severity.Disabled, Array.Empty<string>()));
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"rules.{code} must be a severity string or an object.");
                continue;
            }

            var ruleProperties = ReadProperties(property.Value, $"rules.{code}", RuleKeys, errors);
            DiagnosticSeverity? configuredSeverity = null;
            var disabled = false;
            if (ruleProperties.TryGetValue("severity", out var severityElement))
            {
                var value = ReadString(severityElement, $"rules.{code}.severity", errors);
                if (value is not null)
                {
                    var parsed = ReadRuleSeverity(value, $"rules.{code}.severity", errors);
                    configuredSeverity = parsed.Severity;
                    disabled = parsed.Disabled;
                }
            }

            var allow = ReadStringArray(ruleProperties, "allow", errors);
            if (allow.Count > 0 && !metadata.AllowsTerms)
            {
                errors.Add(
                    $"rules.{code}.allow is only supported by rules that match Latin terms such as UnisolatedLatin.");
            }

            rules.Add(code, new RuleConfig(configuredSeverity, disabled, allow));
        }

        return rules;
    }

    private static IReadOnlyList<IgnoreRule> ReadIgnore(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        if (!properties.TryGetValue("ignore", out var element)) return Array.Empty<IgnoreRule>();
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add("ignore must be an array of suppression objects.");
            return Array.Empty<IgnoreRule>();
        }

        var ignore = new List<IgnoreRule>();
        var knownCodes = RuleCatalog.All.Select(rule => rule.Code).ToArray();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var path = $"ignore[{index}]";
            var ignoreProperties = ReadProperties(item, path, IgnoreKeys, errors);
            if (!ignoreProperties.TryGetValue("reason", out var reasonElement))
            {
                errors.Add($"{path} requires a non-empty reason.");
                index++;
                continue;
            }

            var reason = ReadString(reasonElement, $"{path}.reason", errors);
            if (string.IsNullOrWhiteSpace(reason))
                errors.Add($"{path}.reason must explain why the suppression exists.");

            var rule = ReadOptionalString(ignoreProperties, "rule", $"{path}.rule", errors);
            if (rule is not null)
            {
                if (!RuleCatalog.TryGet(rule, out var metadata))
                {
                    var suggestion = SuggestionHelper.Closest(rule, knownCodes);
                    errors.Add(suggestion is null
                        ? $"{path}.rule is unknown."
                        : $"{path}.rule is unknown. Did you mean \"{suggestion}\"?");
                }
                else if (!metadata.Configurable)
                {
                    errors.Add(
                        $"{path}.rule cannot target a load, format, or configuration diagnostic.");
                }
            }

            var pathPattern = ReadOptionalString(ignoreProperties, "path", $"{path}.path", errors);
            if (pathPattern is not null) ValidateGlob(pathPattern, $"{path}.path", errors);

            var culture = ReadOptionalString(ignoreProperties, "culture", $"{path}.culture", errors);
            if (culture is not null)
            {
                if (CultureTag.TryParse(culture, out var tag))
                    culture = tag!.Name;
                else
                    errors.Add($"{path}.culture must be a known BCP-47 culture.");
            }

            var keyPattern = ReadOptionalString(ignoreProperties, "key", $"{path}.key", errors);
            if (keyPattern is not null) ValidateGlob(keyPattern, $"{path}.key", errors);

            DateOnly? expires = null;
            if (ignoreProperties.TryGetValue("expires", out var expiresElement))
            {
                var value = ReadString(expiresElement, $"{path}.expires", errors);
                if (value is not null)
                {
                    if (DateOnly.TryParseExact(
                            value,
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var parsed))
                    {
                        expires = parsed;
                    }
                    else
                    {
                        errors.Add($"{path}.expires must use YYYY-MM-DD.");
                    }
                }
            }

            if (rule is null && pathPattern is null && culture is null && keyPattern is null)
                errors.Add($"{path} must select at least one of rule, path, culture, or key.");

            if (!string.IsNullOrWhiteSpace(reason))
                ignore.Add(new IgnoreRule(rule, pathPattern, culture, keyPattern, reason, expires));

            index++;
        }

        return ignore;
    }

    private static IReadOnlyList<AllowedLatinTerm> ReadAllowLatin(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        if (!properties.TryGetValue("allowLatin", out var element))
            return Array.Empty<AllowedLatinTerm>();
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add("allowLatin must be an array of terms or term objects.");
            return Array.Empty<AllowedLatinTerm>();
        }

        var terms = new List<AllowedLatinTerm>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var path = $"allowLatin[{index}]";
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"{path} must be a non-empty term.");
                }
                else
                {
                    terms.Add(new AllowedLatinTerm(
                        value,
                        CaseSensitive: true,
                        Reason: null,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
                }
                index++;
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path} must be a string or an object.");
                index++;
                continue;
            }

            var termProperties = ReadProperties(item, path, AllowLatinKeys, errors);
            if (!termProperties.TryGetValue("term", out var termElement))
            {
                errors.Add($"{path} requires a term.");
                index++;
                continue;
            }

            var term = ReadString(termElement, $"{path}.term", errors);
            if (string.IsNullOrWhiteSpace(term))
                errors.Add($"{path}.term must not be empty.");

            var match = ReadOptionalString(termProperties, "match", $"{path}.match", errors);
            var caseSensitive = true;
            if (match is not null)
            {
                switch (match.ToLowerInvariant())
                {
                    case "exact":
                        break;
                    case "case-insensitive":
                        caseSensitive = false;
                        break;
                    default:
                        errors.Add($"{path}.match must be exact or case-insensitive.");
                        break;
                }
            }

            var reason = ReadOptionalString(termProperties, "reason", $"{path}.reason", errors);
            var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (termProperties.TryGetValue("cultures", out var culturesElement))
            {
                foreach (var culture in ReadStringArray(culturesElement, $"{path}.cultures", errors))
                {
                    if (CultureTag.TryParse(culture, out var tag))
                        cultures.Add(tag!.Name);
                    else
                        errors.Add($"{path}.cultures contains invalid culture \"{culture}\".");
                }
            }

            if (!string.IsNullOrWhiteSpace(term))
                terms.Add(new AllowedLatinTerm(term, caseSensitive, reason, cultures));

            index++;
        }

        return terms;
    }

    private static HashSet<string> ReadRtlCultures(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in ReadStringArray(properties, "rtlCultures", errors))
        {
            if (CultureTag.TryParse(value, out var tag))
                cultures.Add(tag!.Name);
            else
                errors.Add($"rtlCultures contains invalid culture \"{value}\".");
        }

        return cultures;
    }

    private static HashSet<string> ReadIsolationWrappers(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        var wrappers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in ReadStringArray(properties, "isolationWrappers", errors))
        {
            if (!IsValidTagName(value))
            {
                errors.Add(
                    $"isolationWrappers contains \"{value}\"; use an element name such as x-bidi or span, without angle brackets.");
                continue;
            }

            wrappers.Add(value);
        }

        return wrappers;
    }

    private static bool IsValidTagName(string value)
    {
        if (value.Length == 0 || !char.IsAsciiLetter(value[0])) return false;
        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or ':'))
                return false;
        }

        return true;
    }

    private static PlaceholderDialect ReadPlaceholderDialect(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        var values = ReadStringArray(properties, "placeholderDialects", errors);
        if (values.Count == 0) return PlaceholderDialect.Auto;

        var dialect = PlaceholderDialect.Auto;
        foreach (var value in values)
        {
            switch (value.ToLowerInvariant())
            {
                case "auto":
                    break;
                case "braced":
                case "dotnet":
                    dialect = PlaceholderDialect.Braced;
                    break;
                case "none":
                    dialect = PlaceholderDialect.None;
                    break;
                case "icu":
                case "printf":
                case "i18next":
                case "angular":
                    errors.Add(
                        $"placeholderDialects: \"{value}\" is recognized but not implemented until 0.7.0.");
                    break;
                default:
                    errors.Add(
                        $"placeholderDialects: unknown dialect \"{value}\". Use auto, braced, dotnet, or none.");
                    break;
            }
        }

        if (dialect == PlaceholderDialect.None && values.Any(value =>
                !value.Equals("none", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("placeholderDialects cannot combine none with another dialect.");
        }

        return dialect;
    }

    private static NumeralPolicy ReadNumeralPolicy(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        if (!properties.TryGetValue("numeralPolicy", out var element)) return NumeralPolicy.Mixed;
        var value = ReadString(element, "numeralPolicy", errors);
        return value?.ToLowerInvariant() switch
        {
            null => NumeralPolicy.Mixed,
            "mixed" => NumeralPolicy.Mixed,
            "allow" => NumeralPolicy.Allow,
            "western" => NumeralPolicy.Western,
            "arabic-indic" => NumeralPolicy.ArabicIndic,
            _ => InvalidNumeralPolicy(value, errors),
        };
    }

    private static NumeralPolicy InvalidNumeralPolicy(string value, List<string> errors)
    {
        errors.Add(
            $"numeralPolicy must be mixed, allow, western, or arabic-indic; found \"{value}\".");
        return NumeralPolicy.Mixed;
    }

    private static IsolationStyle ReadIsolationStyle(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        if (!properties.TryGetValue("isolationStyle", out var element))
            return IsolationStyle.Isolate;

        var value = ReadString(element, "isolationStyle", errors);
        return value?.ToLowerInvariant() switch
        {
            null => IsolationStyle.Isolate,
            "isolate" => IsolationStyle.Isolate,
            "external" => IsolationStyle.External,
            _ => InvalidIsolationStyle(value, errors),
        };
    }

    private static IsolationStyle InvalidIsolationStyle(string value, List<string> errors)
    {
        errors.Add($"isolationStyle must be isolate or external; found \"{value}\".");
        return IsolationStyle.Isolate;
    }

    private static string? ReadBaseline(
        Dictionary<string, JsonElement> properties,
        string configDirectory,
        List<string> errors)
    {
        if (!properties.TryGetValue("baseline", out var element)) return null;
        var value = ReadString(element, "baseline", errors);
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add("baseline must be a non-empty path.");
            return null;
        }

        try
        {
            return Path.GetFullPath(Path.Combine(configDirectory, value));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add($"baseline path is invalid: {ex.Message}");
            return null;
        }
    }

    private static FailOn ReadFailOn(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        if (!properties.TryGetValue("failOn", out var element)) return FailOn.Error;
        var value = ReadString(element, "failOn", errors);
        return value?.ToLowerInvariant() switch
        {
            null => FailOn.Error,
            "error" => FailOn.Error,
            "warning" => FailOn.Warning,
            "none" => FailOn.None,
            _ => InvalidFailOn(value, errors),
        };
    }

    private static FailOn InvalidFailOn(string value, List<string> errors)
    {
        errors.Add($"failOn must be error, warning, or none; found \"{value}\".");
        return FailOn.Error;
    }

    private static OutputConfig ReadOutput(
        Dictionary<string, JsonElement> properties,
        List<string> errors)
    {
        if (!properties.TryGetValue("output", out var element)) return new OutputConfig();
        var outputProperties = ReadProperties(element, "output", OutputKeys, errors);

        OutputFormat? format = null;
        if (outputProperties.TryGetValue("format", out var formatElement))
        {
            var value = ReadString(formatElement, "output.format", errors);
            format = value?.ToLowerInvariant() switch
            {
                "text" => OutputFormat.Text,
                "json" => OutputFormat.Json,
                null => null,
                _ => InvalidOutputFormat(value, errors),
            };
        }

        bool? color = null;
        if (outputProperties.TryGetValue("color", out var colorElement))
        {
            if (colorElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                color = colorElement.GetBoolean();
            else
                errors.Add("output.color must be true or false.");
        }

        bool? quiet = null;
        if (outputProperties.TryGetValue("quiet", out var quietElement))
        {
            if (quietElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                quiet = quietElement.GetBoolean();
            else
                errors.Add("output.quiet must be true or false.");
        }

        int? maxWarnings = null;
        if (outputProperties.TryGetValue("maxWarnings", out var maxWarningsElement))
        {
            if (maxWarningsElement.ValueKind == JsonValueKind.Number &&
                maxWarningsElement.TryGetInt32(out var parsed) &&
                parsed >= 0)
            {
                maxWarnings = parsed;
            }
            else
            {
                errors.Add("output.maxWarnings must be a non-negative integer.");
            }
        }

        return new OutputConfig(format, color, quiet, maxWarnings);
    }

    private static OutputFormat? InvalidOutputFormat(string value, List<string> errors)
    {
        errors.Add($"output.format must be text or json; found \"{value}\".");
        return null;
    }

    private static (DiagnosticSeverity? Severity, bool Disabled) ReadRuleSeverity(
        string? value,
        string path,
        List<string> errors)
    {
        return value?.ToLowerInvariant() switch
        {
            "off" => (null, true),
            "error" => (DiagnosticSeverity.Error, false),
            "warning" => (DiagnosticSeverity.Warning, false),
            "info" => (DiagnosticSeverity.Info, false),
            null => (null, false),
            _ => InvalidRuleSeverity(value, path, errors),
        };
    }

    private static (DiagnosticSeverity? Severity, bool Disabled) InvalidRuleSeverity(
        string value,
        string path,
        List<string> errors)
    {
        errors.Add($"{path} must be off, error, warning, or info; found \"{value}\".");
        return (null, false);
    }

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

    private static string? ReadString(JsonElement element, string path, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            errors.Add($"{path} must be a string.");
            return null;
        }

        return element.GetString();
    }

    private static string? ReadOptionalString(
        Dictionary<string, JsonElement> properties,
        string name,
        string path,
        List<string> errors)
    {
        if (!properties.TryGetValue(name, out var element)) return null;
        var value = ReadString(element, path, errors);
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{path} must not be empty.");
            return null;
        }

        return value;
    }

    private static void ValidateGlob(string pattern, string path, List<string> errors)
    {
        try
        {
            GlobPattern.Create(pattern);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"{path} is not a valid glob: {ex.Message}");
        }
    }

    private static string? ResolveExplicitPath(
        string path,
        string workingDirectory,
        out string? error)
    {
        error = null;
        string fullPath;
        try
        {
            fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(workingDirectory, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Invalid config path: {ex.Message}";
            return null;
        }

        if (Directory.Exists(fullPath))
            fullPath = Path.Combine(fullPath, FileName);

        if (!File.Exists(fullPath))
        {
            error = $"Config file not found: {fullPath}";
            return null;
        }

        return fullPath;
    }

    private static string? FindUpwards(string target, string workingDirectory)
    {
        string fullPath;
        try
        {
            fullPath = Path.IsPathRooted(target)
                ? Path.GetFullPath(target)
                : Path.GetFullPath(Path.Combine(workingDirectory, target));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var directory = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? workingDirectory;
        if (!Directory.Exists(directory)) directory = workingDirectory;

        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, FileName);
            if (File.Exists(candidate)) return candidate;

            var parent = Directory.GetParent(directory);
            if (parent is null) break;
            directory = parent.FullName;
        }

        return null;
    }

    private static bool IsSupportedExplicitExtension(string extension) =>
        extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".resx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".strings", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".po", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".pot", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".arb", StringComparison.OrdinalIgnoreCase);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
