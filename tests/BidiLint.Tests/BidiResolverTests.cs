using Xunit;

namespace BidiLint.Tests;

public class BidiResolverTests
{
    [Fact]
    public void ResolvesEmptyParagraph()
    {
        var result = BidiResolver.Resolve(string.Empty);

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Empty(result.Levels);
        Assert.Empty(result.VisualOrder);
        Assert.Empty(result.Removed);
    }

    [Fact]
    public void ResolvesPlainLeftToRightText()
    {
        var result = BidiResolver.Resolve("abc");

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Equal([0, 0, 0], result.Levels);
        Assert.Equal([0, 1, 2], result.VisualOrder);
        Assert.DoesNotContain(true, result.Removed);
    }

    [Fact]
    public void ResolvesPlainRightToLeftText()
    {
        var result = BidiResolver.Resolve("אבג");

        Assert.Equal(1, result.ParagraphLevel);
        Assert.Equal([1, 1, 1], result.Levels);
        Assert.Equal([2, 1, 0], result.VisualOrder);
    }

    [Fact]
    public void KeepsRightToLeftRunInsideLeftToRightParagraph()
    {
        var result = BidiResolver.Resolve("abc אבג");

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Equal([0, 0, 0, 0, 1, 1, 1], result.Levels);
        Assert.Equal([0, 1, 2, 3, 6, 5, 4], result.VisualOrder);
    }

    [Fact]
    public void HonorsExplicitRightToLeftParagraphDirection()
    {
        var result = BidiResolver.Resolve("abc", BidiParagraphDirection.RightToLeft);

        Assert.Equal(1, result.ParagraphLevel);
        Assert.Equal([2, 2, 2], result.Levels);
        Assert.Equal([0, 1, 2], result.VisualOrder);
    }

    [Fact]
    public void IsolatesRightToLeftContentFromTheOuterParagraph()
    {
        var result = BidiResolver.Resolve("a\u2067אבג\u2069b");

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Equal([0, 0, 1, 1, 1, 0, 0], result.Levels);
        Assert.Equal([0, 1, 4, 3, 2, 5, 6], result.VisualOrder);
    }

    [Fact]
    public void FirstStrongDirectionSkipsIsolatedContent()
    {
        var result = BidiResolver.Resolve("\u2067אבג\u2069abc");

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Equal(1, result.Levels[2]);
        Assert.Equal(0, result.Levels[5]);
    }

    [Fact]
    public void FirstStrongDirectionUsesAutomaticallyDetectedFsiDirection()
    {
        var result = BidiResolver.Resolve("\u2068אבג\u2069");

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Equal([0, 1, 1, 1, 0], result.Levels);
        Assert.Equal([0, 3, 2, 1, 4], result.VisualOrder);
    }

    [Fact]
    public void RemovesEmbeddingControlsFromVisualOrder()
    {
        var result = BidiResolver.Resolve("a\u202Bאבג\u202Cb");

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Equal([0, 0, 1, 1, 1, 1, 0], result.Levels);
        Assert.Equal([false, true, false, false, false, true, false], result.Removed);
        Assert.Equal([0, 4, 3, 2, 6], result.VisualOrder);
    }

    [Fact]
    public void ResolvesLeadingNonSpacingMarkWithTheSequenceStartDirection()
    {
        var result = BidiResolver.Resolve("\u0301");

        Assert.Equal(0, result.ParagraphLevel);
        Assert.Equal([0], result.Levels);
        Assert.Equal([0], result.VisualOrder);
    }
}
