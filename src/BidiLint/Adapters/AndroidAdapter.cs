using System.Xml;
using System.Xml.Linq;

namespace BidiLint;

// Android resources: res/values/strings.xml is the base; res/values-ar,
// res/values-b+ar+EG, and res/values-ar-rEG are localized. Non-locale
// qualifiers (night, sw600dp, land) are intentionally not treated as locales.
internal sealed class AndroidAdapter : ILocaleAdapter
{
    public LocaleIdentity? Identify(
        string path,
        string workingDirectory,
        string baseCulture,
        IReadOnlySet<string>? layoutNames = null)
    {
        if (!Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            return null;

        var segments = LocalePath.Segments(workingDirectory, path);
        for (var i = segments.Length - 2; i >= 1; i--)
        {
            var values = segments[i];
            if (!values.StartsWith("values", StringComparison.OrdinalIgnoreCase)) continue;
            if (!segments[i - 1].Equals("res", StringComparison.OrdinalIgnoreCase)) continue;

            var culture = ParseQualifier(values, baseCulture);
            if (culture is null) return null;

            return new LocaleIdentity(
                path,
                culture,
                LocalePath.ResourceSet(segments, i),
                LocaleFormat.Android);
        }

        return null;
    }

    public LocaleDocument? Load(LocaleIdentity identity, List<Diagnostic> diagnostics)
    {
        var loaded = XmlLocaleFile.Read(identity.FilePath, identity.Culture, diagnostics);
        if (loaded is null) return null;

        var (source, xml) = loaded.Value;
        var document = new LocaleDocument
        {
            Culture = identity.Culture,
            FilePath = identity.FilePath,
            Format = LocaleFormat.Android,
            ResourceSet = identity.ResourceSet,
            Source = source,
        };

        foreach (var element in xml.Root?.Elements() ?? Enumerable.Empty<XElement>())
        {
            switch (element.Name.LocalName)
            {
                case "string":
                    AddString(document, element, source, diagnostics);
                    break;
                case "plurals":
                    AddPlurals(document, element, source, diagnostics);
                    break;
                case "string-array":
                    AddStringArray(document, element, source, diagnostics);
                    break;
            }
        }

        return document;
    }

    private static void AddString(
        LocaleDocument document,
        XElement element,
        SourceText source,
        List<Diagnostic> diagnostics)
    {
        if (IsTranslatableFalse(element)) return;
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name)) return;

        AddEntry(
            document,
            name,
            element.Value,
            SpanOf(element, source),
            SpanOf(element, source),
            diagnostics);
    }

    private static void AddPlurals(
        LocaleDocument document,
        XElement element,
        SourceText source,
        List<Diagnostic> diagnostics)
    {
        if (IsTranslatableFalse(element)) return;
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name)) return;

        foreach (var item in element.Elements("item"))
        {
            var quantity = item.Attribute("quantity")?.Value;
            if (string.IsNullOrEmpty(quantity)) continue;
            AddEntry(
                document,
                $"{name}[{quantity}]",
                item.Value,
                SpanOf(item, source),
                SpanOf(element, source),
                diagnostics);
        }
    }

    private static void AddStringArray(
        LocaleDocument document,
        XElement element,
        SourceText source,
        List<Diagnostic> diagnostics)
    {
        if (IsTranslatableFalse(element)) return;
        var name = element.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(name)) return;

        var index = 0;
        foreach (var item in element.Elements("item"))
        {
            AddEntry(
                document,
                $"{name}[{index}]",
                item.Value,
                SpanOf(item, source),
                SpanOf(element, source),
                diagnostics);
            index++;
        }
    }

    private static void AddEntry(
        LocaleDocument document,
        string key,
        string value,
        SourceSpan? span,
        SourceSpan? keySpan,
        List<Diagnostic> diagnostics)
    {
        if (document.Entries.TryGetValue(key, out var existing))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.DuplicateKey,
                DiagnosticSeverity.Error,
                $"Key \"{key}\" appears more than once. The first value is used.",
                document.FilePath,
                span,
                document.Culture,
                key,
                new[]
                {
                    new RelatedDiagnostic(
                        "First occurrence.",
                        document.FilePath,
                        existing.KeySpan ?? existing.Span,
                        key),
                }));
            return;
        }

        document.Entries.Add(key, new LocaleEntry
        {
            Key = key,
            Value = value,
            Document = document,
            Span = span,
            KeySpan = keySpan,
        });
    }

    private static string? ParseQualifier(string valuesDirectory, string baseCulture)
    {
        if (valuesDirectory.Equals("values", StringComparison.OrdinalIgnoreCase))
            return baseCulture;

        if (!valuesDirectory.StartsWith("values-", StringComparison.OrdinalIgnoreCase))
            return null;

        var qualifier = valuesDirectory["values-".Length..];
        if (qualifier.StartsWith("b+", StringComparison.OrdinalIgnoreCase))
        {
            var bcp47 = qualifier[2..].Replace('+', '-');
            return CultureTag.TryParse(bcp47, out var tag) ? tag!.Name : null;
        }

        var parts = qualifier.Split('-');
        if (parts.Length == 0) return null;

        var cultureParts = new List<string> { parts[0] };
        var index = 1;
        if (index < parts.Length &&
            parts[index].Length == 3 &&
            (parts[index][0] == 'r' || parts[index][0] == 'R'))
        {
            cultureParts.Add(parts[index][1..]);
            index++;
        }

        if (index != parts.Length) return null;

        return CultureTag.TryParse(string.Join('-', cultureParts), out var parsed)
            ? parsed!.Name
            : null;
    }

    private static bool IsTranslatableFalse(XElement element) =>
        element.Attribute("translatable")?.Value.Equals("false", StringComparison.OrdinalIgnoreCase) == true;

    private static SourceSpan? SpanOf(XElement element, SourceText source)
    {
        if (element is not IXmlLineInfo info || !info.HasLineInfo()) return null;
        return SourceSpan.At(source.PositionAt(
            Math.Max(1, info.LineNumber),
            Math.Max(1, info.LinePosition)));
    }
}
