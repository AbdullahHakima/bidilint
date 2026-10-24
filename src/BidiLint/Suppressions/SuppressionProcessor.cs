namespace BidiLint;

// Applies config severity, reasoned ignores, and the baseline after linting.
// Suppressed findings leave the visible diagnostic list but remain available
// for the JSON report so every waiver is auditable.
internal static class SuppressionProcessor
{
    public static void Apply(
        LintResult result,
        LintConfig config,
        string workingDirectory,
        string? baselinePath,
        bool updateBaseline,
        DateOnly today)
    {
        var normalized = new List<Diagnostic>();
        var suppressed = new List<SuppressedDiagnostic>();

        foreach (var diagnostic in result.Diagnostics)
        {
            var normalizedDiagnostic = Normalize(diagnostic, config);
            if (normalizedDiagnostic is not null) normalized.Add(normalizedDiagnostic);
        }

        var compiledIgnores = config.Ignore
            .Select(rule => new CompiledIgnore(rule, config.ConfigDirectory ?? workingDirectory, today))
            .ToList();

        var afterIgnores = new List<Diagnostic>();
        foreach (var diagnostic in normalized)
        {
            var match = compiledIgnores.FirstOrDefault(ignore => ignore.Matches(diagnostic));
            if (match is null)
            {
                afterIgnores.Add(diagnostic);
                continue;
            }

            match.Used = true;
            suppressed.Add(new SuppressedDiagnostic(
                diagnostic,
                "ignore",
                match.Rule.Reason,
                Fingerprint: null));
        }

        var visible = afterIgnores;
        var staleBaseline = new List<Diagnostic>();
        if (!string.IsNullOrWhiteSpace(baselinePath))
        {
            result.BaselinePath = baselinePath;
            if (updateBaseline)
            {
                var entries = BuildBaselineEntries(afterIgnores);
                BaselineStore.Write(baselinePath, entries);
                result.BaselineUpdated = true;
                result.BaselineEntryCount = entries.Count;
            }
            else
            {
                var baselineEntries = BaselineStore.Load(baselinePath);
                result.BaselineEntryCount = baselineEntries.Count;
                visible = ApplyBaseline(afterIgnores, baselineEntries, suppressed);
                staleBaseline = BuildStaleBaselineDiagnostics(afterIgnores, baselineEntries, baselinePath);
            }
        }
        else if (updateBaseline)
        {
            throw new ConfigException(
                "No baseline path was configured. Pass --baseline <path> or set \"baseline\" in bidilint.json.");
        }

        foreach (var ignore in compiledIgnores.Where(ignore => !ignore.Used))
            AddStaleIgnoreDiagnostic(ignore, config);

        result.Diagnostics.Clear();
        result.Diagnostics.AddRange(visible);
        result.Diagnostics.AddRange(staleBaseline
            .Select(diagnostic => Normalize(diagnostic, config))
            .Where(diagnostic => diagnostic is not null)
            .Select(diagnostic => diagnostic!));
        result.Diagnostics.AddRange(compiledIgnores
            .SelectMany(ignore => ignore.StaleDiagnostics)
            .Select(diagnostic => Normalize(diagnostic, config))
            .Where(diagnostic => diagnostic is not null)
            .Select(diagnostic => diagnostic!));
        result.Suppressed.Clear();
        result.Suppressed.AddRange(suppressed);
        result.Diagnostics.Sort(DiagnosticOrder.ByLocation);
    }

    private static Diagnostic? Normalize(Diagnostic diagnostic, LintConfig config)
    {
        if (!RuleCatalog.TryGet(diagnostic.Code, out var metadata))
        {
            return config.Rules.TryGetValue(diagnostic.Code, out var configured) &&
                   configured.Disabled
                ? null
                : diagnostic;
        }

        if (metadata.Configurable &&
            config.Rules.TryGetValue(diagnostic.Code, out var rule) &&
            rule.Disabled)
        {
            return null;
        }

        var configuredRule = metadata.Configurable &&
                             config.Rules.TryGetValue(diagnostic.Code, out var candidate)
            ? candidate
            : null;

        var severity = configuredRule?.Severity ?? metadata.DefaultSeverity;

        return diagnostic with
        {
            Severity = severity,
            HelpUri = diagnostic.HelpUri ?? metadata.HelpUri,
        };
    }

    private static List<Diagnostic> ApplyBaseline(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<BaselineEntry> baselineEntries,
        List<SuppressedDiagnostic> suppressed)
    {
        var expected = baselineEntries
            .Select(entry => entry.Fingerprint)
            .ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var occurrences = new Dictionary<(string Rule, string Culture, string Key), int>();
        var visible = new List<Diagnostic>();

        foreach (var diagnostic in diagnostics.OrderBy(diagnostic => diagnostic, DiagnosticOrder.ByLocation))
        {
            if (!IsBaselineEligible(diagnostic))
            {
                visible.Add(diagnostic);
                continue;
            }

            var fingerprint = NextFingerprint(diagnostic, occurrences);
            if (!expected.Contains(fingerprint) || !used.Add(fingerprint))
            {
                visible.Add(diagnostic);
                continue;
            }

            suppressed.Add(new SuppressedDiagnostic(
                diagnostic,
                "baseline",
                Reason: null,
                fingerprint));
        }

        return visible;
    }

    private static List<Diagnostic> BuildStaleBaselineDiagnostics(
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyList<BaselineEntry> baselineEntries,
        string baselinePath)
    {
        var occurrences = new Dictionary<(string Rule, string Culture, string Key), int>();
        var current = new HashSet<string>(StringComparer.Ordinal);

        foreach (var diagnostic in diagnostics
                     .Where(IsBaselineEligible)
                     .OrderBy(diagnostic => diagnostic, DiagnosticOrder.ByLocation))
        {
            current.Add(NextFingerprint(diagnostic, occurrences));
        }

        var stale = new List<Diagnostic>();
        foreach (var entry in baselineEntries)
        {
            if (current.Contains(entry.Fingerprint)) continue;
            stale.Add(new Diagnostic(
                DiagnosticCodes.StaleBaseline,
                DiagnosticSeverity.Warning,
                $"Baseline entry for {entry.Rule} ({entry.Culture}, {entry.Key}, occurrence {entry.Occurrence}) no longer matches a current finding. Remove it or update the baseline.",
                baselinePath,
                Culture: entry.Culture,
                Key: entry.Key));
        }

        return stale;
    }

    private static List<BaselineEntry> BuildBaselineEntries(IEnumerable<Diagnostic> diagnostics)
    {
        var occurrences = new Dictionary<(string Rule, string Culture, string Key), int>();
        var entries = new List<BaselineEntry>();

        foreach (var diagnostic in diagnostics
                     .Where(IsBaselineEligible)
                     .OrderBy(diagnostic => diagnostic, DiagnosticOrder.ByLocation))
        {
            var key = OccurrenceKey(diagnostic);
            occurrences.TryGetValue(key, out var occurrence);
            occurrence++;
            occurrences[key] = occurrence;

            entries.Add(new BaselineEntry(
                BaselineFingerprint.Compute(diagnostic.Code, diagnostic.Culture!, diagnostic.Key!, occurrence),
                diagnostic.Code,
                diagnostic.Culture!,
                diagnostic.Key!,
                occurrence));
        }

        return entries;
    }

    private static string NextFingerprint(
        Diagnostic diagnostic,
        Dictionary<(string Rule, string Culture, string Key), int> occurrences)
    {
        var key = OccurrenceKey(diagnostic);
        occurrences.TryGetValue(key, out var occurrence);
        occurrence++;
        occurrences[key] = occurrence;
        return BaselineFingerprint.Compute(
            diagnostic.Code,
            diagnostic.Culture!,
            diagnostic.Key!,
            occurrence);
    }

    private static (string Rule, string Culture, string Key) OccurrenceKey(Diagnostic diagnostic) =>
        (diagnostic.Code, diagnostic.Culture!.ToLowerInvariant(), diagnostic.Key!);

    internal static bool IsBaselineEligible(Diagnostic diagnostic) =>
        !string.IsNullOrEmpty(diagnostic.Culture) &&
        !string.IsNullOrEmpty(diagnostic.Key) &&
        RuleCatalog.TryGet(diagnostic.Code, out var metadata) &&
        metadata.BaselineAllowed;

    private static void AddStaleIgnoreDiagnostic(CompiledIgnore ignore, LintConfig config)
    {
        var selectors = ignore.Describe();
        var message = ignore.IsExpired
            ? $"Ignore rule ({selectors}) expired on {ignore.Rule.Expires:yyyy-MM-dd} and no longer suppresses findings. Remove it. Reason: {ignore.Rule.Reason}"
            : $"Ignore rule ({selectors}) did not suppress any findings. Remove it. Reason: {ignore.Rule.Reason}";

        ignore.StaleDiagnostics.Add(new Diagnostic(
            DiagnosticCodes.StaleSuppression,
            DiagnosticSeverity.Warning,
            message,
            config.ConfigPath));
    }

    private sealed class CompiledIgnore
    {
        private readonly GlobPattern? _path;
        private readonly GlobPattern? _key;
        private readonly string _root;

        public CompiledIgnore(IgnoreRule rule, string root, DateOnly today)
        {
            Rule = rule;
            _root = root;
            _path = rule.PathPattern is null ? null : GlobPattern.Create(rule.PathPattern);
            _key = rule.KeyPattern is null ? null : GlobPattern.Create(rule.KeyPattern);
            IsExpired = rule.Expires is not null && rule.Expires.Value < today;
        }

        public IgnoreRule Rule { get; }
        public bool Used { get; set; }
        public bool IsExpired { get; }
        public List<Diagnostic> StaleDiagnostics { get; } = new();

        public bool Matches(Diagnostic diagnostic)
        {
            if (IsExpired) return false;
            if (!RuleCatalog.TryGet(diagnostic.Code, out var metadata) || !metadata.Configurable)
                return false;
            if (Rule.Rule is not null &&
                !string.Equals(Rule.Rule, diagnostic.Code, StringComparison.Ordinal))
            {
                return false;
            }

            if (Rule.Culture is not null &&
                !string.Equals(Rule.Culture, diagnostic.Culture, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_key is not null &&
                (diagnostic.Key is null || !_key.IsMatch(diagnostic.Key)))
            {
                return false;
            }

            if (_path is not null)
            {
                if (diagnostic.FilePath is null) return false;
                var relative = Path.GetRelativePath(_root, diagnostic.FilePath).Replace('\\', '/');
                if (!_path.IsMatch(relative)) return false;
            }

            return true;
        }

        public string Describe()
        {
            var parts = new List<string>();
            if (Rule.Rule is not null) parts.Add($"rule={Rule.Rule}");
            if (Rule.PathPattern is not null) parts.Add($"path={Rule.PathPattern}");
            if (Rule.Culture is not null) parts.Add($"culture={Rule.Culture}");
            if (Rule.KeyPattern is not null) parts.Add($"key={Rule.KeyPattern}");
            return parts.Count == 0 ? "configuration" : string.Join(", ", parts);
        }
    }
}
