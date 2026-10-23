using System.Xml;
using System.Xml.Linq;

namespace BidiLint;

internal static class XmlLocaleFile
{
    private static readonly XmlReaderSettings XmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = false,
        IgnoreWhitespace = false,
    };

    public static (SourceText Source, XDocument Document)? Read(
        string path,
        string culture,
        List<Diagnostic> diagnostics)
    {
        var source = TextLocaleFile.Read(path, culture, diagnostics);
        if (source is null) return null;

        try
        {
            using var stringReader = new StringReader(source.Text);
            using var xmlReader = XmlReader.Create(stringReader, XmlSettings);
            var document = XDocument.Load(
                xmlReader,
                LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            return (source, document);
        }
        catch (XmlException ex)
        {
            var line = Math.Max(1, ex.LineNumber);
            var column = Math.Max(1, ex.LinePosition);
            diagnostics.Add(new Diagnostic(
                DiagnosticCodes.MalformedFile,
                DiagnosticSeverity.Error,
                ex.Message,
                path,
                SourceSpan.At(source.PositionAt(line, column)),
                culture));
            return null;
        }
    }
}
