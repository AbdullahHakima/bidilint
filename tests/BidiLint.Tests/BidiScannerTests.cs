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
