using System.Globalization;
using System.Text;

namespace BidiLint;

// gettext PO/POT. Standard layout:
//   locale/<culture>/LC_MESSAGES/messages.po
//   locale/messages.pot
// The msgid is the key; msgctxt, when present, is prefixed with "|".
// Plural forms are flattened to key[0], key[1], … until the plurals-aware
// comparison planned for 0.7. POT templates use msgid as the base value.
internal sealed class PoAdapter : ILocaleAdapter
{
    public LocaleIdentity? Identify(
        string path,
        string workingDirectory,
        string baseCulture,
        IReadOnlySet<string>? layoutNames = null)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".po", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".pot", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var isTemplate = extension.Equals(".pot", StringComparison.OrdinalIgnoreCase);
        var segments = LocalePath.Segments(workingDirectory, path);
        var messages = LocalePath.FindLastSegment(
            segments,
            segment => segment.Equals("LC_MESSAGES", StringComparison.OrdinalIgnoreCase));

        if (messages >= 1)
        {
            var culture = isTemplate
                ? baseCulture
                : CultureTag.TryParse(segments[messages - 1], out var tag) ? tag!.Name : null;
            if (culture is null) return null;

            // Resource sets pair across cultures and across the PO/POT
            // extension: locale/en/LC_MESSAGES/messages.po and
            // locale/messages.pot both become locale/messages.po.
            var resourceParts = new List<string>(segments.Length - 2);
            for (var i = 0; i < segments.Length; i++)
            {
                if (i == messages - 1 || i == messages) continue;
                resourceParts.Add(segments[i]);
            }
            var resourceSet = Path.ChangeExtension(
                string.Join('/', resourceParts),
                ".po").Replace('\\', '/');
            return new LocaleIdentity(path, culture, resourceSet, LocaleFormat.Po);
        }

        if (isTemplate && segments.Length >= 2 && LocalePath.IsLayoutName(segments[^2], layoutNames))
        {
            var resourceSet = Path.ChangeExtension(
                string.Join('/', segments),
                ".po").Replace('\\', '/');
            return new LocaleIdentity(path, baseCulture, resourceSet, LocaleFormat.Po);
        }

        if (!isTemplate && segments.Length >= 2 && LocalePath.IsLayoutName(segments[^2], layoutNames))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (!CultureTag.TryParse(stem, out var tag)) return null;
            var resourceSet = string.Join('/', segments[..^1]) + "/messages.po";
            return new LocaleIdentity(path, tag!.Name, resourceSet, LocaleFormat.Po);
        }

        return null;
    }

    public LocaleDocument? Load(LocaleIdentity identity, List<Diagnostic> diagnostics)
    {
        var source = TextLocaleFile.Read(identity.FilePath, identity.Culture, diagnostics);
        if (source is null) return null;

        var document = new LocaleDocument
        {
            Culture = identity.Culture,
            FilePath = identity.FilePath,
            Format = LocaleFormat.Po,
            ResourceSet = identity.ResourceSet,
            Source = source,
        };

        var isTemplate = Path.GetExtension(identity.FilePath)
            .Equals(".pot", StringComparison.OrdinalIgnoreCase);
        var parser = new PoParser(source, document, isTemplate, diagnostics);
        return parser.Parse() ? document : null;
    }

    private sealed class PoParser
    {
        private readonly SourceText _source;
        private readonly LocaleDocument _document;
        private readonly bool _isTemplate;
        private readonly List<Diagnostic> _diagnostics;
        private readonly PoEntry _entry = new();
        private StringBuilder? _current;

        public PoParser(
            SourceText source,
            LocaleDocument document,
            bool isTemplate,
            List<Diagnostic> diagnostics)
        {
            _source = source;
            _document = document;
            _isTemplate = isTemplate;
            _diagnostics = diagnostics;
        }

        public bool Parse()
        {
            var lines = _source.Text.ReplaceLineEndings("\n").Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var lineNumber = index + 1;
                var trimmed = line.TrimStart();

                if (trimmed.Length == 0)
                {
                    Flush();
                    continue;
                }

                if (trimmed[0] == '#') continue;

                if (trimmed[0] == '"')
                {
                    if (_current is null)
                        return Malformed(lineNumber, "String continuation without a field.");

                    if (!TryDecodeQuoted(trimmed, out var value))
                        return Malformed(lineNumber, "Malformed quoted string.");

                    _current.Append(value);
                    continue;
                }

                var separator = trimmed.IndexOfAny([' ', '\t']);
                var keyword = separator < 0 ? trimmed : trimmed[..separator];
                var rest = separator < 0 ? "" : trimmed[(separator + 1)..].TrimStart();

                if (keyword == "msgid" && _entry.Id is not null)
                    Flush();

                switch (keyword)
                {
                    case "msgctxt":
                        if (!StartField(ref _entry.Context, ref _current, rest, lineNumber))
                            return false;
                        break;
                    case "msgid":
                        if (!StartField(ref _entry.Id, ref _current, rest, lineNumber))
                            return false;
                        _entry.IdLine = lineNumber;
                        break;
                    case "msgid_plural":
                        if (!StartField(ref _entry.PluralId, ref _current, rest, lineNumber))
                            return false;
                        break;
                    case "msgstr":
                        if (!StartField(ref _entry.Value, ref _current, rest, lineNumber))
                            return false;
                        _entry.ValueLine = lineNumber;
                        break;
                    default:
                        if (keyword.StartsWith("msgstr[", StringComparison.Ordinal) && keyword.EndsWith(']'))
                        {
                            var indexText = keyword["msgstr[".Length..^1];
                            if (!int.TryParse(indexText, out var form) || form < 0)
                                return Malformed(lineNumber, "Invalid plural form index.");

                            if (!_entry.Forms.TryGetValue(form, out var builder))
                            {
                                builder = new StringBuilder();
                                _entry.Forms.Add(form, builder);
                            }
                            if (!TryDecodeQuoted(rest, out var value))
                                return Malformed(lineNumber, "Malformed quoted string.");

                            builder.Append(value);
                            _current = builder;
                            if (form == 0) _entry.ValueLine = lineNumber;
                            break;
                        }

                        return Malformed(lineNumber, $"Unknown PO field \"{keyword}\".");
                }
            }

            Flush();
            return true;
        }

        private bool StartField(
            ref StringBuilder? field,
            ref StringBuilder? current,
            string value,
            int lineNumber)
        {
            field = new StringBuilder();
            if (!TryDecodeQuoted(value, out var decoded))
                return Malformed(lineNumber, "Malformed quoted string.");

            field.Append(decoded);
            current = field;
            return true;
        }

        private void Flush()
        {
            if (!_entry.HasContent)
            {
                _entry.Reset();
                _current = null;
                return;
            }

            var msgid = _entry.Id?.ToString() ?? "";
            if (msgid.Length > 0)
            {
                var context = _entry.Context?.ToString();
                var keyPrefix = string.IsNullOrEmpty(context) ? msgid : $"{context}|{msgid}";
                var span = SpanAt(_entry.ValueLine > 0 ? _entry.ValueLine : _entry.IdLine);
                var keySpan = SpanAt(_entry.IdLine);

                if (_entry.Forms.Count > 0)
                {
                    foreach (var form in _entry.Forms.OrderBy(pair => pair.Key))
                    {
                        var value = form.Value.ToString();
                        if (_isTemplate && value.Length == 0) value = msgid;
                        AddEntry(
                            $"{keyPrefix}[{form.Key}]",
                            value,
                            keySpan,
                            span,
                            new PluralFormInfo(
                                keyPrefix,
                                form.Key.ToString(CultureInfo.InvariantCulture),
                                PluralFormKind.Numeric));
                    }
                }
                else
                {
                    var value = _entry.Value?.ToString() ?? "";
                    if (_isTemplate && value.Length == 0) value = msgid;
                    AddEntry(keyPrefix, value, keySpan, span);
                }
            }

            _entry.Reset();
            _current = null;
        }

        private void AddEntry(
            string key,
            string value,
            SourceSpan? keySpan,
            SourceSpan? span,
            PluralFormInfo? plural = null)
        {
            if (_document.Entries.TryGetValue(key, out var existing))
            {
                _diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.DuplicateKey,
                    DiagnosticSeverity.Error,
                    $"Key \"{key}\" appears more than once. The first value is used.",
                    _document.FilePath,
                    keySpan,
                    _document.Culture,
                    key,
                    new[]
                    {
                        new RelatedDiagnostic(
                            "First occurrence.",
                            _document.FilePath,
                            existing.KeySpan,
                            key),
                    }));
                return;
            }

            _document.Entries.Add(key, new LocaleEntry
            {
                Key = key,
                Value = value,
                Document = _document,
                Span = span,
                KeySpan = keySpan,
                Plural = plural,
            });
        }

        private SourceSpan SpanAt(int line) =>
            SourceSpan.At(_source.PositionAt(Math.Max(1, line), 1));

        private bool Malformed(int line, string message)
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.MalformedFile,
                DiagnosticSeverity.Error,
                message,
                _document.FilePath,
                SpanAt(line),
                _document.Culture));
            return false;
        }

        private static bool TryDecodeQuoted(string text, out string value)
        {
            value = "";
            var trimmed = text.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '"' || trimmed[^1] != '"') return false;

            var builder = new StringBuilder(trimmed.Length - 2);
            for (var i = 1; i < trimmed.Length - 1; i++)
            {
                var current = trimmed[i];
                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                i++;
                if (i >= trimmed.Length - 1) return false;

                var escaped = trimmed[i];
                switch (escaped)
                {
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    case 'r': builder.Append('\r'); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case 'x':
                        if (i + 2 >= trimmed.Length - 1) return false;
                        if (!byte.TryParse(
                                trimmed.AsSpan(i + 1, 2),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var hex))
                        {
                            return false;
                        }
                        builder.Append((char)hex);
                        i += 2;
                        break;
                    default:
                        builder.Append(escaped);
                        break;
                }
            }

            value = builder.ToString();
            return true;
        }

        private sealed class PoEntry
        {
            public StringBuilder? Context;
            public StringBuilder? Id;
            public StringBuilder? PluralId;
            public StringBuilder? Value;
            public Dictionary<int, StringBuilder> Forms { get; } = new();
            public int IdLine;
            public int ValueLine;

            public bool HasContent =>
                Context is not null || Id is not null || PluralId is not null ||
                Value is not null || Forms.Count > 0;

            public void Reset()
            {
                Context = null;
                Id = null;
                PluralId = null;
                Value = null;
                Forms.Clear();
                IdLine = 0;
                ValueLine = 0;
            }
        }
    }
}
