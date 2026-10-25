# BidiLint Roadmap

> **From a working prototype to a production localization linter teams keep in CI.**
>
> Current release: **0.6.0** (2026-10-26) · Target: **1.0.0** · Last reviewed: 2026-10-26

---

## 1. What BidiLint is for

BidiLint catches localization defects that compilers and JSON validators never see:

- Arabic/Hebrew text where a Latin brand, URL, code term, or version number
  renders in the wrong visual order.
- A sentence that mixes Western digits and Arabic-Indic digits.
- A translation that dropped `{1}`, renamed `{name}`, or broke an ICU plural.
- A missing key, an empty string, or dead text left behind after a redesign.
- Invisible bidi controls that are unbalanced, or worse, used to hide text
  (Trojan Source).

The audience is developers and localization engineers shipping RTL products:
.NET teams with `.resx`, web teams with JSON/i18next, and mobile teams with
Android resources, Flutter ARB, gettext, or Apple `.strings`.

The product promise is narrow and testable:

> **Point BidiLint at a locale tree. Get a short, trustworthy list of defects
> that would reach users, each with a file, a position, and a fix.**

### What "production-ready" means here

Production is not "more rules". It means all of the following are true:

1. **Trustworthy** — malformed files, unreadable paths, and unsupported layouts
   produce explicit diagnostics. Nothing is silently skipped.
2. **Actionable** — every finding identifies the file, line, column, culture,
   key, and the exact span; comparison findings link to the base string.
3. **Adoptable** — zero-config works on common layouts; config, baselines, and
   suppressions make a legacy repository green without hiding new defects.
4. **Correct** — bidi analysis is based on Unicode data, not a regex guess;
   placeholder analysis understands the formats teams actually use.
5. **CI-native** — stable exit codes, SARIF/GitHub output, fast runs, and a
   published package that installs on a clean machine.
6. **Maintainable** — format adapters and rules are isolated, documented,
   tested on every PR, and versioned with a compatibility policy.
7. **Useful on day one** — a first run on a real repository finds at least one
   real bug and produces no more noise than the team can reasonably triage.

---

## 2. Baseline: what exists today

### Shipped

| Version | Scope |
| --- | --- |
| 0.1.0 | JSON locales, nested-key flattening, missing/extra/empty/placeholder checks, Arabic bidi checks, `--strict`, sample project |
| 0.2.0 | `.resx` support, JSON + resx merge per culture, `--format json`, resx sample, NuGet release workflow |
| 0.3.0 | Diagnostics with source spans, BCP-47 discovery, malformed/duplicate/collision handling, safe discovery, testable CLI, stable exit codes, Hebrew/RTL script detection, CI and package hardening |
| 0.4.0 | Format adapters for JSON culture dirs, Android, Apple `.strings`, gettext PO/POT, and Flutter ARB; layout-aware resource sets; format-specific diagnostics and fixtures |
| 0.5.0 | JSONC config and schema, custom layouts/files, rule metadata and generated docs, reasoned ignores, baselines, CI thresholds, quiet/color/debug output |
| 0.6.0 | Unicode 17.0.0 bidi data and UAX #9 resolver, script/culture direction detection, isolation and control rewrite, numeral/normalization rules, Trojan Source security rules, RTL fixture corpus, golden outputs, and a 100k-entry CI benchmark |

Verified on 2026-10-26: `dotnet build`, `dotnet test`, and `dotnet format
--verify-no-changes` succeed; **366 tests pass**; the storefront sample reports
4 errors and 5 warnings; the resx sample reports 1 error and 2 warnings; the
packed tool installs and runs in CI. Six locale formats now share one
span-aware document model. The 0.6 engine passes the full Unicode 17.0.0
`BidiTest` and `BidiCharacterTest` suites, and the 100k-entry benchmark
completes inside the CI budget.

### Honest audit after 0.6.0

The foundation, the common format adapters, the adoption layer, and the bidi
engine are in place. The remaining gaps below drive the next phases.

| Area | After 0.6.0 | Remaining gap |
| --- | --- | --- |
| Discovery | JSON culture files/directories, `.resx` sets, Android `res/values*`, Apple `.lproj`, gettext PO/POT, ARB; generated folders and symlink loops skipped | YAML/i18next variants and richer config discovery (1.1) |
| Load failures | Malformed JSON/XML/PO/.strings, bad encodings, duplicate keys, and flattening collisions produce positioned diagnostics | Size limits, partial-parse recovery, and diagnostics for every ignored implicit candidate |
| Provenance | Per-document entries with source spans; format-specific parser positions; comparison findings link to the base; bidi findings map decoded values back to source ranges where the format allows it | Snippets, SARIF, and edit ranges for fixes |
| Text output | Relative `path:line:column`, code, key, message, related base location, quiet/color modes, suppression counts | Snippets, SARIF/GitHub annotations, output files, and MSBuild/JUnit formats |
| Placeholders | Braced-token comparison against the base; configurable dialect gate | Escaped braces, ICU plural/select, printf, i18next, Angular, and format-aware validation (0.7) |
| Bidi checks | Script-aware RTL detection; UAX #9 resolved levels; span-aware isolation, control, depth, number-adjacency, mixed-numeral, presentation-form, NFC, and tatweel rules; escaped logical/run-level explanations in text and JSON; built-in mitigation recognition; Trojan Source and invisible-control security rules with reasoned waivers | SARIF (0.8) |
| Culture model | BCP-47 validation, explicit layouts, neutral-resource mapping to `--base`, config overrides and custom layout names | Base auto-detection when neutral resources are ambiguous, and custom tags (1.1) |
| Config | `bidilint.json`/JSONC, published schema, discovery, `--config`, `--no-config`, `--init`, include/exclude, layouts/files, rules, terms, RTL cultures, isolation wrappers, output policy | Path-scoped rule severity overrides (0.8), YAML config |
| Baseline | SHA-256 baseline create/update/apply, stale-entry reporting, tamper validation, security-rule exclusion and rejection | Baseline merge workflows |
| Integration | Text and JSON schema 1.0 with file/range/format/resourceSet/related fields; config and suppressed findings in JSON | SARIF, GitHub annotations, output file, and editor fixes |
| CI | PR matrix (Linux/macOS/Windows), build/test/format/pack/install smoke on both samples, Dependabot | Coverage gate, CodeQL, and release provenance/attestations |
| Packaging | Version single-source, symbols/snupkg, deterministic build, repository metadata, schema and rule docs in the package | SourceLink verification, signed packages, and package-validation baseline |
| Docs | README covers every adapter, config, suppressions, baselines, and limitations; rule catalog is generated and synchronized by tests | Migration guide and a docs site |

### The first five trust bugs

These were the prerequisites before any new format:

1. Surface malformed/unreadable files as diagnostics instead of skipping them.
   **Done in 0.3.0.**
2. Stop treating arbitrary JSON filenames as cultures; validate BCP-47 and
   support explicit file/layout mappings. **Done for supported layouts in
   0.3.0; more layouts in 0.4.0.**
3. Add file, line, column, and span provenance to every finding. **Done in
   0.3.0.**
4. Replace regex-only placeholders with format-aware parsers. **Still open;
   0.7.0.**
5. Make bidi analysis script-aware (Arabic, Hebrew, Syriac, Thaana, N'Ko, and
   other RTL scripts), not Arabic-only. **Done in 0.6.0.**

---

## 3. Product principles

These principles settle scope arguments during implementation.

1. **No silent skips.** Every file that looks like a candidate is either
   linted or produces a diagnostic explaining why it was not.
2. **A finding without a location is a bug.** Value-level rules report the
   value span; run-level rules report the run; missing keys link to the base.
3. **False positives are product bugs.** Every rule ships with negative
   fixtures, an allowlist mechanism, and a documented rationale. When in
   doubt, prefer a precise warning over a broad error.
4. **Zero-config first, config for scale.** A standard JSON/resx tree must
   work with one command. Config exists to describe non-standard layouts,
   legacy debt, and team policy — not to make the tool start.
5. **Fix only the mechanical cases.** Never auto-translate, auto-delete a
   key, or guess a numeral system. Fixes must be idempotent and preserve
   formatting.
6. **Stable machine contracts.** Rule codes, exit codes, the JSON report
   schema, and the config schema are versioned. Renames require aliases and a
   deprecation window.
7. **Unicode correctness over heuristics.** Use code points and generated
   Unicode tables. A regex may be a fast path, never the source of truth.
8. **Security is a first-class lint category.** Bidi overrides and invisible
   controls are not just rendering issues; they can hide code and text.
9. **The tool must be boring in CI.** Deterministic ordering, bounded
   runtime, no network, no telemetry, and no stack traces for user errors.
10. **Every phase ends with a demo.** A phase is not done until a real
    repository (or a realistic fixture) demonstrates the new capability.

---

## 4. Definition of done for 1.0

BidiLint 1.0 ships only when this checklist is green.

### Correctness and trust

- [ ] Malformed JSON/XML/resx/ARB/PO/strings files produce a `MalformedFile`
      error with a path and position; they never disappear silently.
- [ ] Duplicate keys, cross-file key collisions, and merged-resource conflicts
      are reported with both origins.
- [ ] Every finding has rule code, severity, culture, key, file, line, column,
      end position, and a help URL.
- [ ] Comparison findings include a related location in the base file.
- [ ] Unreadable directories, invalid encodings, broken symlinks, and oversized
      files are handled without a crash.
- [ ] Output is deterministic across runs, operating systems, and file-system
      enumeration order.

### Format coverage (Tier 1 for 1.0)

- [ ] JSON: flat, nested, arrays, JSONC, duplicate keys, culture-file and
      culture-directory layouts.
- [ ] `.resx`: neutral/satellite pairing per resource set, typed resources
      skipped, comments and CDATA preserved.
- [ ] Android `strings.xml`: `values`/`values-*` qualifiers, `<string>`,
      `<string-array>`, `<plurals>`, `translatable="false"`, `%1$s` args.
- [ ] Flutter ARB: `@@locale`, `@key` metadata, ICU messages and plural forms.
- [ ] gettext PO/POT: `msgctxt`, plurals, fuzzy/untranslated entries, headers.
- [ ] Apple `.strings`: `.lproj` cultures, UTF-8/UTF-16, escapes, comments.
- [ ] YAML/TOML locale files (read-only) are planned for 1.1, not 1.0.

### Rule coverage

- [ ] Structural: missing/extra/empty keys, duplicate keys, malformed files,
      untranslated values, whitespace/newline drift.
- [ ] Placeholders: .NET composite, ICU MessageFormat, printf, i18next,
      Angular, Rails, Python, Handlebars; syntax, set, type, and plural
      category checks.
- [ ] Markup: HTML/XML tag parity and attribute placeholder parity.
- [ ] Bidi: script-aware isolation, legacy controls, mixed numerals, isolate
      balance/depth, number adjacency, presentation forms, invisible controls,
      Trojan Source sequences.
- [ ] Every rule has a catalog page with a bad example, a good example, the
      default severity, and a suppression example.

### Workflow and output

- [ ] `--version`, `--help` in any position, `--`, multiple paths, and file
      arguments.
- [ ] Text output with relative path, line, column, code frame, and escaped
      invisible characters.
- [ ] JSON schema v1 with `schemaVersion`, file/range, related locations,
      fixes, summary by rule and culture.
- [ ] SARIF 2.1.0, GitHub annotations, MSBuild, and JUnit formatters.
- [ ] `--output`, `--quiet`, `--max-warnings`, `--fail-on`, `--no-color`,
      `NO_COLOR`, and `--debug`.
- [ ] Config discovery, `--config`, `--no-config`, and `--init`.
- [ ] Baseline create/update/apply with stale-entry reporting.
- [ ] Inline/file suppressions with reasons and optional expiry.
- [ ] Safe `--fix` (isolation only at 1.0) with `--diff`/`--check`, atomic
      writes, and idempotence tests.

### Engineering and release

- [ ] PR CI on Linux, macOS, and Windows; .NET 8 and .NET 10; build with
      warnings as errors; format check; coverage gate.
- [ ] Unit, integration, CLI end-to-end, golden-output, fuzz, and conformance
      tests.
- [ ] Bidi engine validated against Unicode `BidiTest.txt` and
      `BidiCharacterTest.txt` for the documented subset.
- [ ] Pack/install/run smoke test on a clean machine; `dotnet tool install`
      works from the produced `.nupkg`.
- [ ] Single-source version, SourceLink, symbols, deterministic build, package
      validation, SBOM/attestation.
- [ ] Docs: README, rule catalog, config reference, format guide, CI recipes,
      fixing guide, baseline guide, troubleshooting, migration, architecture.
- [ ] CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, issue and PR templates.
- [ ] Dogfooded on at least 10 real public locale trees; every false-positive
      class is either fixed, configurable, or documented.

### Performance and reliability

- [ ] 100,000 entries across 500 files complete in under 3 seconds on a
      developer laptop; typical 10,000-entry projects under 1 second. The 0.6
      CI gate is five seconds for the generated benchmark (about 1.8 seconds
      locally on 2026-10-26); the 1.0 target remains three seconds.
- [ ] Peak memory under 256 MB for the 100k-entry fixture.
- [ ] No regex without a timeout or non-backtracking mode.
- [ ] No network calls, no telemetry, no execution of locale content.
- [ ] Graceful Ctrl+C and broken-pipe handling.

---

## 5. Release plan

Each phase is independently shippable. A phase is complete only when its exit
criteria are met, not when the code is merged.

### 0.3.0 — Trustworthy foundation

**Status:** shipped in 0.3.0 (2026-10-23).

**Theme:** make the existing checks safe to run on a real repository.

**Why first:** more formats and rules multiply damage if discovery, error
handling, and locations are unreliable.

- [x] Introduce a `Diagnostic` model with file, line, column, end position,
      span, culture, key, related locations, help URL, and optional fix.
- [x] Replace the one-path-per-culture model with per-document entries and
      provenance. Keep logical resource sets separate (for example
      `Strings.resx` + `Strings.ar.resx` versus `Errors.resx` +
      `Errors.ar.resx`).
- [x] Emit `MalformedFile` for invalid JSON/XML and for invalid UTF-8; include
      the parser message and position.
- [x] Detect duplicate keys within a file and key collisions across merged
      files. Report both origins.
- [x] Replace `LooksLikeCulture` with a BCP-47-aware parser and explicit
      layout detection. Never treat `app.json` as a culture. (Culture-named
      JSON and resx sets are the supported layouts; additional adapters are
      tracked in 0.4.0.)
- [x] Refactor CLI entry into a testable `Cli.Run(args, ...)` returning an
      exit code. `dotnet run` is not needed in tests.
- [x] Adopt a tested argument parser or cover the hand-rolled parser with a
      full matrix: `--version`, `--help` in any position, `--`, missing option
      values, unknown options, multiple paths, file paths.
- [x] Define and document exit codes. Contract:
      `0` clean, `1` usage/config error, `2` input/path error,
      `3` nothing to lint, `4` findings at or above the threshold,
      `5` internal error. Findings fail by default in 0.3.0;
      `--strict` escalates warnings.
- [x] Add `.editorconfig`, `Directory.Build.props`, `global.json`, recommended
      analyzers, and `TreatWarningsAsErrors`; make the build deterministic.
- [x] Add a PR workflow: build, test, format check, pack, install the packed
      tool, run it on both samples, assert exit codes.
- [x] Make version single-source (assembly informational version), remove the
      hardcoded `ToolInfo` value, and stop tests from asserting a literal
      version.
- [x] Regenerate the README sample output from a golden test so docs cannot
      drift.

**Exit criteria:** malformed input is never silent; every finding has a
location; `app.json` is not linted as a locale; the tool installs from its own
package and runs in CI on three operating systems.

### 0.4.0 — Works in real repositories

**Status:** shipped in 0.4.0 (2026-10-23).

**Theme:** discover and parse the layouts and formats teams actually have.

**Why:** the tool is only useful if it can be pointed at a real project
without renaming files first.

- [x] Define `ILocaleFormat` (discover → parse → entries with spans) and an
      `ILocaleSource` representation. Rules must not know file formats.
      (Implemented as `ILocaleAdapter` + `LocaleIdentity` + `LocaleDocument`.)
- [x] Implement layout discovery:
      - culture-named files: `en.json`, `ar.json`, `ar-EG.json`;
      - culture directories: `locales/en/translation.json`,
        `locales/ar/common.json`;
      - Android `res/values*`;
      - Apple `*.lproj`;
      - gettext `locale/ar/LC_MESSAGES/*.po`;
      - resx neutral/satellite sets.
- [x] Add config-independent include/exclude defaults for `bin`, `obj`,
      `.git`, `node_modules`, `.next`, `dist`, `build`, `coverage`, `vendor`,
      and other generated trees. Do not follow symlink loops.
- [x] Support JSON arrays of strings and arrays of objects with indexed keys;
      report key-flattening collisions.
- [x] Support JSONC comments and trailing commas for locale files.
- [x] Add Tier 1 adapters: Android strings/plurals, Flutter ARB, gettext
      PO/POT, and Apple `.strings` (UTF-8 and UTF-16).
- [x] Treat `@@locale` and `@key` metadata in ARB as metadata, not messages;
      use placeholder metadata for later type checks.
- [x] Handle Android locale qualifiers (`values-ar`, `values-ar-rEG`,
      `values-b+ar+EG`) and ignore non-locale qualifiers such as `night`,
      `sw600dp`, and `land`.
- [x] Build realistic fixture repositories under `tests/fixtures/` and assert
      the exact files/cultures/entries discovered.
- [x] Emit `NoLocaleFiles` with a list of candidate files when discovery
      fails, and a base-culture error that lists the cultures that *were*
      found.
- [ ] Auto-detect the base culture from neutral resources, `values/`,
      `Base.lproj`, or a common default. If candidates are ambiguous, stop
      with the list rather than guessing. (Neutral resources map to the
      configured base in 0.4; multi-base detection is planned for 0.8.)

**Exit criteria:** a realistic JSON, resx, Android, ARB, PO, and `.strings`
repository can each be linted with zero config or a single config entry; no
supported file is silently skipped.

### 0.5.0 — Adoptable in legacy codebases

**Status:** shipped in 0.5.0 (2026-10-23).

**Theme:** config, suppressions, and baselines so teams can turn the tool on
without a cleanup project.

- [x] Add `bidilint.json` (JSONC) with a published JSON Schema, `$schema`
      support, strict unknown-key validation, and helpful suggestions.
- [x] Config sections: `baseCulture`, `include`, `exclude`, `files`/`layouts`,
      `rules` (severity and options), `ignore`, `allowLatin`, `rtlCultures`,
      `placeholderDialects`, `numeralPolicy`, `isolationStyle`, `baseline`,
      `failOn`, `output`.
- [x] Config discovery walks upward from the target path; `--config` overrides;
      `--no-config` disables; `--init` writes a documented starter file.
- [x] Rule metadata registry: code, title, category, default severity,
      fixable, docs anchor, since-version. Generate the rule table and docs.
- [x] Allowed Latin terms and brand glossary: exact/case-insensitive entries,
      optional reason, optional per-culture scope.
- [x] Ignore rules by path, culture, key glob, and rule code, with a required
      reason. Report unused/stale suppressions.
- [x] Baseline file with stable fingerprints (`rule + culture + key +
      occurrence`), create/update/apply modes, and stale-entry reporting. New
      findings still fail; baselines never suppress load or format errors.
- [x] `--fail-on error|warning|none`, `--max-warnings`, `--quiet`,
      `--no-color`, `NO_COLOR`, `--debug`.
- [x] Document precedence: CLI > config > defaults. Path-scoped rule severity
      overrides are deferred to 0.8; path-aware `ignore` entries cover adoption
      exceptions now.

**Exit criteria:** a 10,000-key legacy project can be baselined, run in CI,
and fail only on newly introduced findings; every suppression is auditable.

### 0.6.0 — Real bidi engine

**Status:** shipped in 0.6.0 (2026-10-26).

**Theme:** replace regex guesses with Unicode-aware bidi analysis.

**Why:** this is the differentiator. A bidi linter that cannot explain visual
order or handle Hebrew is a demo, not a tool.

- [x] Iterate over Unicode scalar values (`Rune`), not UTF-16 `char`, and
      generate bidi/script tables from a pinned Unicode version. Record the
      Unicode version in the report and `--version`.
- [x] Implement a UAX #9 resolver: paragraph level, explicit
      embeddings/isolates, weak/neutral resolution, implicit levels, and
      reordering. Validated against the full Unicode 17.0.0 `BidiTest.txt` and
      `BidiCharacterTest.txt` suites, checked in as compressed fixtures.
- [x] Replace the Arabic-only gate with script detection: Arabic, Hebrew,
      Syriac, Thaana, N'Ko, Samaritan, Mandaic, Adlam, and configured scripts.
- [x] Detect RTL from culture language, script subtag, and content. `he` and
      `yi` must actually be scanned; `az-Arab` must not be missed; `ku-Latn`
      must not be treated as RTL.
- [x] Run bidi rules on any value containing RTL script, including values in
      a nominally LTR base file.
- [x] Rewrite isolation rules with spans:
      - unisolated Latin runs, URLs, emails, paths, code identifiers, version
        strings, and file names;
      - Latin runs inside the wrong isolate type or crossing a boundary;
      - legacy `LRE/RLE/LRO/RLO/PDF` balance and isolate-carried nesting;
      - isolate depth and empty isolates;
      - number adjacency only where resolved levels show a real reorder risk.
- [x] Recognize mitigations already present in the value: LRI/FSI/RLI..PDI,
      `<bdi>`, `dir="ltr"`, `<code>`, and configured wrappers. Do not flag
      content that is already isolated correctly.
- [x] Separate numeral systems: Western, Arabic-Indic (`0660–0669`), and
      Extended Arabic-Indic (`06F0–06F9`); report which systems were found and
      apply the configured numeral policy.
- [x] Add security rules: `BidiOverride` for every visible LRO/RLO (Trojan
      Source), and `InvisibleControl` for mid-string BOM, disallowed C0/C1
      controls, contextless ZWJ/ZWNJ, and ineffective LRM/RLM/ALM. Joiners are
      accepted next to Arabic/Indic joining-script letters, Indic viramas, and
      emoji ZWJ sequences. Security rules are not configurable under `rules`
      and cannot be baselined; waive a specific finding only with a targeted,
      reasoned `ignore` entry that names the rule.
- [x] Add normalization checks: presentation forms (`FB50–FDFF`, `FE70–FEFE`),
      non-NFC sequences, and tatweel policy (warning, not error). U+FEFF is
      handled as a BOM by the security rules, not as a presentation form.
- [x] Add a visual-order explanation: for each flagged run, show the resolved
      level change and an escaped logical string, not just "may render wrong".
- [x] Add Hebrew, Persian, Urdu, mixed-numeral, control-character, and
      visual-order fixtures. Each independent resource set covers every 0.6
      rule with a positive and a benign counterpart.
- [x] Check in golden text and JSON reports for the 0.6 corpus, with elapsed
      time pinned to zero and the package version pinned to 0.6.0.
- [x] Add a deterministic 100,000-entry, 500-file benchmark fixture and enforce
      the five-second CI budget in `BenchmarkTests`.
- [x] Document the full Unicode 17.0.0 UAX #9 conformance suite and its
      exclusions in `docs/conformance.md`.

**Exit criteria:** the engine passes the documented Unicode conformance
subset; Hebrew, Persian, and Urdu fixtures are linted; no rule claims to
implement bidi without using resolved levels; false positives on
already-isolated content are zero in the fixture corpus; the package builds,
installs, and passes both sample smoke tests.

### 0.7.0 — Localization correctness beyond bidi

**Theme:** become the linter teams run for every localization defect, not only
RTL ordering.

- [ ] Build one placeholder AST with dialect adapters:
      - .NET composite format (`{0}`, `{0:N2}`, escaped `{{`/`}}`);
      - ICU MessageFormat 1 (`{count, plural, ...}`, select, selectordinal,
        `#`, `=0`, apostrophe escaping);
      - printf (`%s`, `%1$s`, `%d`, `%%`, `%(name)s`);
      - i18next (`{{name}}`, `_one`/`_other` keys, nesting);
      - Angular (`{{ name }}`), Rails (`%{name}`), Python (`{name}`),
        Handlebars/Mustache (`{{name}}`), JavaScript templates (`${name}`).
- [ ] Dialect is inferred per format and overridable per path; a message is
      never parsed as two dialects at once.
- [ ] Placeholder rules:
      - syntax errors and unmatched braces;
      - missing, extra, renamed, and duplicate arguments;
      - positional/named mismatch and argument-index range;
      - format-specifier and type mismatches where metadata exists;
      - plural/select branch parity against the base;
      - required `other` branch and valid CLDR categories per culture;
      - nested placeholders inside branches.
- [ ] Markup rules: balanced HTML/XML tags, tag-name sequence parity, and
      attribute placeholder parity. Compare structure, not translated text.
- [ ] Value rules: untranslated (identical to base) values with an allowlist,
      leading/trailing whitespace, double spaces, newline drift, and empty
      after trimming.
- [ ] Glossary/brand-term checks (opt-in): required spelling, casing, and
      isolation policy.
- [ ] Format-aware tests with escaped braces, nested ICU, `#`, apostrophes,
      plural categories, and printf positional arguments.

**Exit criteria:** the existing `PlaceholderMismatch` false positives on ICU
and escaped braces are gone; a dropped plural branch or missing `{1}` is
reported with both base and target spans; rules are independently
configurable.

### 0.8.0 — Actionable output and CI integrations

**Theme:** make findings impossible to ignore and trivial to route.

- [ ] Text formatter: relative `path:line:column`, severity, code, key,
      message, base/target snippet, caret span, and help URL. Escape invisible
      characters as `\u{...}` so the terminal cannot reorder the evidence.
- [ ] Stable JSON schema v1. Keep 0.2 fields and add `schemaVersion`, tool and
      Unicode versions, files with format and culture, findings with range,
      related locations, fix, suppression/baseline state, and summary counts
      by rule and culture.
- [ ] SARIF 2.1.0 with rule metadata, locations, related locations, fixes, and
      baseline state. Validate against the official schema.
- [ ] GitHub annotations (`::error file=...,line=...,col=...::...`) with
      correct escaping.
- [ ] MSBuild and JUnit formatters; `--output <path|->`.
- [ ] Color only on a TTY; honor `NO_COLOR` and `--no-color`; never rely on
      color to convey severity.
- [ ] Broken-pipe handling so `bidilint ... | head` exits cleanly.
- [ ] CI recipes: GitHub Actions, GitLab CI, Azure Pipelines, pre-commit.
- [ ] Add a sample SARIF upload workflow to the repository and validate it in
      CI.

**Exit criteria:** a finding is clickable in GitHub code scanning; JSON is
schema-versioned and documented; text output alone is enough to fix a defect.

### 0.9.0 — Safe auto-fix

**Theme:** remove the mechanical work without taking control away from users.

- [ ] Add `--fix` (write), `--diff` (unified diff, no write), and `--check`
      (exit nonzero if a fix would be applied). `--fix` is opt-in.
- [ ] Fixable rules at 1.0: wrap unisolated Latin runs in FSI..PDI (plain
      text) or `<bdi>` (HTML-configured values). Nothing else is auto-fixed.
- [ ] Never fix across an existing isolate, a placeholder, a markup boundary,
      or an allowlisted term. Never guess at mixed numerals, plural branches,
      missing keys, or unbalanced legacy controls.
- [ ] Format-preserving edits: preserve key order, indentation style, line
      endings, BOM/encoding, escaping style, and surrounding whitespace.
      Implement span-aware string mapping per adapter and test it.
- [ ] Apply edits from the end of the file backwards; write atomically;
      re-lint after fixing; report fixed counts and remaining findings.
- [ ] Emit machine-readable fixes in JSON and SARIF even without `--fix`, so
      editors can offer quick fixes later.
- [ ] Idempotence test: fixing twice changes nothing the second time.
- [ ] Golden fixture test: parse tree and all non-target values are unchanged
      after a fix.

**Exit criteria:** `bidilint --fix` on the sample produces a clean run with a
reviewable diff; fixing is idempotent; no format loses data or reorders keys.

### 1.0.0 — Production release

**Theme:** freeze the contracts, prove the quality, and make installation and
support boring.

- [ ] Multi-target `net8.0` and `net10.0` (or target `net8.0` with verified
      major roll-forward) and test on runtime-only installs.
- [ ] Parallel discovery/parsing with bounded concurrency and deterministic
      output; benchmark the 100k-entry fixture and enforce the budget in CI.
- [ ] Handle permissions, locked files, broken symlinks, huge files, invalid
      encodings, and cancellation cleanly.
- [ ] Top-level error handling: no stack traces for user errors; `--debug`
      exposes them.
- [ ] Explicit XML settings with DTD/external entities disabled; add an XXE
      regression test.
- [ ] Freeze the CLI, config schema, diagnostic codes, JSON schema, and exit
      codes under SemVer. Document the deprecation policy.
- [ ] Package metadata: icon, tags, project URL, repository type, release
      notes, SourceLink, symbols, deterministic build, package validation,
      SBOM, and build provenance.
- [ ] Release workflow: tag-driven, tests first, packs symbols, publishes with
      `--skip-duplicate`, attaches artifacts and checksums, uses a protected
      NuGet environment.
- [ ] Complete documentation set and a migration guide from 0.x.
- [ ] Dogfood on at least 10 real repositories; publish a false-positive
      report and resolve every P0/P1 finding.
- [ ] Run a beta with at least five external users; collect install, config,
      and false-positive feedback; fix blockers before the 1.0 tag.
- [ ] Publish a demo repository and a rendered bidi-bug gallery that shows the
      tool catching and fixing real examples.

**Exit criteria:** every checklist item in section 4 is green; a new user can
install, run on a real project, understand every finding, and gate CI in under
ten minutes.

## 6. Format coverage plan

Tier 1 is required for 1.0. Tier 2 is planned after the core contracts are
stable. A format is not "supported" until it has discovery, parsing with
spans, placeholder dialect handling, fixtures, and docs.

| Tier | Format | Discovery | Placeholder dialect | Milestone |
| --- | --- | --- | --- | --- |
| 1 | JSON / JSONC | `en.json`, `locales/en/*.json`, nested/flat, arrays | inferred: none, i18next, ICU, Angular | 0.4 |
| 1 | `.resx` | neutral + `*.xx.resx` per resource set | .NET composite | 0.3–0.4 |
| 1 | Android `strings.xml` | `res/values`, `values-ar`, `values-ar-rEG`, `values-b+ar+EG` | printf / Android args | 0.4 |
| 1 | Flutter ARB | `*_ar.arb`, `@@locale`, `@key` metadata | ICU | 0.4 |
| 1 | gettext PO/POT | `locale/ar/LC_MESSAGES/*.po`, `*.po`, `*.pot` | printf / custom | 0.4 |
| 1 | Apple `.strings` | `ar.lproj/*.strings`, UTF-8/UTF-16 | printf / `%@` | 0.4 |
| 2 | YAML / TOML | culture files or culture directories | i18next / printf | 1.1 |
| 2 | XLIFF 1.2/2.0 | `target-language`, trans-units | inline codes | 1.1 |
| 2 | Java `.properties` | `messages_ar.properties` | `{0}` / printf | 1.1 |
| 2 | CSV | culture columns or per-culture files | configurable | 1.2 |
| 2 | Apple `.stringsdict` | plural rules XML | `%#@...@` | 1.2 |

**Format rules**

- Reject or explicitly skip files that are not locale data; never infer a
  culture from a filename alone unless it parses as BCP-47 and the layout is
  plausible.
- Preserve provenance per entry: path, format, logical resource set, key,
  raw value, decoded value, line, column, and span.
- Parse with the format's real grammar where practical. A regex is acceptable
  for cheap detection, not for extracting values or placeholders.
- Unsupported constructs produce a diagnostic, not a silent skip. Examples:
  resx binary resources, Android `translatable="false"`, PO fuzzy entries.
- Every adapter has a "weird but valid" fixture: BOM, CRLF, escaped quotes,
  CDATA, multiline values, duplicate keys, empty values, and comments.
- Fidelity matters: the fixer must not rewrite a format it cannot preserve.

---

## 7. Rule catalog target

The existing four structural rules and three bidi rules stay. New rules are
grouped by the milestone that introduces them. Default severities are
proposals to validate against the false-positive corpus; they are not final
until 1.0.

### Structural and file-level

| Rule | Default | Milestone | Catches |
| --- | --- | --- | --- |
| `MalformedFile` | error | 0.3 | Invalid JSON/XML/encoding, unreadable content |
| `DuplicateKey` | error | 0.3 | Same key twice in one file |
| `KeyCollision` | error | 0.3 | Two files/layouts produce the same logical key |
| `MissingKey` | error | shipped | Key missing from a translated culture |
| `ExtraKey` | warning | shipped | Dead or unowned translation key |
| `EmptyValue` | warning | shipped | Empty/whitespace-only value |
| `UntranslatedValue` | warning | 0.7 | Translation identical to base, allowlist-aware |
| `WhitespaceDrift` | warning | 0.7 | Leading/trailing/double spaces or newline mismatch |
| `KeyConvention` | info | 1.1 | Optional naming/namespace policy |

### Placeholder, plural, and markup

| Rule | Default | Milestone | Catches |
| --- | --- | --- | --- |
| `PlaceholderMismatch` | error | shipped, expanded in 0.7 | Missing/extra/renamed args |
| `PlaceholderSyntax` | error | 0.7 | Unclosed braces, malformed ICU, invalid printf |
| `PlaceholderType` | error | 0.7 | Type/format-spec mismatch where metadata exists |
| `PluralMismatch` | error | 0.7 | Missing branches, invalid categories, missing `other` |
| `MarkupMismatch` | error | 0.7 | Tag or attribute structure differs from base |
| `HtmlUnbalanced` | error | 0.7 | Unclosed/overlapping tags in a value |
| `GlossaryMismatch` | warning | 0.7 | Brand/term spelling or casing policy |

### Bidi, numerals, and Unicode

| Rule | Default | Milestone | Catches |
| --- | --- | --- | --- |
| `UnisolatedLatin` | warning | shipped, rewritten 0.6 | LTR run not isolated in RTL text |
| `MixedNumerals` | warning | shipped, extended 0.6 | More than one numeral system in one string |
| `UnbalancedIsolates` | error | shipped, extended 0.6 | Unclosed/misordered isolates, stray PDI |
| `BidiControl` | error | shipped 0.6 | Unbalanced/ineffective legacy embeddings/overrides, overflow |
| `BidiOverride` | error | shipped 0.6 | Trojan Source-style override sequences |
| `IsolateDepth` | warning | shipped 0.6 | Excessive or empty isolate nesting |
| `NumberAdjacency` | warning | shipped 0.6 | Adjacent numeric tokens resolve in a different visual order |
| `PresentationForm` | warning | shipped 0.6 | Legacy Arabic presentation forms |
| `Normalization` | warning | shipped 0.6 | Non-NFC values, mixed normalization |
| `InvisibleControl` | error | shipped 0.6 | BOM, disallowed controls, contextless joiners, ineffective marks |
| `Tatweel` | warning | shipped 0.6 | Optional kashida policy |

### Security

| Rule | Default | Milestone | Catches |
| --- | --- | --- | --- |
| `BidiOverride` | error | shipped 0.6 | RLO/LRO sequences that can hide content |
| `InvisibleControl` | error | shipped 0.6 | Controls with no legitimate localization use |
| `XxeRisk` | error | 1.0 | XML input that would require external entities |

`BidiOverride` and `InvisibleControl` appear in both the bidi and security
groups because they are rendering bugs and security gates at the same time.
Security findings are excluded from baselines and rejected when a baseline
names them; they may be waived only by an explicit, reasoned `ignore` entry
that names the rule.

---

## 8. Target CLI and output contracts

### Desired text output

```text
src/locales/ar.json:42:18  error  PlaceholderMismatch  checkout.discount
  expected: {0}, {1}
  found:    {0}
  base en:  "Save {0}% for {1} days"
  ar:       "وفّر {0}% لفترة محدودة"
  help:     https://github.com/AbdullahHakima/bidilint/blob/main/docs/rules/PlaceholderMismatch.md

src/locales/ar.json:61:9   error  BidiControl  promo.broken
  unclosed FSI (U+2066); the isolate runs to the end of the value
  ar: "جرّب \u{2066}CqrsLens الجديد"
            ^
```

Requirements:

- Paths are relative to the working directory by default; `--absolute-paths`
  is available.
- Line and column are 1-based; columns count UTF-16 code units, documented
  and tested for astral characters.
- Invisible and bidi-control characters are escaped in the evidence, never
  printed raw.
- A code frame is capped so a pathological one-line value cannot flood CI.
- Findings are ordered by file, position, severity, rule, then key.

### Target JSON shape

```json
{
  "schemaVersion": "1.0",
  "tool": "BidiLint",
  "version": "1.0.0",
  "unicodeVersion": "17.0.0",
  "baseCulture": "en",
  "keyCount": 842,
  "errors": 3,
  "warnings": 7,
  "elapsedMilliseconds": 61,
  "run": {
    "baseCulture": "en",
    "root": ".",
    "fileCount": 12,
    "entryCount": 842,
    "configPath": "bidilint.json"
  },
  "summary": {
    "errors": 3,
    "warnings": 7,
    "byRule": { "MissingKey": 2, "UnisolatedLatin": 4 },
    "byCulture": { "ar": 8, "he": 2 }
  },
  "files": [
    { "culture": "ar", "format": "json", "path": "src/locales/ar.json", "keys": 120 }
  ],
  "findings": [
    {
      "code": "UnisolatedLatin",
      "severity": "warning",
      "culture": "ar",
      "key": "skills.stack",
      "message": "Latin run \"ASP.NET Core\" is not isolated in RTL text.",
      "file": "src/locales/ar.json",
      "range": { "start": { "line": 12, "column": 24 }, "end": { "line": 12, "column": 36 } },
      "related": [{ "file": "src/locales/en.json", "key": "skills.stack" }],
      "fix": { "title": "Wrap in FSI..PDI", "replacement": "\u2066ASP.NET Core\u2069" }
    }
  ]
}
```

Compatibility: keep the 0.2 `tool`, `version`, `baseCulture`, `files`,
`keyCount`, `errors`, `warnings`, `elapsedMilliseconds`, and `findings`
fields, add the new ones, and version the schema. Consumers should ignore
unknown fields.

### Target config shape

```json
{
  "$schema": "https://raw.githubusercontent.com/AbdullahHakima/bidilint/main/schema/bidilint.schema.json",
  "baseCulture": "en",
  "include": ["src/locales/**/*.json", "src/**/*.resx"],
  "exclude": ["**/bin/**", "**/obj/**", "**/generated/**"],
  "rules": {
    "UnisolatedLatin": { "severity": "warning", "allow": ["BidiLint", ".NET"] },
    "Tatweel": "off",
    "BidiControl": "warning"
  },
  "ignore": [
    { "rule": "ExtraKey", "key": "legacy.*", "reason": "Remove after 2027-01", "expires": "2027-01-31" }
  ],
  "baseline": "bidilint-baseline.json",
  "failOn": "error"
}
```

Config must be forgiving to read (JSONC, comments, trailing commas) and strict
to validate (unknown keys are errors with a suggestion). The schema is
published and tested in CI.

## 9. Architecture direction

The current static classes are fine for a prototype but couple parsing,
rules, and console output. Before 1.0, move to a pipeline that can be tested
and reused by editors.

```text
discovery → format adapters → normalized documents/entries
          → rule engine (config + suppression + baseline)
          → diagnostics (spans, related info, fixes)
          → formatters (text/json/sarif/github/msbuild/junit)
          → exit code
```

Key types:

- `SourceFile` — path, format, culture, resource set, raw text, encoding.
- `LocaleEntry` — key, value, raw value, span, metadata, format dialect.
- `Diagnostic` — rule, severity, message, location, related locations,
  help URI, fix, suppression state.
- `ILocaleFormat` — detect, discover, parse, and preserve-edit capabilities.
- `Rule` — metadata plus a pure check over a document/entry and context.
- `LintConfig` — immutable, validated, with path overrides.
- `IOutputFormatter` — one implementation per output contract.
- `Cli.Run` — argument parsing, orchestration, error mapping, no logic in
  top-level statements.

Constraints:

- No console I/O inside rules or parsers.
- No exceptions for expected user errors; convert to diagnostics or a
  documented exit code.
- No rule depends on another rule's message text.
- No static mutable state; all context is passed explicitly.
- Parsers and formatters must be independently testable.
- Cancellation tokens flow through discovery, parsing, and linting.

It is acceptable to keep one project through 0.5 if namespaces and folders are
clean. Split `BidiLint.Core` from `BidiLint.Cli` when the first non-CLI
consumer (editor, MSBuild task, or tests using a library API) appears; do not
split earlier for its own sake.

---

## 10. Validation, metrics, and quality gates

### Test layers

| Layer | What it proves |
| --- | --- |
| Unit | One rule or parser behavior, including negatives and edge cases |
| Adapter fixtures | Realistic file shapes per format, with expected entries and spans |
| Golden output | Text/JSON/SARIF output does not drift accidentally |
| CLI end-to-end | Arguments, exit codes, stdout/stderr, files, config, baseline |
| Conformance | Bidi engine against Unicode test files; CLDR plural rules |
| Property/fuzz | Parsers never crash; fixes are idempotent and parse-preserving |
| Cross-platform | Windows/Linux/macOS paths, case sensitivity, encodings, CRLF |
| Package smoke | Packed tool installs and runs on a clean runtime-only image |
| Dogfood | Real repositories surface false-positive classes before users do |

### Metrics to track

- **Recall:** percentage of seeded defects caught in a curated corpus
  (target ≥ 95% for structural and bidi categories).
- **Precision:** false positives per 1,000 strings across the dogfood corpus
  (target < 5, and zero for already-isolated content).
- **Runtime:** milliseconds per 1,000 entries; hard CI budget for 100k entries.
- **Actionability:** percentage of findings with a precise span (target 100%
  for value/run rules).
- **Adoption friction:** commands and minutes from install to first useful CI
  run (target < 10 minutes).
- **Reliability:** crash-free runs on malformed-input fuzz corpus (target
  100%); no silent skipped files (target 0).
- **Coverage:** line and branch coverage of core and adapters (target ≥ 85%
  for 1.0, with security and bidi paths ≥ 90%).

### CI gates

- Build with warnings as errors and `dotnet format --verify-no-changes`.
- Unit/integration/CLI/golden tests on the OS and .NET version matrix.
- Coverage threshold with a report artifact.
- Pack, install into a clean tool manifest, run samples, assert exit codes.
- SARIF schema validation and a GitHub code-scanning upload smoke test.
- Fuzz smoke run on PRs (short) and a longer scheduled run.
- CodeQL, Dependabot, dependency review, and a lock file for build
  dependencies.
- Release workflow verifies version consistency, tests, packs symbols,
  publishes, and attaches checksums/SBOM.

---

## 11. Risks and mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Format scope explodes | 1.0 slips; adapters half-done | Freeze Tier 1; adapter interface first; Tier 2 after 1.0 |
| False positives destroy trust | Teams disable the tool | Negative fixtures, allowlists, conservative defaults, dogfood corpus, rule-level severity |
| UAX #9 implementation is wrong | Confident but incorrect findings | Use Unicode conformance files; document the covered subset; prefer a proven implementation if conformance stalls |
| Placeholder dialect guessing is wrong | Noise on ICU/i18next/printf | Explicit dialect per format and config; syntax detection with confidence; never parse two dialects at once |
| Fixer rewrites formatting/data | Unreviewable diffs or data loss | Span-aware edits, preserve key order/encoding/line endings, golden parse-tree tests, opt-in fixes only |
| Broad regex backtracking | CI hangs on hostile input | Non-backtracking regex or timeouts; fuzz; file-size limits |
| Silent skips return false green | Security and quality failure | Treat skip reasons as diagnostics; tests assert every fixture file is either linted or reported |
| Config complexity harms adoption | Users cannot start | Zero-config defaults; `--init`; JSON Schema and editor completion; documented precedence |
| Runtime/package incompatibility | Tool will not start | Multi-target or verified roll-forward; install smoke test on clean images |
| Maintenance burnout | Project stalls after 1.0 | Small rule surface, generated docs, adapter isolation, CI automation, clear non-goals |
| Security findings are suppressed by baseline | Hidden trojan-source debt | Security rules always excluded from baseline; explicit reasoned waiver only |

---

## 12. Non-goals (through 1.0)

BidiLint does **not** aim to be:

- A translation management system, translation memory, or machine translation
  tool.
- A spelling, grammar, tone, or general copywriting checker.
- A visual regression or screenshot-diff tool. It analyzes text, not layout.
- A full browser/ICU rendering engine. It computes enough bidi resolution to
  make trustworthy lint decisions and documents the subset.
- A source-code string extractor for C#/TypeScript/Roslyn ASTs. It consumes
  locale files; extraction can be a separate tool or a post-1.0 adapter.
- A GUI or hosted service. The CLI and machine-readable outputs are the
  product.
- A runtime library that changes how applications render text.
- A tool that sends locale content to a network service. No telemetry, no
  uploads, no network calls.

Explicitly out of scope does not mean "never"; it means "not required for
1.0" and protects the release from scope creep.

---

## 13. Launch checklist for 1.0

### Product

- [ ] Run the full acceptance scenario on a fresh machine:
      install → initialize config → lint a multi-format repo → open SARIF in
      GitHub → baseline a legacy folder → fix isolation → gate CI.
- [ ] Verify every rule has at least one real-world example and one negative
      test.
- [ ] Confirm the default rule set is quiet enough for a clean project: zero
      findings on the two bundled samples after fixes, apart from intentional
      demo errors.
- [ ] Publish the bidi-bug gallery with rendered before/after examples.
- [ ] Write the "what BidiLint does not catch" page.

### Distribution

- [ ] Publish the stable package to NuGet with a signed tag and release notes.
- [ ] Verify `dotnet tool install -g BidiLint`, local tool manifests, and
      `dotnet tool update`.
- [ ] Attach `.nupkg`, `.snupkg`, SBOM, and checksums to the GitHub release.
- [ ] Publish the JSON Schema and link it from the README.
- [ ] Add a CI badge and a NuGet badge to the README.

### Documentation

- [ ] Rewrite the README around: problem, quick start, supported formats,
      rule summary, config, CI, exit codes, limitations, roadmap.
- [ ] Publish rule catalog, config reference, format guide, baseline guide,
      fixing guide, CI recipes, troubleshooting, architecture, and migration.
- [ ] Add CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, issue templates, and a PR
      template.
- [ ] Add a short "how to report a false positive" guide with a minimal
      reproduction format.

### Support

- [ ] Define supported .NET versions and operating systems.
- [ ] Define the SemVer and deprecation policy.
- [ ] Create a triage label set for false positives, format requests, rule
      requests, and security reports.
- [ ] Set up a security reporting channel and a response target.
- [ ] Tag a `good first issue` backlog so the project is maintainable by
      others.

---

## 14. Immediate next sprint (two weeks)

These are the next concrete tasks after 0.5, in order. They are deliberately
small and independently reviewable.

1. Build the Unicode-aware bidi resolver and generated character tables for
   the documented UAX #9 subset; validate against `BidiTest.txt` and
   `BidiCharacterTest.txt`.
2. Add script detection for every supported RTL script and run bidi rules on
   RTL content in nominally LTR base files.
3. Add security rules for bidi overrides, suspicious invisible controls, and
   Trojan Source sequences, with baseline restrictions and reasoned waivers.
4. Split numeral systems beyond Western and Arabic-Indic, and add number
   adjacency checks based on resolved levels.
5. Replace regex placeholders with the dialect parser layer and remove the
   current ICU/printf false positives.
6. Extend fixtures and golden outputs for Hebrew, Persian, Urdu, mixed
   numerals, controls, and already-isolated content.
7. Add a benchmark fixture and enforce the performance budget before 1.0.
8. Start the SARIF formatter and JSON schema versioning work in parallel with
   the rule engine so CI integration is ready when the rules land.

---

## 15. How we will know it is real

A skeptical reviewer should be able to do this on a clean machine:

```bash
dotnet tool install -g BidiLint
cd examples/realistic-storefront
bidilint . --format text
```

and get a short list of defects, each pointing at a real line, with a base
comparison and a concrete next action. Then:

```bash
bidilint . --baseline bidilint-baseline.json --fail-on error
```

should turn a legacy repository green without hiding new defects, and:

```bash
bidilint . --format sarif --output bidilint.sarif
```

should upload cleanly to GitHub code scanning. Finally:

```bash
bidilint . --fix --diff
```

should show a minimal, reviewable patch that fixes only mechanical isolation
problems and leaves every translation decision to a human.

When that scenario works across JSON, resx, Android XML, ARB, PO, and Apple
`.strings` — with no silent skips, no crashes, stable output, documented
rules, and a published package — BidiLint is no longer a demo. It is a
production tool.
