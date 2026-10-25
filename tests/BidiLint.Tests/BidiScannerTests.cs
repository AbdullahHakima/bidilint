using System.Text;
using BidiLint;
using Xunit;

namespace BidiLint.Tests;

public class BidiScannerTests
{
    [Fact]
    public void PureArabic_HasNoLatinRuns()
    {
        var scan = BidiScanner.Scan("ماكينات خياطة وقطع غيار");
        Assert.True(scan.HasArabic);
        Assert.Empty(scan.UnisolatedLatinRuns);
    }

    [Fact]
    public void PureLatin_IsNotArabic()
    {
        var scan = BidiScanner.Scan("Built with ASP.NET Core");
        Assert.False(scan.HasArabic);
        Assert.False(scan.HasRtlScript);
    }

    [Fact]
    public void ArabicText_HasRtlScript()
    {
        var scan = BidiScanner.Scan("ماكينات خياطة");
        Assert.True(scan.HasArabic);
        Assert.True(scan.HasRtlScript);
    }

    [Fact]
    public void HebrewText_HasRtlScript()
    {
        var scan = BidiScanner.Scan("נבנה עם ASP.NET Core");
        Assert.True(scan.HasHebrew);
        Assert.True(scan.HasRtlScript);
        Assert.Contains("ASP.NET Core", scan.UnisolatedLatinRuns);
    }

    [Theory]
    [InlineData("\u0627")] // Arabic
    [InlineData("\u05D0")] // Hebrew
    [InlineData("\u0710")] // Syriac
    [InlineData("\u0780")] // Thaana
    [InlineData("\u07CA")] // NKo
    [InlineData("\u0800")] // Samaritan
    [InlineData("\u0840")] // Mandaic
    [InlineData("\U0001E900")] // Adlam
    [InlineData("\U00010D00")] // Hanifi Rohingya
    public void SupportedRtlScripts_AreDetected(string value)
    {
        Assert.True(BidiScanner.Scan(value).HasRtlScript, value);
    }

    [Fact]
    public void CommonDirectionalMarks_AreNotTreatedAsRtlScript()
    {
        Assert.False(BidiScanner.Scan("\u200F").HasRtlScript);
        Assert.False(BidiScanner.Scan("\u061C").HasRtlScript);
    }

    [Fact]
    public void SupplementaryRtlCharacters_AreDetectedWithTheRuneOverload()
    {
        Assert.True(BidiScanner.IsRtlChar(new Rune(0x1E900)));
        Assert.False(BidiScanner.IsRtlChar(new Rune('A')));
    }

    [Fact]
    public void LatinRuns_InArabicText_AreFound()
    {
        var scan = BidiScanner.Scan("مبني باستخدام ASP.NET Core و C# الحديثة");
        Assert.Contains("ASP.NET Core", scan.UnisolatedLatinRuns);
        Assert.Contains("C#", scan.UnisolatedLatinRuns);
    }

    [Fact]
    public void IsolatedLatinRun_IsNotFlagged()
    {
        var value = "الإصدار الجديد من \u2066.NET Core\u2069 متاح الآن";
        var scan = BidiScanner.Scan(value);
        Assert.Empty(scan.UnisolatedLatinRuns);
        Assert.Equal(0, scan.UnclosedIsolates);
    }

    [Fact]
    public void UnclosedIsolate_IsCounted()
    {
        var scan = BidiScanner.Scan("جرّب \u2066CqrsLens الجديد");
        Assert.Equal(1, scan.UnclosedIsolates);
        // The run sits inside the (unclosed) isolate, so it is not "unisolated".
        Assert.Empty(scan.UnisolatedLatinRuns);
    }

    [Fact]
    public void StrayPdi_IsCounted()
    {
        var scan = BidiScanner.Scan("نص عربي \u2069 بدون فاتح");
        Assert.Equal(1, scan.StrayClosers);
    }

    [Fact]
    public void MixedNumeralSystems_AreDetected()
    {
        var scan = BidiScanner.Scan("عدد المنتجات ٥ و 12 منتج");
        Assert.True(scan.HasWesternDigits);
        Assert.True(scan.HasArabicIndicDigits);
        Assert.False(scan.HasExtendedArabicIndicDigits);
    }

    [Fact]
    public void ExtendedArabicIndic_IsADistinctSystem()
    {
        var scan = BidiScanner.Scan("قیمت ۵ ریال");
        Assert.False(scan.HasWesternDigits);
        Assert.False(scan.HasArabicIndicDigits);
        Assert.True(scan.HasExtendedArabicIndicDigits);
        Assert.Equal(NumeralSystem.ExtendedArabicIndic, scan.NumeralSystems);
    }

    [Fact]
    public void AllThreeNumeralSystems_AreReportedTogether()
    {
        var scan = BidiScanner.Scan("٥ 5 ۵");
        Assert.Equal(
            NumeralSystem.Western |
            NumeralSystem.ArabicIndic |
            NumeralSystem.ExtendedArabicIndic,
            scan.NumeralSystems);
    }

    [Fact]
    public void WesternDigitsOnly_DoNotCountAsMixed()
    {
        var scan = BidiScanner.Scan("خصم 25 بالمئة");
        Assert.True(scan.HasWesternDigits);
        Assert.False(scan.HasArabicIndicDigits);
    }

    [Fact]
    public void Placeholders_AreExtractedSorted()
    {
        var found = BidiScanner.Placeholders("Save {0}% for {1} days, {name}");
        Assert.Equal(new[] { "{0}", "{1}", "{name}" }, found);
    }
}
