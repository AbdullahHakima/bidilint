namespace BidiLint;

// Discovers locale files and turns them into LocaleDocuments through format
// adapters. Parsing never silently drops a candidate: invalid content,
// duplicate keys, and collisions become diagnostics.
public static class LocaleLoader
{
    private static readonly ILocaleAdapter[] Adapters =
    [
        new AndroidAdapter(),
        new AppleStringsAdapter(),
        new ArbAdapter(),
        new PoAdapter(),
        new JsonCultureDirectoryAdapter(),
        new JsonFileAdapter(),
        new ResxAdapter(),
    ];

    public static LoadResult Load(
        IEnumerable<string> paths,
        string baseCulture = "en",
        string? workingDirectory = null)
    {
        var cwd = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());
        var normalizedBase = CultureTag.TryParse(baseCulture, out var baseTag)
            ? baseTag!.Name
            : baseCulture;

        var discovery = FileDiscovery.Discover(paths, cwd);
        var result = new LoadResult { WorkingDirectory = cwd };
        result.Diagnostics.AddRange(discovery.Diagnostics);
        result.CandidateFiles.AddRange(discovery.Files.Select(file => file.FullPath));

        foreach (var file in discovery.Files)
        {
            ILocaleAdapter? adapter = null;
            LocaleIdentity? identity = null;

            foreach (var candidate in Adapters)
            {
                identity = candidate.Identify(file.FullPath, cwd, normalizedBase);
                if (identity is null) continue;
                adapter = candidate;
                break;
            }

            if (identity is null || adapter is null)
            {
                if (file.IsExplicit) AddUnidentified(result, file, normalizedBase);
                continue;
            }

            LocaleDocument? document;
            try
            {
                document = adapter.Load(identity, result.Diagnostics);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.UnreadablePath,
                    DiagnosticSeverity.Error,
                    $"Cannot read file: {ex.Message}",
                    file.FullPath,
                    Culture: identity.Culture));
                continue;
            }

            if (document is not null) result.Documents.Add(document);
        }

        result.Documents.Sort((left, right) =>
            string.Compare(left.FilePath, right.FilePath, StringComparison.Ordinal));
        return result;
    }

    private static void AddUnidentified(LoadResult result, DiscoveredFile file, string baseCulture)
    {
        if (Path.GetExtension(file.FullPath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            result.Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.CultureNotFound,
                DiagnosticSeverity.Error,
                $"Cannot read a culture from the file name \"{Path.GetFileName(file.FullPath)}\". Use a name such as en.json, ar-EG.json, or place it under locales/<culture>/.",
                file.FullPath));
            return;
        }

        result.Diagnostics.Add(new Diagnostic(
            DiagnosticCodes.UnsupportedFile,
            DiagnosticSeverity.Error,
            "No supported locale layout matched this file. Expected JSON, .resx, Android res/values XML, Apple .strings, gettext .po/.pot, or Flutter .arb.",
            file.FullPath));
    }
}
