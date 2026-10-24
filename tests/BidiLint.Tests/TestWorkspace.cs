using System.Text;

namespace BidiLint.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "bidilint-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public LoadResult Load(string baseCulture = "en") =>
        LocaleLoader.Load(new[] { Root }, baseCulture, Root);

    public LintResult Lint(string baseCulture = "en") =>
        Linter.Lint(Load(baseCulture), baseCulture);

    public (int Code, string Stdout, string Stderr) RunCli(params string[] args) =>
        RunCli(null, args);

    public (int Code, string Stdout, string Stderr) RunCli(
        IReadOnlyDictionary<string, string>? environment,
        params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = Cli.Run(args, stdout, stderr, Root, environment);
        return (code, stdout.ToString(), stderr.ToString());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A failed cleanup must not hide the test result.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above; the temporary directory is disposable.
        }
    }
}

internal static class TestPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BidiLint.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate BidiLint.sln from the test output directory.");
    }
}
