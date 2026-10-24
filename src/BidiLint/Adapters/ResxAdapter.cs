namespace BidiLint;

internal sealed class ResxAdapter : ILocaleAdapter
{
    public LocaleIdentity? Identify(
        string path,
        string workingDirectory,
        string baseCulture,
        IReadOnlySet<string>? layoutNames = null)
    {
        if (!Path.GetExtension(path).Equals(".resx", StringComparison.OrdinalIgnoreCase))
            return null;

        var identity = ResxLoader.Describe(path, baseCulture, workingDirectory);
        return new LocaleIdentity(path, identity.Culture, identity.ResourceSet, LocaleFormat.Resx);
    }

    public LocaleDocument? Load(LocaleIdentity identity, List<Diagnostic> diagnostics) =>
        ResxLoader.Load(
            identity.FilePath,
            new ResxIdentity(identity.Culture, identity.ResourceSet),
            diagnostics);
}
