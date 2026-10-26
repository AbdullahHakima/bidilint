# Field validation: Nextcloud RTL locale trees

BidiLint 0.7.0 was run against real Arabic, Persian, and Hebrew locale files
from Nextcloud server (`master`, downloaded 2026-10-26). The goal was to find
crashes, false positives, and gaps that unit fixtures miss, not to add every
Nextcloud string as a regression fixture. The 0.6.1 resource-set fix and the
0.7.0 placeholder parser were both checked against the same corpus.

## Corpus

| Run | Files | Entries | 0.6.1 findings | 0.7.0 findings | 0.7.0 errors | 0.7.0 warnings |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `core/l10n` (`ar`, `fa`, `en_GB`) | 3 | 1,505 | 435 | 435 | 263 | 172 |
| `apps/settings/l10n` (`ar`, `fa`, `he`, `en_GB`) | 4 | 3,318 | 1,783 | 1,792 | 985 | 807 |
| Five apps (`comments`, `dashboard`, `files`, `settings`, `sharing`) | 17 | 4,773 | 1,995 | 2,006 | 1,127 | 879 |

All runs used `--base en_GB --no-config`. No run crashed, produced an unreadable
path, or reported a malformed file. The 0.7.0 delta is entirely
`PlaceholderMismatch`: seven newly detected dropped printf arguments and four
deferred flattened-plural branches. Bidi and security counts are unchanged from
0.6.1.

## Triage

### Fixed in 0.6.1: cross-app `KeyCollision`

Before 0.6.1, every culture-named JSON file used the resource set `"json"`.
That merged unrelated apps into one logical set, so ordinary shared keys such
as `Cancel` and `Rename` produced `KeyCollision` errors across app boundaries.
The five-app run now reports zero `KeyCollision` findings because each
directory is its own resource set. Regression coverage lives in
`tests/BidiLint.Tests/AdapterTests.cs`.

### Fixed in 0.7.0: Nextcloud brace tags

Nextcloud uses brace tags such as `{s}{usage}{/s}` and a literal percent sign
before a closing tag in `{s}{usageRelative}%{/s}`. An early 0.7.0 build treated
`{/s}` as an invalid .NET argument and `%{/s}` as a Rails placeholder. That
produced three `PlaceholderSyntax` findings and twelve extra
`UnisolatedLatin` findings on the settings app.

The final parser accepts closing brace tags as named placeholders, and only
treats `%{` as Rails when the next character starts a Rails name. The final run
has zero `PlaceholderSyntax` findings and the `UnisolatedLatin` counts return to
their 0.6.1 values (700 in settings, 730 in the five-app run). Regression
coverage is
`PlaceholderTests.NextcloudBraceTagsAndPercentBeforeClosingTag_AreMasked`.

### New true positives: dropped printf arguments

The 0.7.0 printf parser found seven Persian strings in the settings app that
drop a base argument:

```text
expected [%s], found []
expected [%.1f, %s], found []
```

Examples include the app-directory permissions warning, the cron error report,
the PHP-modules recommendation, the temporary-directory status messages, and
the `.well-known` resolver warning. These are real dropped placeholders, not
parser noise; 0.6.1 did not parse printf and could not report them. Regression
coverage is `PlaceholderTests.MissingPrintfArgument_IsReported`.

### Deferred: flattened plural branches

Flattened plural groups still compare branch by branch. The core run has one
finding, the settings app has two, and the five-app run has four because it
includes the settings app:

```text
_%n app has an update available_::_%n apps have an update available_[0]
expected [%n], found []
```

The singular branch legitimately omits the count. The 0.7.0 ICU parser handles
plural messages inside one value, but JSON plural arrays and gettext plural
forms are flattened before comparison. Pairing those groups by key and culture
is the next 0.7.x slice. No broad "plural branch may omit placeholders"
heuristic was added, because that would hide genuinely dropped arguments.

### Retained: Hebrew right-to-left mark

The settings run reports one `InvisibleControl` finding for an RLM before a
maqaf and `%2$s`:

```text
ססמת ב‏־%2$s הוחלפה על ידי %1$s.
```

The mark does not change the resolved order of visible text, so the finding is
retained. Placeholder-boundary effects may need a more precise model in 0.7.x;
the security rule was not weakened.

### Policy-consistent findings

- `MissingKey` is the largest group because the Arabic, Persian, and Hebrew
  translations do not cover every `en_GB` key. These are real coverage gaps,
  not parser failures.
- `ExtraKey` is dominated by additional plural forms. This is the same
  flattened-plural limitation documented in the README and scheduled for
  0.7.x.
- `UnisolatedLatin` mostly reports acronyms, brand names, and code terms such
  as `IP`, `API`, `PHP`, `cron`, and product names. That matches the current
  rule policy; changing it would be a precision-policy change.
- `Normalization` and `Tatweel` findings are real Unicode or typographic
  observations. `Tatweel` remains configurable and can be set to `info` or
  `off` when kashida is intentional.

## Reproduction

Run from the directory that contains the downloaded locale trees so resource
sets stay relative:

```bash
dotnet build src/BidiLint/BidiLint.csproj -c Release
dotnet run --project src/BidiLint -c Release -- nextcloud-core --base en_GB --no-config
dotnet run --project src/BidiLint -c Release -- nextcloud-settings --base en_GB --no-config
dotnet run --project src/BidiLint -c Release -- nextcloud-apps --base en_GB --no-config
```

The Nextcloud corpus itself is not checked into this repository.
