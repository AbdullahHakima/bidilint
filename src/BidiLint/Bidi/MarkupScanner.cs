using System.Text.RegularExpressions;

namespace BidiLint;

internal enum IsolationDirection
{
    Automatic,
    LeftToRight,
    RightToLeft,
}

internal sealed record IsolationWrapper(
    int Start,
    int End,
    string Tag,
    IsolationDirection Direction);

internal sealed record MaskedRange(int Start, int Length);

internal sealed class MarkupScanResult
{
    public List<IsolationWrapper> Wrappers { get; } = new();
    public List<MaskedRange> MaskedRanges { get; } = new();
}

// A deliberately small HTML/XML fragment scanner. It only needs to find
// elements that establish or reset direction (bdi, code, dir, configured
// tags) and to keep tag text out of the Latin-run scanner. It does not build a
// DOM or validate markup.
internal static class MarkupScanner
{
    private static readonly Regex Entity = new(
        """&(?:#\d{1,7}|#x[0-9A-Fa-f]{1,6}|[A-Za-z][A-Za-z0-9]{1,31});""",
        RegexOptions.Compiled | RegexOptions.NonBacktracking);

    public static MarkupScanResult Scan(string value, IReadOnlySet<string> configuredWrappers)
    {
        var result = new MarkupScanResult();
        var stack = new List<OpenElement>();
        var index = 0;

        while (index < value.Length)
        {
            if (value[index] == '<')
            {
                if (TryScanSpecial(value, index, out var specialEnd))
                {
                    result.MaskedRanges.Add(new MaskedRange(index, specialEnd - index));
                    index = specialEnd;
                    continue;
                }

                if (TryScanTag(value, index, out var tag))
                {
                    result.MaskedRanges.Add(new MaskedRange(tag.Start, tag.End - tag.Start));
                    if (tag.IsEnd)
                    {
                        CloseElement(stack, tag, result);
                    }
                    else if (!tag.SelfClosing)
                    {
                        var isWrapper = IsWrapper(tag, configuredWrappers);
                        stack.Add(new OpenElement(
                            tag.Name,
                            tag.ContentStart,
                            isWrapper,
                            tag.Direction));
                    }

                    index = tag.End;
                    continue;
                }
            }

            if (value[index] == '&')
            {
                var match = Entity.Match(value, index);
                if (match.Success)
                {
                    result.MaskedRanges.Add(new MaskedRange(match.Index, match.Length));
                    index += match.Length;
                    continue;
                }
            }

            index++;
        }

        return result;
    }

    private static bool TryScanSpecial(string value, int index, out int end)
    {
        if (value.AsSpan(index).StartsWith("<!--", StringComparison.Ordinal))
        {
            var close = value.IndexOf("-->", index + 4, StringComparison.Ordinal);
            end = close < 0 ? value.Length : close + 3;
            return true;
        }

        if (value.AsSpan(index).StartsWith("<![CDATA[", StringComparison.Ordinal))
        {
            var close = value.IndexOf("]]>", index + 9, StringComparison.Ordinal);
            end = close < 0 ? value.Length : close + 3;
            return true;
        }

        if (value.AsSpan(index).StartsWith("<!", StringComparison.Ordinal) ||
            value.AsSpan(index).StartsWith("<?", StringComparison.Ordinal))
        {
            var close = value.IndexOf('>', index + 2);
            end = close < 0 ? value.Length : close + 1;
            return true;
        }

        end = index;
        return false;
    }

    private static bool TryScanTag(string value, int index, out TagToken tag)
    {
        tag = default;
        var cursor = index + 1;
        var isEnd = false;
        if (cursor < value.Length && value[cursor] == '/')
        {
            isEnd = true;
            cursor++;
        }

        var nameStart = cursor;
        if (cursor >= value.Length || !char.IsLetter(value[cursor])) return false;
        cursor++;
        while (cursor < value.Length &&
               (char.IsLetterOrDigit(value[cursor]) || value[cursor] is '-' or '_' or '.' or ':'))
        {
            cursor++;
        }

        var name = value[nameStart..cursor].ToLowerInvariant();
        var attributesStart = cursor;
        var quote = '\0';
        while (cursor < value.Length)
        {
            var current = value[cursor];
            if (quote != '\0')
            {
                if (current == quote) quote = '\0';
                cursor++;
                continue;
            }

            if (current is '"' or '\'')
            {
                quote = current;
                cursor++;
                continue;
            }

            if (current == '>')
            {
                var selfClosing = cursor > attributesStart + 1 && value[cursor - 1] == '/';
                tag = new TagToken(
                    index,
                    cursor + 1,
                    isEnd,
                    selfClosing,
                    name,
                    attributesStart,
                    cursor,
                    ResolveDirection(name, ParseDirection(value, attributesStart, cursor)));
                return true;
            }

            cursor++;
        }

        return false;
    }

    private static void CloseElement(
        List<OpenElement> stack,
        TagToken tag,
        MarkupScanResult result)
    {
        var match = -1;
        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (string.Equals(stack[i].Name, tag.Name, StringComparison.OrdinalIgnoreCase))
            {
                match = i;
                break;
            }
        }

        if (match < 0) return;

        var contentEnd = tag.Start;
        for (var i = stack.Count - 1; i >= match; i--)
        {
            var element = stack[i];
            if (element.IsWrapper && contentEnd >= element.ContentStart)
            {
                result.Wrappers.Add(new IsolationWrapper(
                    element.ContentStart,
                    contentEnd,
                    element.Name,
                    element.Direction));
            }
        }

        stack.RemoveRange(match, stack.Count - match);
    }

    private static bool IsWrapper(TagToken tag, IReadOnlySet<string> configuredWrappers) =>
        tag.Name is "bdi" or "code" ||
        configuredWrappers.Contains(tag.Name) ||
        tag.Direction is IsolationDirection.LeftToRight or IsolationDirection.RightToLeft;

    private static IsolationDirection ResolveDirection(
        string name,
        IsolationDirection? explicitDirection)
    {
        if (explicitDirection.HasValue) return explicitDirection.Value;

        // <code> is expected to hold identifiers even when the page is RTL.
        // <bdi> and configured wrappers follow their content when dir is absent.
        return name == "code"
            ? IsolationDirection.LeftToRight
            : IsolationDirection.Automatic;
    }

    private static IsolationDirection? ParseDirection(string value, int start, int end)
    {
        var index = start;
        while (index < end)
        {
            while (index < end && char.IsWhiteSpace(value[index])) index++;
            if (index >= end) break;

            if (value[index] is '/' or '>')
            {
                index++;
                continue;
            }

            var nameStart = index;
            while (index < end &&
                   !char.IsWhiteSpace(value[index]) &&
                   value[index] is not ('=' or '/' or '>'))
            {
                index++;
            }

            var name = value[nameStart..index];
            while (index < end && char.IsWhiteSpace(value[index])) index++;

            string? attributeValue = null;
            if (index < end && value[index] == '=')
            {
                index++;
                while (index < end && char.IsWhiteSpace(value[index])) index++;

                if (index < end && value[index] is '"' or '\'')
                {
                    var quote = value[index++];
                    var valueStart = index;
                    while (index < end && value[index] != quote) index++;
                    attributeValue = value[valueStart..index];
                    if (index < end) index++;
                }
                else
                {
                    var valueStart = index;
                    while (index < end &&
                           !char.IsWhiteSpace(value[index]) &&
                           value[index] != '>')
                    {
                        index++;
                    }

                    attributeValue = value[valueStart..index];
                }
            }

            if (!string.Equals(name, "dir", StringComparison.OrdinalIgnoreCase))
                continue;

            if (attributeValue is null) return IsolationDirection.Automatic;
            return attributeValue.Trim().ToLowerInvariant() switch
            {
                "ltr" => IsolationDirection.LeftToRight,
                "rtl" => IsolationDirection.RightToLeft,
                _ => IsolationDirection.Automatic,
            };
        }

        return null;
    }

    private readonly record struct TagToken(
        int Start,
        int End,
        bool IsEnd,
        bool SelfClosing,
        string Name,
        int AttributesStart,
        int AttributesEnd,
        IsolationDirection Direction)
    {
        public int ContentStart => End;
    }

    private sealed record OpenElement(
        string Name,
        int ContentStart,
        bool IsWrapper,
        IsolationDirection Direction);
}
