namespace BidiLint;

// Structural checks compare each culture/resource-set against its base.
// Bidi checks run on values that contain RTL script, bidi controls, or belong
// to an RTL culture. Security controls are scanned in every value.
public static class Linter
{
    public static bool IsRtl(string culture) => CultureTag.IsRtl(culture);

    internal static bool IsRtl(string culture, LintConfig config) =>
        CultureTag.IsRtl(culture) || config.RtlCultures.Contains(culture);

    public static LintResult Lint(LoadResult load, string baseCulture) =>
        Lint(load, baseCulture, LintConfig.Default);

    internal static LintResult Lint(
        LoadResult load,
        string baseCulture,
        LintConfig config)
    {
        var result = new LintResult
        {
            UnicodeVersion = UnicodeInfo.Version,
            BaseCulture = baseCulture,
            WorkingDirectory = load.WorkingDirectory,
            ConfigPath = config.ConfigPath,
        };
        result.Documents.AddRange(load.Documents);
        result.Diagnostics.AddRange(load.Diagnostics);

        if (load.Documents.Count == 0)
        {
            var message = "No locale files were found. Expected culture-named JSON, .resx, Android res/values XML, Apple .strings, gettext .po/.pot, or Flutter .arb.";
            if (load.CandidateFiles.Count > 0)
            {
                var candidates = string.Join(
                    ", ",
                    load.CandidateFiles
                        .Take(10)
                        .Select(path => Path.GetRelativePath(load.WorkingDirectory, path).Replace('\\', '/')));
                message += $" Candidate files: {candidates}.";
            }

            result.Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.NoLocaleFiles,
                DiagnosticSeverity.Error,
                message,
                Culture: baseCulture));
            Sort(result);
            return result;
        }

        var sets = LocaleSet.Build(load.Documents, result.Diagnostics);
        result.KeyCount = sets.Sum(set => set.Entries.Count);

        if (!sets.Any(set => CultureEquals(set.Culture, baseCulture)))
        {
            var discovered = string.Join(
                ", ",
                load.Documents
                    .Select(document => document.Culture)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(culture => culture, StringComparer.Ordinal));
            result.Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.BaseCultureMissing,
                DiagnosticSeverity.Error,
                $"Base culture \"{baseCulture}\" was not found. Discovered cultures: {discovered}.",
                Culture: baseCulture));
            Sort(result);
            return result;
        }

        var baseSets = sets
            .Where(set => CultureEquals(set.Culture, baseCulture))
            .ToDictionary(set => set.ResourceSet, StringComparer.Ordinal);

        foreach (var set in sets)
        {
            if (!baseSets.TryGetValue(set.ResourceSet, out var baseSet))
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.BaseCultureMissing,
                    DiagnosticSeverity.Error,
                    $"No {baseCulture} base exists for resource set \"{set.ResourceSet}\"; this culture cannot be compared.",
                    set.PrimaryDocument.FilePath,
                    Culture: set.Culture));
            }
            else if (!CultureEquals(set.Culture, baseCulture))
            {
                CompareToBase(result, baseSet, set, config);
            }

            CheckValues(result, set, config);
        }

        Sort(result);
        return result;
    }

    private static void CompareToBase(
        LintResult result,
        LocaleSet baseSet,
        LocaleSet set,
        LintConfig config)
    {
        var basePluralGroups = BuildPluralGroups(baseSet);
        var targetPluralGroups = BuildPluralGroups(set);

        ComparePluralGroups(
            result,
            baseSet,
            set,
            basePluralGroups,
            targetPluralGroups,
            config);

        foreach (var key in baseSet.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var baseEntry = baseSet.Entries[key];
            var basePlural = baseEntry.Plural;
            PluralGroup? targetGroup = null;
            var targetGroupExists = basePlural is not null &&
                targetPluralGroups.TryGetValue(
                    (baseEntry.Document.Format, basePlural.GroupKey),
                    out targetGroup);

            if (!set.Entries.TryGetValue(key, out var entry))
            {
                if (targetGroupExists)
                {
                    // A group may legitimately have fewer categories in the
                    // target culture. Named Android categories still require
                    // the CLDR-mandated "other" branch.
                    if (basePlural!.Kind == PluralFormKind.Category &&
                        string.Equals(basePlural.Form, "other", StringComparison.OrdinalIgnoreCase) &&
                        targetGroup!.OtherEntry is null)
                    {
                        AddMissingKey(result, baseSet, set, key, baseEntry);
                    }

                    continue;
                }

                AddMissingKey(result, baseSet, set, key, baseEntry);
                continue;
            }

            // Plural placeholder parity is checked once for the whole group.
            if (basePlural is not null) continue;

            if (string.IsNullOrWhiteSpace(entry.Value)) continue;

            if (config.PlaceholderDialect == PlaceholderDialect.None) continue;

            var baseMessage = PlaceholderParser.Parse(
                baseEntry.Value,
                baseSet.PrimaryDocument.Format,
                config.PlaceholderDialect);
            var targetMessage = PlaceholderParser.Parse(
                entry.Value,
                set.PrimaryDocument.Format,
                config.PlaceholderDialect);
            if (baseMessage.HasIssues || targetMessage.HasIssues) continue;

            var comparison = PlaceholderComparer.Compare(baseMessage, targetMessage);
            if (comparison.IsMatch) continue;

            var code = comparison.Kind == PlaceholderDifferenceKind.Type
                ? DiagnosticCodes.PlaceholderType
                : DiagnosticCodes.PlaceholderMismatch;
            var verb = comparison.Kind == PlaceholderDifferenceKind.Type
                ? "Placeholder type or format differs"
                : "Placeholders differ";
            var targetSpan = comparison.TargetToken is { } targetToken
                ? MapSpan(entry, targetToken.Start, targetToken.Length)
                : entry.Span;
            var baseSpan = comparison.BaseToken is { } baseToken
                ? MapSpan(baseEntry, baseToken.Start, baseToken.Length)
                : baseEntry.Span;

            result.Diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                $"{verb} from {baseSet.Culture}: expected [{comparison.Expected}], found [{comparison.Found}].",
                entry.Document.FilePath,
                targetSpan,
                set.Culture,
                key,
                new[]
                {
                    new RelatedDiagnostic(
                        "Base value.",
                        baseEntry.Document.FilePath,
                        baseSpan,
                        key),
                }));
        }

        foreach (var key in set.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (baseSet.Entries.ContainsKey(key)) continue;
            var entry = set.Entries[key];
            if (entry.Plural is { } targetPlural &&
                basePluralGroups.ContainsKey((entry.Document.Format, targetPlural.GroupKey)))
            {
                continue;
            }

            result.Diagnostics.Add(new Diagnostic(
                DiagnosticCodes.ExtraKey,
                DiagnosticSeverity.Warning,
                $"Key does not exist in {baseSet.Culture}; it may be dead text.",
                entry.Document.FilePath,
                entry.KeySpan ?? entry.Span,
                set.Culture,
                key));
        }
    }

    private static void AddMissingKey(
        LintResult result,
        LocaleSet baseSet,
        LocaleSet set,
        string key,
        LocaleEntry baseEntry)
    {
        result.Diagnostics.Add(new Diagnostic(
            DiagnosticCodes.MissingKey,
            DiagnosticSeverity.Error,
            $"Key exists in {baseSet.Culture} but is missing here.",
            set.PrimaryDocument.FilePath,
            Culture: set.Culture,
            Key: key,
            Related: new[]
            {
                new RelatedDiagnostic(
                    "Base value.",
                    baseEntry.Document.FilePath,
                    baseEntry.KeySpan ?? baseEntry.Span,
                    key),
            }));
    }

    private static Dictionary<(LocaleFormat Format, string GroupKey), PluralGroup> BuildPluralGroups(
        LocaleSet set)
    {
        var groups = new Dictionary<(LocaleFormat, string), PluralGroup>();
        foreach (var entry in set.Entries.Values)
        {
            if (entry.Plural is not { } plural) continue;

            var groupKey = (entry.Document.Format, plural.GroupKey);
            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = new PluralGroup(plural.GroupKey);
                groups.Add(groupKey, group);
            }

            group.Entries.Add(entry);
        }

        return groups;
    }

    private static void ComparePluralGroups(
        LintResult result,
        LocaleSet baseSet,
        LocaleSet set,
        Dictionary<(LocaleFormat Format, string GroupKey), PluralGroup> baseGroups,
        Dictionary<(LocaleFormat Format, string GroupKey), PluralGroup> targetGroups,
        LintConfig config)
    {
        if (config.PlaceholderDialect == PlaceholderDialect.None) return;

        foreach (var (groupKey, baseGroup) in baseGroups
                     .OrderBy(pair => pair.Key.GroupKey, StringComparer.Ordinal))
        {
            if (!targetGroups.TryGetValue(groupKey, out var targetGroup)) continue;

            // A named Android group missing "other" already has a MissingKey
            // diagnostic. Comparing the remaining branches as a union would
            // add a second, misleading placeholder finding.
            if (baseGroup.OtherEntry is not null && targetGroup.OtherEntry is null)
                continue;

            var baseMessages = ParseGroup(baseGroup, config);
            var targetMessages = ParseGroup(targetGroup, config);
            if (baseMessages.Count == 0 || targetMessages.Count == 0) continue;
            if (baseMessages.Any(parsed => parsed.Message.HasIssues) ||
                targetMessages.Any(parsed => parsed.Message.HasIssues))
            {
                // PlaceholderSyntax is reported per value; comparing a
                // partially parsed group would add noise.
                continue;
            }

            var comparison = PlaceholderComparer.ComparePluralGroup(
                baseMessages.Select(parsed => parsed.Message).ToList(),
                targetMessages.Select(parsed => parsed.Message).ToList());
            if (comparison.IsMatch) continue;

            var targetEntry = FindEntry(targetMessages, comparison.TargetToken) ??
                              targetGroup.Representative;
            var baseEntry = FindEntry(baseMessages, comparison.BaseToken) ??
                            baseGroup.Representative;
            var code = comparison.Kind == PlaceholderDifferenceKind.Type
                ? DiagnosticCodes.PlaceholderType
                : DiagnosticCodes.PlaceholderMismatch;
            var verb = comparison.Kind == PlaceholderDifferenceKind.Type
                ? "Plural placeholder type or format differs"
                : "Plural placeholders differ";
            var targetSpan = comparison.TargetToken is { } targetToken &&
                             FindEntry(targetMessages, targetToken) is { } targetTokenEntry
                ? MapSpan(targetTokenEntry, targetToken.Start, targetToken.Length)
                : targetEntry.Span;
            var baseSpan = comparison.BaseToken is { } baseToken &&
                           FindEntry(baseMessages, baseToken) is { } baseTokenEntry
                ? MapSpan(baseTokenEntry, baseToken.Start, baseToken.Length)
                : baseEntry.Span;

            result.Diagnostics.Add(new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                $"{verb} from {baseSet.Culture}: expected [{comparison.Expected}], found [{comparison.Found}].",
                targetEntry.Document.FilePath,
                targetSpan,
                set.Culture,
                targetEntry.Key,
                new[]
                {
                    new RelatedDiagnostic(
                        "Base plural value.",
                        baseEntry.Document.FilePath,
                        baseSpan,
                        baseEntry.Key),
                }));
        }
    }

    private static List<ParsedGroupEntry> ParseGroup(PluralGroup group, LintConfig config)
    {
        var parsed = new List<ParsedGroupEntry>();
        foreach (var entry in group.Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entry.Value)) continue;

            parsed.Add(new ParsedGroupEntry(
                entry,
                PlaceholderParser.Parse(
                    entry.Value,
                    entry.Document.Format,
                    config.PlaceholderDialect)));
        }

        return parsed;
    }

    private static LocaleEntry? FindEntry(
        IReadOnlyList<ParsedGroupEntry> entries,
        PlaceholderToken? token)
    {
        if (token is null) return null;

        foreach (var parsed in entries)
        {
            if (ContainsToken(parsed.Message.Tokens, token))
                return parsed.Entry;
        }

        return null;
    }

    private static bool ContainsToken(
        IReadOnlyList<PlaceholderToken> tokens,
        PlaceholderToken target)
    {
        foreach (var token in tokens)
        {
            if (ReferenceEquals(token, target)) return true;
            if (token.Branches is null) continue;

            foreach (var branch in token.Branches)
            {
                if (ContainsToken(branch.Tokens, target)) return true;
            }
        }

        return false;
    }

    private static void CheckValues(LintResult result, LocaleSet set, LintConfig config)
    {
        foreach (var entry in set.Entries.Values.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.EmptyValue,
                    DiagnosticSeverity.Warning,
                    "Value is empty or whitespace.",
                    entry.Document.FilePath,
                    entry.Span,
                    set.Culture,
                    entry.Key));
                continue;
            }

            if (config.PlaceholderDialect != PlaceholderDialect.None &&
                PlaceholderParser.MightContain(entry.Value))
            {
                var placeholderMessage = PlaceholderParser.Parse(
                    entry.Value,
                    set.PrimaryDocument.Format,
                    config.PlaceholderDialect);
                foreach (var issue in placeholderMessage.Issues)
                {
                    result.Diagnostics.Add(new Diagnostic(
                        DiagnosticCodes.PlaceholderSyntax,
                        DiagnosticSeverity.Error,
                        issue.Message,
                        entry.Document.FilePath,
                        MapSpan(entry, issue.Start, issue.Length),
                        set.Culture,
                        entry.Key));
                }
            }

            var scan = BidiScanner.Scan(entry.Value);
            var rtlCulture = IsRtl(set.Culture, config);
            var needsBidi = rtlCulture || scan.HasRtlScript || scan.HasBidiControls;
            var needsNormalization = NormalizationAnalyzer.MightContainFindings(entry.Value);
            var needsSecurity = SecurityAnalyzer.MightContainFindings(entry.Value);
            if (!needsBidi && !needsNormalization && !needsSecurity) continue;

            var content = ContentMask.Build(
                entry.Value,
                config.IsolationWrappers,
                out var markup);
            var findings = new List<ValueFinding>();
            if (needsBidi)
            {
                findings.AddRange(NumeralAnalyzer.Analyze(content, config.NumeralPolicy));
                findings.AddRange(IsolationAnalyzer.Analyze(
                    entry.Value,
                    set.Culture,
                    rtlCulture,
                    config,
                    scan,
                    content,
                    markup));
            }

            if (needsNormalization)
                findings.AddRange(NormalizationAnalyzer.Analyze(entry.Value, content));

            if (needsSecurity)
                findings.AddRange(SecurityAnalyzer.Analyze(content, rtlCulture));

            foreach (var finding in findings)
            {
                result.Diagnostics.Add(new Diagnostic(
                    finding.Code,
                    finding.Severity,
                    finding.Message,
                    entry.Document.FilePath,
                    MapSpan(entry, finding),
                    set.Culture,
                    entry.Key,
                    Bidi: finding.Bidi));
            }
        }
    }

    private static SourceSpan? MapSpan(LocaleEntry entry, ValueFinding finding) =>
        MapSpan(entry, finding.Start, finding.Length);

    private static SourceSpan? MapSpan(LocaleEntry entry, int start, int length)
    {
        if (entry.ValueMap is not null)
        {
            try
            {
                return entry.ValueMap.Map(start, length);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Fall through to the whole value when a decoder and its map
                // disagree; a broad span is better than a missing diagnostic.
            }
        }

        return entry.Span;
    }

    private static void Sort(LintResult result) =>
        result.Diagnostics.Sort(DiagnosticOrder.ByLocation);

    private static bool CultureEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record ParsedGroupEntry(
        LocaleEntry Entry,
        PlaceholderMessage Message);

    private sealed class PluralGroup
    {
        public PluralGroup(string groupKey)
        {
            GroupKey = groupKey;
        }

        public string GroupKey { get; }
        public List<LocaleEntry> Entries { get; } = new();

        public LocaleEntry? OtherEntry => Entries.FirstOrDefault(entry =>
            entry.Plural?.Kind == PluralFormKind.Category &&
            string.Equals(entry.Plural.Form, "other", StringComparison.OrdinalIgnoreCase));

        public LocaleEntry Representative =>
            OtherEntry ?? Entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).First();
    }

    private sealed class LocaleSet
    {
        private LocaleSet(string culture, string resourceSet, LocaleDocument primary)
        {
            Culture = culture;
            ResourceSet = resourceSet;
            PrimaryDocument = primary;
        }

        public string Culture { get; }
        public string ResourceSet { get; }
        public LocaleDocument PrimaryDocument { get; }
        public Dictionary<string, LocaleEntry> Entries { get; } = new(StringComparer.Ordinal);

        public static List<LocaleSet> Build(
            IEnumerable<LocaleDocument> documents,
            List<Diagnostic> diagnostics)
        {
            var sets = new Dictionary<(string Culture, string ResourceSet), LocaleSet>(
                new SetKeyComparer());

            foreach (var document in documents)
            {
                var key = (document.Culture, document.ResourceSet);
                if (!sets.TryGetValue(key, out var set))
                {
                    set = new LocaleSet(document.Culture, document.ResourceSet, document);
                    sets.Add(key, set);
                }

                foreach (var entry in document.Entries.Values.OrderBy(e => e.Key, StringComparer.Ordinal))
                {
                    if (set.Entries.TryGetValue(entry.Key, out var existing))
                    {
                        diagnostics.Add(new Diagnostic(
                            DiagnosticCodes.KeyCollision,
                            DiagnosticSeverity.Error,
                            $"Key \"{entry.Key}\" appears in more than one file for {document.Culture}/{document.ResourceSet}. The first value is used.",
                            entry.Document.FilePath,
                            entry.KeySpan ?? entry.Span,
                            document.Culture,
                            entry.Key,
                            new[]
                            {
                                new RelatedDiagnostic(
                                    "First value for this key.",
                                    existing.Document.FilePath,
                                    existing.KeySpan ?? existing.Span,
                                    entry.Key),
                            }));
                        continue;
                    }

                    set.Entries.Add(entry.Key, entry);
                }
            }

            return sets.Values
                .OrderBy(set => set.Culture, StringComparer.Ordinal)
                .ThenBy(set => set.ResourceSet, StringComparer.Ordinal)
                .ToList();
        }

        private sealed class SetKeyComparer : IEqualityComparer<(string Culture, string ResourceSet)>
        {
            public bool Equals((string Culture, string ResourceSet) x, (string Culture, string ResourceSet) y) =>
                string.Equals(x.Culture, y.Culture, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.ResourceSet, y.ResourceSet, StringComparison.Ordinal);

            public int GetHashCode((string Culture, string ResourceSet) obj) =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Culture),
                    StringComparer.Ordinal.GetHashCode(obj.ResourceSet));
        }
    }
}
