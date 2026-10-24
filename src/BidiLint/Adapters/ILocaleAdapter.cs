namespace BidiLint;

internal sealed record LocaleIdentity(
    string FilePath,
    string Culture,
    string ResourceSet,
    LocaleFormat Format);

// A format/layout adapter answers two questions: does this path belong to a
// supported locale layout, and if so, how is it parsed? Rules never depend on
// the file format; they consume LocaleDocument entries.
internal interface ILocaleAdapter
{
    LocaleIdentity? Identify(
        string path,
        string workingDirectory,
        string baseCulture,
        IReadOnlySet<string>? layoutNames = null);

    LocaleDocument? Load(LocaleIdentity identity, List<Diagnostic> diagnostics);
}
