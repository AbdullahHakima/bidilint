# Changelog

## [0.7.1] - 2026-10-26

### Added

- Flattened plural groups are now paired by key and culture for Nextcloud-style
  JSON arrays (`_singular_::_plural_`), Android `<plurals>` items, and gettext
  `msgstr[n]` forms. The metadata is internal; the JSON report schema and
  flattened public keys are unchanged.
- Group-aware placeholder comparison checks the union of non-empty branches,
  preserving the maximum multiplicity seen in any one branch. Additional target
  plural categories no longer produce `ExtraKey`, and a singular-like branch may
  omit the count without producing a false `PlaceholderMismatch`.
- Named Android plural groups still require the CLDR-mandated `other` category;
  a group missing `other` reports `MissingKey` for that branch.
- Added 30 regressions covering Nextcloud Persian/Hebrew/Arabic plural
  shapes, nested ICU categories, cross-variant swaps, multi-variant pairing,
  repeated placeholders, Android and PO groups, generic arrays, bidi analysis,
  empty branches, placeholder syntax, POT templates, and PO per-form spans.

### Fixed

- Fixed false `ExtraKey` findings for additional plural forms in JSON,
  Android, and PO resources.
- Fixed false `PlaceholderMismatch` findings when a plural branch legitimately
  omits the count placeholder, including the Nextcloud
  `_%n app has an update available_::_%n apps have an update available_` and
  `_%n folder_::_%n folders_` arrays.
- Nested ICU placeholders now use the maximum multiplicity per branch, so an
  extra target plural category may repeat `{name}` without a false mismatch.
  Exact selector variants are compared first, and unmatched extra categories
  are checked against the base union, so different-selector cross-variant
  swaps and wrong nested placeholders are still reported.
- POT plural templates now use `msgid_plural` for non-zero forms instead of
  repeating the singular `msgid`.
- An Android group missing `other` now reports only `MissingKey`; the
  remaining branches are not compared as a union.
- PO plural diagnostics use the span of the form that contains the offending
  placeholder, not always `msgstr[0]`.

### Changed

- JSON arrays are paired as plural groups only when the key has the Nextcloud
  `_::_` shape (a leading or `._` singular segment and a trailing `_`). Generic
  `colors::palette`, `colors_::_palette`, and arrays under a `namespace::thing`
  parent remain indexed lists.
- Generic JSON string arrays and Android `<string-array>` remain indexed lists
  and still report extra and missing items.
- A placeholder missing from every branch of a plural group is still reported;
  branch-specific placeholder drops inside a group are not reported
  individually yet, and same-selector cross-variant swaps remain invisible
  because those variants are merged before nested comparison.
- No bidi or security rule behavior changed.

### Tests

- The full suite is now 422 tests. The Nextcloud field corpus was re-run:
  core 422 findings (262 errors / 160 warnings), settings 1,777 (983 / 794),
  and the five-app set 1,963 (1,122 / 841). The only removed findings were the
  known flattened-plural `ExtraKey` and `PlaceholderMismatch` false positives;
  no new findings were introduced.

## [0.7.0] - 2026-10-26

### Added

- Added a placeholder AST with dialect adapters for .NET composite
  (`{0}`, `{0:N2}`, escaped `{{`/`}}`, named arguments), ICU MessageFormat 1
  (`plural`, `select`, `selectordinal`, `#`, `=0`, `offset:`, apostrophe
  escaping, nested placeholders), printf (`%s`, `%1$s`, `%d`, `%%`,
  `%(name)s`, `%@`), i18next/Angular/Mustache (`{{name}}`), Rails
  (`%{name}`), and JavaScript templates (`${name}`).
- Added `PlaceholderSyntax` for malformed or unclosed placeholders and
  `PlaceholderType` for format-specifier or argument-type differences. Both
  are configurable, baselinable, and documented in the generated rule catalog.
- Placeholder comparison now compares names as a multiset instead of raw
  source strings, reports missing and extra arguments with target and related
  base spans, and checks ICU select branch parity, the required `other` branch,
  and nested placeholders inside branches. Plural categories may differ by
  language, so Arabic `zero`/`two`/`few`/`many` branches are accepted.
- ICU branch message text remains visible to bidi, numeral, normalization, and
  security analysis. Only ICU argument syntax, selectors, braces, `#`, and
  nested placeholders are masked.

### Changed

- `placeholderDialects` now accepts `auto`, `dotnet` (alias `braced`), `icu`,
  `printf`, `i18next`, `angular`, `rails`, `javascript`, `mustache` (alias
  `handlebars`), and `none`. `auto` uses the format default (.resx -> .NET,
  ARB -> ICU, Android/Apple/PO -> printf, JSON -> detection). Selecting more
  than one specific dialect is a config error because a message is never
  parsed as two dialects at once.
- `PlaceholderMismatch` no longer treats escaped .NET braces or ICU syntax as
  raw placeholder names. Existing JSON/text report fields are unchanged.

### Deferred

- Flattened JSON/Android/PO plural groups still compare branch by branch, so
  additional plural forms can still produce `ExtraKey` and a singular branch
  that omits the count can still produce `PlaceholderMismatch` until the
  0.7.x plural-group slice.
- Per-path dialect overrides, CLDR plural-category validation per culture,
  markup parity, value/glossary rules, and auto-fix remain later milestones.

### Tests

- Added 24 placeholder tests covering escaped braces, .NET format differences,
  missing `{1}`, printf positional/type/missing cases, ICU plural/select/
  apostrophe/nested cases, ICU branch bidi masking, i18next spacing, Rails,
  JavaScript, Nextcloud brace tags, syntax errors, config disabling, and every
  explicit dialect. The full suite is now 392 tests.

## [0.6.1] - 2026-10-26

### Fixed

- Fixed false `KeyCollision` findings when culture-named JSON files live in
  different directories. `JsonFileAdapter` now uses the containing directory
  as the logical resource set instead of the global name `json`, so
  `apps/files/l10n/ar.json` and `apps/settings/l10n/ar.json` are compared
  separately. Root-level `en.json` / `ar.json` use the resource set `.`.

### Tests

- Added regression coverage for separate culture-named JSON directories and
  for the root-level layout.

### Changed

- The JSON report's `resourceSet` value for culture-named files is now the
  relative containing directory rather than `json`. Explicit `files` mappings
  are unchanged. Consumers that matched on the old constant should switch to
  the directory or an explicit mapping.
- Added a [field-validation record](docs/field-validation.md) for the
  Nextcloud RTL corpus. The run found no crashes or malformed files; the
  plural-branch placeholder comparison and other 0.7 placeholder work remain
  explicitly out of scope.

## [0.6.0] - 2026-10-26

### Added

- Added a pinned Unicode 17.0.0 data foundation: a reproducible generator for
  bidi classes, scripts, bracket pairs, and mirroring; scalar-aware UTF-16
  iteration and escaping helpers; and Unicode version reporting in `--version`
  and the JSON `unicodeVersion` field.
- Added the UAX #9 bidi resolver: paragraph direction, explicit
  embeddings/isolates, weak and neutral resolution, paired brackets, implicit
  levels, whitespace/separator resets, and L2 visual ordering. The full Unicode
  17.0.0 `BidiTest` and `BidiCharacterTest` suites are checked in as compressed
  conformance fixtures and run on every test invocation.
- Replaced character-range RTL detection with Unicode 17 script, bidi-class,
  and letter-category detection. Arabic, Hebrew, Syriac, Thaana, N'Ko,
  Samaritan, Mandaic, Adlam, Hanifi Rohingya, and other RTL scripts are covered,
  including supplementary-plane characters. Explicit script subtags now
  override language defaults, so `az-Arab` is RTL while `ar-Latn` and
  `ku-Latn` are not.
- Rewrote isolation and control-character analysis with resolved UAX #9 levels
  and exact value spans. `UnisolatedLatin` now covers brands, URLs, emails,
  paths, code identifiers, versions, and file names; it recognizes LRI/FSI..PDI,
  `<bdi>`, `<code>`, `dir="ltr"`, and configured wrappers, and reports wrong
  isolate direction or a run split by an isolate boundary.
- Added `BidiControl`, `IsolateDepth`, and `NumberAdjacency` as configurable
  rules. Legacy embeddings/overrides are checked for balance, PDFs ignored
  inside isolates, and the Unicode depth limit; isolate nesting and empty
  isolates are reported; neutral-separated numbers are checked against the
  resolved visual order.
- Added the `isolationWrappers` config key and schema support for custom
  HTML/XML isolation elements. Rule metadata, generated rule docs, the JSON
  schema, and baselines are synchronized with the new rules.
- Added separate Western, Arabic-Indic (`0660–0669`), and Extended Arabic-Indic
  (`06F0–06F9`) numeral-system detection. `numeralPolicy` now accepts
  `extended-arabic-indic`, reports every system found, applies the configured
  policy, ignores digits in markup and placeholders, and points at the visible
  digit span.
- Added `PresentationForm`, `Normalization`, and `Tatweel` as configurable
  warning rules. Presentation Forms-A/B (`FB50–FDFF`, `FE70–FEFE`), non-NFC and
  mixed composed/decomposed text, and U+0640 tatweel are checked on visible
  values regardless of direction; markup and placeholders are masked.
- Added run-level bidi explanations to text and JSON reports: the escaped
  logical string, paragraph level, resolved level runs, and the visual token
  order for `NumberAdjacency`. Findings now carry an optional `bidi` object in
  JSON, and `Diagnostic.Bidi` exposes the same evidence to API consumers.
- Added `BidiOverride` and `InvisibleControl` as error-level security rules.
  `BidiOverride` reports every visible LRO/RLO, including balanced Trojan Source
  sequences. `InvisibleControl` reports mid-string BOMs, disallowed C0/C1
  controls, ZWJ/ZWNJ without a legitimate joining-script or emoji context, and
  LRM/RLM/ALM marks that do not change the paragraph level or any visible
  resolved level. Joiners remain accepted in Arabic/Persian/Urdu and other
  joining-script text, after Indic viramas, and in emoji ZWJ sequences.
- Added security-rule metadata and reasoned waivers. Security rules cannot be
  set under `rules`; a targeted `ignore` entry that names the rule and includes
  a reason can suppress a specific finding. Path-, culture-, and key-only
  ignores do not match security findings.
- Excluded security rules from baseline creation and rejected baseline entries
  that name them. `--update-baseline` leaves security findings visible and still
  affects the exit code.
- Added tests for security positives, benign contexts, reasoned waivers,
  config rejection, baseline exclusion/rejection, markup masking, and exact
  spans.
- Added a checked-in RTL fixture corpus with independent Arabic, Hebrew,
  Persian, and Urdu resource sets. It covers all eleven 0.6 rules with a
  positive and a benign counterpart, plus golden text and JSON reports with
  deterministic elapsed time and version.
- Added a deterministic 100,000-entry, 500-file benchmark fixture. The test
  suite enforces a five-second CI budget for discovery, parsing, comparison,
  and linting.
- Documented the full Unicode 17.0.0 UAX #9 conformance suite, the tested
  counts, and the exclusions for paragraph splitting, line layout, rendering,
  and higher-level protocols in `docs/conformance.md`.
- The full suite now has 366 tests.

### Changed

- `StringScan.HasArabicIndicDigits` now covers only Arabic-Indic digits
  (`0660–0669`). Use `HasExtendedArabicIndicDigits` or `NumeralSystems` for
  Extended Arabic-Indic digits.
- `MixedNumerals` now points at the first-to-last visible digit instead of the
  whole value, and its message reports every recognized numeral system.
- `Tatweel` defaults to `warning` (configurable to `info` or `off`) rather than
  the `info` default previously sketched in the roadmap.
- Security rules are non-suppressible through `rules` and baselines; only a
  targeted, reasoned `ignore` entry can waive one.
- `PresentationForm` no longer treats U+FEFF as an Arabic presentation form;
  U+FEFF is handled only as a BOM by `InvisibleControl`.

### Fixed

- Corrected the Presentation Forms-B range to `FE70–FEFE`, so a visible BOM is
  reported once by `InvisibleControl` instead of also as a presentation form.

## [0.5.0] - 2026-10-23

- Added `bidilint.json` (JSONC) with upward discovery, explicit `--config`,
  `--no-config`, and `--init`. Precedence is CLI > config > defaults.
- Added the published config schema (`schema/bidilint.schema.json`) with strict
  unknown-key validation, nested-key validation, and nearest-key suggestions.
- Added config controls for base culture, include/exclude globs, custom layout
  names, explicit file mappings, per-rule severity and options, allowed Latin
  terms, RTL cultures, numeral policy, placeholder dialect, isolation style,
  baseline path, `failOn`, and output settings.
- Added a rule metadata catalog that is the source of truth for default
  severity, help links, baseline eligibility, configurable rules, and
  generated rule documentation.
- Added reasoned ignore rules by rule, path glob, culture, and key glob, with
  optional expiry. Unused or expired ignore entries produce `StaleSuppression`
  warnings.
- Added baselines with SHA-256 fingerprints over rule, culture, key, and
  occurrence. Create/update/apply, stale-entry reporting, tamper checks, and
  deterministic ordering are covered by tests. Update runs do not write a
  partial baseline after an input path error or a missing base culture, and
  diagnostics that cannot be baselined still affect the exit code.
- Load, format, and configuration diagnostics cannot be reconfigured, ignored,
  or baselined; they always remain visible.
- Added `--baseline`, `--update-baseline`, `--no-baseline`, `--fail-on`,
  `--max-warnings`, `--quiet`, `--color`, `--no-color`, and `--debug`.
  `--strict` remains an alias for `--fail-on warning`, and `NO_COLOR` is
  honored.
- JSON reports now include resolved config, suppression and baseline details,
  suppressed findings, and info counts. Text output includes suppression
  counts and baseline-update status.
- Rule documentation is checked against the catalog in CI, and the package
  includes the schema and rule catalog.

### Known limitations in 0.5.0

- Path-scoped rule severity overrides are not implemented yet; path-aware
  `ignore` entries cover adoption exceptions. They are planned for 0.8.
- PO plural forms and ARB ICU placeholders still use the simple comparison
  behavior described in 0.4 and are scheduled for 0.7.
- Security-rule baseline restrictions and the full bidi engine are scheduled
  for 0.6.

## [0.4.0] - 2026-10-23

- Format adapters: JSON culture files and culture directories, `.resx`,
  Android resources, Apple `.strings`, gettext PO/POT, and Flutter ARB all
  produce the same `LocaleDocument` model. Rules stay format-agnostic.
- Layout-aware resource sets: `locales/en/translation.json` pairs with
  `locales/ar/translation.json`; Android `res/values` pairs with
  `res/values-ar`; PO `locale/en/LC_MESSAGES/messages.po` pairs with
  `locale/ar/LC_MESSAGES/messages.po`; Apple `en.lproj` pairs with
  `ar.lproj`. Unrelated sets are never merged.
- Android support for `<string>`, `<plurals>` (`key[one]`), and
  `<string-array>` (`key[0]`), BCP-47 `values-b+ar+EG`, legacy
  `values-ar-rEG`, and `translatable="false"`.
- Apple `.strings` parser with comments, quoted/bare tokens, escapes, source
  positions, duplicate detection, and UTF-16 BOM support.
- gettext parser for `msgid`, `msgctxt`, `msgstr`, and plural forms, plus POT
  templates that use `msgid` as the base value.
- Flutter ARB support with `@@locale` metadata, `@key` metadata skipping, and
  filename conventions such as `app_en.arb`.
- JSON culture-directory discovery is explicit: only directories under known
  layout roots are treated as locales, so unrelated `app.json` files remain
  ignored.
- `NoLocaleFiles` now lists candidate files when discovery finds no usable
  locale, making empty runs actionable.
- Format names appear in the JSON report (`android`, `apple-strings`, `arb`,
  `po`), and the README documents every supported layout and its limitations.
- UTF-8/UTF-16 decoding is shared by text and XML formats; malformed encodings
  and malformed format syntax produce positioned `MalformedFile` diagnostics.

### Known limitations in 0.4.0

- PO plural forms are flattened; plural-aware comparison is planned for 0.7.
- ARB ICU placeholders are not yet parsed by the placeholder rule (0.7).
- Config files, baselines, SARIF, and auto-fix are later roadmap phases.

## [0.3.0] - 2026-10-23

- Trustworthy foundation: every parsed entry keeps its file and source span.
  Diagnostics now include a code, severity, culture, key, file, line/column
  range, related base location, help URL, and an optional fix model.
- File discovery is safe and explicit: generated folders and symlink loops are
  skipped; unreadable paths are errors instead of silent omissions.
- JSON and `.resx` parsing report `MalformedFile` with parser positions,
  `DuplicateKey`, flattening `KeyCollision`, and non-string JSON values.
- Culture detection now validates known BCP-47 tags. Arbitrary files such as
  `app.json` are ignored, while explicit non-culture files are input errors.
- Logical resource sets are kept separate: `Strings.resx` and `Errors.resx`
  compare independently, and JSON and `.resx` no longer merge implicitly.
- The CLI is testable end to end (`Cli.Run`), supports multiple paths,
  `--base`, `--format text|json`, `--strict`, `--help`, and `--version`.
- Exit codes are documented and stable: `0` clean, `1` usage, `2` input/path,
  `3` no locale files or missing base, `4` findings, `5` internal error.
  Errors fail by default; `--strict` also fails on warnings.
- Bidi checks now recognize Hebrew and other RTL scripts, not only Arabic.
- Build hardening: `.editorconfig`, `Directory.Build.props`, pinned SDK,
  warnings-as-errors, recommended analyzers, deterministic builds, CI matrix,
  package install smoke tests, Dependabot, and a README golden-output test.
- Version is read from assembly informational version; the source of truth is
  now `src/BidiLint/BidiLint.csproj`.

### Breaking changes in 0.3.0

- Findings exit with code `4` without `--strict`; previously errors only failed
  under `--strict`. Use `--strict` to also fail on warnings.
- JSON and `.resx` are separate resource sets. If you relied on JSON winning
  over `.resx` for a duplicate key, compare and consolidate those resources
  explicitly.
- Unknown culture file names are ignored during directory discovery and are an
  input error when passed as an explicit file.

## [0.2.0] - 2026-10-21

- `.resx` support: satellite files by name (`Strings.ar.resx`), a neutral
  `Strings.resx` counts as the base culture, non-string resources skipped.
  JSON and .resx merge per culture (JSON wins on duplicate keys).
- `--format json`: machine-readable report (files, counts, findings) for
  tooling. Sample .resx pair under `samples/resx-storefront`.

## [0.1.0] - 2026-10-21

- First release: JSON locale loading, structural checks against a base culture,
  and bidi checks (unisolated Latin runs, mixed numerals, unbalanced isolates)
  for RTL strings. `--strict` exit code for CI. Sample locale pair under
  `samples/storefront`.
