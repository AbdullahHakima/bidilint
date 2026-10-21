using System.Diagnostics;
using BidiLint;

// Exit codes: 0 ok · 1 usage error · 2 path not found · 3 no locale files or
// base culture missing · 4 errors found under --strict.
var sw = Stopwatch.StartNew();

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var root = args[0];
var baseCulture = "en";
var strict = false;
for (var i = 1; i < args.Length; i++)
{
    if (args[i] == "--strict") strict = true;
    else if (args[i] == "--base" && i + 1 < args.Length) baseCulture = args[++i];
    else
    {
        Console.Error.WriteLine($"Unknown argument: {args[i]}");
        PrintUsage();
        return 1;
    }
}

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"Path not found: {root}");
    return 2;
}

var files = LocaleLoader.LoadFolder(root);
if (files.Count == 0)
{
    Console.Error.WriteLine($"No locale files found under {root} (expected files named by culture, e.g. en.json, ar.json).");
    return 3;
}

var baseFile = files.FirstOrDefault(f => string.Equals(f.Culture, baseCulture, StringComparison.OrdinalIgnoreCase));
if (baseFile is null)
{
    Console.Error.WriteLine($"Base culture file '{baseCulture}.json' not found under {root}.");
    return 3;
}

var result = Linter.Lint(files, baseFile);
result.ElapsedMilliseconds = (long)sw.Elapsed.TotalMilliseconds;
Reporter.Print(result, baseCulture);

var errors = result.Findings.Count(f => f.Severity == FindingSeverity.Error);
return strict && errors > 0 ? 4 : 0;

static void PrintUsage()
{
    Console.WriteLine("BidiLint — lint localization files for bidi (bidirectional text) pitfalls.");
    Console.WriteLine();
    Console.WriteLine("Usage: bidilint <folder> [--base en] [--strict]");
    Console.WriteLine();
    Console.WriteLine("  <folder>     Folder holding JSON locale files named by culture (en.json, ar.json, ...).");
    Console.WriteLine("  --base       Base culture every other file is compared against (default: en).");
    Console.WriteLine("  --strict     Exit 4 when any error is found (built for CI gates).");
}
