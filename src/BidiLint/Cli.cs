using System.Diagnostics;
using System.Globalization;

namespace BidiLint;

public static class Cli
{
    public static int Run(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var parsed = CommandLineParser.Parse(args);

        if (parsed.ShowHelp)
        {
            PrintUsage(stdout);
            return ExitCodes.Ok;
        }

        if (parsed.ShowVersion)
        {
            stdout.WriteLine($"BidiLint {ToolInfo.Version} (Unicode {UnicodeInfo.Version})");
            return ExitCodes.Ok;
        }

        if (parsed.Errors.Count > 0)
        {
            PrintErrors(stderr, parsed.Errors);
            stderr.WriteLine();
            PrintUsage(stderr);
            return ExitCodes.Usage;
        }

        var options = parsed.Options!;
        var cwd = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());

        try
        {
            if (options.Init) return Init(options, cwd, stdout, stderr);

            var resolution = ConfigLoader.Resolve(
                options.ConfigPath,
                options.NoConfig,
                options.Paths,
                cwd);
            if (resolution.Errors.Count > 0)
            {
                PrintErrors(stderr, resolution.Errors);
                return ExitCodes.Usage;
            }

            var config = resolution.Config;
            var baseCulture = options.BaseCultureSpecified
                ? options.BaseCulture
                : config.BaseCulture;
            var format = options.FormatSpecified
                ? options.Format
                : config.Output.Format ?? OutputFormat.Text;
            var failOn = options.FailOnSpecified
                ? options.FailOn
                : options.Strict
                    ? FailOn.Warning
                    : config.FailOn;
            var maxWarnings = options.MaxWarnings ?? config.Output.MaxWarnings;
            var quiet = options.Quiet || config.Output.Quiet == true;
            var baselinePath = ResolveBaselinePath(options, config, cwd);
            var updateBaseline = options.UpdateBaseline;
            if (updateBaseline && baselinePath is null)
                baselinePath = Path.Combine(cwd, "bidilint-baseline.json");

            var useColor = ShouldUseColor(
                stdout,
                options.Color,
                config.Output.Color,
                environment);

            if (options.Debug)
            {
                stderr.WriteLine($"debug: config {(config.ConfigPath ?? "(none)")}");
                var maxWarningsText = maxWarnings is null
                    ? "none"
                    : maxWarnings.Value.ToString(CultureInfo.InvariantCulture);
                stderr.WriteLine(
                    $"debug: base {baseCulture}, format {FormatName(format)}, fail-on {FailOnName(failOn)}, " +
                    $"max-warnings {maxWarningsText}, baseline {(baselinePath ?? "(none)")}");
            }

            var load = LocaleLoader.Load(options.Paths, baseCulture, cwd, config);
            var result = Linter.Lint(load, baseCulture, config);

            // Never write or apply a baseline when the requested input paths or
            // base culture are invalid. This avoids publishing a partial
            // baseline from a failed run.
            var baselineReady = !load.HasInputErrors && HasCulture(load, baseCulture);
            var shouldUpdateBaseline = updateBaseline && baselineReady;
            var activeBaselinePath = updateBaseline && !baselineReady ? null : baselinePath;
            if (updateBaseline && !baselineReady)
            {
                stderr.WriteLine(
                    "error: baseline was not updated because the input paths or base culture are invalid.");
            }

            SuppressionProcessor.Apply(
                result,
                config,
                cwd,
                activeBaselinePath,
                shouldUpdateBaseline,
                DateOnly.FromDateTime(DateTime.UtcNow));
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            if (format == OutputFormat.Json)
                stdout.WriteLine(Reporter.RenderJson(result));
            else
                Reporter.Print(result, stdout, new ReporterOptions(quiet, useColor));

            if (load.HasInputErrors) return ExitCodes.Input;
            if (!HasCulture(load, baseCulture)) return ExitCodes.NoInput;
            if (result.BaselineUpdated)
            {
                // The baseline intentionally keeps eligible findings out of CI.
                // Only diagnostics that can never be baselined decide the exit
                // code of an update run.
                var remaining = result.Diagnostics
                    .Where(diagnostic => !SuppressionProcessor.IsBaselineEligible(diagnostic));
                return ShouldFail(remaining, failOn, maxWarnings)
                    ? ExitCodes.Findings
                    : ExitCodes.Ok;
            }

            return ShouldFail(result, failOn, maxWarnings)
                ? ExitCodes.Findings
                : ExitCodes.Ok;
        }
        catch (ConfigException ex)
        {
            PrintErrors(stderr, ex.Errors);
            return ExitCodes.Usage;
        }
        catch (Exception ex)
        {
            if (options.Debug)
                stderr.WriteLine(ex);
            else
                stderr.WriteLine($"BidiLint failed: {ex.Message}");
            return ExitCodes.InternalError;
        }
    }

    private static int Init(
        CliOptions options,
        string workingDirectory,
        TextWriter stdout,
        TextWriter stderr)
    {
        var path = options.ConfigPath is null
            ? Path.Combine(workingDirectory, ConfigLoader.FileName)
            : ResolvePath(options.ConfigPath, workingDirectory);
        if (Directory.Exists(path)) path = Path.Combine(path, ConfigLoader.FileName);

        try
        {
            ConfigLoader.WriteStarter(path);
            stdout.WriteLine($"Created {DisplayPath(workingDirectory, path)}");
            return ExitCodes.Ok;
        }
        catch (ConfigException ex)
        {
            PrintErrors(stderr, ex.Errors);
            return ExitCodes.Usage;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PrintErrors(stderr, new[] { $"Cannot write config file {path}: {ex.Message}" });
            return ExitCodes.Usage;
        }
    }

    private static string? ResolveBaselinePath(
        CliOptions options,
        LintConfig config,
        string workingDirectory)
    {
        if (options.NoBaseline) return null;
        if (options.BaselinePath is not null)
            return ResolvePath(options.BaselinePath, workingDirectory);
        return config.BaselinePath;
    }

    private static string ResolvePath(string path, string workingDirectory)
    {
        try
        {
            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(workingDirectory, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ConfigException($"Invalid path \"{path}\": {ex.Message}");
        }
    }

    private static bool ShouldUseColor(
        TextWriter stdout,
        bool? cliColor,
        bool? configColor,
        IReadOnlyDictionary<string, string>? environment)
    {
        if (!ReferenceEquals(stdout, Console.Out)) return false;
        if (Console.IsOutputRedirected) return false;

        var noColor = environment is null
            ? Environment.GetEnvironmentVariable("NO_COLOR") is not null
            : environment.ContainsKey("NO_COLOR");

        return ColorDecision(cliColor, configColor, noColor);
    }

    internal static bool ColorDecision(
        bool? cliColor,
        bool? configColor,
        bool noColorEnvironment) =>
        !noColorEnvironment && (cliColor ?? configColor ?? true);

    private static bool ShouldFail(LintResult result, FailOn failOn, int? maxWarnings) =>
        ShouldFail(result.Diagnostics, failOn, maxWarnings);

    private static bool ShouldFail(
        IEnumerable<Diagnostic> diagnostics,
        FailOn failOn,
        int? maxWarnings)
    {
        var errors = 0;
        var warnings = 0;
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error) errors++;
            else if (diagnostic.Severity == DiagnosticSeverity.Warning) warnings++;
        }

        if (maxWarnings is not null && warnings > maxWarnings.Value) return true;

        return failOn switch
        {
            FailOn.None => false,
            FailOn.Warning => errors > 0 || warnings > 0,
            _ => errors > 0,
        };
    }

    private static bool HasCulture(LoadResult load, string culture) =>
        load.Documents.Any(document =>
            string.Equals(document.Culture, culture, StringComparison.OrdinalIgnoreCase));

    private static void PrintErrors(TextWriter writer, IEnumerable<string> errors)
    {
        foreach (var error in errors)
            writer.WriteLine($"error: {error}");
    }

    private static string DisplayPath(string workingDirectory, string path) =>
        Path.GetRelativePath(workingDirectory, path).Replace('\\', '/');

    private static string FormatName(OutputFormat format) =>
        format == OutputFormat.Json ? "json" : "text";

    private static string FailOnName(FailOn failOn) => failOn switch
    {
        FailOn.Warning => "warning",
        FailOn.None => "none",
        _ => "error",
    };

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("BidiLint — lint localization files for bidi (bidirectional text) pitfalls.");
        writer.WriteLine();
        writer.WriteLine("Usage: bidilint <path> [<path>...] [options]");
        writer.WriteLine("       bidilint --init [--config <file>]");
        writer.WriteLine();
        writer.WriteLine("  <path>              Folder or file to lint. May be repeated.");
        writer.WriteLine("  --base <culture>    Base culture other cultures are compared against (default: en).");
        writer.WriteLine("  --format text|json  Output format. Defaults to text.");
        writer.WriteLine("  --fail-on <level>   error (default), warning, or none.");
        writer.WriteLine("  --strict            Alias for --fail-on warning.");
        writer.WriteLine("  --max-warnings <n>  Fail when the warning count exceeds n.");
        writer.WriteLine("  --quiet             Print only errors in text output.");
        writer.WriteLine("  --color             Force color on a terminal.");
        writer.WriteLine("  --no-color          Disable color; NO_COLOR is also honored.");
        writer.WriteLine("  --config <file>     Use a specific bidilint.json.");
        writer.WriteLine("  --no-config         Ignore config discovery and use defaults.");
        writer.WriteLine("  --init              Write a documented bidilint.json starter file.");
        writer.WriteLine("  --baseline <file>   Apply a baseline of accepted findings.");
        writer.WriteLine("  --update-baseline   Rewrite the baseline from current findings.");
        writer.WriteLine("  --no-baseline       Ignore a configured baseline.");
        writer.WriteLine("  --debug             Print config details and full exceptions.");
        writer.WriteLine("  --help              Show this help.");
        writer.WriteLine("  --version           Show the version.");
        writer.WriteLine();
        writer.WriteLine("Formats: JSON, .resx, Android res/values XML, Apple .strings, gettext PO/POT, Flutter ARB.");
        writer.WriteLine("Config: bidilint.json (JSONC), discovered upward from the target path.");
        writer.WriteLine("Exit codes: 0 clean · 1 usage/config · 2 input/path · 3 no locale files or base missing ·");
        writer.WriteLine("            4 findings · 5 internal error.");
    }
}

public static class ExitCodes
{
    public const int Ok = 0;
    public const int Usage = 1;
    public const int Input = 2;
    public const int NoInput = 3;
    public const int Findings = 4;
    public const int InternalError = 5;
}
