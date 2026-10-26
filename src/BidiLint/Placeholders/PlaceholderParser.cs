using System.Globalization;

namespace BidiLint;

// Parses the placeholder dialects BidiLint can compare. Parsing is deliberately
// permissive about literal text and strict only about the placeholder syntax it
// recognizes, so ordinary prose with braces or percent signs is not reported.
internal static class PlaceholderParser
{
    public static bool MightContain(string value) =>
        value.Contains('{') ||
        value.Contains('%') ||
        value.Contains("${", StringComparison.Ordinal);

    public static PlaceholderMessage Parse(
        string value,
        LocaleFormat format,
        PlaceholderDialect configured)
    {
        var dialect = ResolveDialect(value, format, configured);
        return dialect switch
        {
            PlaceholderDialect.DotNet => ParseDotNet(value),
            PlaceholderDialect.Icu => ParseIcu(value),
            PlaceholderDialect.Printf => ParsePrintf(value),
            PlaceholderDialect.I18Next or PlaceholderDialect.Angular or PlaceholderDialect.Mustache =>
                ParseDoubleBraced(value, dialect),
            PlaceholderDialect.Rails => ParseRails(value),
            PlaceholderDialect.JavaScript => ParseJavaScript(value),
            _ => new PlaceholderMessage(PlaceholderDialect.None, [], []),
        };
    }

    private static PlaceholderDialect ResolveDialect(
        string value,
        LocaleFormat format,
        PlaceholderDialect configured)
    {
        if (configured is not PlaceholderDialect.Auto and not PlaceholderDialect.None)
            return configured;

        var formatDefault = format switch
        {
            LocaleFormat.Resx => PlaceholderDialect.DotNet,
            LocaleFormat.Arb => PlaceholderDialect.Icu,
            LocaleFormat.Android or LocaleFormat.AppleStrings or LocaleFormat.Po =>
                PlaceholderDialect.Printf,
            _ => PlaceholderDialect.Auto,
        };

        return formatDefault != PlaceholderDialect.Auto
            ? formatDefault
            : Detect(value);
    }

    private static PlaceholderDialect Detect(string value)
    {
        if (ContainsIcuMarker(value)) return PlaceholderDialect.Icu;
        if (ContainsRailsPlaceholder(value)) return PlaceholderDialect.Rails;
        if (value.Contains("${", StringComparison.Ordinal)) return PlaceholderDialect.JavaScript;

        var hasDoubleBraces = value.Contains("{{", StringComparison.Ordinal);
        var hasSingleBraces = HasSingleBracedPlaceholder(value);
        if (hasDoubleBraces && !hasSingleBraces) return PlaceholderDialect.I18Next;
        if (hasSingleBraces || HasBracePlaceholderStart(value)) return PlaceholderDialect.DotNet;
        if (hasDoubleBraces) return PlaceholderDialect.I18Next;
        if (value.Contains("%1$", StringComparison.Ordinal) ||
            value.Contains("%(", StringComparison.Ordinal))
        {
            return PlaceholderDialect.Printf;
        }

        if (LooksLikePrintf(value)) return PlaceholderDialect.Printf;
        return PlaceholderDialect.None;
    }

    private static bool HasBracePlaceholderStart(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '{') continue;
            if (index + 1 >= value.Length) return false;
            var next = value[index + 1];
            if (next is '{' or '}' or '_' or '.' || char.IsLetterOrDigit(next))
                return true;
            if (next == '/' && index + 2 < value.Length && char.IsLetter(value[index + 2]))
                return true;
        }

        return false;
    }

    private static bool ContainsIcuMarker(string value) =>
        value.Contains(", plural,", StringComparison.Ordinal) ||
        value.Contains(", select,", StringComparison.Ordinal) ||
        value.Contains(", selectordinal,", StringComparison.Ordinal);

    // A percent sign immediately before a brace tag (for example Nextcloud's
    // "{s}{usageRelative}%{/s}") is not a Rails placeholder. Rails names start
    // with a letter or underscore.
    private static bool ContainsRailsPlaceholder(string value)
    {
        for (var index = 0; index < value.Length - 2; index++)
        {
            if (value[index] != '%' || value[index + 1] != '{') continue;
            var first = value[index + 2];
            if (char.IsLetter(first) || first == '_') return true;
        }

        return false;
    }

    private static bool HasSingleBracedPlaceholder(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '{') continue;
            if (index + 1 < value.Length && value[index + 1] == '{')
            {
                var close = value.IndexOf("}}", index + 2, StringComparison.Ordinal);
                if (close < 0) return false;
                index = close + 1;
                continue;
            }

            var end = value.IndexOf('}', index + 1);
            if (end < 0) return false;
            var content = value[(index + 1)..end].Trim();
            if (content.Length > 0 &&
                (char.IsDigit(content[0]) ||
                 char.IsLetter(content[0]) ||
                 content[0] is '_' or '.' ||
                 (content[0] == '/' && content.Length > 1 && char.IsLetter(content[1]))))
            {
                return true;
            }

            index = end;
        }

        return false;
    }

    private static bool LooksLikePrintf(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%') continue;
            var cursor = index + 1;
            if (cursor >= value.Length) continue;
            if (value[cursor] == '%') return true;

            if (value[cursor] == '(')
            {
                var close = value.IndexOf(')', cursor + 1);
                if (close < 0) continue;
                cursor = close + 1;
            }
            else
            {
                while (cursor < value.Length && char.IsDigit(value[cursor])) cursor++;
                if (cursor < value.Length && value[cursor] == '$') cursor++;
            }

            while (cursor < value.Length && "-+ 0#'I".Contains(value[cursor]))
                cursor++;
            while (cursor < value.Length && char.IsDigit(value[cursor])) cursor++;
            if (cursor < value.Length && value[cursor] == '.')
            {
                cursor++;
                while (cursor < value.Length && char.IsDigit(value[cursor])) cursor++;
            }

            while (cursor < value.Length && "hlLzjtq".Contains(value[cursor]))
                cursor++;

            if (cursor < value.Length && "diouxXfFeEgGaAcspn@bB".Contains(value[cursor]))
                return true;
        }

        return false;
    }

    private static PlaceholderMessage ParseDotNet(string value)
    {
        var tokens = new List<PlaceholderToken>();
        var issues = new List<PlaceholderIssue>();

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '{')
            {
                if (index + 1 < value.Length && value[index + 1] == '{')
                {
                    index++;
                    continue;
                }

                var close = value.IndexOf('}', index + 1);
                if (close < 0)
                {
                    issues.Add(new PlaceholderIssue(
                        "Unclosed placeholder; add a closing '}'.",
                        index,
                        value.Length - index));
                    break;
                }

                var content = value[(index + 1)..close];
                var name = ParseDotNetName(content, out var format, out var error);
                if (error is not null)
                {
                    issues.Add(new PlaceholderIssue(error, index, close - index + 1));
                }
                else
                {
                    tokens.Add(new PlaceholderToken(
                        name!,
                        value[index..(close + 1)],
                        PlaceholderTokenKind.Braced,
                        index,
                        close - index + 1,
                        format));
                }

                index = close;
            }
            else if (value[index] == '}')
            {
                if (index + 1 < value.Length && value[index + 1] == '}')
                {
                    index++;
                    continue;
                }

                issues.Add(new PlaceholderIssue(
                    "Unescaped closing brace; use '}}' for a literal brace.",
                    index,
                    1));
            }
        }

        return new PlaceholderMessage(PlaceholderDialect.DotNet, tokens, issues);
    }

    private static string? ParseDotNetName(
        string content,
        out string? format,
        out string? error)
    {
        format = null;
        error = null;

        var value = content.Trim();
        if (value.Length == 0)
        {
            error = "Empty placeholder.";
            return null;
        }

        var colon = value.IndexOf(':');
        var beforeFormat = colon >= 0 ? value[..colon] : value;
        if (colon >= 0)
        {
            var formatText = value[(colon + 1)..].Trim();
            if (formatText.Length > 0) format = formatText;
        }

        var bang = beforeFormat.IndexOf('!');
        if (bang >= 0)
        {
            var conversion = beforeFormat[(bang + 1)..].Trim();
            if (conversion.Length > 0) format = "!" + conversion;
            beforeFormat = beforeFormat[..bang];
        }

        var comma = beforeFormat.IndexOf(',');
        var name = (comma >= 0 ? beforeFormat[..comma] : beforeFormat).Trim();
        if (name.Length == 0)
        {
            error = "Placeholder is missing an argument name.";
            return null;
        }

        if (!IsArgumentName(name))
        {
            error = $"Placeholder argument \"{name}\" is not a valid name.";
            return null;
        }

        return name;
    }

    private static bool IsArgumentName(string name)
    {
        // Closing brace tags such as Nextcloud's {/s} are common in locale
        // values. Treat them as named placeholders so they are compared and
        // masked instead of reported as malformed .NET arguments.
        if (name[0] == '/')
            return name.Length > 1 && IsArgumentName(name[1..]);

        if (char.IsDigit(name[0]))
            return name.All(char.IsDigit);

        if (!char.IsLetter(name[0]) && name[0] != '_') return false;
        return name.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '_' or '.' or '-');
    }

    private static PlaceholderMessage ParseDoubleBraced(
        string value,
        PlaceholderDialect dialect)
    {
        var tokens = new List<PlaceholderToken>();
        var issues = new List<PlaceholderIssue>();

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '{' ||
                index + 1 >= value.Length ||
                value[index + 1] != '{')
            {
                continue;
            }

            var triple = index + 2 < value.Length && value[index + 2] == '{';
            var openLength = triple ? 3 : 2;
            var closing = triple ? "}}}" : "}}";
            var close = value.IndexOf(closing, index + openLength, StringComparison.Ordinal);
            if (close < 0)
            {
                issues.Add(new PlaceholderIssue(
                    triple
                        ? "Unclosed triple-brace placeholder; add '}}}'."
                        : "Unclosed double-brace placeholder; add '}}'.",
                    index,
                    value.Length - index));
                break;
            }

            var content = value[(index + openLength)..close].Trim();
            if (content.Length == 0)
            {
                issues.Add(new PlaceholderIssue(
                    "Empty placeholder.",
                    index,
                    close + closing.Length - index));
            }
            else
            {
                tokens.Add(new PlaceholderToken(
                    content,
                    value[index..(close + closing.Length)],
                    PlaceholderTokenKind.DoubleBraced,
                    index,
                    close + closing.Length - index,
                    triple ? "triple" : null));
            }

            index = close + closing.Length - 1;
        }

        return new PlaceholderMessage(dialect, tokens, issues);
    }

    private static PlaceholderMessage ParseRails(string value)
    {
        var tokens = new List<PlaceholderToken>();
        var issues = new List<PlaceholderIssue>();

        for (var index = 0; index < value.Length - 1; index++)
        {
            if (value[index] != '%' || value[index + 1] != '{') continue;
            var close = value.IndexOf('}', index + 2);
            if (close < 0)
            {
                issues.Add(new PlaceholderIssue(
                    "Unclosed Rails placeholder; add '}'.",
                    index,
                    value.Length - index));
                break;
            }

            var name = value[(index + 2)..close].Trim();
            if (name.Length == 0)
            {
                issues.Add(new PlaceholderIssue(
                    "Empty Rails placeholder.",
                    index,
                    close - index + 1));
            }
            else
            {
                tokens.Add(new PlaceholderToken(
                    name,
                    value[index..(close + 1)],
                    PlaceholderTokenKind.Rails,
                    index,
                    close - index + 1));
            }

            index = close;
        }

        return new PlaceholderMessage(PlaceholderDialect.Rails, tokens, issues);
    }

    private static PlaceholderMessage ParseJavaScript(string value)
    {
        var tokens = new List<PlaceholderToken>();
        var issues = new List<PlaceholderIssue>();

        for (var index = 0; index < value.Length - 1; index++)
        {
            if (value[index] != '$' || value[index + 1] != '{') continue;
            var close = value.IndexOf('}', index + 2);
            if (close < 0)
            {
                issues.Add(new PlaceholderIssue(
                    "Unclosed JavaScript placeholder; add '}'.",
                    index,
                    value.Length - index));
                break;
            }

            var name = value[(index + 2)..close].Trim();
            if (name.Length == 0)
            {
                issues.Add(new PlaceholderIssue(
                    "Empty JavaScript placeholder.",
                    index,
                    close - index + 1));
            }
            else
            {
                tokens.Add(new PlaceholderToken(
                    name,
                    value[index..(close + 1)],
                    PlaceholderTokenKind.JavaScript,
                    index,
                    close - index + 1));
            }

            index = close;
        }

        return new PlaceholderMessage(PlaceholderDialect.JavaScript, tokens, issues);
    }

    private static PlaceholderMessage ParsePrintf(string value)
    {
        var tokens = new List<PlaceholderToken>();
        var issues = new List<PlaceholderIssue>();
        var sequential = 0;

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%') continue;
            if (index + 1 >= value.Length) continue;
            if (value[index + 1] == '%')
            {
                index++;
                continue;
            }

            var start = index;
            var cursor = index + 1;
            string? name = null;
            var explicitPosition = false;

            if (value[cursor] == '(')
            {
                var close = value.IndexOf(')', cursor + 1);
                if (close < 0)
                {
                    issues.Add(new PlaceholderIssue(
                        "Unclosed named printf placeholder; add ')'.",
                        start,
                        value.Length - start));
                    break;
                }

                name = value[(cursor + 1)..close].Trim();
                cursor = close + 1;
            }
            else
            {
                var digitsStart = cursor;
                while (cursor < value.Length && char.IsDigit(value[cursor])) cursor++;
                if (cursor > digitsStart && cursor < value.Length && value[cursor] == '$')
                {
                    name = value[digitsStart..cursor];
                    explicitPosition = true;
                    cursor++;
                }
            }

            while (cursor < value.Length && "-+ 0#'I".Contains(value[cursor]))
                cursor++;
            while (cursor < value.Length && char.IsDigit(value[cursor])) cursor++;
            if (cursor < value.Length && value[cursor] == '.')
            {
                cursor++;
                while (cursor < value.Length && char.IsDigit(value[cursor])) cursor++;
            }

            while (cursor < value.Length && "hlLzjtq".Contains(value[cursor]))
                cursor++;

            if (cursor >= value.Length ||
                !"diouxXfFeEgGaAcspn@bB".Contains(value[cursor]))
            {
                if (explicitPosition || name is not null)
                {
                    issues.Add(new PlaceholderIssue(
                        "Malformed printf placeholder; expected a conversion character such as 's' or 'd'.",
                        start,
                        Math.Max(1, cursor - start)));
                }

                continue;
            }

            var conversion = value[cursor];
            if (name is null)
            {
                sequential++;
                name = sequential.ToString(CultureInfo.InvariantCulture);
            }
            else if (name.Length == 0)
            {
                issues.Add(new PlaceholderIssue(
                    "Empty named printf placeholder.",
                    start,
                    cursor - start + 1));
                continue;
            }

            tokens.Add(new PlaceholderToken(
                name,
                value[start..(cursor + 1)],
                PlaceholderTokenKind.Printf,
                start,
                cursor - start + 1,
                conversion.ToString()));
            index = cursor;
        }

        return new PlaceholderMessage(PlaceholderDialect.Printf, tokens, issues);
    }

    private static PlaceholderMessage ParseIcu(string value) =>
        new IcuParser(value).Parse();

    private sealed class IcuParser
    {
        private readonly string _value;
        private readonly List<PlaceholderIssue> _issues = new();
        private int _index;

        public IcuParser(string value) => _value = value;

        public PlaceholderMessage Parse()
        {
            var tokens = ParseMessage(stopAtBrace: false, inPlural: false);
            return new PlaceholderMessage(PlaceholderDialect.Icu, tokens, _issues);
        }

        private List<PlaceholderToken> ParseMessage(bool stopAtBrace, bool inPlural)
        {
            var tokens = new List<PlaceholderToken>();

            while (_index < _value.Length)
            {
                var character = _value[_index];
                if (stopAtBrace && character == '}')
                {
                    _index++;
                    return tokens;
                }

                if (character == '\'')
                {
                    SkipQuoted();
                    continue;
                }

                if (character == '{')
                {
                    tokens.Add(ParseArgument());
                    continue;
                }

                if (character == '#' && inPlural)
                {
                    tokens.Add(new PlaceholderToken(
                        "#",
                        "#",
                        PlaceholderTokenKind.IcuPound,
                        _index,
                        1));
                    _index++;
                    continue;
                }

                if (character == '}' && !stopAtBrace)
                {
                    _issues.Add(new PlaceholderIssue(
                        "Unescaped closing brace in ICU message; use an apostrophe escape.",
                        _index,
                        1));
                }

                _index++;
            }

            if (stopAtBrace)
            {
                _issues.Add(new PlaceholderIssue(
                    "Unclosed ICU branch; add a closing '}'.",
                    Math.Max(0, _value.Length - 1),
                    1));
            }

            return tokens;
        }

        private void SkipQuoted()
        {
            if (_index + 1 < _value.Length && _value[_index + 1] == '\'')
            {
                _index += 2;
                return;
            }

            var next = _index + 1 < _value.Length ? _value[_index + 1] : '\0';
            if (next is not ('{' or '}' or '#'))
            {
                _index++;
                return;
            }

            var close = _value.IndexOf('\'', _index + 1);
            _index = close < 0 ? _value.Length : close + 1;
        }

        private PlaceholderToken ParseArgument()
        {
            var start = _index;
            _index++;

            var nameStart = _index;
            while (_index < _value.Length && IsNameCharacter(_value[_index])) _index++;
            var name = _value[nameStart.._index].Trim();
            SkipWhitespace();

            if (_index >= _value.Length)
            {
                _issues.Add(new PlaceholderIssue(
                    "Unclosed ICU argument; add a closing '}'.",
                    start,
                    _value.Length - start));
                return SimpleToken(name, start);
            }

            if (_value[_index] == '}')
            {
                _index++;
                if (name.Length == 0)
                {
                    _issues.Add(new PlaceholderIssue(
                        "Empty ICU argument.",
                        start,
                        _index - start));
                }

                return SimpleToken(name, start);
            }

            if (_value[_index] != ',')
            {
                _issues.Add(new PlaceholderIssue(
                    "Expected ',' or '}' after the ICU argument name.",
                    _index,
                    1));
                SkipToArgumentEnd();
                return SimpleToken(name, start);
            }

            _index++;
            SkipWhitespace();

            var typeStart = _index;
            while (_index < _value.Length && char.IsLetter(_value[_index])) _index++;
            var type = _value[typeStart.._index].ToLowerInvariant();
            SkipWhitespace();

            if (type is "plural" or "select" or "selectordinal")
            {
                return ParseChoiceArgument(start, name, type);
            }

            var styleStart = _index;
            if (_index < _value.Length && _value[_index] == ',')
            {
                _index++;
                SkipWhitespace();
                styleStart = _index;
            }

            var depth = 0;
            while (_index < _value.Length)
            {
                if (_value[_index] == '{')
                {
                    depth++;
                }
                else if (_value[_index] == '}')
                {
                    if (depth == 0) break;
                    depth--;
                }

                _index++;
            }

            if (_index >= _value.Length)
            {
                _issues.Add(new PlaceholderIssue(
                    "Unclosed ICU argument; add a closing '}'.",
                    start,
                    _value.Length - start));
            }
            else
            {
                _index++;
            }

            var styleEnd = Math.Min(_index - 1, _value.Length);
            var style = styleStart < styleEnd ? _value[styleStart..styleEnd].Trim() : null;
            if (style is { Length: 0 }) style = null;
            if (type.Length == 0) type = null;

            var rawLength = Math.Min(_index, _value.Length) - start;
            return new PlaceholderToken(
                name,
                _value.Substring(start, rawLength),
                PlaceholderTokenKind.IcuArgument,
                start,
                rawLength,
                style,
                PlaceholderArgumentKind.Simple,
                type);
        }

        private PlaceholderToken ParseChoiceArgument(
            int start,
            string name,
            string type)
        {
            if (_index >= _value.Length || _value[_index] != ',')
            {
                _issues.Add(new PlaceholderIssue(
                    $"Expected ',' after ICU {type} argument.",
                    _index,
                    1));
                SkipToArgumentEnd();
                return SimpleToken(name, start);
            }

            _index++;
            SkipWhitespace();

            if (type != "select" &&
                _value.AsSpan(_index).StartsWith("offset:", StringComparison.Ordinal))
            {
                _index += "offset:".Length;
                while (_index < _value.Length && char.IsDigit(_value[_index])) _index++;
                SkipWhitespace();
            }

            var branches = new List<PlaceholderBranch>();
            var closed = false;
            while (_index < _value.Length)
            {
                SkipWhitespace();
                if (_index >= _value.Length) break;
                if (_value[_index] == '}')
                {
                    _index++;
                    closed = true;
                    break;
                }

                var selectorStart = _index;
                while (_index < _value.Length &&
                       _value[_index] != '{' &&
                       !char.IsWhiteSpace(_value[_index]) &&
                       _value[_index] != '}')
                {
                    _index++;
                }

                var selector = _value[selectorStart.._index].Trim();
                SkipWhitespace();
                if (_index >= _value.Length || _value[_index] != '{')
                {
                    _issues.Add(new PlaceholderIssue(
                        "Expected '{' after an ICU branch selector.",
                        _index,
                        Math.Max(1, _value.Length - _index)));
                    SkipToArgumentEnd();
                    break;
                }

                var branchStart = _index;
                _index++;
                var branchTokens = ParseMessage(stopAtBrace: true, inPlural: type != "select");
                var branchLength = Math.Min(_index, _value.Length) - branchStart;
                if (selector.Length == 0)
                {
                    _issues.Add(new PlaceholderIssue(
                        "ICU branch is missing a selector.",
                        branchStart,
                        branchLength));
                }

                branches.Add(new PlaceholderBranch(
                    selector,
                    branchStart,
                    branchLength,
                    branchTokens));
            }

            if (!closed)
            {
                _issues.Add(new PlaceholderIssue(
                    $"Unclosed ICU {type} argument; add a closing '}}'.",
                    start,
                    Math.Max(1, Math.Min(_index, _value.Length) - start)));
            }

            if (type != "select" && !branches.Any(branch => branch.Selector == "other"))
            {
                _issues.Add(new PlaceholderIssue(
                    $"ICU {type} argument is missing the required 'other' branch.",
                    start,
                    Math.Max(1, Math.Min(_index, _value.Length) - start)));
            }

            if (branches.Count == 0)
            {
                _issues.Add(new PlaceholderIssue(
                    $"ICU {type} argument has no branches.",
                    start,
                    Math.Max(1, Math.Min(_index, _value.Length) - start)));
            }

            var kind = type switch
            {
                "plural" => PlaceholderArgumentKind.Plural,
                "selectordinal" => PlaceholderArgumentKind.SelectOrdinal,
                _ => PlaceholderArgumentKind.Select,
            };

            var rawLength = Math.Min(_index, _value.Length) - start;
            return new PlaceholderToken(
                name,
                _value.Substring(start, rawLength),
                PlaceholderTokenKind.IcuArgument,
                start,
                rawLength,
                ArgumentKind: kind,
                IcuType: type,
                Branches: branches);
        }

        private PlaceholderToken SimpleToken(string name, int start)
        {
            var rawLength = Math.Max(0, Math.Min(_index, _value.Length) - start);
            return new PlaceholderToken(
                name,
                _value.Substring(start, rawLength),
                PlaceholderTokenKind.IcuArgument,
                start,
                rawLength);
        }

        private void SkipToArgumentEnd()
        {
            var depth = 0;
            while (_index < _value.Length)
            {
                if (_value[_index] == '{')
                {
                    depth++;
                }
                else if (_value[_index] == '}')
                {
                    if (depth == 0)
                    {
                        _index++;
                        return;
                    }

                    depth--;
                }

                _index++;
            }
        }

        private void SkipWhitespace()
        {
            while (_index < _value.Length && char.IsWhiteSpace(_value[_index])) _index++;
        }

        private static bool IsNameCharacter(char character) =>
            char.IsLetterOrDigit(character) ||
            character is '_' or '.' or '-';
    }
}
