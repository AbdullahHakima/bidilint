using System.Globalization;
using System.Text.Json;

namespace BidiLint;

// Parses one JSON locale file into dotted keys while keeping source spans and
// reporting duplicate keys and flattening collisions instead of swallowing
// them. Comments and trailing commas are accepted.
internal sealed class JsonLocaleParser
{
    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        MaxDepth = 128,
    };

    private static readonly string[] I18NextPluralSuffixes =
    [
        "_other",
        "_zero",
        "_one",
        "_two",
        "_few",
        "_many",
    ];

    private readonly SourceText _source;
    private readonly LocaleDocument _document;
    private readonly List<Diagnostic> _diagnostics;
    private readonly string _filePath;
    private readonly string _culture;
    private readonly JsonParseOptions _options;

    private JsonLocaleParser(
        SourceText source,
        LocaleDocument document,
        List<Diagnostic> diagnostics,
        string filePath,
        string culture,
        JsonParseOptions options)
    {
        _source = source;
        _document = document;
        _diagnostics = diagnostics;
        _filePath = filePath;
        _culture = culture;
        _options = options;
    }

    public static LocaleDocument? Parse(
        string filePath,
        string culture,
        string resourceSet,
        SourceText source,
        List<Diagnostic> diagnostics,
        JsonParseOptions? options = null)
    {
        var effectiveOptions = options ?? new JsonParseOptions();
        var document = new LocaleDocument
        {
            Culture = culture,
            FilePath = filePath,
            Format = effectiveOptions.Format,
            ResourceSet = resourceSet,
            Source = source,
        };

        var parser = new JsonLocaleParser(
            source,
            document,
            diagnostics,
            filePath,
            culture,
            effectiveOptions);
        return parser.Parse();
    }

    private LocaleDocument? Parse()
    {
        var reader = new Utf8JsonReader(_source.Bytes, ReaderOptions);

        try
        {
            if (!reader.Read())
            {
                AddMalformed("Empty JSON document.", SourceSpan.At(new SourcePosition(1, 1, 0)));
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                AddMalformed("A locale file must have a JSON object at its root.", SpanOf(reader));
                return null;
            }

            ParseObject(ref reader, prefix: "", collect: true);

            if (reader.Read())
            {
                AddMalformed("Unexpected content after the root object.", SpanOf(reader));
                _document.Entries.Clear();
                return null;
            }

            return _document;
        }
        catch (JsonException ex)
        {
            var position = _source.PositionAtLineByteOffset(
                (int)(ex.LineNumber ?? 0),
                (int)(ex.BytePositionInLine ?? 0));
            AddMalformed(ex.Message, SourceSpan.At(position));
            _document.Entries.Clear();
            return null;
        }
    }

    private void ParseObject(ref Utf8JsonReader reader, string prefix, bool collect)
    {
        var seen = new Dictionary<string, SourceSpan>(StringComparer.Ordinal);
        var pluralCandidates = new List<PluralCandidate>();

        while (true)
        {
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON while reading an object.");

            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException($"Expected a property name but found {reader.TokenType}.");

            var name = reader.GetString() ?? "";
            var keySpan = SpanOf(reader);
            var metadata = _options.SkipMetadataKeys && name.StartsWith('@');
            var fullKey = prefix.Length == 0 ? name : $"{prefix}.{name}";

            var duplicate = false;
            if (!metadata && seen.TryGetValue(name, out var firstSpan))
            {
                duplicate = true;
                if (collect)
                {
                    _diagnostics.Add(new Diagnostic(
                        DiagnosticCodes.DuplicateKey,
                        DiagnosticSeverity.Error,
                        $"Key \"{fullKey}\" appears more than once in this object. The first value is used.",
                        _filePath,
                        keySpan,
                        _culture,
                        fullKey,
                        new[]
                        {
                            new RelatedDiagnostic("First occurrence.", _filePath, firstSpan, fullKey),
                        }));
                }
            }
            else
            {
                if (!metadata) seen[name] = keySpan;
            }

            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON after a property name.");

            var collectValue = collect && !duplicate && !metadata;
            var entry = ParseValue(ref reader, fullKey, keySpan, collectValue);
            if (entry is not null &&
                collectValue &&
                _options.Format == LocaleFormat.Json &&
                TryGetI18NextPluralSuffix(name, out var form, out var baseName))
            {
                pluralCandidates.Add(new PluralCandidate(entry, baseName, form));
            }
        }

        MarkI18NextPluralGroups(prefix, pluralCandidates);
    }

    private void ParseArray(ref Utf8JsonReader reader, string prefix, bool collect)
    {
        // Nextcloud and similar gettext-derived JSON trees encode a plural
        // group as a string array under "_singular_::_plural_". Other JSON
        // arrays are ordinary indexed lists and must keep their ExtraKey
        // behavior. Android and PO use explicit plural metadata instead.
        var pluralGroup = IsPluralArrayKey(prefix) ? prefix : null;
        var index = 0;

        while (true)
        {
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON while reading an array.");

            if (reader.TokenType == JsonTokenType.EndArray) return;

            var plural = pluralGroup is null
                ? null
                : new PluralFormInfo(
                    pluralGroup,
                    index.ToString(CultureInfo.InvariantCulture),
                    PluralFormKind.Numeric);
            ParseValue(ref reader, $"{prefix}[{index}]", keySpan: null, collect, plural);
            index++;
        }
    }

    private static bool IsPluralArrayKey(string key) =>
        key.Contains("_::_", StringComparison.Ordinal) &&
        key.EndsWith('_') &&
        (key.StartsWith('_') || key.Contains("._", StringComparison.Ordinal));

    private LocaleEntry? ParseValue(
        ref Utf8JsonReader reader,
        string key,
        SourceSpan? keySpan,
        bool collect,
        PluralFormInfo? plural = null)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                ParseObject(ref reader, key, collect);
                return null;

            case JsonTokenType.StartArray:
                ParseArray(ref reader, key, collect);
                return null;

            case JsonTokenType.String:
                return collect
                    ? AddEntry(key, reader.GetString() ?? "", keySpan, SpanOf(reader), plural)
                    : null;

            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:
                if (collect)
                {
                    _diagnostics.Add(new Diagnostic(
                        DiagnosticCodes.NonStringValue,
                        DiagnosticSeverity.Warning,
                        "Value is not a string and cannot be linted; expected a string.",
                        _filePath,
                        SpanOf(reader),
                        _culture,
                        key));
                }
                return null;

            default:
                throw new JsonException($"Unexpected token {reader.TokenType}.");
        }
    }

    private LocaleEntry? AddEntry(
        string key,
        string value,
        SourceSpan? keySpan,
        SourceSpan valueSpan,
        PluralFormInfo? plural = null)
    {
        if (_document.Entries.TryGetValue(key, out var existing))
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.KeyCollision,
                DiagnosticSeverity.Error,
                $"Flattening produces the key \"{key}\" more than once. The first value is used.",
                _filePath,
                valueSpan,
                _culture,
                key,
                new[]
                {
                    new RelatedDiagnostic(
                        "First value for this flattened key.",
                        _filePath,
                        existing.Span,
                        key),
                }));
            return null;
        }

        var entry = new LocaleEntry
        {
            Key = key,
            Value = value,
            Document = _document,
            Span = valueSpan,
            KeySpan = keySpan,
            ValueMap = ValueSourceMap.TryCreate(
                _source,
                valueSpan,
                value,
                ValueQuoteSyntax.Json),
            Plural = plural,
        };
        _document.Entries.Add(key, entry);
        return entry;
    }

    // i18next v4 stores plural categories as sibling keys with a suffix, for
    // example item_one and item_other. Only mark a group when the same object
    // has an other value and at least one non-other category; otherwise an
    // ordinary key that happens to end in _one stays an ordinary key.
    private static bool TryGetI18NextPluralSuffix(
        string name,
        out string form,
        out string baseName)
    {
        foreach (var suffix in I18NextPluralSuffixes)
        {
            if (name.Length <= suffix.Length ||
                !name.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            form = suffix[1..];
            baseName = name[..^suffix.Length];
            return true;
        }

        form = "";
        baseName = "";
        return false;
    }

    private static void MarkI18NextPluralGroups(
        string prefix,
        List<PluralCandidate> candidates)
    {
        if (candidates.Count == 0) return;

        foreach (var group in candidates.GroupBy(
                     candidate => candidate.BaseName,
                     StringComparer.Ordinal))
        {
            var hasOther = group.Any(candidate =>
                string.Equals(candidate.Form, "other", StringComparison.Ordinal));
            var hasNonOther = group.Any(candidate =>
                !string.Equals(candidate.Form, "other", StringComparison.Ordinal));
            if (!hasOther || !hasNonOther) continue;

            var groupKey = prefix.Length == 0
                ? group.Key
                : $"{prefix}.{group.Key}";
            foreach (var candidate in group)
            {
                candidate.Entry.Plural = new PluralFormInfo(
                    groupKey,
                    candidate.Form,
                    PluralFormKind.Category);
            }
        }
    }

    private sealed record PluralCandidate(
        LocaleEntry Entry,
        string BaseName,
        string Form);

    private void AddMalformed(string message, SourceSpan span)
    {
        _diagnostics.Add(new Diagnostic(
            DiagnosticCodes.MalformedFile,
            DiagnosticSeverity.Error,
            message,
            _filePath,
            span,
            _culture));
    }

    private SourceSpan SpanOf(in Utf8JsonReader reader)
    {
        var start = _source.PositionAtByteOffset((int)reader.TokenStartIndex);
        var end = _source.PositionAtByteOffset((int)reader.BytesConsumed);
        return new SourceSpan(start, end);
    }
}

internal sealed record JsonParseOptions(
    bool SkipMetadataKeys = false,
    LocaleFormat Format = LocaleFormat.Json);
