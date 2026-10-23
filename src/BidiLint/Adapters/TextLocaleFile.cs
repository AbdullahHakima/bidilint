using System.Text;

namespace BidiLint;

// Reads locale text with BOM detection and strict decoding. Formats that are
// UTF-16 in the wild (Apple .strings, some .resx) are handled by StreamReader;
// invalid byte sequences become a diagnostic rather than a silent skip.
internal static class TextLocaleFile
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static SourceText? Read(
        string path,
        string culture,
        List<Diagnostic> diagnostics)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(
                stream,
                StrictUtf8,
                detectEncodingFromByteOrderMarks: true);
            return SourceText.FromText(reader.ReadToEnd());
        }
        catch (DecoderFallbackException ex)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.MalformedFile,
                DiagnosticSeverity.Error,
                $"File is not valid UTF-8/UTF-16: {ex.Message}",
                path,
                Culture: culture));
            return null;
        }
    }
}
