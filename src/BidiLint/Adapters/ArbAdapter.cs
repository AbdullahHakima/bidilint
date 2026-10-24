using System.Text.Json;

namespace BidiLint;

// Flutter ARB. Convention is app_en.arb / app_ar.arb, but @@locale is
// authoritative when present. Metadata keys (@key, @@locale) are skipped and
// never linted as user-visible strings.
internal sealed class ArbAdapter : ILocaleAdapter
{
    public LocaleIdentity? Identify(
        string path,
        string workingDirectory,
        string baseCulture,
        IReadOnlySet<string>? layoutNames = null)
    {
        if (!Path.GetExtension(path).Equals(".arb", StringComparison.OrdinalIgnoreCase))
            return null;

        var stem = Path.GetFileNameWithoutExtension(path);
        var (resourceName, culture) = SplitName(stem);
        var relative = Path.GetRelativePath(workingDirectory, path).Replace('\\', '/');
        var directory = Path.GetDirectoryName(relative)?.Replace('\\', '/') ?? "";
        var resourceSet = directory.Length == 0
            ? resourceName
            : $"{directory}/{resourceName}";

        return new LocaleIdentity(path, culture ?? "", resourceSet, LocaleFormat.Arb);
    }

    public LocaleDocument? Load(LocaleIdentity identity, List<Diagnostic> diagnostics)
    {
        var source = JsonLocaleFile.Read(identity.FilePath, identity.Culture, diagnostics);
        if (source is null) return null;

        var culture = identity.Culture;
        try
        {
            using var json = JsonDocument.Parse(
                source.Text,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                    MaxDepth = 128,
                });

            if (json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("@@locale", out var locale) &&
                locale.ValueKind == JsonValueKind.String)
            {
                var value = locale.GetString();
                if (!CultureTag.TryParse(value, out var tag))
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticCodes.MalformedFile,
                        DiagnosticSeverity.Error,
                        $"ARB @@locale \"{value}\" is not a valid culture tag.",
                        identity.FilePath,
                        Culture: identity.Culture));
                    return null;
                }

                culture = tag!.Name;
            }
        }
        catch (JsonException ex)
        {
            var position = source.PositionAtLineByteOffset(
                (int)(ex.LineNumber ?? 0),
                (int)(ex.BytePositionInLine ?? 0));
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.MalformedFile,
                DiagnosticSeverity.Error,
                ex.Message,
                identity.FilePath,
                SourceSpan.At(position),
                identity.Culture));
            return null;
        }

        if (string.IsNullOrEmpty(culture))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.CultureNotFound,
                DiagnosticSeverity.Error,
                "Cannot determine the ARB culture. Add @@locale or name the file app_en.arb.",
                identity.FilePath));
            return null;
        }

        return JsonLocaleParser.Parse(
            identity.FilePath,
            culture,
            identity.ResourceSet,
            source,
            diagnostics,
            new JsonParseOptions(SkipMetadataKeys: true, Format: LocaleFormat.Arb));
    }

    private static (string ResourceName, string? Culture) SplitName(string stem)
    {
        for (var i = 0; i < stem.Length; i++)
        {
            if (stem[i] is not ('_' or '-')) continue;
            var suffix = stem[(i + 1)..];
            if (CultureTag.TryParse(suffix, out var tag))
                return (stem[..i], tag!.Name);
        }

        if (CultureTag.TryParse(stem, out var whole))
            return ("arb", whole!.Name);

        return (stem, null);
    }
}
