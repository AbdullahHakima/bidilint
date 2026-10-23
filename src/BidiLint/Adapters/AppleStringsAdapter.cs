using System.Globalization;
using System.Text;

namespace BidiLint;

// Apple .strings: Resources/en.lproj/Localizable.strings. Base.lproj maps to
// the configured base culture. The parser is deliberately small but strict:
// comments, quoted and bare tokens, and the usual C-style escapes are handled.
internal sealed class AppleStringsAdapter : ILocaleAdapter
{
    public LocaleIdentity? Identify(string path, string workingDirectory, string baseCulture)
    {
        if (!Path.GetExtension(path).Equals(".strings", StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = LocalePath.Segments(workingDirectory, path);
        for (var i = segments.Length - 2; i >= 0; i--)
        {
            if (!segments[i].EndsWith(".lproj", StringComparison.OrdinalIgnoreCase)) continue;

            var name = segments[i][..^".lproj".Length];
            var culture = name.Equals("Base", StringComparison.OrdinalIgnoreCase)
                ? baseCulture
                : CultureTag.TryParse(name, out var tag) ? tag!.Name : null;
            if (culture is null) return null;

            return new LocaleIdentity(
                path,
                culture,
                LocalePath.ResourceSet(segments, i),
                LocaleFormat.AppleStrings);
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
            Format = LocaleFormat.AppleStrings,
            ResourceSet = identity.ResourceSet,
            Source = source,
        };

        var parser = new StringsParser(source, document, diagnostics);
        return parser.Parse() ? document : null;
    }

    private sealed class StringsParser
    {
        private readonly SourceText _source;
        private readonly string _text;
        private readonly LocaleDocument _document;
        private readonly List<Diagnostic> _diagnostics;
        private int _index;

        public StringsParser(SourceText source, LocaleDocument document, List<Diagnostic> diagnostics)
        {
            _source = source;
            _text = source.Text;
            _document = document;
            _diagnostics = diagnostics;
        }

        public bool Parse()
        {
            while (true)
            {
                if (!SkipTrivia()) return Malformed("Unterminated comment.");
                if (_index >= _text.Length) return true;

                if (!TryReadToken(out var key, out var keyStart, out var keyEnd))
                    return Malformed("Expected a key.");

                if (!SkipTrivia()) return Malformed("Unterminated comment.");
                if (!TryConsume('=')) return Malformed("Expected '=' after the key.");

                if (!SkipTrivia()) return Malformed("Unterminated comment.");
                if (!TryReadToken(out var value, out var valueStart, out var valueEnd))
                    return Malformed("Expected a value.");

                if (!SkipTrivia()) return Malformed("Unterminated comment.");
                if (!TryConsume(';')) return Malformed("Expected ';' after the value.");

                AddEntry(key, value, keyStart, keyEnd, valueStart, valueEnd);
            }
        }

        private void AddEntry(
            string key,
            string value,
            int keyStart,
            int keyEnd,
            int valueStart,
            int valueEnd)
        {
            var keySpan = Span(keyStart, keyEnd);
            var valueSpan = Span(valueStart, valueEnd);

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
                Span = valueSpan,
                KeySpan = keySpan,
            });
        }

        private bool TryReadToken(out string value, out int start, out int end)
        {
            start = _index;
            if (_index < _text.Length && _text[_index] == '"')
            {
                if (!TryReadQuoted(out value))
                {
                    end = _index;
                    return false;
                }
                end = _index;
                return true;
            }

            var builder = new StringBuilder();
            while (_index < _text.Length)
            {
                var current = _text[_index];
                if (char.IsWhiteSpace(current) || current is '=' or ';') break;
                builder.Append(current);
                _index++;
            }

            end = _index;
            value = builder.ToString();
            return value.Length > 0;
        }

        private bool TryReadQuoted(out string value)
        {
            var builder = new StringBuilder();
            _index++; // opening quote

            while (_index < _text.Length)
            {
                var current = _text[_index];
                if (current == '"')
                {
                    _index++;
                    value = builder.ToString();
                    return true;
                }

                if (current is '\r' or '\n')
                {
                    value = "";
                    return false;
                }

                if (current != '\\')
                {
                    builder.Append(current);
                    _index++;
                    continue;
                }

                _index++;
                if (_index >= _text.Length)
                {
                    value = "";
                    return false;
                }

                var escaped = _text[_index++];
                switch (escaped)
                {
                    case 'n': builder.Append('\n'); break;
                    case 't': builder.Append('\t'); break;
                    case 'r': builder.Append('\r'); break;
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '\'': builder.Append('\''); break;
                    case 'u':
                    case 'U':
                        if (_index + 4 > _text.Length ||
                            !int.TryParse(
                                _text.AsSpan(_index, 4),
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out var code))
                        {
                            value = "";
                            return false;
                        }
                        builder.Append((char)code);
                        _index += 4;
                        break;
                    default:
                        builder.Append(escaped);
                        break;
                }
            }

            value = "";
            return false;
        }

        private bool SkipTrivia()
        {
            while (_index < _text.Length)
            {
                if (char.IsWhiteSpace(_text[_index]))
                {
                    _index++;
                    continue;
                }

                if (_text[_index] != '/') return true;

                if (_index + 1 >= _text.Length) return true;

                if (_text[_index + 1] == '/')
                {
                    _index += 2;
                    while (_index < _text.Length && _text[_index] is not ('\r' or '\n')) _index++;
                    continue;
                }

                if (_text[_index + 1] == '*')
                {
                    var end = _text.IndexOf("*/", _index + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        _index = _text.Length;
                        return false;
                    }
                    _index = end + 2;
                    continue;
                }

                return true;
            }

            return true;
        }

        private bool TryConsume(char expected)
        {
            if (_index >= _text.Length || _text[_index] != expected) return false;
            _index++;
            return true;
        }

        private SourceSpan Span(int start, int end) =>
            new(_source.PositionAtCharOffset(start), _source.PositionAtCharOffset(end));

        private bool Malformed(string message)
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticCodes.MalformedFile,
                DiagnosticSeverity.Error,
                message,
                _document.FilePath,
                SourceSpan.At(_source.PositionAtCharOffset(_index)),
                _document.Culture));
            return false;
        }
    }
}
