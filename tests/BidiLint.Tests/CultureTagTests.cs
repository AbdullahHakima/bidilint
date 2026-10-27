using Xunit;

namespace BidiLint.Tests;

public class CultureTagTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    [InlineData("ar-EG")]
    [InlineData("en_US")]
    [InlineData("he")]
    [InlineData("az-Arab")]
    [InlineData("zh-Hans")]
    [InlineData("sr-Latn")]
    [InlineData("ar_EG.UTF-8")]
    [InlineData("en-US-u-ca-gregory")]
    [InlineData("ar-EG-u-nu-arab")]
    public void ValidTags_AreAccepted(string value)
    {
        Assert.True(CultureTag.TryParse(value, out var tag), value);
        Assert.False(string.IsNullOrWhiteSpace(tag!.Name));
    }

    [Theory]
    [InlineData("app")]
    [InlineData("data")]
    [InlineData("conf")]
    [InlineData("package")]
    [InlineData("en--US")]
    [InlineData("-en")]
    [InlineData("en-")]
    [InlineData("not a culture")]
    [InlineData("en-US-abc")]
    public void ArbitraryFileNames_AreRejected(string value)
    {
        Assert.False(CultureTag.TryParse(value, out _), value);
    }

    [Theory]
    [InlineData("ar-eg", "ar-EG")]
    [InlineData("AR_eg", "ar-EG")]
    [InlineData("en-us", "en-US")]
    [InlineData("ar_EG.UTF-8", "ar-EG")]
    public void Tags_AreCanonicalized(string value, string expected)
    {
        Assert.True(CultureTag.TryParse(value, out var tag));
        Assert.Equal(expected, tag!.Name);
    }

    [Theory]
    [InlineData("ar", true)]
    [InlineData("he", true)]
    [InlineData("fa", true)]
    [InlineData("yi", true)]
    [InlineData("az-Arab", true)]
    [InlineData("ku-Arab", true)]
    [InlineData("en-Arab", true)]
    [InlineData("en", false)]
    [InlineData("az-Latn", false)]
    [InlineData("sr-Latn", false)]
    [InlineData("ar-Latn", false)]
    [InlineData("he-Latn", false)]
    [InlineData("ku-Latn", false)]
    public void RightToLeft_IsDerivedFromLanguageAndScript(string value, bool expected)
    {
        Assert.Equal(expected, CultureTag.IsRtl(value));
        Assert.Equal(expected, Linter.IsRtl(value));
    }

    [Theory]
    [InlineData("ja", true)]
    [InlineData("zh-Hant", true)]
    [InlineData("ko", true)]
    [InlineData("th", true)]
    [InlineData("id", true)]
    [InlineData("ms", true)]
    [InlineData("en", false)]
    [InlineData("ru", false)]
    [InlineData("ar", false)]
    [InlineData("vi", false)]
    public void UsesSinglePluralForm_RecognizesOneFormLanguages(string value, bool expected)
    {
        Assert.Equal(expected, CultureTag.UsesSinglePluralForm(value));
    }
}
