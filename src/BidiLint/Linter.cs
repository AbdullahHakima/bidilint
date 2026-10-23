namespace BidiLint;

// Structural checks compare each culture/resource-set against its base.
// Bidi checks run on values that contain RTL script or belong to an RTL culture.
public static class Linter
{
    public static bool IsRtl(string culture) => CultureTag.IsRtl(culture);

    public static LintResult Lint(LoadResult load, string baseCulture)
    {
        var result = new LintResult
        {
            BaseCulture = baseCulture,
            WorkingDirectory = load.WorkingDirectory,
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
                CompareToBase(result, baseSet, set);
            }

            CheckValues(result, set);
        }

        Sort(result);
        return result;
    }

    private static void CompareToBase(LintResult result, LocaleSet baseSet, LocaleSet set)
    {
        foreach (var key in baseSet.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var baseEntry = baseSet.Entries[key];
            if (!set.Entries.TryGetValue(key, out var entry))
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
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Value)) continue;

            var basePlaceholders = BidiScanner.Placeholders(baseEntry.Value);
            var placeholders = BidiScanner.Placeholders(entry.Value);
            if (!basePlaceholders.SequenceEqual(placeholders))
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.PlaceholderMismatch,
                    DiagnosticSeverity.Error,
                    $"Placeholders differ from {baseSet.Culture}: expected [{string.Join(", ", basePlaceholders)}], found [{string.Join(", ", placeholders)}].",
                    entry.Document.FilePath,
                    entry.Span,
                    set.Culture,
                    key,
                    new[]
                    {
                        new RelatedDiagnostic(
                            "Base value.",
                            baseEntry.Document.FilePath,
                            baseEntry.Span,
                            key),
                    }));
            }
        }

        foreach (var key in set.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (baseSet.Entries.ContainsKey(key)) continue;
            var entry = set.Entries[key];
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

    private static void CheckValues(LintResult result, LocaleSet set)
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

            var scan = BidiScanner.Scan(entry.Value);
            var rtlCulture = IsRtl(set.Culture);
            if (!rtlCulture && !scan.HasRtlScript) continue;

            if (scan.HasRtlScript)
            {
                foreach (var run in scan.UnisolatedLatinRuns)
                {
                    result.Diagnostics.Add(new Diagnostic(
                        DiagnosticCodes.UnisolatedLatin,
                        DiagnosticSeverity.Warning,
                        $"Latin run \"{run}\" is not wrapped in bidi isolates; it can render in the wrong order inside RTL text. Wrap it (FSI..PDI) or confirm the UI isolates it.",
                        entry.Document.FilePath,
                        entry.Span,
                        set.Culture,
                        entry.Key));
                }
            }

            if ((scan.HasRtlScript || rtlCulture) &&
                scan.HasWesternDigits &&
                scan.HasArabicIndicDigits)
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.MixedNumerals,
                    DiagnosticSeverity.Warning,
                    "Mixes Western digits (0-9) and Arabic-Indic digits (٠-٩) in one string; pick one numeral system per string.",
                    entry.Document.FilePath,
                    entry.Span,
                    set.Culture,
                    entry.Key));
            }

            if (scan.UnclosedIsolates > 0)
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.UnbalancedIsolates,
                    DiagnosticSeverity.Error,
                    $"{scan.UnclosedIsolates} bidi isolate opener(s) without a closing PDI; everything after the opener inherits the isolation.",
                    entry.Document.FilePath,
                    entry.Span,
                    set.Culture,
                    entry.Key));
            }

            if (scan.StrayClosers > 0)
            {
                result.Diagnostics.Add(new Diagnostic(
                    DiagnosticCodes.UnbalancedIsolates,
                    DiagnosticSeverity.Error,
                    $"{scan.StrayClosers} PDI closer(s) without a matching isolate opener.",
                    entry.Document.FilePath,
                    entry.Span,
                    set.Culture,
                    entry.Key));
            }
        }
    }

    private static void Sort(LintResult result) =>
        result.Diagnostics.Sort(DiagnosticOrder.ByLocation);

    private static bool CultureEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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
