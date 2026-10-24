# Changelog

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
