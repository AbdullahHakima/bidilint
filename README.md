# BidiLint

**Lint your RTL locale files for the bidi bugs you only see after shipping.**

I build Arabic-first interfaces. The bugs that kept reaching production were
never in the C#; they were in the strings: a brand name like `.NET Core`
rendering in the wrong order inside an Arabic sentence, a counter mixing
Arabic-Indic and Western digits, a `{1}` placeholder the translator dropped.
Nothing failed to compile. Everything looked fine in the JSON file. It broke
in the browser, in RTL, in front of users.

BidiLint is a small `dotnet tool` that scans localization files and reports
those problems before they ship.

## Install

```bash
dotnet tool install -g BidiLint
```

BidiLint targets .NET 8. Install the .NET 8 SDK or runtime first.

## Usage

```bash
bidilint <path> [<path>...] [options]
bidilint --init [--config <file>]
```

A path may be a folder or a single locale file. Folders are searched
recursively; `bin`, `obj`, `node_modules`, and similar generated folders are
skipped.

| Option | Meaning |
| --- | --- |
| `--base <culture>` | Source culture compared against; defaults to `en` or `baseCulture` from config. |
| `--format text\|json` | Report format; defaults to `text` or `output.format` from config. |
| `--fail-on error\|warning\|none` | Findings threshold for exit code `4`; defaults to `error`. |
| `--strict` | Alias for `--fail-on warning`. |
| `--max-warnings <n>` | Fail when the warning count is greater than `n`. |
| `--quiet` | Print only errors in text output. The summary still shows all counts. |
| `--color` / `--no-color` | Force or disable ANSI color on a terminal. `NO_COLOR` is honored. |
| `--config <file>` | Use a specific config file instead of discovering one. |
| `--no-config` | Ignore config discovery and use built-in defaults. |
| `--init` | Write a documented `bidilint.json` starter file and exit. |
| `--baseline <file>` | Apply a baseline of accepted findings. |
| `--update-baseline` | Rewrite the baseline from current findings. Diagnostics that cannot be baselined still decide the exit code. |
| `--no-baseline` | Ignore a baseline configured in `bidilint.json`. |
| `--debug` | Print resolved config details and full exception stacks. |
| `--help`, `--version` | Available without a path. |

Exit codes:

| Code | Meaning |
| --- | --- |
| `0` | Clean, a baseline update succeeded within the configured threshold, or the threshold tolerates the findings |
| `1` | Usage, config, or baseline-file error |
| `2` | Input or path error |
| `3` | No locale files, or the base culture is missing |
| `4` | Findings at or above the configured threshold, or above `--max-warnings` |
| `5` | Internal error |

## Configuration

BidiLint discovers `bidilint.json` by walking upward from each target path.
Comments and trailing commas are allowed. Precedence is always:

```text
CLI flags  >  bidilint.json  >  built-in defaults
```

If two target paths find different config files, BidiLint reports a usage
error; pass `--config` to choose one explicitly. `--no-config` disables
discovery. `bidilint --init` writes a starter config in the working directory;
`--init --config path/to/bidilint.json` writes it elsewhere.

```jsonc
{
  "$schema": "https://raw.githubusercontent.com/AbdullahHakima/bidilint/main/schema/bidilint.schema.json",
  "baseCulture": "en",
  "include": ["src/locales/**/*.json"],
  "exclude": ["**/generated/**"],

  "rules": {
    "UnisolatedLatin": { "severity": "warning", "allow": ["BidiLint", ".NET"] },
    "ExtraKey": "warning",
    "EmptyValue": "off"
  },

  "allowLatin": [
    { "term": "iOS", "match": "case-insensitive", "reason": "Product name." },
    { "term": "SAR", "cultures": ["ar"], "reason": "Currency code." }
  ],

  "ignore": [
    {
      "rule": "MissingKey",
      "path": "src/locales/ar.json",
      "key": "legacy.*",
      "reason": "Remove after the 2027-01 migration.",
      "expires": "2027-01-31"
    }
  ],

  "rtlCultures": ["ku-Arab"],
  "placeholderDialects": ["auto"],
  "numeralPolicy": "mixed",
  "isolationStyle": "isolate",

  "baseline": "bidilint-baseline.json",
  "failOn": "error",
  "output": {
    "format": "text",
    "color": true,
    "quiet": false,
    "maxWarnings": 20
  }
}
```

The checked-in [JSON Schema](schema/bidilint.schema.json) documents every key
and is used by editors for completion and validation. Unknown keys are config
errors with a nearest-key suggestion; misspelled rule codes get the same help.

### Path selection

`include` and `exclude` are glob patterns relative to the config file.
`*` matches within one segment, `**` crosses segments, and `?` matches one
character. If `include` is non-empty, a file must match at least one include
pattern; `exclude` always wins. Explicit files named on the command line are
always linted, even when a directory-level filter would exclude them.

`layouts` adds directory names that behave like `locales`, `i18n`, or
`translations`. `files` maps arbitrary filenames to a culture and a shared
resource set:

```jsonc
{
  "layouts": ["copy"],
  "files": [
    { "path": "copy/english.json", "culture": "en", "resourceSet": "app" },
    { "path": "copy/arabic.json", "culture": "ar", "resourceSet": "app" }
  ]
}
```

### Rule settings

Only lint rules can be reconfigured or ignored. Load, format, and configuration
diagnostics such as `MalformedFile`, `PathNotFound`, and `DuplicateKey` always
remain visible and keep their built-in severity.

Configurable rules:

| Rule | Default |
| --- | --- |
| `MissingKey` | error |
| `ExtraKey` | warning |
| `EmptyValue` | warning |
| `PlaceholderMismatch` | error |
| `UnisolatedLatin` | warning |
| `MixedNumerals` | warning |
| `UnbalancedIsolates` | error |

Set a rule to `off`, `error`, `warning`, or `info`. A rule object may also carry
`allow` (used by `UnisolatedLatin`). `allowLatin` accepts exact strings or
objects with `match: "exact" | "case-insensitive"`, an optional `reason`, and an
optional `cultures` scope.

`rtlCultures` supplements the built-in RTL language/script list.
`placeholderDialects` currently accepts `auto`, `braced`, `dotnet`, and `none`;
ICU, printf, and i18next dialect parsing is scheduled for 0.7.
`numeralPolicy` accepts `mixed` (default), `allow`, `western`, or
`arabic-indic`. `isolationStyle: "external"` is an explicit declaration that
the UI wraps text at render time; it disables `UnisolatedLatin`.

The complete rule catalog, including default severity and baseline
eligibility, is generated in [docs/rules/README.md](docs/rules/README.md).

### Ignore entries

Every `ignore` entry needs a non-empty `reason` and at least one selector:
`rule`, `path`, `culture`, or `key`. Selectors combine with AND. `path` and
`key` are globs. An optional `expires` date (`YYYY-MM-DD`) makes temporary
waivers self-cleaning. Entries that match nothing, or that have expired,
produce `StaleSuppression` warnings so the config does not grow a permanent
graveyard.

Suppressed findings are removed from the visible report but remain auditable in
the JSON `suppressedFindings` array with their source, reason, and the original
finding.

## Baselines

Baselines accept existing debt without hiding new defects. Fingerprints are
SHA-256 over the rule, lower-cased culture, key, and occurrence number, so they
survive line moves and formatting changes.

```bash
# Create or refresh the baseline.
bidilint src/locales --baseline bidilint-baseline.json --update-baseline

# Apply it in local runs and CI.
bidilint src/locales --baseline bidilint-baseline.json
```

`--update-baseline` with no baseline path writes
`bidilint-baseline.json` in the working directory. A configured `baseline` path
is relative to the config file; a `--baseline` path is relative to the working
directory. A baseline is not written when an input path is missing or the base
culture cannot be found. After a successful write, diagnostics that cannot be
baselined still decide the exit code. Baselines never suppress load, format, or
configuration diagnostics; those cannot be hidden by config. Entries that no
longer match a current finding produce `StaleBaseline` warnings, and malformed
or tampered baseline files are usage errors. Security rule baseline
restrictions are part of the 0.6 security work.

## Supported layouts

Each file is identified by a format adapter. Formats are never merged
implicitly: every document belongs to one logical resource set, and each set is
compared against its own base.

| Format | Discovery and resource-set examples |
| --- | --- |
| **JSON, culture-named file** | `en.json`, `ar-EG.json`. Nested objects and arrays flatten to dotted/indexed keys (`checkout.payNow`, `items[0].label`). Files that are not named like a culture (`app.json`) are ignored unless mapped in `files`. |
| **JSON, culture directory** | `locales/en/translation.json`, `web/locales/ar/common.json`. The culture must sit directly below a conventional layout folder (`locales`, `locale`, `i18n`, `lang`, `languages`, `translations`, `messages`, `intl`, `resources`, `po`) or a configured `layouts` name. |
| **.resx** | `Strings.resx` (base), `Strings.ar.resx` (Arabic). Non-string resources are skipped. `Errors.resx` and `Strings.resx` are separate sets. |
| **Android** | `app/src/main/res/values/strings.xml` (base), `res/values-ar/strings.xml`, `res/values-b+ar+EG/strings.xml`, `res/values-ar-rEG/strings.xml`. `<plurals>` becomes `key[one]`, `key[other]`; `<string-array>` becomes `key[0]`, `key[1]`. Non-locale qualifiers (`values-night`, `values-sw600dp`) are ignored. |
| **Apple .strings** | `Resources/en.lproj/Localizable.strings`, `Resources/ar.lproj/Localizable.strings`. `Base.lproj` maps to `--base`. Comments and C-style escapes are parsed. |
| **gettext PO/POT** | `locale/en/LC_MESSAGES/messages.po`, `locale/ar/LC_MESSAGES/messages.po`, `locale/messages.pot`. `msgid` is the key; `msgctxt` is prefixed as `context|msgid`; plural forms become `msgid[0]`, `msgid[1]`, … |
| **Flutter ARB** | `lib/l10n/app_en.arb`, `lib/l10n/app_ar.arb`, or a file with `@@locale`. Metadata keys (`@key`, `@@locale`) are skipped and never linted as user-visible text. |

A resource set is the relative path with the culture segment removed:
`locales/en/translation.json` and `locales/ar/translation.json` both belong to
`locales/translation.json`. A base must exist for every set; otherwise the
finding says which set cannot be compared.

## What it checks

Compared against the base culture:

| Check | Severity | Meaning |
| --- | --- | --- |
| `MissingKey` | error | A key exists in the base file but not in this one |
| `PlaceholderMismatch` | error | `{0}`, `{name}` placeholders differ from the base string |
| `ExtraKey` | warning | A key here does not exist in the base file (dead text) |
| `EmptyValue` | warning | The value is empty or whitespace |

File integrity:

| Check | Severity | Meaning |
| --- | --- | --- |
| `MalformedFile` | error | Invalid JSON/XML/PO/.strings, an invalid root, or a broken encoding |
| `DuplicateKey` | error | The same key appears more than once |
| `KeyCollision` | error | Flattening or merging produces the same key twice |
| `NonStringValue` | warning | A JSON value is not a string and cannot be linted |

Inside strings that contain RTL script (Arabic, Hebrew, Persian, Urdu, ...):

| Check | Severity | Meaning |
| --- | --- | --- |
| `UnisolatedLatin` | warning | A Latin run (brand, product name, code term) is not wrapped in bidi isolates, so it can render in the wrong order |
| `MixedNumerals` | warning | One string mixes Western digits (0-9) and Arabic-Indic digits (٠-٩), or violates the configured numeral policy |
| `UnbalancedIsolates` | error | An isolate opener (FSI/LRI/RLI) without its PDI, or a stray PDI |

## Sample output

From [`samples/storefront`](samples/storefront), a pair of locale files with
the usual accidents left in:

```text
samples/storefront/ar.json  error    MissingKey  checkout.payNow
  Key exists in en but is missing here.
  Base value. samples/storefront/en.json:1:82
samples/storefront/ar.json:1:75  warning  EmptyValue  footer.note
  Value is empty or whitespace.
samples/storefront/ar.json:1:114  warning  ExtraKey  offer.old
  Key does not exist in en; it may be dead text.
samples/storefront/ar.json:1:152  error    PlaceholderMismatch  products.count
  Placeholders differ from en: expected [{0}], found [].
  Base value. samples/storefront/en.json:1:184
samples/storefront/ar.json:1:152  warning  MixedNumerals  products.count
  Mixes Western digits (0-9) and Arabic-Indic digits (٠-٩) in one string; pick one numeral system per string.
samples/storefront/ar.json:1:190  error    PlaceholderMismatch  products.discount
  Placeholders differ from en: expected [{0}, {1}], found [{0}].
  Base value. samples/storefront/en.json:1:210
samples/storefront/ar.json:1:234  error    UnbalancedIsolates  promo.broken
  1 bidi isolate opener(s) without a closing PDI; everything after the opener inherits the isolation.
samples/storefront/ar.json:1:330  warning  UnisolatedLatin  skills.stack
  Latin run "ASP.NET Core" is not wrapped in bidi isolates; it can render in the wrong order inside RTL text. Wrap it (FSI..PDI) or confirm the UI isolates it.
samples/storefront/ar.json:1:330  warning  UnisolatedLatin  skills.stack
  Latin run "C#" is not wrapped in bidi isolates; it can render in the wrong order inside RTL text. Wrap it (FSI..PDI) or confirm the UI isolates it.

BidiLint 0.5.0: 2 file(s), base en, 18 entries — 4 error(s), 5 warning(s) in 72 ms.
```

Elapsed time varies between runs.

## JSON output

`--format json` (or `output.format: "json"`) emits a stable machine-readable
report. Alongside `files` and `findings`, the report includes `configPath`,
`infos`, `suppressed`, and `suppressedFindings` for auditable suppressions.
The root `unicodeVersion` field records the pinned Unicode data version used
for bidi and script lookups; `--version` prints the same version.
When a baseline is active it includes a `baseline` object with the path,
whether it was updated, its entry count, and how many findings it suppressed.
Findings carry `severity`, `code`, `culture`, `key`, `message`, `file`,
`range`, `related`, and `helpUri`.

## How it works / limitations

- Culture names are validated as known BCP-47 tags. Arbitrary files cannot be
  mistaken for translations, and explicit non-culture files are input errors.
- File discovery reports unreadable paths and malformed files instead of
  silently skipping them. Every finding carries a file and, when available, a
  line and column.
- The bidi checks are lint rules, not a full Unicode bidi algorithm (UAX #9)
  implementation. They catch recurring mistakes; they do not prove a string
  renders correctly in every context.
- With the default `numeralPolicy: "mixed"`, Western digits inside Arabic text
  are common (Egypt writes prices that way), so digits alone are not flagged.
  Only *mixing* two numeral systems in one string is. Use `western` or
  `arabic-indic` to enforce one system deliberately.
- A Latin run wrapped by the UI at render time (for example a `<bdi>` element
  or CSS `unicode-bidi: isolate`) is still flagged in the file. Declare
  `isolationStyle: "external"` only when that wrapping is guaranteed for every
  render path.
- PO plural forms are flattened. Languages with different plural counts will
  report `ExtraKey` on their additional forms until the plural-aware rule
  engine lands in 0.7.
- ARB ICU plurals are read correctly as strings, but placeholder comparison is
  still the simple braced-token scanner; ICU-aware placeholders are scheduled
  for 0.7.
- Only `auto`, `braced`, `dotnet`, and `none` are accepted for
  `placeholderDialects`. ICU, printf, and i18next parsing is scheduled for
  0.7; BidiLint does not pretend to analyze those dialects yet.
- If both `Base.lproj` and an explicit base culture (for example
  `en.lproj`) exist, both map to the base and duplicate keys are reported.
- JSON and `.resx` currently have no implicit cross-format merge. Use an
  explicit `files` mapping when formats must be paired.
- No baseline or ignore can hide load, format, or configuration diagnostics.
  Security-rule baseline restrictions are part of the 0.6 work.

## Roadmap

The detailed plan from the current 0.5 foundation to a production-ready 1.0 —
the bidi engine and security rules, SARIF, safe auto-fix, and release gates —
lives in [ROADMAP.md](ROADMAP.md).

## Development

```bash
dotnet build BidiLint.sln
dotnet test  BidiLint.sln
dotnet format BidiLint.sln --verify-no-changes
dotnet pack  src/BidiLint/BidiLint.csproj -c Release
```

CI runs build, tests, formatting, packaging, docs/schema synchronization tests,
and an install-and-run smoke test on the sample repositories.

## License

MIT — see [LICENSE](LICENSE). Built by [Abdullah Hakim](https://github.com/AbdullahHakima).
