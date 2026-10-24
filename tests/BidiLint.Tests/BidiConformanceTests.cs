using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Xunit.Sdk;

namespace BidiLint.Tests;

public class BidiConformanceTests
{
    private static readonly Dictionary<string, BidiClass> ClassNames =
        Enum.GetValues<BidiClass>().ToDictionary(value => value.ToString(), value => value, StringComparer.Ordinal);

    [Fact]
    public void ConformanceFixturesMatchDocumentedChecksums()
    {
        AssertFixtureChecksum(
            "BidiTest.txt.gz",
            "888bdfc8090652272d1f859cdb00ae659e2dc6c26740be61ef1d03998a687620");
        AssertFixtureChecksum(
            "BidiCharacterTest.txt.gz",
            "a3e6e905ab5afbe318a96df5401d0372a04cd73ef139ab5e3cf0ae241c255488");
    }

    [Fact]
    public void PassesUnicodeBidiTest()
    {
        var cases = 0;
        string[]? expectedLevels = null;
        int[]? expectedOrder = null;
        using var reader = OpenFixture("BidiTest.txt.gz");
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#') continue;

            if (trimmed.StartsWith("@Levels:", StringComparison.Ordinal))
            {
                expectedLevels = SplitFields(trimmed["@Levels:".Length..]);
                continue;
            }

            if (trimmed.StartsWith("@Reorder:", StringComparison.Ordinal))
            {
                expectedOrder = SplitFields(trimmed["@Reorder:".Length..])
                    .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
                    .ToArray();
                continue;
            }

            var separator = trimmed.LastIndexOf(';');
            if (separator < 0) continue;
            if (expectedLevels is null || expectedOrder is null)
            {
                Fail("BidiTest", lineNumber, trimmed, "test data appears before its directives");
            }

            var input = trimmed[..separator].Trim();
            var maskText = trimmed[(separator + 1)..].Trim();
            var mask = int.Parse(maskText, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var classes = SplitFields(input)
                .Select(token => ClassNames.TryGetValue(token, out var value)
                    ? value
                    : throw new XunitException($"Unknown bidi class '{token}' at BidiTest line {lineNumber}."))
                .ToArray();

            foreach (var (bit, direction) in new[]
                     {
                         (1, BidiParagraphDirection.Auto),
                         (2, BidiParagraphDirection.LeftToRight),
                         (4, BidiParagraphDirection.RightToLeft),
                     })
            {
                if ((mask & bit) == 0) continue;

                cases++;
                var result = BidiResolver.Resolve(classes, direction);
                if (direction != BidiParagraphDirection.Auto)
                {
                    var expectedParagraphLevel = direction == BidiParagraphDirection.RightToLeft ? 1 : 0;
                    if (result.ParagraphLevel != expectedParagraphLevel)
                    {
                        Fail(
                            "BidiTest",
                            lineNumber,
                            input,
                            direction,
                            $"paragraph level {result.ParagraphLevel}, expected {expectedParagraphLevel}");
                    }
                }

                for (var index = 0; index < classes.Length; index++)
                {
                    var expected = expectedLevels[index];
                    if (expected == "x")
                    {
                        if (!result.Removed[index])
                        {
                            Fail(
                                "BidiTest",
                                lineNumber,
                                input,
                                direction,
                                $"character {index} ({classes[index]}) was expected to be removed by X9");
                        }

                        continue;
                    }

                    if (result.Removed[index])
                    {
                        Fail(
                            "BidiTest",
                            lineNumber,
                            input,
                            direction,
                            $"character {index} ({classes[index]}) was unexpectedly removed by X9");
                    }

                    var expectedLevel = int.Parse(expected, CultureInfo.InvariantCulture);
                    if (result.Levels[index] != expectedLevel)
                    {
                        Fail(
                            "BidiTest",
                            lineNumber,
                            input,
                            direction,
                            $"level {index} is {result.Levels[index]}, expected {expectedLevel}; " +
                            $"actual levels [{string.Join(' ', result.Levels)}]");
                    }
                }

                if (!result.VisualOrder.SequenceEqual(expectedOrder))
                {
                    Fail(
                        "BidiTest",
                        lineNumber,
                        input,
                        direction,
                        $"order [{string.Join(' ', result.VisualOrder)}], expected [{string.Join(' ', expectedOrder)}]");
                }
            }
        }

        Assert.Equal(770_241, cases);
    }

    [Fact]
    public void PassesUnicodeBidiCharacterTest()
    {
        var cases = 0;
        using var reader = OpenFixture("BidiCharacterTest.txt.gz");
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#') continue;

            var fields = trimmed.Split(';');
            if (fields.Length != 5)
            {
                Fail("BidiCharacterTest", lineNumber, trimmed, $"expected five fields, found {fields.Length}");
            }

            var text = BuildText(fields[0], lineNumber);
            var direction = fields[1].Trim() switch
            {
                "0" => BidiParagraphDirection.LeftToRight,
                "1" => BidiParagraphDirection.RightToLeft,
                "2" => BidiParagraphDirection.Auto,
                var value => throw new XunitException(
                    $"Unknown paragraph direction '{value}' at BidiCharacterTest line {lineNumber}."),
            };
            var expectedParagraphLevel = int.Parse(fields[2], CultureInfo.InvariantCulture);
            var expectedLevels = SplitFields(fields[3]);
            var expectedOrder = SplitFields(fields[4])
                .Select(value => int.Parse(value, CultureInfo.InvariantCulture))
                .ToArray();

            cases++;
            var result = BidiResolver.Resolve(text, direction);
            if (result.ParagraphLevel != expectedParagraphLevel)
            {
                Fail(
                    "BidiCharacterTest",
                    lineNumber,
                    text,
                    direction,
                    $"paragraph level {result.ParagraphLevel}, expected {expectedParagraphLevel}");
            }

            if (result.Levels.Length != expectedLevels.Length)
            {
                Fail(
                    "BidiCharacterTest",
                    lineNumber,
                    text,
                    direction,
                    $"resolved {result.Levels.Length} levels, expected {expectedLevels.Length}");
            }

            for (var index = 0; index < expectedLevels.Length; index++)
            {
                var expected = expectedLevels[index];
                if (expected == "x")
                {
                    if (!result.Removed[index])
                    {
                        Fail(
                            "BidiCharacterTest",
                            lineNumber,
                            text,
                            direction,
                            $"character {index} was expected to be removed by X9");
                    }

                    continue;
                }

                if (result.Removed[index])
                {
                    Fail(
                        "BidiCharacterTest",
                        lineNumber,
                        text,
                        direction,
                        $"character {index} was unexpectedly removed by X9");
                }

                var expectedLevel = int.Parse(expected, CultureInfo.InvariantCulture);
                if (result.Levels[index] != expectedLevel)
                {
                    Fail(
                        "BidiCharacterTest",
                        lineNumber,
                        text,
                        direction,
                        $"level {index} is {result.Levels[index]}, expected {expectedLevel}; " +
                        $"actual levels [{string.Join(' ', result.Levels)}]");
                }
            }

            if (!result.VisualOrder.SequenceEqual(expectedOrder))
            {
                Fail(
                    "BidiCharacterTest",
                    lineNumber,
                    text,
                    direction,
                    $"order [{string.Join(' ', result.VisualOrder)}], expected [{string.Join(' ', expectedOrder)}]");
            }
        }

        Assert.Equal(91_707, cases);
    }

    private static void AssertFixtureChecksum(string name, string expected)
    {
        using var stream = new GZipStream(
            File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Conformance", name)),
            CompressionMode.Decompress);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        Assert.Equal(expected, actual);
    }

    private static StreamReader OpenFixture(string name) =>
        new(
            new GZipStream(
                File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Conformance", name)),
                CompressionMode.Decompress),
            Encoding.UTF8);

    private static string[] SplitFields(string value) =>
        value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    private static string BuildText(string codePoints, int lineNumber)
    {
        var builder = new StringBuilder();
        foreach (var token in SplitFields(codePoints))
        {
            if (!int.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
            {
                throw new XunitException($"Invalid code point '{token}' at BidiCharacterTest line {lineNumber}.");
            }

            builder.Append(char.ConvertFromUtf32(codePoint));
        }

        return builder.ToString();
    }

    [DoesNotReturn]
    private static void Fail(
        string suite,
        int lineNumber,
        string input,
        BidiParagraphDirection direction,
        string message) =>
        throw new XunitException($"{suite} line {lineNumber}, {direction}, input '{input}': {message}");

    [DoesNotReturn]
    private static void Fail(string suite, int lineNumber, string input, string message) =>
        throw new XunitException($"{suite} line {lineNumber}, input '{input}': {message}");
}
