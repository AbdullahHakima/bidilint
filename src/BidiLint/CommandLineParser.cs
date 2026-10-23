namespace BidiLint;

internal enum OutputFormat
{
    Text,
    Json,
}

internal sealed record CliOptions(
    IReadOnlyList<string> Paths,
    string BaseCulture,
    OutputFormat Format,
    bool Strict);

internal sealed record CliParseResult(
    CliOptions? Options,
    bool ShowHelp,
    bool ShowVersion,
    IReadOnlyList<string> Errors);

internal static class CommandLineParser
{
    public static CliParseResult Parse(string[] args)
    {
        var paths = new List<string>();
        var errors = new List<string>();
        var baseCulture = "en";
        var format = OutputFormat.Text;
        var strict = false;
        var showHelp = false;
        var showVersion = false;
        var endOfOptions = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (!endOfOptions && arg == "--")
            {
                endOfOptions = true;
                continue;
            }

            if (!endOfOptions && arg is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            if (!endOfOptions && arg == "--version")
            {
                showVersion = true;
                continue;
            }

            if (!endOfOptions && arg == "--strict")
            {
                strict = true;
                continue;
            }

            if (!endOfOptions && arg == "--base")
            {
                if (!TryReadValue(args, ref i, "--base", errors, out var value)) continue;
                baseCulture = value;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--base=", StringComparison.Ordinal))
            {
                baseCulture = arg["--base=".Length..];
                continue;
            }

            if (!endOfOptions && arg == "--format")
            {
                if (!TryReadValue(args, ref i, "--format", errors, out var value)) continue;
                if (!TryParseFormat(value, out format, errors)) continue;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--format=", StringComparison.Ordinal))
            {
                var value = arg["--format=".Length..];
                if (TryParseFormat(value, out var parsedFormat, errors)) format = parsedFormat;
                continue;
            }

            if (!endOfOptions && arg.StartsWith('-') && arg != "-")
            {
                errors.Add($"Unknown option: {arg}");
                continue;
            }

            paths.Add(arg);
        }

        if (!showHelp && !showVersion)
        {
            if (!CultureTag.TryParse(baseCulture, out var tag))
            {
                errors.Add($"Unknown or invalid base culture: {baseCulture}");
            }
            else
            {
                baseCulture = tag!.Name;
            }

            if (paths.Count == 0)
            {
                errors.Add("No path specified.");
            }
        }

        var options = errors.Count == 0 && !showHelp && !showVersion
            ? new CliOptions(paths, baseCulture, format, strict)
            : null;

        return new CliParseResult(options, showHelp, showVersion, errors);
    }

    private static bool TryReadValue(
        string[] args,
        ref int index,
        string option,
        List<string> errors,
        out string value)
    {
        value = "";
        if (index + 1 >= args.Length)
        {
            errors.Add($"Option {option} requires a value.");
            return false;
        }

        value = args[++index];
        if (value.StartsWith('-') && value != "-")
        {
            errors.Add($"Option {option} requires a value, but found {value}.");
            return false;
        }

        return true;
    }

    private static bool TryParseFormat(string value, out OutputFormat format, List<string> errors)
    {
        switch (value.ToLowerInvariant())
        {
            case "text":
                format = OutputFormat.Text;
                return true;
            case "json":
                format = OutputFormat.Json;
                return true;
            default:
                format = OutputFormat.Text;
                errors.Add($"Unknown format: {value} (expected text or json).");
                return false;
        }
    }
}
