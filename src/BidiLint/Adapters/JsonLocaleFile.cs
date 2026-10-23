using System.Text;

namespace BidiLint;

internal static class JsonLocaleFile
{
    public static SourceText? Read(
        string path,
        string culture,
        List<Diagnostic> diagnostics)
    {
        try
        {
            return SourceText.FromUtf8Bytes(File.ReadAllBytes(path));
        }
        catch (DecoderFallbackException ex)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.MalformedFile,
                DiagnosticSeverity.Error,
                $"File is not valid UTF-8: {ex.Message}",
                path,
                Culture: culture));
            return null;
        }
    }
}
