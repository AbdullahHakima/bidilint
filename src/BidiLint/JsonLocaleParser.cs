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

        while (true)
        {
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON while reading an object.");

            if (reader.TokenType == JsonTokenType.EndObject) return;
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

            ParseValue(ref reader, fullKey, keySpan, collect && !duplicate && !metadata);
        }
    }

    private void ParseArray(ref Utf8JsonReader reader, string prefix, bool collect)
    {
        var index = 0;

        while (true)
        {
            if (!reader.Read())
                throw new JsonException("Unexpected end of JSON while reading an array.");

            if (reader.TokenType == JsonTokenType.EndArray) return;

            ParseValue(ref reader, $"{prefix}[{index}]", keySpan: null, collect);
            index++;
        }
    }

    private void ParseValue(
        ref Utf8JsonReader reader,
        string key,
        SourceSpan? keySpan,
        bool collect)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                ParseObject(ref reader, key, collect);
                break;

            case JsonTokenType.StartArray:
                ParseArray(ref reader, key, collect);
                break;

            case JsonTokenType.String:
                if (collect)
                {
                    AddEntry(key, reader.GetString() ?? "", keySpan, SpanOf(reader));
                }
                break;

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
                break;

            default:
                throw new JsonException($"Unexpected token {reader.TokenType}.");
        }
    }

    private void AddEntry(
        string key,
        string value,
        SourceSpan? keySpan,
        SourceSpan valueSpan)
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
            return;
        }

        _document.Entries.Add(key, new LocaleEntry
        {
            Key = key,
            Value = value,
            Document = _document,
            Span = valueSpan,
            KeySpan = keySpan,
        });
    }

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
