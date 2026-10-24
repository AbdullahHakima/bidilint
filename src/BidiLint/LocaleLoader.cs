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
        string? workingDirectory = null) =>
        Load(paths, baseCulture, workingDirectory, LintConfig.Default);

    internal static LoadResult Load(
        IEnumerable<string> paths,
        string baseCulture,
        string? workingDirectory,
        LintConfig config)
    {
        var cwd = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());
        var normalizedBase = CultureTag.TryParse(baseCulture, out var baseTag)
            ? baseTag!.Name
            : baseCulture;

        PathFilter? filter = null;
        if (config.ConfigDirectory is not null &&
            (config.Include.Count > 0 || config.Exclude.Count > 0))
        {
            filter = new PathFilter(config.ConfigDirectory, config.Include, config.Exclude);
        }

        var configuredFiles = config.Files;
        var discovery = FileDiscovery.Discover(
            paths,
            cwd,
            filter,
            configuredFiles.Select(file => file.FullPath));
        var result = new LoadResult { WorkingDirectory = cwd };
        result.Diagnostics.AddRange(discovery.Diagnostics);
        result.CandidateFiles.AddRange(discovery.Files.Select(file => file.FullPath));

        foreach (var file in discovery.Files)
        {
            var configured = configuredFiles.FirstOrDefault(configuredFile =>
                PathComparer.Equals(configuredFile.FullPath, file.FullPath));

            if (configured is not null)
            {
                var configuredIdentity = new LocaleIdentity(
                    configured.FullPath,
                    configured.Culture,
                    configured.ResourceSet,
                    FormatForExtension(configured.FullPath));
                var configuredDocument = LoadConfigured(configuredIdentity, result.Diagnostics);
                if (configuredDocument is not null) result.Documents.Add(configuredDocument);
                continue;
            }

            ILocaleAdapter? adapter = null;
            LocaleIdentity? identity = null;

            foreach (var candidate in Adapters)
            {
                identity = candidate.Identify(
                    file.FullPath,
                    cwd,
                    normalizedBase,
                    config.LayoutNames);
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

    private static LocaleDocument? LoadConfigured(
        LocaleIdentity identity,
        List<Diagnostic> diagnostics)
    {
        try
        {
            return Path.GetExtension(identity.FilePath).ToLowerInvariant() switch
            {
                ".json" => LoadJson(identity, diagnostics),
                ".arb" => new ArbAdapter().Load(identity, diagnostics),
                ".resx" => new ResxAdapter().Load(identity, diagnostics),
                ".xml" => new AndroidAdapter().Load(identity, diagnostics),
                ".strings" => new AppleStringsAdapter().Load(identity, diagnostics),
                ".po" or ".pot" => new PoAdapter().Load(identity, diagnostics),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.UnreadablePath,
                DiagnosticSeverity.Error,
                $"Cannot read file: {ex.Message}",
                identity.FilePath,
                Culture: identity.Culture));
            return null;
        }
    }

    private static LocaleDocument? LoadJson(
        LocaleIdentity identity,
        List<Diagnostic> diagnostics)
    {
        var source = JsonLocaleFile.Read(identity.FilePath, identity.Culture, diagnostics);
        if (source is null) return null;

        return JsonLocaleParser.Parse(
            identity.FilePath,
            identity.Culture,
            identity.ResourceSet,
            source,
            diagnostics);
    }

    private static LocaleFormat FormatForExtension(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => LocaleFormat.Json,
            ".arb" => LocaleFormat.Arb,
            ".resx" => LocaleFormat.Resx,
            ".xml" => LocaleFormat.Android,
            ".strings" => LocaleFormat.AppleStrings,
            ".po" or ".pot" => LocaleFormat.Po,
            _ => LocaleFormat.Json,
        };

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

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
