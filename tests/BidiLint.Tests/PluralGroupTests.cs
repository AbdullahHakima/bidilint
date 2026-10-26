using Xunit;

namespace BidiLint.Tests;

public class PluralGroupTests
{
    [Fact]
    public void JsonPluralArray_AllowsAdditionalFormsAndOmittedCount()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "translations": {
                "_%n folder_::_%n folders_": ["%n folder", "%n folders"]
              }
            }
            """);
        workspace.Write("ar.json", """
            {
              "translations": {
                "_%n folder_::_%n folders_": [
                  "لا توجد مجلدات",
                  "مجلد واحد",
                  "مجلدان",
                  "%n مجلدات",
                  "%n مجلد",
                  "%n مجلدات"
                ]
              }
            }
            """);

        var result = workspace.Lint();

        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void JsonPluralArray_NextcloudSettingsBranchesMayOmitCount()
    {
        using var workspace = new TestWorkspace();
        const string key = "_%n app has an update available_::_%n apps have an update available_";
        workspace.Write("en_GB.json", $$"""
            { "translations": { "{{key}}": ["%n app has an update available", "%n apps have an update available"] } }
            """);
        workspace.Write("fa.json", $$"""
            { "translations": { "{{key}}": ["۱ کاره به‌روز رسانی دارد", "%n کاره به‌روز رسانی دارند"] } }
            """);
        workspace.Write("he.json", $$"""
            { "translations": { "{{key}}": ["לאחד היישומונים יש עדכון זמין", "ל־%n יישומונים יש עדכון זמין", "ל־%n יישומונים יש עדכון זמין"] } }
            """);

        var result = workspace.Lint("en_GB");

        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void JsonPluralArray_DroppedPlaceholderAcrossEveryBranch_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "translations": {
                "_%n folder_::_%n folders_": ["%n folder", "%n folders"]
              }
            }
            """);
        workspace.Write("ar.json", """
            {
              "translations": {
                "_%n folder_::_%n folders_": ["مجلدات", "مجلدات", "مجلدات"]
              }
            }
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);

        Assert.Contains("expected [%n]", finding.Message, StringComparison.Ordinal);
        Assert.Contains("found []", finding.Message, StringComparison.Ordinal);
        Assert.NotNull(finding.Span);
        Assert.NotNull(finding.Related);
    }

    [Fact]
    public void JsonPluralArray_FormatDifference_IsAPlaceholderType()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "translations": {
                "_%s folder_::_%s folders_": ["%s folder", "%s folders"]
              }
            }
            """);
        workspace.Write("ar.json", """
            {
              "translations": {
                "_%s folder_::_%s folders_": ["%d مجلد", "%d مجلدات"]
              }
            }
            """);

        var result = workspace.Lint();

        Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderType);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void JsonPluralArray_ExtraBranchPlaceholder_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            {
              "translations": {
                "_%n file_::_%n files_": ["%n file", "%n files"]
              }
            }
            """);
        workspace.Write("ar.json", """
            {
              "translations": {
                "_%n file_::_%n files_": ["%n ملف", "%n ملفات", "%n ملفات {name}"]
              }
            }
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);

        Assert.Contains("expected [%n]", finding.Message, StringComparison.Ordinal);
        Assert.Contains("found [%n, {name}]", finding.Message, StringComparison.Ordinal);
        Assert.EndsWith("[2]", finding.Key, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericJsonArray_ExtraTargetItem_IsStillAnExtraKey()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """{ "colors": ["red", "green"] }""");
        workspace.Write("ar.json", """{ "colors": ["أحمر", "أخضر", "أزرق"] }""");

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.ExtraKey);

        Assert.Equal("colors[2]", finding.Key);
    }

    [Fact]
    public void AndroidPluralGroup_AllowsAdditionalCategoriesAndOmittedCount()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("res/values/strings.xml", """
            <resources>
              <plurals name="messages">
                <item quantity="one">%d message</item>
                <item quantity="other">%d messages</item>
              </plurals>
            </resources>
            """);
        workspace.Write("res/values-ar/strings.xml", """
            <resources>
              <plurals name="messages">
                <item quantity="zero">لا رسائل</item>
                <item quantity="one">رسالة واحدة</item>
                <item quantity="two">رسالتان</item>
                <item quantity="few">%d رسائل</item>
                <item quantity="many">%d رسالة</item>
                <item quantity="other">%d رسالة</item>
              </plurals>
            </resources>
            """);

        var result = workspace.Lint();

        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void AndroidPluralGroup_MissingOther_IsAMissingKey()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("res/values/strings.xml", """
            <resources>
              <plurals name="messages">
                <item quantity="one">%d message</item>
                <item quantity="other">%d messages</item>
              </plurals>
            </resources>
            """);
        workspace.Write("res/values-ar/strings.xml", """
            <resources>
              <plurals name="messages">
                <item quantity="one">%d رسالة</item>
              </plurals>
            </resources>
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.MissingKey);

        Assert.Equal("messages[other]", finding.Key);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.ExtraKey);
    }

    [Fact]
    public void AndroidStringArray_ExtraTargetItem_IsStillAnExtraKey()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("res/values/strings.xml", """
            <resources>
              <string-array name="colors">
                <item>red</item>
                <item>green</item>
              </string-array>
            </resources>
            """);
        workspace.Write("res/values-ar/strings.xml", """
            <resources>
              <string-array name="colors">
                <item>أحمر</item>
                <item>أخضر</item>
                <item>أزرق</item>
              </string-array>
            </resources>
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.ExtraKey);

        Assert.Equal("colors[2]", finding.Key);
    }

    [Fact]
    public void PoPluralGroup_AllowsAdditionalFormsAndOmittedCount()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locale/en/LC_MESSAGES/messages.po", """
            msgid ""
            msgstr ""
            "Content-Type: text/plain; charset=UTF-8\n"

            msgid "%n file"
            msgid_plural "%n files"
            msgstr[0] "%n file"
            msgstr[1] "%n files"
            """);
        workspace.Write("locale/ar/LC_MESSAGES/messages.po", """
            msgid ""
            msgstr ""
            "Content-Type: text/plain; charset=UTF-8\n"

            msgid "%n file"
            msgid_plural "%n files"
            msgstr[0] "لا ملفات"
            msgstr[1] "ملف واحد"
            msgstr[2] "ملفان"
            msgstr[3] "%n ملفات"
            msgstr[4] "%n ملف"
            msgstr[5] "%n ملفات"
            """);

        var result = workspace.Lint();

        AssertNoPluralStructuralFindings(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.PlaceholderMismatch or DiagnosticCodes.PlaceholderType);
    }

    [Fact]
    public void PoPluralGroup_DroppedPlaceholderAcrossEveryForm_IsReported()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locale/en/LC_MESSAGES/messages.po", """
            msgid "%n file"
            msgid_plural "%n files"
            msgstr[0] "%n file"
            msgstr[1] "%n files"
            """);
        workspace.Write("locale/ar/LC_MESSAGES/messages.po", """
            msgid "%n file"
            msgid_plural "%n files"
            msgstr[0] "ملفات"
            msgstr[1] "ملف"
            """);

        var result = workspace.Lint();
        var finding = Assert.Single(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);

        Assert.Contains("expected [%n]", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluralBranchText_StillGetsBidiAnalysis()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "translations": { "_%n file_::_%n files_": ["%n file", "%n files"] } }
            """);
        workspace.Write("ar.json", """
            { "translations": { "_%n file_::_%n files_": ["%n ملف", "%n Cancel جديد", "%n ملفات"] } }
            """);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.UnisolatedLatin);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void PluralEmptyBranch_StillGetsEmptyValue()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "translations": { "_%n file_::_%n files_": ["%n file", "%n files"] } }
            """);
        workspace.Write("ar.json", """
            { "translations": { "_%n file_::_%n files_": ["", "%n ملفات"] } }
            """);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.EmptyValue);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    [Fact]
    public void PluralSyntaxIssue_StillGetsPlaceholderSyntaxWithoutGroupMismatch()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("en.json", """
            { "translations": { "_%n file_::_%n files_": ["%n file", "%n files"] } }
            """);
        workspace.Write("ar.json", """
            { "translations": { "_%n file_::_%n files_": ["{0", "ملفات"] } }
            """);

        var result = workspace.Lint();

        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderSyntax);
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code == DiagnosticCodes.PlaceholderMismatch);
    }

    private static void AssertNoPluralStructuralFindings(LintResult result)
    {
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Code is DiagnosticCodes.MissingKey or DiagnosticCodes.ExtraKey);
    }
}
