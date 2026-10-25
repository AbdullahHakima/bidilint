using System.Text.Json;
using Xunit;

namespace BidiLint.Tests;

public class AdoptionTests
{
    [Fact]
    public void IncludeAndExcludeFilterDiscoveredFiles()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en/main.json", """{ "a": "A" }""");
        workspace.Write("locales/ar/main.json", """{ "a": "أ" }""");
        workspace.Write("locales/ar/generated/main.json", """{ "a": "مولّد" }""");
        workspace.Write("bidilint.json", """
            {
              "include": ["locales/**/main.json"],
              "exclude": ["**/generated/**"]
            }
            """);

        var (code, stdout, stderr) = workspace.RunCli("locales", "--format", "json");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(2, document.RootElement.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void ConfiguredLayoutNamesAreDiscovered()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("copy/en/app.json", """{ "a": "A" }""");
        workspace.Write("copy/ar/app.json", """{ "a": "أ" }""");
        workspace.Write("bidilint.json", """{ "layouts": ["copy"] }""");

        var (code, stdout, stderr) = workspace.RunCli("copy", "--format", "json");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(2, document.RootElement.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void ExplicitFileMappingsLoadArbitraryNames()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("translations/english.json", """{ "a": "A" }""");
        workspace.Write("translations/arabic.json", """{ "a": "أ" }""");
        workspace.Write("bidilint.json", """
            {
              "files": [
                { "path": "translations/english.json", "culture": "en", "resourceSet": "app" },
                { "path": "translations/arabic.json", "culture": "ar", "resourceSet": "app" }
              ]
            }
            """);

        var (code, stdout, stderr) = workspace.RunCli("translations", "--format", "json");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(2, document.RootElement.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void RuleSeverityCanBeOverriddenOrTurnedOff()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        workspace.Write("bidilint.json", """{ "rules": { "MissingKey": "warning" } }""");

        var warningRun = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(ExitCodes.Ok, warningRun.Code);
        using (var document = JsonDocument.Parse(warningRun.Stdout))
        {
            Assert.Equal(1, document.RootElement.GetProperty("warnings").GetInt32());
            Assert.Equal(0, document.RootElement.GetProperty("errors").GetInt32());
        }

        var strictRun = workspace.RunCli("locales", "--strict");
        Assert.Equal(ExitCodes.Findings, strictRun.Code);

        workspace.Write("bidilint.json", """{ "rules": { "MissingKey": "off" } }""");
        var offRun = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(ExitCodes.Ok, offRun.Code);
        using var offDocument = JsonDocument.Parse(offRun.Stdout);
        Assert.Equal(0, offDocument.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Fact]
    public void ConfigRuleSeverityCanRaiseAWarningToAnError()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ", "extra": "زائد" }""");
        workspace.Write("bidilint.json", """{ "rules": { "ExtraKey": "error" } }""");

        var (code, stdout, _) = workspace.RunCli("locales", "--format", "json");

        Assert.Equal(ExitCodes.Findings, code);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(1, document.RootElement.GetProperty("errors").GetInt32());
    }

    [Fact]
    public void AllowLatinSupportsExactCaseInsensitiveAndCultureScopes()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "Hello BidiLint" }""");
        workspace.Write("locales/ar.json", """{ "a": "مرحبا BidiLint" }""");

        var baseline = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(1, CountFindings(baseline.Stdout, "UnisolatedLatin"));

        workspace.Write("bidilint.json", """{ "allowLatin": ["BidiLint"] }""");
        var exact = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(0, CountFindings(exact.Stdout, "UnisolatedLatin"));

        workspace.Write("bidilint.json", """
            { "allowLatin": [{ "term": "bidilint", "match": "case-insensitive" }] }
            """);
        var insensitive = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(0, CountFindings(insensitive.Stdout, "UnisolatedLatin"));

        workspace.Write("bidilint.json", """
            { "allowLatin": [{ "term": "BidiLint", "cultures": ["he"] }] }
            """);
        var scoped = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(1, CountFindings(scoped.Stdout, "UnisolatedLatin"));

        workspace.Write("bidilint.json", """
            { "rules": { "UnisolatedLatin": { "allow": ["BidiLint"] } } }
            """);
        var ruleAllow = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(0, CountFindings(ruleAllow.Stdout, "UnisolatedLatin"));
    }

    [Fact]
    public void IgnoreRulesNeedReasonsAndReportStaleEntries()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "legacy.one": "One", "legacy.two": "Two" }""");
        workspace.Write("locales/ar.json", """{ "legacy.one": "واحد" }""");
        workspace.Write("bidilint.json", """
            {
              "ignore": [
                {
                  "rule": "MissingKey",
                  "path": "locales/ar.json",
                  "culture": "ar",
                  "key": "legacy.*",
                  "reason": "The migration is still in progress."
                }
              ]
            }
            """);

        var suppressed = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(ExitCodes.Ok, suppressed.Code);
        using (var document = JsonDocument.Parse(suppressed.Stdout))
        {
            Assert.Equal(0, document.RootElement.GetProperty("findings").GetArrayLength());
            Assert.Equal(1, document.RootElement.GetProperty("suppressed").GetInt32());
            Assert.Equal(0, document.RootElement.GetProperty("warnings").GetInt32());
        }

        workspace.Write("bidilint.json", """
            {
              "ignore": [
                {
                  "rule": "MissingKey",
                  "key": "does-not-exist.*",
                  "reason": "Left behind after the migration."
                }
              ]
            }
            """);
        var stale = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(ExitCodes.Findings, stale.Code);
        using var staleDocument = JsonDocument.Parse(stale.Stdout);
        Assert.Equal(
            1,
            staleDocument.RootElement.GetProperty("findings")
                .EnumerateArray()
                .Count(finding => finding.GetProperty("code").GetString() == "StaleSuppression"));
    }

    [Fact]
    public void ExpiredIgnoreDoesNotSuppress()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        workspace.Write("bidilint.json", """
            {
              "ignore": [
                {
                  "rule": "MissingKey",
                  "reason": "Temporary migration waiver.",
                  "expires": "2000-01-01"
                }
              ]
            }
            """);

        var (code, stdout, _) = workspace.RunCli("locales", "--format", "json");

        Assert.Equal(ExitCodes.Findings, code);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(1, document.RootElement.GetProperty("errors").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("suppressed").GetInt32());
        Assert.Contains(
            document.RootElement.GetProperty("findings").EnumerateArray(),
            finding => finding.GetProperty("code").GetString() == "StaleSuppression");
    }

    [Fact]
    public void PathOnlyIgnoreCannotHideMalformedFiles()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": """);
        workspace.Write("bidilint.json", """
            {
              "ignore": [
                { "path": "locales/ar.json", "reason": "Attempt to hide a parse error." }
              ]
            }
            """);

        var (code, stdout, stderr) = workspace.RunCli("locales", "--format", "json");

        Assert.Equal(ExitCodes.Findings, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Contains(
            document.RootElement.GetProperty("findings").EnumerateArray(),
            finding => finding.GetProperty("code").GetString() == "MalformedFile");
        Assert.Equal(0, document.RootElement.GetProperty("suppressed").GetInt32());
    }

    [Theory]
    [InlineData("BidiOverride", "A\\u202Eb\\u202C")]
    [InlineData("InvisibleControl", "A\\uFEFFB")]
    public void SecurityRule_CanBeWaivedByTargetedReasonedIgnore(string rule, string value)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", $$"""{ "a": "{{value}}" }""");
        workspace.Write("bidilint.json", $$"""
            {
              "ignore": [
                { "rule": "{{rule}}", "reason": "Reviewed legacy content; tracked in SEC-123." }
              ]
            }
            """);

        var (code, stdout, stderr) = workspace.RunCli("locales", "--format", "json");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Equal(0, document.RootElement.GetProperty("findings").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("suppressed").GetInt32());
    }

    [Fact]
    public void BroadIgnore_DoesNotHideSecurityFindings()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "A\u202Eb\u202C" }""");
        workspace.Write("bidilint.json", """
            {
              "ignore": [
                { "path": "locales/ar.json", "reason": "Attempt to hide a security finding." }
              ]
            }
            """);

        var (code, stdout, stderr) = workspace.RunCli("locales", "--format", "json");

        Assert.Equal(ExitCodes.Findings, code);
        Assert.Equal("", stderr);
        using var document = JsonDocument.Parse(stdout);
        Assert.Contains(
            document.RootElement.GetProperty("findings").EnumerateArray(),
            finding => finding.GetProperty("code").GetString() == DiagnosticCodes.BidiOverride);
        Assert.Equal(0, document.RootElement.GetProperty("suppressed").GetInt32());
    }

    [Theory]
    [InlineData("BidiOverride", "A\\u202Eb\\u202C")]
    [InlineData("InvisibleControl", "A\\uFEFFB")]
    public void BaselineUpdate_ExcludesSecurityFindings(string rule, string value)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", $$"""{ "a": "{{value}}" }""");
        var baselinePath = Path.Combine(workspace.Root, "baseline.json");

        var (code, stdout, stderr) = workspace.RunCli(
            "locales",
            "--format", "json",
            "--baseline", baselinePath,
            "--update-baseline");

        Assert.Equal(ExitCodes.Findings, code);
        Assert.Equal("", stderr);
        using (var document = JsonDocument.Parse(stdout))
        {
            Assert.Contains(
                document.RootElement.GetProperty("findings").EnumerateArray(),
                finding => finding.GetProperty("code").GetString() == rule);
            Assert.Equal(
                0,
                document.RootElement.GetProperty("baseline").GetProperty("entries").GetInt32());
        }

        using (var baseline = JsonDocument.Parse(File.ReadAllText(baselinePath)))
        {
            Assert.Equal(0, baseline.RootElement.GetProperty("findings").GetArrayLength());
        }

        var apply = workspace.RunCli(
            "locales",
            "--format", "json",
            "--baseline", baselinePath);
        Assert.Equal(ExitCodes.Findings, apply.Code);
        using var applied = JsonDocument.Parse(apply.Stdout);
        Assert.Contains(
            applied.RootElement.GetProperty("findings").EnumerateArray(),
            finding => finding.GetProperty("code").GetString() == rule);
    }

    [Theory]
    [InlineData("BidiOverride")]
    [InlineData("InvisibleControl")]
    public void BaselineFile_RejectsSecurityEntries(string rule)
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        var fingerprint = BaselineFingerprint.Compute(rule, "ar", "a", 1);
        var baselinePath = workspace.Write("baseline.json", $$"""
            {
              "version": 1,
              "findings": [
                {
                  "fingerprint": "{{fingerprint}}",
                  "rule": "{{rule}}",
                  "culture": "ar",
                  "key": "a",
                  "occurrence": 1
                }
              ]
            }
            """);

        var (code, _, stderr) = workspace.RunCli("locales", "--baseline", baselinePath);

        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("not eligible for baselining", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void BaselineCreateApplyAndStaleEntryReporting()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        var baselinePath = Path.Combine(workspace.Root, "baseline.json");

        var create = workspace.RunCli(
            "locales",
            "--format", "json",
            "--baseline", baselinePath,
            "--update-baseline");
        Assert.Equal(ExitCodes.Ok, create.Code);
        Assert.True(File.Exists(baselinePath));
        using (var created = JsonDocument.Parse(File.ReadAllText(baselinePath)))
        {
            Assert.Equal(1, created.RootElement.GetProperty("findings").GetArrayLength());
        }

        var apply = workspace.RunCli(
            "locales",
            "--format", "json",
            "--baseline", baselinePath);
        Assert.Equal(ExitCodes.Ok, apply.Code);
        using (var applied = JsonDocument.Parse(apply.Stdout))
        {
            Assert.Equal(0, applied.RootElement.GetProperty("findings").GetArrayLength());
            Assert.Equal(1, applied.RootElement.GetProperty("suppressed").GetInt32());
            Assert.Equal(
                1,
                applied.RootElement.GetProperty("baseline").GetProperty("entries").GetInt32());
        }

        workspace.Write("locales/ar.json", """{ "a": "أ", "b": "ب" }""");
        var stale = workspace.RunCli(
            "locales",
            "--format", "json",
            "--baseline", baselinePath);
        Assert.Equal(ExitCodes.Ok, stale.Code);
        using var staleDocument = JsonDocument.Parse(stale.Stdout);
        Assert.Contains(
            staleDocument.RootElement.GetProperty("findings").EnumerateArray(),
            finding => finding.GetProperty("code").GetString() == "StaleBaseline");
    }

    [Fact]
    public void BaselineDoesNotHideMalformedFiles()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        var baselinePath = Path.Combine(workspace.Root, "baseline.json");
        var create = workspace.RunCli(
            "locales",
            "--baseline", baselinePath,
            "--update-baseline");
        Assert.Equal(ExitCodes.Ok, create.Code);

        workspace.Write("locales/ar.json", """{ "a": """);
        var apply = workspace.RunCli(
            "locales",
            "--format", "json",
            "--baseline", baselinePath);

        Assert.Equal(ExitCodes.Findings, apply.Code);
        using var document = JsonDocument.Parse(apply.Stdout);
        Assert.Contains(
            document.RootElement.GetProperty("findings").EnumerateArray(),
            finding => finding.GetProperty("code").GetString() == "MalformedFile");
    }

    [Fact]
    public void UpdateBaselineStillFailsOnUnbaselineableDiagnostics()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": """);
        var baselinePath = Path.Combine(workspace.Root, "baseline.json");

        var (code, stdout, _) = workspace.RunCli(
            "locales",
            "--format", "json",
            "--baseline", baselinePath,
            "--update-baseline");

        Assert.Equal(ExitCodes.Findings, code);
        Assert.True(File.Exists(baselinePath));
        using var document = JsonDocument.Parse(stdout);
        Assert.True(document.RootElement.GetProperty("baseline").GetProperty("updated").GetBoolean());
        Assert.Contains(
            document.RootElement.GetProperty("findings").EnumerateArray(),
            finding => finding.GetProperty("code").GetString() == "MalformedFile");
    }

    [Fact]
    public void UpdateBaselineDoesNotWriteWhenTheBaseCultureIsMissing()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        var baselinePath = Path.Combine(workspace.Root, "baseline.json");

        var (code, _, stderr) = workspace.RunCli(
            "locales",
            "--base", "en",
            "--baseline", baselinePath,
            "--update-baseline");

        Assert.Equal(ExitCodes.NoInput, code);
        Assert.False(File.Exists(baselinePath));
        Assert.Contains("baseline was not updated", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateBaselineDoesNotWriteAfterAnInputPathError()
    {
        using var workspace = new TestWorkspace();
        var baselinePath = Path.Combine(workspace.Root, "baseline.json");

        var (code, _, stderr) = workspace.RunCli(
            "missing",
            "--baseline", baselinePath,
            "--update-baseline");

        Assert.Equal(ExitCodes.Input, code);
        Assert.False(File.Exists(baselinePath));
        Assert.Contains("baseline was not updated", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingMalformedAndTamperedBaselinesAreUsageErrors()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");

        var missing = workspace.RunCli(
            "locales",
            "--baseline", Path.Combine(workspace.Root, "missing.json"));
        Assert.Equal(ExitCodes.Usage, missing.Code);
        Assert.Contains("Baseline file not found", missing.Stderr, StringComparison.Ordinal);

        var malformedPath = workspace.Write("malformed.json", """{ "version": 1, """);
        var malformed = workspace.RunCli("locales", "--baseline", malformedPath);
        Assert.Equal(ExitCodes.Usage, malformed.Code);
        Assert.Contains("malformed.json", malformed.Stderr, StringComparison.Ordinal);

        var tamperedPath = Path.Combine(workspace.Root, "tampered.json");
        var create = workspace.RunCli(
            "locales",
            "--baseline", tamperedPath,
            "--update-baseline");
        Assert.Equal(ExitCodes.Ok, create.Code);
        var tampered = File.ReadAllText(tamperedPath)
            .Replace("\"rule\": \"MissingKey\"", "\"rule\": \"ExtraKey\"", StringComparison.Ordinal);
        File.WriteAllText(tamperedPath, tampered);

        var tamperedRun = workspace.RunCli("locales", "--baseline", tamperedPath);
        Assert.Equal(ExitCodes.Usage, tamperedRun.Code);
        Assert.Contains("fingerprint does not match", tamperedRun.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateBaselineDefaultsToTheWorkingDirectory()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");

        var (code, _, _) = workspace.RunCli("locales", "--update-baseline");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.True(File.Exists(Path.Combine(workspace.Root, "bidilint-baseline.json")));
    }

    [Fact]
    public void ConfigBaselineIsAppliedAndCanBeDisabled()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        workspace.Write("bidilint.json", """
            { "baseline": "suppressions/baseline.json" }
            """);

        var create = workspace.RunCli("locales", "--update-baseline");
        Assert.Equal(ExitCodes.Ok, create.Code);
        var baselinePath = Path.Combine(workspace.Root, "suppressions", "baseline.json");
        Assert.True(File.Exists(baselinePath));

        var applied = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(ExitCodes.Ok, applied.Code);
        using (var document = JsonDocument.Parse(applied.Stdout))
        {
            Assert.Equal(0, document.RootElement.GetProperty("findings").GetArrayLength());
            Assert.Equal(1, document.RootElement.GetProperty("suppressed").GetInt32());
        }

        var disabled = workspace.RunCli("locales", "--no-baseline", "--format", "json");
        Assert.Equal(ExitCodes.Findings, disabled.Code);
        Assert.Equal(1, CountFindings(disabled.Stdout, "MissingKey"));
    }

    [Fact]
    public void FailOnAndMaxWarningsControlTheExitCode()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ", "extra": "زائد" }""");

        Assert.Equal(ExitCodes.Ok, workspace.RunCli("locales").Code);
        Assert.Equal(ExitCodes.Findings, workspace.RunCli("locales", "--strict").Code);
        Assert.Equal(
            ExitCodes.Findings,
            workspace.RunCli("locales", "--fail-on", "warning").Code);
        Assert.Equal(
            ExitCodes.Findings,
            workspace.RunCli("locales", "--max-warnings", "0").Code);
        Assert.Equal(
            ExitCodes.Ok,
            workspace.RunCli("locales", "--fail-on", "none", "--max-warnings", "1").Code);
    }

    [Fact]
    public void ConfigMaxWarningsAndFailOnAreApplied()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ", "extra": "زائد" }""");
        workspace.Write("bidilint.json", """
            { "output": { "maxWarnings": 0 } }
            """);

        Assert.Equal(ExitCodes.Findings, workspace.RunCli("locales").Code);

        workspace.Write("bidilint.json", """
            { "failOn": "warning" }
            """);
        Assert.Equal(ExitCodes.Findings, workspace.RunCli("locales").Code);
    }

    [Fact]
    public void StrictCannotConflictWithAnExplicitFailOn()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");

        var (code, _, stderr) = workspace.RunCli("locales", "--strict", "--fail-on", "error");

        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("same threshold", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void QuietHidesNonErrorsButKeepsTheSummary()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ", "extra": "زائد" }""");

        var (code, stdout, _) = workspace.RunCli("locales", "--quiet");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.DoesNotContain("ExtraKey", stdout, StringComparison.Ordinal);
        Assert.Contains("1 warning(s)", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigOutputAndBaseCultureAreOverriddenByCli()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "shared": "S", "enOnly": "E" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ", "shared": "س" }""");
        workspace.Write("bidilint.json", """
            { "baseCulture": "ar", "output": { "format": "json", "quiet": true } }
            """);

        var configRun = workspace.RunCli("locales");
        Assert.Equal(ExitCodes.Ok, configRun.Code);
        using (var document = JsonDocument.Parse(configRun.Stdout))
        {
            var extra = document.RootElement.GetProperty("findings")
                .EnumerateArray()
                .Single(finding => finding.GetProperty("code").GetString() == "ExtraKey");
            Assert.Equal("en", extra.GetProperty("culture").GetString());
        }

        var cliRun = workspace.RunCli("locales", "--base", "en");
        Assert.Equal(ExitCodes.Findings, cliRun.Code);
        using var cliDocument = JsonDocument.Parse(cliRun.Stdout);
        var cliMissing = cliDocument.RootElement.GetProperty("findings")
            .EnumerateArray()
            .Single(finding => finding.GetProperty("code").GetString() == "MissingKey");
        Assert.Equal("ar", cliMissing.GetProperty("culture").GetString());
    }

    [Fact]
    public void NoConfigUsesBuiltInDefaults()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        workspace.Write("bidilint.json", """{ "rules": { "MissingKey": "off" } }""");

        var configured = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(0, CountFindings(configured.Stdout, "MissingKey"));

        var defaults = workspace.RunCli("locales", "--no-config", "--format", "json");
        Assert.Equal(1, CountFindings(defaults.Stdout, "MissingKey"));
    }

    [Fact]
    public void DebugPrintsTheResolvedConfigAndThresholds()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        workspace.Write("bidilint.json", """{ "failOn": "warning" }""");

        var (code, _, stderr) = workspace.RunCli("locales", "--debug");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Contains("debug: config", stderr, StringComparison.Ordinal);
        Assert.Contains("fail-on warning", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void InitWritesAStarterThatParsesAndRefusesToOverwrite()
    {
        using var workspace = new TestWorkspace();

        var first = workspace.RunCli("--init");
        Assert.Equal(ExitCodes.Ok, first.Code);
        var path = Path.Combine(workspace.Root, "bidilint.json");
        Assert.True(File.Exists(path));
        Assert.Empty(ConfigLoader.Load(path).Errors);

        var second = workspace.RunCli("--init");
        Assert.Equal(ExitCodes.Usage, second.Code);
        Assert.Contains("already exists", second.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void InitHonorsConfigPathAndDirectory()
    {
        using var workspace = new TestWorkspace();

        var nested = workspace.RunCli("--init", "--config", "config/bidilint.json");
        Assert.Equal(ExitCodes.Ok, nested.Code);
        Assert.True(File.Exists(Path.Combine(workspace.Root, "config", "bidilint.json")));

        var directory = Path.Combine(workspace.Root, "existing");
        Directory.CreateDirectory(directory);
        var inDirectory = workspace.RunCli("--init", "--config", directory);
        Assert.Equal(ExitCodes.Ok, inDirectory.Code);
        Assert.True(File.Exists(Path.Combine(directory, "bidilint.json")));
    }

    [Fact]
    public void ConfigCanBePassedAsADirectory()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A", "b": "B" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");
        workspace.Write("config/bidilint.json", """{ "rules": { "MissingKey": "off" } }""");

        var (code, stdout, _) = workspace.RunCli("locales", "--config", "config", "--format", "json");

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Equal(0, CountFindings(stdout, "MissingKey"));
    }

    [Fact]
    public void InvalidConfigReturnsUsageExitCode()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("bidilint.json", """{ "baseCulture": "not-a-culture" }""");

        var (code, _, stderr) = workspace.RunCli("locales");

        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("baseCulture", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleDiscoveredConfigsAreAUsageError()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("one/locales/en.json", """{ "a": "A" }""");
        workspace.Write("one/bidilint.json", """{ "baseCulture": "en" }""");
        workspace.Write("two/locales/en.json", """{ "a": "A" }""");
        workspace.Write("two/bidilint.json", """{ "baseCulture": "en" }""");

        var (code, _, stderr) = workspace.RunCli("one/locales", "two/locales");

        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("Multiple bidilint.json", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void NumeralPolicyAndPlaceholderDialectAreConfigurable()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "Value {0}" }""");
        workspace.Write("locales/ar.json", """{ "a": "قيمة ١2" }""");

        var mixed = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(1, CountFindings(mixed.Stdout, "MixedNumerals"));
        Assert.Equal(1, CountFindings(mixed.Stdout, "PlaceholderMismatch"));

        workspace.Write("bidilint.json", """
            {
              "numeralPolicy": "allow",
              "placeholderDialects": ["none"]
            }
            """);
        var configured = workspace.RunCli("locales", "--format", "json");
        Assert.Equal(0, CountFindings(configured.Stdout, "MixedNumerals"));
        Assert.Equal(0, CountFindings(configured.Stdout, "PlaceholderMismatch"));
    }

    [Fact]
    public void ExternalIsolationSuppressesTheUnisolatedLatinRule()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "Hello BidiLint" }""");
        workspace.Write("locales/ar.json", """{ "a": "مرحبا BidiLint" }""");

        Assert.Equal(
            1,
            CountFindings(workspace.RunCli("locales", "--format", "json").Stdout, "UnisolatedLatin"));

        workspace.Write("bidilint.json", """{ "isolationStyle": "external" }""");
        Assert.Equal(
            0,
            CountFindings(workspace.RunCli("locales", "--format", "json").Stdout, "UnisolatedLatin"));
    }

    [Fact]
    public void NoColorAndNoColorEnvironmentAreAccepted()
    {
        using var workspace = new TestWorkspace();
        workspace.Write("locales/en.json", """{ "a": "A" }""");
        workspace.Write("locales/ar.json", """{ "a": "أ" }""");

        var flag = workspace.RunCli("locales", "--no-color");
        Assert.Equal(ExitCodes.Ok, flag.Code);
        Assert.DoesNotContain("\u001b[", flag.Stdout, StringComparison.Ordinal);

        var environment = workspace.RunCli(
            new Dictionary<string, string> { ["NO_COLOR"] = "1" },
            "locales",
            "--color");
        Assert.Equal(ExitCodes.Ok, environment.Code);
        Assert.DoesNotContain("\u001b[", environment.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void ColorDecisionUsesCliThenConfigAndHonorsNoColor()
    {
        Assert.True(Cli.ColorDecision(cliColor: null, configColor: null, noColorEnvironment: false));
        Assert.False(Cli.ColorDecision(cliColor: null, configColor: false, noColorEnvironment: false));
        Assert.True(Cli.ColorDecision(cliColor: true, configColor: false, noColorEnvironment: false));
        Assert.False(Cli.ColorDecision(cliColor: false, configColor: true, noColorEnvironment: false));
        Assert.False(Cli.ColorDecision(cliColor: true, configColor: true, noColorEnvironment: true));
    }

    private static int CountFindings(string json, string code)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("findings")
            .EnumerateArray()
            .Count(finding => finding.GetProperty("code").GetString() == code);
    }
}
