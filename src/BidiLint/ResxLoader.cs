using System.Xml;
using System.Xml.Linq;

namespace BidiLint;

internal sealed record ResxIdentity(string Culture, string ResourceSet);

// Loads .resx resources without merging unrelated resource sets. The file name
// identifies the culture and the set: Strings.ar.resx is the Arabic member of
// the Strings set, while App.Resources.resx is a neutral set with a dotted name.
internal static class ResxLoader
{
    public static ResxIdentity Describe(string path, string baseCulture, string workingDirectory)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var culture = baseCulture;
        var baseName = name;

        var dot = name.LastIndexOf('.');
        if (dot > 0)
        {
            var suffix = name[(dot + 1)..];
            if (CultureTag.TryParse(suffix, out var tag))
            {
                culture = tag!.Name;
                baseName = name[..dot];
            }
        }

        var relative = Path.GetRelativePath(workingDirectory, path);
        var directory = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? "";
        var resourceSet = directory.Length == 0 ? baseName : $"{directory}/{baseName}";
        return new ResxIdentity(culture, resourceSet);
    }

    public static LocaleDocument? Load(
        string path,
        ResxIdentity identity,
        List<Diagnostic> diagnostics)
    {
        var loaded = XmlLocaleFile.Read(path, identity.Culture, diagnostics);
        if (loaded is null) return null;

        var (source, xml) = loaded.Value;

        var document = new LocaleDocument
        {
            Culture = identity.Culture,
            FilePath = path,
            Format = LocaleFormat.Resx,
            ResourceSet = identity.ResourceSet,
            Source = source,
        };

        foreach (var data in xml.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
        {
            var name = data.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            // Typed resources (images, file references, serialized objects) are
            // not localizable strings.
            if (data.Attribute("type") is not null) continue;

            var value = data.Element("value");
            if (value is null) continue;

            if (document.Entries.TryGetValue(name, out var existing))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.DuplicateKey,
                    DiagnosticSeverity.Error,
                    $"Key \"{name}\" appears more than once in this resource set. The first value is used.",
                    path,
                    SpanOf(data, source),
                    identity.Culture,
                    name,
                    new[]
                    {
                        new RelatedDiagnostic(
                            "First occurrence.",
                            path,
                            existing.KeySpan,
                            name),
                    }));
                continue;
            }

            document.Entries.Add(name, new LocaleEntry
            {
                Key = name,
                Value = value.Value,
                Document = document,
                Span = SpanOf(value, source),
                KeySpan = SpanOf(data, source),
            });
        }

        return document;
    }

    private static SourceSpan? SpanOf(XElement element, SourceText source)
    {
        if (element is not IXmlLineInfo info || !info.HasLineInfo()) return null;
        var line = Math.Max(1, info.LineNumber);
        var column = Math.Max(1, info.LinePosition);
        return SourceSpan.At(source.PositionAt(line, column));
    }
}
