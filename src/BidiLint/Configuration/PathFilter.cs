using System.Text;
using System.Text.RegularExpressions;

namespace BidiLint;

// Small glob matcher for config include/exclude and ignore selectors.
// Supports *, **, and ?. Matching is case-insensitive so the same config
// behaves the same on Windows, macOS, and Linux.
internal sealed class GlobPattern
{
    private readonly Regex _regex;

    private GlobPattern(string pattern, Regex regex)
    {
        Pattern = pattern;
        _regex = regex;
    }

    public string Pattern { get; }

    public static GlobPattern Create(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("A glob pattern cannot be empty.", nameof(pattern));

        var normalized = pattern.Trim().Replace('\\', '/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        if (normalized.EndsWith('/'))
            normalized += "**";

        var builder = new StringBuilder("^");
        if (!normalized.Contains('/')) builder.Append("(?:.*/)?");

        for (var index = 0; index < normalized.Length; index++)
        {
            var current = normalized[index];
            switch (current)
            {
                case '*':
                    if (index + 1 < normalized.Length && normalized[index + 1] == '*')
                    {
                        index++;
                        if (index + 1 < normalized.Length && normalized[index + 1] == '/')
                        {
                            index++;
                            builder.Append("(?:.*/)?");
                        }
                        else
                        {
                            builder.Append(".*");
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }
                    break;

                case '?':
                    builder.Append("[^/]");
                    break;

                default:
                    builder.Append(Regex.Escape(current.ToString()));
                    break;
            }
        }

        builder.Append('$');
        var regex = new Regex(
            builder.ToString(),
            RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase |
            RegexOptions.NonBacktracking,
            TimeSpan.FromSeconds(1));
        return new GlobPattern(normalized, regex);
    }

    public bool IsMatch(string relativePath) =>
        _regex.IsMatch(relativePath.Replace('\\', '/'));
}

internal sealed class PathFilter
{
    private readonly string _root;
    private readonly IReadOnlyList<GlobPattern> _include;
    private readonly IReadOnlyList<GlobPattern> _exclude;

    public PathFilter(
        string root,
        IEnumerable<string> include,
        IEnumerable<string> exclude)
    {
        _root = Path.GetFullPath(root);
        _include = include.Select(GlobPattern.Create).ToList();
        _exclude = exclude.Select(GlobPattern.Create).ToList();
    }

    public bool IsIncluded(string fullPath)
    {
        var relative = RelativePath(fullPath);
        if (_exclude.Any(pattern => pattern.IsMatch(relative))) return false;
        if (_include.Count == 0) return true;
        return _include.Any(pattern => pattern.IsMatch(relative));
    }

    public bool IsExcludedDirectory(string fullDirectory)
    {
        var relative = RelativePath(fullDirectory);
        return _exclude.Any(pattern =>
            pattern.IsMatch(relative) ||
            pattern.IsMatch(relative + "/"));
    }

    private string RelativePath(string fullPath) =>
        Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
}
