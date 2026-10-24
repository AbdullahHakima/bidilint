using System.Text;
using Xunit;

namespace BidiLint.Tests;

public class UnicodeDataTests
{
    [Fact]
    public void ReportsPinnedUnicodeVersion()
    {
        Assert.Equal("17.0.0", UnicodeInfo.Version);
    }

    [Theory]
    [InlineData(0x0041, "L", "Latin")]
    [InlineData(0x0020, "WS", "Common")]
    [InlineData(0x05D0, "R", "Hebrew")]
    [InlineData(0x0590, "R", "Unknown")]
    [InlineData(0x0627, "AL", "Arabic")]
    [InlineData(0x061C, "AL", "Arabic")]
    [InlineData(0x200F, "R", "Common")]
    [InlineData(0x2066, "LRI", "Common")]
    [InlineData(0x2069, "PDI", "Common")]
    [InlineData(0x0660, "AN", "Arabic")]
    [InlineData(0x06F0, "EN", "Arabic")]
    [InlineData(0x1F600, "ON", "Common")]
    [InlineData(0x0378, "L", "Unknown")]
    public void ResolvesBidiClassAndScript(int codePoint, string bidi, string script)
    {
        var rune = new Rune(codePoint);

        Assert.Equal(Enum.Parse<BidiClass>(bidi), UnicodeInfo.GetBidiClass(rune));
        Assert.Equal(Enum.Parse<UnicodeScript>(script), UnicodeInfo.GetScript(rune));
    }

    [Theory]
    [InlineData(0x05D0, "Hebrew")]
    [InlineData(0x0710, "Syriac")]
    [InlineData(0x0780, "Thaana")]
    [InlineData(0x07CA, "Nko")]
    [InlineData(0x0800, "Samaritan")]
    [InlineData(0x0840, "Mandaic")]
    [InlineData(0x1E900, "Adlam")]
    public void DetectsEverySupportedRtlScript(int codePoint, string script)
    {
        Assert.Equal(Enum.Parse<UnicodeScript>(script), UnicodeInfo.GetScript(new Rune(codePoint)));
    }

    [Fact]
    public void ResolvesPairedBracketsAndMirroring()
    {
        Assert.True(UnicodeInfo.TryGetBracketPair(new Rune('('), out var open));
        Assert.Equal(')', open.PairedCodePoint);
        Assert.Equal(BracketType.Open, open.Type);

        Assert.True(UnicodeInfo.TryGetBracketPair(new Rune(')'), out var close));
        Assert.Equal('(', close.PairedCodePoint);
        Assert.Equal(BracketType.Close, close.Type);

        Assert.False(UnicodeInfo.TryGetBracketPair(new Rune('A'), out _));

        Assert.True(UnicodeInfo.TryGetMirror(new Rune('<'), out var mirror));
        Assert.Equal(new Rune('>'), mirror);
        Assert.False(UnicodeInfo.TryGetMirror(new Rune('A'), out _));
    }

    [Fact]
    public void TablesCoverEveryScalarWithoutOverlap()
    {
        AssertCoverage(UnicodeTables.BidiRanges.Select(range => (range.Start, range.End)));
        AssertCoverage(UnicodeTables.ScriptRanges.Select(range => (range.Start, range.End)));

        Assert.Equal(
            UnicodeTables.BracketPairs.Length,
            UnicodeTables.BracketPairs.Select(pair => pair.CodePoint).Distinct().Count());
        Assert.Equal(
            UnicodeTables.MirrorPairs.Length,
            UnicodeTables.MirrorPairs.Select(pair => pair.CodePoint).Distinct().Count());
        AssertSorted(UnicodeTables.BracketPairs.Select(pair => pair.CodePoint));
        AssertSorted(UnicodeTables.MirrorPairs.Select(pair => pair.CodePoint));
    }

    [Fact]
    public void EnumeratesScalarsWithUtf16Offsets()
    {
        var scalars = UnicodeScalars.Enumerate("A😀ب").ToArray();

        Assert.Equal(3, scalars.Length);
        Assert.Equal(new[] { 0x41, 0x1F600, 0x0628 }, scalars.Select(item => item.CodePoint));
        Assert.Equal(new[] { 0, 1, 3 }, scalars.Select(item => item.Utf16Offset));
        Assert.Equal(new[] { 1, 2, 1 }, scalars.Select(item => item.Utf16Length));
        Assert.Equal(3, UnicodeScalars.Count("A😀ب"));
    }

    [Fact]
    public void ReplacesInvalidUtf16WithOneReplacementScalar()
    {
        var scalar = Assert.Single(UnicodeScalars.Enumerate("\uD800"));

        Assert.Equal(Rune.ReplacementChar, scalar.Value);
        Assert.Equal(0, scalar.Utf16Offset);
        Assert.Equal(1, scalar.Utf16Length);
    }

    [Fact]
    public void EscapesBmpAndSupplementaryScalars()
    {
        Assert.Equal("\\u0628", UnicodeScalars.Escape(new Rune(0x0628)));
        Assert.Equal("\\U0001F600", UnicodeScalars.Escape(new Rune(0x1F600)));
        Assert.Equal("\\u0041\\U0001F600\\u0628", UnicodeScalars.Escape("A😀ب"));
    }

    private static void AssertSorted(IEnumerable<int> values)
    {
        var ordered = values.ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            Assert.True(ordered[index - 1] < ordered[index]);
        }
    }

    private static void AssertCoverage(IEnumerable<(int Start, int End)> ranges)
    {
        var ordered = ranges.OrderBy(range => range.Start).ToArray();
        Assert.NotEmpty(ordered);
        Assert.Equal(0, ordered[0].Start);
        Assert.Equal(0x10FFFF, ordered[^1].End);

        for (var index = 0; index < ordered.Length; index++)
        {
            Assert.True(ordered[index].Start <= ordered[index].End);
            if (index > 0) Assert.Equal(ordered[index - 1].End + 1, ordered[index].Start);
        }
    }
}
