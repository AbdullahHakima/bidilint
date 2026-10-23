using System.Diagnostics;

namespace BidiLint;

public static class Cli
{
    public static int Run(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        string? workingDirectory = null)
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
            stdout.WriteLine($"BidiLint {ToolInfo.Version}");
            return ExitCodes.Ok;
        }

        if (parsed.Errors.Count > 0)
        {
            foreach (var error in parsed.Errors)
            {
                stderr.WriteLine($"error: {error}");
            }
            stderr.WriteLine();
            PrintUsage(stderr);
            return ExitCodes.Usage;
        }

        var options = parsed.Options!;
        try
        {
            var cwd = Path.GetFullPath(workingDirectory ?? Directory.GetCurrentDirectory());
            var load = LocaleLoader.Load(options.Paths, options.BaseCulture, cwd);
            var result = Linter.Lint(load, options.BaseCulture);
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            if (options.Format == OutputFormat.Json)
                stdout.WriteLine(Reporter.RenderJson(result));
            else
                Reporter.Print(result, stdout);

            if (load.HasInputErrors) return ExitCodes.Input;
            if (!HasCulture(load, options.BaseCulture)) return ExitCodes.NoInput;

            var failing = result.Diagnostics.Any(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error ||
                (options.Strict && diagnostic.Severity == DiagnosticSeverity.Warning));
            return failing ? ExitCodes.Findings : ExitCodes.Ok;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"BidiLint failed: {ex.Message}");
            return ExitCodes.InternalError;
        }
    }

    private static bool HasCulture(LoadResult load, string culture) =>
        load.Documents.Any(document =>
            string.Equals(document.Culture, culture, StringComparison.OrdinalIgnoreCase));

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("BidiLint — lint localization files for bidi (bidirectional text) pitfalls.");
        writer.WriteLine();
        writer.WriteLine("Usage: bidilint <path> [<path>...] [--base <culture>] [--format text|json] [--strict]");
        writer.WriteLine();
        writer.WriteLine("  <path>      Folder or file to lint. May be repeated.");
        writer.WriteLine("  --base      Base culture other cultures are compared against (default: en).");
        writer.WriteLine("  --format    text (default) or json.");
        writer.WriteLine("  --strict    Treat warnings as failures too. Errors always fail.");
        writer.WriteLine("  --help      Show this help.");
        writer.WriteLine("  --version   Show the version.");
        writer.WriteLine();
        writer.WriteLine("Formats: JSON, .resx, Android res/values XML, Apple .strings, gettext PO/POT, Flutter ARB.");
        writer.WriteLine("Exit codes: 0 clean · 1 usage · 2 input/path · 3 no locale files or base missing ·");
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
