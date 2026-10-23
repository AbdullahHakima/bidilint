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

## Usage

```bash
bidilint <path> [<path>...] [--base en] [--format text|json] [--strict]
```

A path may be a folder or a single locale file. Folders are searched
recursively; `bin`, `obj`, `node_modules`, and similar generated folders are
skipped. `--base` names the source culture and defaults to `en`.

`--format json` prints a machine-readable report for editors and CI. Errors
fail the command by default. `--strict` also fails on warnings. `--help` and
`--version` are available without a path.

Exit codes:

| Code | Meaning |
| --- | --- |
| `0` | Clean |
| `1` | Usage error |
| `2` | Input or path error |
| `3` | No locale files, or the base culture is missing |
| `4` | Findings (errors, or warnings with `--strict`) |
| `5` | Internal error |

## Supported layouts

Each file is identified by a format adapter. Formats are never merged
implicitly: every document belongs to one logical resource set, and each set is
compared against its own base.

| Format | Discovery and resource-set examples |
| --- | --- |
| **JSON, culture-named file** | `en.json`, `ar-EG.json`. Nested objects and arrays flatten to dotted/indexed keys (`checkout.payNow`, `items[0].label`). Files that are not named like a culture (`app.json`) are ignored. |
| **JSON, culture directory** | `locales/en/translation.json`, `web/locales/ar/common.json`. The culture must sit directly below a conventional layout folder (`locales`, `locale`, `i18n`, `lang`, `languages`, `translations`, `messages`, `intl`, `resources`, `po`). |
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
| `MixedNumerals` | warning | One string mixes Western digits (0-9) and Arabic-Indic digits (٠-٩) |
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

BidiLint 0.4.0: 2 file(s), base en, 18 entries — 4 error(s), 5 warning(s) in 72 ms.
```

Elapsed time varies between runs.

## How it works / limitations

- Culture names are validated as known BCP-47 tags. Arbitrary files cannot be
  mistaken for translations, and explicit non-culture files are input errors.
- File discovery reports unreadable paths and malformed files instead of
  silently skipping them. Every finding carries a file and, when available, a
  line and column.
- The bidi checks are lint rules, not a full Unicode bidi algorithm (UAX #9)
  implementation. They catch recurring mistakes; they do not prove a string
  renders correctly in every context.
- Western digits inside Arabic text are common (Egypt writes prices that way),
  so digits alone are not flagged. Only *mixing* two numeral systems in one
  string is.
- A Latin run wrapped by the UI at render time (for example a `<bdi>` element
  or CSS `unicode-bidi: isolate`) will still be flagged in the file. Fix the
  file, or treat the warning as the reminder to check.
- PO plural forms are flattened. Languages with different plural counts will
  report `ExtraKey` on their additional forms until the plural-aware rule
  engine lands in 0.7.
- ARB ICU plurals are read correctly as strings, but placeholder comparison is
  still the simple braced-token scanner; ICU-aware placeholders are scheduled
  for 0.7.
- If both `Base.lproj` and an explicit base culture (for example
  `en.lproj`) exist, both map to the base and duplicate keys are reported.
- JSON and `.resx` currently have no implicit cross-format merge. Config,
  ignores, severity overrides, and baselines are the next phase (0.5).

## Roadmap

The detailed plan from the current 0.4 foundation to a production-ready 1.0 —
configuration and baselines, a real bidi engine, SARIF, safe auto-fix, and
release gates — lives in [ROADMAP.md](ROADMAP.md).

## Development

```bash
dotnet build BidiLint.sln
dotnet test  BidiLint.sln
dotnet format BidiLint.sln --verify-no-changes
dotnet pack  src/BidiLint/BidiLint.csproj -c Release
```

CI runs build, tests, formatting, packaging, and an install-and-run smoke test
on the sample repositories.

## License

MIT — see [LICENSE](LICENSE). Built by [Abdullah Hakim](https://github.com/AbdullahHakima).
