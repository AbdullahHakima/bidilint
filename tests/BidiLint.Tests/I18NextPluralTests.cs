using Xunit;

namespace BidiLint.Tests;

public class I18NextPluralTests
{
    [Fact]
    public void JsonI18NextPluralSuffix_AllowsAdditionalTargetCategories()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ar.json", """
            {
              "item_zero": "لا عناصر",
              "item_one": "عنصر واحد",
              "item_two": "عنصران",
              "item_few": "{{count}} عناصر",
              "item_many": "{{count}} عنصرًا",
              "item_other": "{{count}} عنصر"
            }
            """);

        var result = workspace.Lint();

        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_NestedGroupIsPaired()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "cart": {
                "item_one": "{{count}} item",
                "item_other": "{{count}} items"
              }
            }
            """);
        workspace.Write("ar.json", """
            {
              "cart": {
                "item_zero": "لا عناصر",
                "item_one": "عنصر واحد",
                "item_two": "عنصران",
                "item_few": "{{count}} عناصر",
                "item_many": "{{count}} عنصرًا",
                "item_other": "{{count}} عنصر"
              }
            }
            """);

        var load = workspace.Load();
        var en = Assert.Single(load.Documents, document => document.Culture == "en");
        Assert.Equal("cart.item", en.Entries["cart.item_one"].Plural!.GroupKey);
        Assert.Equal("one", en.Entries["cart.item_one"].Plural!.Form);
        Assert.Equal("cart.item", en.Entries["cart.item_other"].Plural!.GroupKey);

        var result = workspace.Lint();
        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_MissingOtherIsStillReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ar.json", """
            {
              "item_one": "عنصر واحد"
            }
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.MissingKey);

        Assert.Equal("item_other", finding.Key);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.ExtraKey);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_BranchSpecificCountOmissionIsAccepted()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ar.json", """
            {
              "item_one": "عنصر واحد",
              "item_other": "{{count}} عناصر"
            }
            """);

        var result = workspace.Lint();

        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_CountDroppedFromEveryBranchIsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ar.json", """
            {
              "item_one": "عنصر واحد",
              "item_other": "عناصر"
            }
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);

        Assert.Contains("expected [{{count}}]", finding.Message, StringComparison.Ordinal);
        Assert.Contains("found []", finding.Message, StringComparison.Ordinal);
        Assert.NotNull(finding.Span);
        Assert.NotNull(finding.Related);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_ExtraCategoryWithWrongPlaceholderIsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ar.json", """
            {
              "item_zero": "{{wrong}} لا عناصر",
              "item_one": "{{count}} عنصر واحد",
              "item_other": "{{count}} عناصر"
            }
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);

        Assert.Contains("{{wrong}}", finding.Message, StringComparison.Ordinal);
        Assert.Equal("item_zero", finding.Key);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_MissingGroupStillChecksSurvivingBranchPlaceholders()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ar.json", """
            {
              "item_one": "{{wrong}} عنصر واحد"
            }
            """);

        var result = workspace.Lint();

        var missing = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.MissingKey);
        Assert.Equal("item_other", missing.Key);

        var mismatch = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
        Assert.Equal("item_one", mismatch.Key);
        Assert.Contains("{{wrong}}", mismatch.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_NonStringOtherDoesNotFormAGroup()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ar.json", """
            {
              "item_one": "{{wrong}} عنصر واحد",
              "item_other": 42
            }
            """);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.MissingKey && diagnostic.Key == "item_other");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch && diagnostic.Key == "item_one");
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.NonStringValue && diagnostic.Key == "item_other");
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.ExtraKey);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_OneFormTargetWithWrongPlaceholderIsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ja.json", """
            {
              "item_other": "{{wrong}} 個"
            }
            """);

        var result = workspace.Lint();

        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
        Assert.Equal("item_other", finding.Key);
        Assert.Contains("{{wrong}}", finding.Message, StringComparison.Ordinal);
        AssertNoPluralStructuralFindings(result);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_RequiresOtherForNonOtherSuffixes()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "phone_one": "one phone",
              "phone_other": "phones"
            }
            """);

        var load = workspace.Load();
        var en = Assert.Single(load.Documents);
        Assert.Equal("phone", en.Entries["phone_one"].Plural!.GroupKey);
        Assert.Equal("other", en.Entries["phone_other"].Plural!.Form);

        using var loneWorkspace = new TestWorkspace();
        loneWorkspace.Write("en.json", """
            {
              "lone_one": "one"
            }
            """);
        var loneLoad = loneWorkspace.Load();
        var lone = Assert.Single(loneLoad.Documents);
        Assert.Null(lone.Entries["lone_one"].Plural);
    }

    [Fact]
    public void JsonI18NextPluralSuffix_OtherAloneIsAOneFormGroup()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "item_one": "{{count}} item",
              "item_other": "{{count}} items"
            }
            """);
        workspace.Write("ja.json", """
            {
              "item_other": "{{count}} 個"
            }
            """);

        var load = workspace.Load();
        var ja = Assert.Single(load.Documents, document => document.Culture == "ja");
        Assert.Equal("item", ja.Entries["item_other"].Plural!.GroupKey);
        Assert.Equal("other", ja.Entries["item_other"].Plural!.Form);

        var result = workspace.Lint();
        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void ArbSuffixKeys_AreNotGrouped()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("app_en.arb", """
            {
              "@@locale": "en",
              "item_one": "one item",
              "item_other": "items"
            }
            """);
        workspace.Write("app_ar.arb", """
            {
              "@@locale": "ar",
              "item_zero": "لا عناصر",
              "item_one": "عنصر واحد",
              "item_other": "عناصر"
            }
            """);

        var load = workspace.Load();
        var ar = Assert.Single(load.Documents, document => document.Culture == "ar");
        Assert.Null(ar.Entries["item_one"].Plural);
        Assert.Null(ar.Entries["item_other"].Plural);
    }

    private static void AssertNoPluralStructuralFindings(LintResult result)
    {
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.MissingKey or DiagnosticCodes.ExtraKey);
    }
}
