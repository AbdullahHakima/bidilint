# BidiLint

**Lint your Arabic/English locale files for the bidi bugs you only see after shipping.**

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
bidilint <folder> [--base en] [--format text|json] [--strict]
```

Two file shapes, mixed freely in one folder tree:

- **JSON** — one file per culture, named by culture: `en.json`, `ar.json`,
  `ar-EG.json`, and so on. Nested objects flatten to dotted keys
  (`checkout.payNow`). Files that are not named like a culture are ignored.
- **.resx** — `Strings.ar.resx` is Arabic; a neutral `Strings.resx` counts
  as the base culture. Non-string resources (images, file references) are
  skipped.

If a culture exists in both shapes, the entries merge and JSON wins on a
duplicate key. `--format json` prints a machine-readable report (files,
counts, findings) for editors and CI annotations.

- Exit codes: `0` ok · `1` usage error · `2` path not found · `3` no locale
  files or base file missing · `4` errors found under `--strict` (for CI).

## What it checks

Compared against the base culture:

| Check | Severity | Meaning |
| --- | --- | --- |
| `MissingKey` | error | A key exists in the base file but not in this one |
| `PlaceholderMismatch` | error | `{0}`, `{name}` placeholders differ from the base string |
| `ExtraKey` | warning | A key here does not exist in the base file (dead text) |
| `EmptyValue` | warning | The value is empty or whitespace |

Inside RTL strings (Arabic, Hebrew, Persian, Urdu, ...):

| Check | Severity | Meaning |
| --- | --- | --- |
| `UnisolatedLatin` | warning | A Latin run (brand, product name, code term) is not wrapped in bidi isolates, so it can render in the wrong order |
| `MixedNumerals` | warning | One string mixes Western digits (0-9) and Arabic-Indic digits (٠-٩) |
| `UnbalancedIsolates` | error | An isolate opener (FSI/LRI/RLI) without its PDI, or a stray PDI |

## Sample output

From [`samples/storefront`](samples/storefront), a pair of locale files with
the usual accidents left in:

```text
ar:
  error   MissingKey · checkout.payNow
         Key exists in en but is missing here.
  warning EmptyValue · footer.note
         Value is empty or whitespace.
  warning ExtraKey · offer.old
         Key does not exist in en; it may be dead text.
  error   PlaceholderMismatch · products.discount
         Placeholders differ from en: expected [{0}, {1}], found [{0}].
  warning MixedNumerals · products.count
         Mixes Western digits (0-9) and Arabic-Indic digits (٠-٩) in one string; pick one numeral system per string.
  error   UnbalancedIsolates · promo.broken
         1 bidi isolate opener(s) without a closing PDI; everything after the opener inherits the isolation.
  warning UnisolatedLatin · skills.stack
         Latin run "ASP.NET Core" is not wrapped in bidi isolates; it can render in the wrong order inside Arabic text. Wrap it (FSI..PDI) or confirm the UI isolates it.
  warning UnisolatedLatin · skills.stack
         Latin run "C#" is not wrapped in bidi isolates; it can render in the wrong order inside Arabic text. Wrap it (FSI..PDI) or confirm the UI isolates it.

BidiLint 0.2.0: 2 file(s), base en, 18 keys — 4 error(s), 5 warning(s) in 68 ms.
```

## How it works / limitations

- JSON and .resx files. One neutral .resx counts as the base culture; the
  base of a JSON-only project is the file named after `--base`.
- The bidi checks are lint rules, not a full Unicode bidi algorithm (UAX #9)
  implementation. They catch the recurring mistakes; they do not prove a
  string renders correctly in every context.
- Western digits inside Arabic text are common (Egypt writes prices that
  way), so digits alone are not flagged. Only *mixing* two numeral systems
  in one string is.
- A Latin run wrapped by the UI at render time (for example a `<bdi>`
  element or a CSS `unicode-bidi: isolate`) will still be flagged in the
  file. Fix the file, or treat the warning as the reminder to check.

## Roadmap

- Auto-fix for the mechanical cases (wrapping a run in FSI..PDI)
- Number-adjacency rules (a number glued to Arabic words without isolation)
- Config file: per-project base culture, ignored keys, allowed Latin terms

## Development

```bash
dotnet build BidiLint.sln
dotnet test  BidiLint.sln
dotnet pack  src/BidiLint/BidiLint.csproj -c Release
```

## License

MIT — see [LICENSE](LICENSE). Built by [Abdullah Hakim](https://github.com/AbdullahHakima).
