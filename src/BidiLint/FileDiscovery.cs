namespace BidiLint;

internal sealed record DiscoveredFile(string FullPath, bool IsExplicit);

internal sealed class DiscoveryResult
{
    public List<DiscoveredFile> Files { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
}

internal static class FileDiscovery
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", ".vscode", ".next", ".nuxt", ".output", ".cache",
        ".venv", "bin", "obj", "node_modules", "dist", "build", "coverage",
        "artifacts", "TestResults", "vendor", "target",
    };

    public static DiscoveryResult Discover(IEnumerable<string> paths, string workingDirectory)
    {
        var result = new DiscoveryResult();
        var seen = new HashSet<string>(PathComparer);

        foreach (var rawPath in paths)
        {
            string fullPath;
            try
            {
                fullPath = Path.IsPathRooted(rawPath)
                    ? Path.GetFullPath(rawPath)
                    : Path.GetFullPath(Path.Combine(workingDirectory, rawPath));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.PathNotFound,
                    DiagnosticSeverity.Error,
                    $"Invalid path: {ex.Message}",
                    rawPath));
                continue;
            }

            if (File.Exists(fullPath))
            {
                AddFile(result, seen, fullPath, isExplicit: true);
            }
            else if (Directory.Exists(fullPath))
            {
                DiscoverDirectory(result, seen, fullPath);
            }
            else
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.PathNotFound,
                    DiagnosticSeverity.Error,
                    "Path not found.",
                    fullPath));
            }
        }

        result.Files.Sort((left, right) =>
            string.Compare(left.FullPath, right.FullPath, StringComparison.Ordinal));
        return result;
    }

    private static void DiscoverDirectory(DiscoveryResult result, HashSet<string> seen, string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    AddFile(result, seen, file, isExplicit: false);
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    var name = Path.GetFileName(child);
                    if (SkippedDirectories.Contains(name)) continue;
                    if (IsReparsePoint(child)) continue;
                    pending.Push(child);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.UnreadablePath,
                    DiagnosticSeverity.Error,
                    $"Cannot read directory: {ex.Message}",
                    directory));
            }
        }
    }

    private static void AddFile(
        DiscoveryResult result,
        HashSet<string> seen,
        string path,
        bool isExplicit)
    {
        var extension = Path.GetExtension(path);
        if (!IsSupportedExtension(extension))
        {
            if (isExplicit)
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.UnsupportedFile,
                    DiagnosticSeverity.Error,
                    "Unsupported file type. Expected .json, .resx, .xml, .strings, .po, .pot, or .arb.",
                    path));
            }
            return;
        }

        if (seen.Add(path))
        {
            result.Files.Add(new DiscoveredFile(path, isExplicit));
        }
        else if (isExplicit)
        {
            var index = result.Files.FindIndex(f => PathComparer.Equals(f.FullPath, path));
            if (index >= 0) result.Files[index] = result.Files[index] with { IsExplicit = true };
        }
    }

    private static bool IsSupportedExtension(string extension) =>
        extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".resx", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".strings", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".po", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".pot", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".arb", StringComparison.OrdinalIgnoreCase);

    private static bool IsReparsePoint(string directory)
    {
        try
        {
            return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
