using System.Text.Json;
using Xunit;

namespace BidiLint.Tests;

public class DocumentationTests
{
    [Fact]
    public void RuleDocumentationMatchesTheCatalog()
    {
        var expected = Normalize(RuleCatalog.ToMarkdown());
        var actual = Normalize(File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, "docs", "rules", "README.md")));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JsonSchemaIsValidAndListsEveryRootKey()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, "schema", "bidilint.schema.json")));
        var root = document.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        var schemaKeys = root.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var loaderKeys = ConfigLoader.RootPropertyNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(loaderKeys, schemaKeys);
    }

    [Fact]
    public void JsonSchemaListsExactlyTheConfigurableRules()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, "schema", "bidilint.schema.json")));
        var schemaRules = document.RootElement
            .GetProperty("properties")
            .GetProperty("rules")
            .GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var catalogRules = RuleCatalog.All
            .Where(rule => rule.Configurable)
            .Select(rule => rule.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogRules, schemaRules);
    }

    [Fact]
    public void ConformanceDocumentationRecordsTheSuiteAndExclusions()
    {
        var text = File.ReadAllText(
            Path.Combine(TestPaths.RepositoryRoot, "docs", "conformance.md"));

        Assert.Contains("770,241", text, StringComparison.Ordinal);
        Assert.Contains("91,707", text, StringComparison.Ordinal);
        Assert.Contains("## Exclusions", text, StringComparison.Ordinal);
        Assert.Contains("## Implemented scope", text, StringComparison.Ordinal);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
}
