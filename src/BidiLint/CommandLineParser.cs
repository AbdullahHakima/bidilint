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
    bool Strict,
    bool BaseCultureSpecified = false,
    bool FormatSpecified = false,
    FailOn FailOn = FailOn.Error,
    bool FailOnSpecified = false,
    int? MaxWarnings = null,
    bool Quiet = false,
    bool? Color = null,
    bool Debug = false,
    string? ConfigPath = null,
    bool NoConfig = false,
    bool Init = false,
    string? BaselinePath = null,
    bool NoBaseline = false,
    bool UpdateBaseline = false);

internal sealed record CliParseResult(
    CliOptions? Options,
    bool ShowHelp,
    bool ShowVersion,
    IReadOnlyList<string> Errors);

internal static class CommandLineParser
{
    private static readonly string[] KnownOptions =
    [
        "--help",
        "--version",
        "--base",
        "--format",
        "--strict",
        "--fail-on",
        "--max-warnings",
        "--quiet",
        "--color",
        "--no-color",
        "--debug",
        "--config",
        "--no-config",
        "--init",
        "--baseline",
        "--no-baseline",
        "--update-baseline",
    ];

    public static CliParseResult Parse(string[] args)
    {
        var paths = new List<string>();
        var errors = new List<string>();
        var baseCulture = "en";
        var baseCultureSpecified = false;
        var format = OutputFormat.Text;
        var formatSpecified = false;
        var failOn = FailOn.Error;
        var failOnSpecified = false;
        var strict = false;
        var maxWarnings = (int?)null;
        var quiet = false;
        var color = (bool?)null;
        var debug = false;
        var configPath = (string?)null;
        var noConfig = false;
        var init = false;
        var baselinePath = (string?)null;
        var noBaseline = false;
        var updateBaseline = false;
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

            if (!endOfOptions && arg == "--quiet")
            {
                quiet = true;
                continue;
            }

            if (!endOfOptions && arg == "--no-color")
            {
                color = false;
                continue;
            }

            if (!endOfOptions && arg == "--color")
            {
                color = true;
                continue;
            }

            if (!endOfOptions && arg == "--debug")
            {
                debug = true;
                continue;
            }

            if (!endOfOptions && arg == "--init")
            {
                init = true;
                continue;
            }

            if (!endOfOptions && arg == "--no-config")
            {
                noConfig = true;
                continue;
            }

            if (!endOfOptions && arg == "--no-baseline")
            {
                noBaseline = true;
                continue;
            }

            if (!endOfOptions && arg == "--update-baseline")
            {
                updateBaseline = true;
                continue;
            }

            if (!endOfOptions && arg == "--base")
            {
                if (!TryReadValue(args, ref i, "--base", errors, out var value)) continue;
                baseCulture = value;
                baseCultureSpecified = true;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--base=", StringComparison.Ordinal))
            {
                baseCulture = arg["--base=".Length..];
                baseCultureSpecified = true;
                continue;
            }

            if (!endOfOptions && arg == "--format")
            {
                if (!TryReadValue(args, ref i, "--format", errors, out var value)) continue;
                if (!TryParseFormat(value, out format, errors)) continue;
                formatSpecified = true;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--format=", StringComparison.Ordinal))
            {
                var value = arg["--format=".Length..];
                if (TryParseFormat(value, out var parsedFormat, errors))
                {
                    format = parsedFormat;
                    formatSpecified = true;
                }
                continue;
            }

            if (!endOfOptions && arg == "--fail-on")
            {
                if (!TryReadValue(args, ref i, "--fail-on", errors, out var value)) continue;
                if (!TryParseFailOn(value, out failOn, errors)) continue;
                failOnSpecified = true;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--fail-on=", StringComparison.Ordinal))
            {
                var value = arg["--fail-on=".Length..];
                if (TryParseFailOn(value, out var parsedFailOn, errors))
                {
                    failOn = parsedFailOn;
                    failOnSpecified = true;
                }
                continue;
            }

            if (!endOfOptions && arg == "--max-warnings")
            {
                if (!TryReadValue(args, ref i, "--max-warnings", errors, out var value)) continue;
                if (TryParseMaxWarnings(value, errors, out var parsed)) maxWarnings = parsed;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--max-warnings=", StringComparison.Ordinal))
            {
                var value = arg["--max-warnings=".Length..];
                if (TryParseMaxWarnings(value, errors, out var parsed)) maxWarnings = parsed;
                continue;
            }

            if (!endOfOptions && arg == "--config")
            {
                if (!TryReadValue(args, ref i, "--config", errors, out var value)) continue;
                configPath = value;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--config=", StringComparison.Ordinal))
            {
                configPath = arg["--config=".Length..];
                continue;
            }

            if (!endOfOptions && arg == "--baseline")
            {
                if (!TryReadValue(args, ref i, "--baseline", errors, out var value)) continue;
                baselinePath = value;
                continue;
            }

            if (!endOfOptions && arg.StartsWith("--baseline=", StringComparison.Ordinal))
            {
                baselinePath = arg["--baseline=".Length..];
                continue;
            }

            if (!endOfOptions && arg.StartsWith('-') && arg != "-")
            {
                var suggestion = SuggestionHelper.Closest(arg, KnownOptions);
                errors.Add(suggestion is null
                    ? $"Unknown option: {arg}"
                    : $"Unknown option: {arg}. Did you mean {suggestion}?");
                continue;
            }

            paths.Add(arg);
        }

        if (!showHelp && !showVersion)
        {
            if (configPath is not null && string.IsNullOrWhiteSpace(configPath))
                errors.Add("--config requires a non-empty path.");
            if (baselinePath is not null && string.IsNullOrWhiteSpace(baselinePath))
                errors.Add("--baseline requires a non-empty path.");
            if (noConfig && configPath is not null)
                errors.Add("--config and --no-config cannot be used together.");
            if (noBaseline && baselinePath is not null)
                errors.Add("--baseline and --no-baseline cannot be used together.");
            if (noBaseline && updateBaseline)
                errors.Add("--update-baseline and --no-baseline cannot be used together.");
            if (strict && failOnSpecified && failOn != FailOn.Warning)
                errors.Add("--strict and --fail-on must use the same threshold.");
            if (init && noConfig)
                errors.Add("--init and --no-config cannot be used together.");
            if (init && paths.Count > 0)
                errors.Add("--init does not accept a path; it writes bidilint.json in the working directory or at --config.");

            if (!CultureTag.TryParse(baseCulture, out var tag))
            {
                errors.Add($"Unknown or invalid base culture: {baseCulture}");
            }
            else
            {
                baseCulture = tag!.Name;
            }

            if (!init && paths.Count == 0)
            {
                errors.Add("No path specified.");
            }
        }

        var options = errors.Count == 0 && !showHelp && !showVersion
            ? new CliOptions(
                paths,
                baseCulture,
                format,
                strict,
                baseCultureSpecified,
                formatSpecified,
                failOn,
                failOnSpecified,
                maxWarnings,
                quiet,
                color,
                debug,
                configPath,
                noConfig,
                init,
                baselinePath,
                noBaseline,
                updateBaseline)
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

    private static bool TryParseFailOn(string value, out FailOn failOn, List<string> errors)
    {
        switch (value.ToLowerInvariant())
        {
            case "error":
                failOn = FailOn.Error;
                return true;
            case "warning":
                failOn = FailOn.Warning;
                return true;
            case "none":
                failOn = FailOn.None;
                return true;
            default:
                failOn = FailOn.Error;
                errors.Add($"Unknown fail-on value: {value} (expected error, warning, or none).");
                return false;
        }
    }

    private static bool TryParseMaxWarnings(string value, List<string> errors, out int maxWarnings)
    {
        if (int.TryParse(value, out maxWarnings) && maxWarnings >= 0) return true;
        errors.Add($"--max-warnings requires a non-negative integer, but found {value}.");
        maxWarnings = 0;
        return false;
    }
}
