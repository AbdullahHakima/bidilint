# BidiLint

**Lint your RTL locale files for the bidi bugs you only see after shipping.**

I build Arabic-first interfaces. The bugs that kept reaching production were
never in the C#; they were in the strings: a brand name like `.NET Core`
rendering in the wrong order inside an Arabic sentence, a counter mixing
Arabic-Indic and Western digits, a `{1}` placeholder the translator dropped.
Nothing failed to compile. Everything looked fine in the JSON file. It broke
in the browser, in RTL, in front of users.

BidiLint is a small `dotnet tool` that scans localization files and reports
those problems before they ship. It also catches Trojan Source bidi overrides,
BOMs, and contextless invisible controls that can hide text.

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
  "isolationWrappers": ["x-bidi", "app-ltr"],
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

Lint rules can be reconfigured or ignored. Security rules are the exception:
they cannot be disabled under `rules` and cannot be baselined. Load, format, and
configuration diagnostics such as `MalformedFile`, `PathNotFound`, and
`DuplicateKey` always remain visible and keep their built-in severity.

Configurable rules:

| Rule | Default |
| --- | --- |
| `MissingKey` | error |
| `ExtraKey` | warning |
| `EmptyValue` | warning |
| `PlaceholderMismatch` | error |
| `PlaceholderSyntax` | error |
| `PlaceholderType` | error |
| `UnisolatedLatin` | warning |
| `MixedNumerals` | warning |
| `PresentationForm` | warning |
| `Normalization` | warning |
| `Tatweel` | warning |
| `UnbalancedIsolates` | error |
| `BidiControl` | error |
| `IsolateDepth` | warning |
| `NumberAdjacency` | warning |

Set a rule to `off`, `error`, `warning`, or `info`. A rule object may also carry
`allow` (used by `UnisolatedLatin`). `allowLatin` accepts exact strings or
objects with `match: "exact" | "case-insensitive"`, an optional `reason`, and an
optional `cultures` scope.

Security rules are not configurable through `rules`:

| Rule | Default | Meaning |
| --- | --- | --- |
| `BidiOverride` | error | A visible LRO/RLO override is present; overrides can reorder or hide text (Trojan Source) |
| `InvisibleControl` | error | A BOM, disallowed C0/C1 control, contextless ZWJ/ZWNJ, or ineffective directional mark is present |

Waive a specific security finding with an `ignore` entry that names the rule and
includes a reason. A path-, culture-, or key-only ignore never suppresses a
security finding.

`rtlCultures` supplements the built-in RTL language/script list. An explicit
script subtag overrides the language default: `az-Arab` and `en-Arab` are RTL,
while `ar-Latn` and `ku-Latn` are not.
`isolationWrappers` adds HTML/XML element names, such as `x-bidi`, that already
isolate their content in the rendered UI. Built-in `<bdi>`, `<code>`,
`dir="ltr"`, and LRI/FSI/RLI controls are always recognized.
`placeholderDialects` accepts `auto`, `dotnet` (alias `braced`), `icu`,
`printf`, `i18next`, `angular`, `rails`, `javascript`, `mustache` (alias
`handlebars`), and `none`. `auto` infers the dialect from the format and value;
selecting more than one specific dialect is a config error because a message is
never parsed as two dialects at once. See [Placeholder dialects](#placeholder-dialects).
`numeralPolicy` accepts `mixed` (default), `allow`, `western`,
`arabic-indic`, or `extended-arabic-indic`. `western` uses `0-9`,
`arabic-indic` uses `٠-٩` (U+0660–U+0669), and `extended-arabic-indic` uses
`۰-۹` (U+06F0–U+06F9). `mixed` flags strings that use more than one recognized
system; the other values require that exact set. Markup and placeholders are
ignored when counting visible digits. `isolationStyle: "external"` is an
explicit declaration that the UI wraps text at render time; it disables
`UnisolatedLatin`.

The complete rule catalog, including default severity and baseline
eligibility, is generated in [docs/rules/README.md](docs/rules/README.md).

### Ignore entries

Every `ignore` entry needs a non-empty `reason` and at least one selector:
`rule`, `path`, `culture`, or `key`. Selectors combine with AND. `path` and
`key` are globs. An optional `expires` date (`YYYY-MM-DD`) makes temporary
waivers self-cleaning. Entries that match nothing, or that have expired,
produce `StaleSuppression` warnings so the config does not grow a permanent
graveyard. Security findings require the explicit `rule` selector; broad
path/key suppressions do not match them.

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
configuration diagnostics; those cannot be hidden by config. Security rules
(`BidiOverride` and `InvisibleControl`) are never written to a baseline, and a
baseline file that names one is rejected. Entries that no longer match a
current finding produce `StaleBaseline` warnings, and malformed or tampered
baseline files are usage errors.

## Supported layouts

Each file is identified by a format adapter. Formats are never merged
implicitly: every document belongs to one logical resource set, and each set is
compared against its own base.

| Format | Discovery and resource-set examples |
| --- | --- |
| **JSON, culture-named file** | `en.json`, `ar-EG.json`. Nested objects and arrays flatten to dotted/indexed keys (`checkout.payNow`, `items[0].label`). Arrays under a Nextcloud-style `_singular_::_plural_` key are paired as plural groups; other arrays remain indexed lists. Files that are not named like a culture (`app.json`) are ignored unless mapped in `files`. |
| **JSON, culture directory** | `locales/en/translation.json`, `web/locales/ar/common.json`. The culture must sit directly below a conventional layout folder (`locales`, `locale`, `i18n`, `lang`, `languages`, `translations`, `messages`, `intl`, `resources`, `po`) or a configured `layouts` name. |
| **.resx** | `Strings.resx` (base), `Strings.ar.resx` (Arabic). Non-string resources are skipped. `Errors.resx` and `Strings.resx` are separate sets. |
| **Android** | `app/src/main/res/values/strings.xml` (base), `res/values-ar/strings.xml`, `res/values-b+ar+EG/strings.xml`, `res/values-ar-rEG/strings.xml`. `<plurals>` becomes `key[one]`, `key[other]` and is paired as a plural group; `<string-array>` becomes `key[0]`, `key[1]` and remains an indexed list. Non-locale qualifiers (`values-night`, `values-sw600dp`) are ignored. |
| **Apple .strings** | `Resources/en.lproj/Localizable.strings`, `Resources/ar.lproj/Localizable.strings`. `Base.lproj` maps to `--base`. Comments and C-style escapes are parsed. |
| **gettext PO/POT** | `locale/en/LC_MESSAGES/messages.po`, `locale/ar/LC_MESSAGES/messages.po`, `locale/messages.pot`. `msgid` is the key; `msgctxt` is prefixed as `context|msgid`; plural forms become `msgid[0]`, `msgid[1]`, … and are paired as a plural group. |
| **Flutter ARB** | `lib/l10n/app_en.arb`, `lib/l10n/app_ar.arb`, or a file with `@@locale`. Metadata keys (`@key`, `@@locale`) are skipped and never linted as user-visible text. |

A resource set is the relative path with the culture segment removed:
`locales/en/translation.json` and `locales/ar/translation.json` both belong to
`locales/translation.json`. For culture-named files the culture is the file
name, so the containing directory is the set: `apps/files/l10n/en.json` and
`apps/files/l10n/ar.json` belong to `apps/files/l10n`. Root-level `en.json`
and `ar.json` belong to `.`. Directories are intentionally separate, even when
they contain the same keys; map a base and a translation that live in different
directories through `files` with the same `resourceSet` when they should be
compared. A base must exist for every set; otherwise the finding says which set
cannot be compared.

### Placeholder dialects

Placeholder comparison is AST-based. The format selects a default dialect:

| Format | Default dialect |
| --- | --- |
| JSON / JSONC | auto-detect |
| .resx | .NET composite |
| Flutter ARB | ICU MessageFormat 1 |
| Android, Apple .strings, gettext PO/POT | printf |

`placeholderDialects` overrides the default: `auto`, `dotnet` (`braced`),
`icu`, `printf`, `i18next`, `angular`, `rails`, `javascript`, `mustache`
(`handlebars`), or `none`. Only one specific dialect may be selected; `none`
disables placeholder comparison. Auto-detection recognizes escaped .NET braces,
ICU plural/select, printf positional arguments, double-braced
i18next/Angular/Mustache placeholders, Rails `%{name}`, and JavaScript
`${name}`. It is a heuristic: if a JSON value is ambiguous (for example,
literal `{{...}}` without another placeholder), set the dialect explicitly.

ICU plural and select messages compare argument names, the required `other`
branch, select branch parity, and nested placeholders. Plural categories may
differ by language; extra categories such as Arabic
`zero`/`two`/`few`/`many` are allowed. Branch message text remains visible to
bidi and security analysis; only ICU syntax, selectors, braces, `#`, and nested
placeholders are masked.

Flattened plural groups from Nextcloud-style JSON arrays, Android `<plurals>`,
and gettext plural forms are paired by group. Additional target categories are
accepted instead of reported as `ExtraKey`. Placeholder names and format/type
signatures are compared over the union of non-empty branches, so a singular
branch may omit the count while a placeholder missing from every branch is
still reported. Exact ICU selector variants are compared first; unmatched
extra categories are checked against the base union. Generic JSON arrays and
Android `<string-array>` are not plural groups and keep indexed `ExtraKey` and
`MissingKey` behavior.

## What it checks

Compared against the base culture:

| Check | Severity | Meaning |
| --- | --- | --- |
| `MissingKey` | error | A key exists in the base file but not in this one |
| `PlaceholderMismatch` | error | Placeholders differ from the base: missing, extra, renamed, or ICU branch structure |
| `PlaceholderSyntax` | error | A placeholder is malformed or unclosed |
| `PlaceholderType` | error | A placeholder's format specifier or argument type differs from the base |
| `ExtraKey` | warning | A key here does not exist in the base file (dead text) |
| `EmptyValue` | warning | The value is empty or whitespace |

File integrity:

| Check | Severity | Meaning |
| --- | --- | --- |
| `MalformedFile` | error | Invalid JSON/XML/PO/.strings, an invalid root, or a broken encoding |
| `DuplicateKey` | error | The same key appears more than once |
| `KeyCollision` | error | Flattening or merging produces the same key twice |
| `NonStringValue` | warning | A JSON value is not a string and cannot be linted |

Inside strings that contain RTL script or bidi controls (Arabic, Hebrew,
Persian, Urdu, ...):

| Check | Severity | Meaning |
| --- | --- | --- |
| `UnisolatedLatin` | warning | A Latin run (brand, URL, email, path, code term, version, or file name) is not isolated, uses the wrong isolate direction, or crosses an isolate boundary |
| `MixedNumerals` | warning | One string uses more than one of Western (0-9), Arabic-Indic (٠-٩), or Extended Arabic-Indic (۰-۹) digits, or violates the configured numeral policy |
| `UnbalancedIsolates` | error | An isolate opener (FSI/LRI/RLI) without its PDI, or a stray PDI; the finding points at the unmatched control |
| `BidiControl` | error | A legacy LRE/RLE/LRO/RLO/PDF control is unbalanced, ignored inside an isolate, or exceeds the Unicode nesting limit |
| `IsolateDepth` | warning | Isolates are nested deeper than the recommended 16 levels, or an isolate contains no visible content |
| `NumberAdjacency` | warning | Numbers separated only by neutral characters resolve in a different visual order, so they can appear swapped |

Inside any visible value, regardless of direction:

| Check | Severity | Meaning |
| --- | --- | --- |
| `PresentationForm` | warning | Arabic Presentation Forms-A/B compatibility characters are present; store the equivalent standard Arabic letters |
| `Normalization` | warning | A visible text element is not Unicode NFC, or the value mixes composed and decomposed forms |
| `Tatweel` | warning | Arabic tatweel (U+0640) is present; keep it only for intentional kashida |

Security checks (every visible value, including English and other LTR text):

| Check | Severity | Meaning |
| --- | --- | --- |
| `BidiOverride` | error | A visible LRO/RLO override can reorder or hide text (Trojan Source); use isolates instead |
| `InvisibleControl` | error | A BOM, disallowed C0/C1 control, contextless ZWJ/ZWNJ, or ineffective LRM/RLM/ALM is present |

ZWJ and ZWNJ are accepted next to Arabic, Persian, Urdu, and other
joining-script letters, after a virama in Indic text, and (for ZWJ) inside emoji
sequences. A directional mark is reported when replacing it with the
boundary-neutral sentinel leaves the paragraph level and every visible resolved
level unchanged.

BidiLint uses the checked-in UAX #9 resolver to decide whether a run is
actually at risk. It recognizes LRI/FSI/RLI..PDI, `<bdi>`, `<code>`,
`dir="ltr"`, and configured `isolationWrappers` as mitigation. An RLI, an
explicit `dir="rtl"` wrapper, or an auto-direction wrapper whose first strong
character is RTL is reported as the wrong isolate direction. Markup tags,
entities, and placeholders are masked before Latin-run, number, numeral,
normalization, and security analysis, so their identifiers do not become
findings.

The resolver passes the full Unicode 17.0.0 UAX #9 `BidiTest` and
`BidiCharacterTest` suites. The documented conformance subset and the
exclusions (paragraph splitting, line layout, rendering, and higher-level
protocols) are in [docs/conformance.md](docs/conformance.md).

For `UnisolatedLatin` and `NumberAdjacency`, the report also explains the
resolved order. Text output includes the escaped logical run, the paragraph
level, and the resolved level runs. JSON adds a `bidi` object with the same
evidence; `NumberAdjacency` also records the visual token order.

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
  Base value. samples/storefront/en.json:1:185
samples/storefront/ar.json:1:166  warning  MixedNumerals  products.count
  Found Western digits (0-9) and Arabic-Indic digits (٠-٩) in one string; pick one numeral system per string.
samples/storefront/ar.json:1:190  error    PlaceholderMismatch  products.discount
  Placeholders differ from en: expected [{0}, {1}], found [{0}].
  Base value. samples/storefront/en.json:1:225
samples/storefront/ar.json:1:240  error    UnbalancedIsolates  promo.broken
  Unclosed LRI (U+2066); it runs to the end of the value. Add a matching PDI (U+2069) after the isolated text.
samples/storefront/ar.json:1:345  warning  UnisolatedLatin  skills.stack
  Latin run "ASP.NET Core" is not isolated from the surrounding RTL text (resolved level 2). Wrap it in LRI/FSI..PDI, <bdi>, <code>, or dir="ltr".
  logical: "ASP.NET Core"; paragraph level 1 (RTL), run level 2 (LTR) for 12 code units
samples/storefront/ar.json:1:360  warning  UnisolatedLatin  skills.stack
  Latin run "C#" is not isolated from the surrounding RTL text (resolved level 2). Wrap it in LRI/FSI..PDI, <bdi>, <code>, or dir="ltr".
  logical: "C#"; paragraph level 1 (RTL), run levels 2 (LTR), 1 (RTL)

BidiLint 0.7.1: 2 file(s), base en, 18 entries — 4 error(s), 5 warning(s) in 72 ms.
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
`range`, `related`, and `helpUri`. A bidi finding also carries `bidi` with
`logical` (the escaped run), `paragraphLevel`, and `levels` (`start`, `length`,
`level`); `visual` is present when the rule can state the reordered token
order. Level offsets index the decoded logical run; `logical` escapes invisible
characters for display. `range` remains the source span.

## How it works / limitations

- Culture names are validated as known BCP-47 tags. Arbitrary files cannot be
  mistaken for translations, and explicit non-culture files are input errors.
- File discovery reports unreadable paths and malformed files instead of
  silently skipping them. Every finding carries a file and, when available, a
  line and column.
- Bidi findings use the checked-in UAX #9 resolver for a single paragraph.
  Values are resolved as one paragraph; multi-paragraph splitting and
  line-level UI context are not modeled yet, and no CSS computed direction or
  runtime DOM is available.
- The markup scanner is a small quote-aware fragment scanner for `<bdi>`,
  `<code>`, `dir`, and configured wrappers. It masks tag text but does not build
  or validate a DOM. A wrapper applied only by the UI at render time is
  invisible to BidiLint; declare `isolationStyle: "external"` only when that
  wrapping is guaranteed for every render path, or list a wrapper that is
  present in the value in `isolationWrappers`.
- `BidiControl` covers broken legacy embeddings/overrides and isolate
  structure. `BidiOverride` separately reports every visible LRO/RLO, including
  balanced Trojan Source sequences. Isolate-depth findings use a recommended
  maximum of 16; the Unicode explicit-direction limit is 125. A security finding
  can be waived only by a targeted, reasoned `ignore` entry, never by `rules` or
  a baseline.
- `NumberAdjacency` only fires when neutral-separated numeric tokens actually
  change visual order in the resolved paragraph. It can still warn when the
  right-to-left reading order is intentional; isolate the sequence or adjust
  the rule severity in that case.
- With the default `numeralPolicy: "mixed"`, Western digits inside Arabic text
  are common (Egypt writes prices that way), so digits alone are not flagged.
  Only *mixing* recognized systems in one string is flagged. Western, Arabic-Indic, and
  Extended Arabic-Indic are separate systems; the configured policy selects
  which one is expected. Other scripts' digits are outside the current numeral
  policy and are not counted as a recognized system.
- Normalization checks cover Unicode 17.0.0 NFC and Arabic Presentation
  Forms-A/B. They flag one visible text element at a time and do not rewrite
  files. `Tatweel` defaults to `warning`; set it to `info` or `off` when
  kashida is an intentional typographic choice. Presentation-form and
  normalization findings can be baselined, like lint rules.
- Flattened plural groups are paired for Nextcloud-style JSON arrays whose key
  has the `_::_` shape, Android `<plurals>`, and gettext plural forms.
  Additional target categories are accepted instead of reported as `ExtraKey`,
  and placeholder parity is compared over the union of non-empty branches so a
  singular branch may omit the count. A placeholder missing from every branch
  is still reported. Exact ICU selector variants are paired and compared in
  full; unmatched extra categories are checked against the base union. Generic
  JSON arrays and Android `<string-array>` remain indexed lists and still report
  extra and missing items.
- Branch-specific placeholder drops inside a plural group are not reported
  individually yet. Same-selector swaps and swaps between two variants that
  both add categories remain invisible because those variants are merged or
  handled by the union check. The group-level union is the 0.7.1 contract.
  CLDR plural category validation per culture is still planned for 0.7.x.
- ICU messages inside a single JSON or ARB value are compared with the ICU AST.
  ICU comparison checks argument names, required `other`, select branch parity,
  and nested placeholders. It does not yet validate that every plural category
  is legal for the target culture; that CLDR-category check is planned for
  0.7.x.
- Auto-detection is heuristic. Set `placeholderDialects` explicitly when a
  value is ambiguous, especially when literal double braces are not
  placeholders.
- If both `Base.lproj` and an explicit base culture (for example
  `en.lproj`) exist, both map to the base and duplicate keys are reported.
- JSON and `.resx` currently have no implicit cross-format merge. Use an
  explicit `files` mapping when formats must be paired.
- No baseline or ignore can hide load, format, or configuration diagnostics.
  Security findings can only be waived by a targeted, reasoned `ignore` entry
  that names the rule; they are never written to a baseline.

## Roadmap

The detailed plan from the current 0.7 foundation to a production-ready 1.0 -
per-path dialects, CLDR plural categories, SARIF, safe auto-fix, and release
gates - lives in [ROADMAP.md](ROADMAP.md).

## Development

```bash
dotnet build BidiLint.sln
dotnet test  BidiLint.sln
dotnet format BidiLint.sln --verify-no-changes
dotnet pack  src/BidiLint/BidiLint.csproj -c Release
```

CI runs build, tests, formatting, packaging, docs/schema synchronization tests,
the Unicode conformance suite, the RTL corpus golden outputs, the 100k-entry
benchmark, and an install-and-run smoke test on the sample repositories.

## License

MIT — see [LICENSE](LICENSE). Built by [Abdullah Hakim](https://github.com/AbdullahHakima).
