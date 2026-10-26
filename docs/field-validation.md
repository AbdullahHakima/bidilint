# Field validation: Nextcloud RTL locale trees

BidiLint 0.7.0 and 0.7.1 were run against real Arabic, Persian, and Hebrew
locale files from Nextcloud server (`master`, downloaded 2026-10-26). The goal
was to find crashes, false positives, and gaps that unit fixtures miss, not to
add every Nextcloud string as a regression fixture. The 0.6.1 resource-set fix,
the 0.7.0 placeholder parser, and the 0.7.1 plural grouping were all checked
against the same corpus.

## Corpus

| Run | Files | Entries | 0.6.1 findings | 0.7.0 findings | 0.7.1 findings | 0.7.1 errors | 0.7.1 warnings |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `core/l10n` (`ar`, `fa`, `en_GB`) | 3 | 1,505 | 435 | 435 | 422 | 262 | 160 |
| `apps/settings/l10n` (`ar`, `fa`, `he`, `en_GB`) | 4 | 3,318 | 1,783 | 1,792 | 1,777 | 983 | 794 |
| Five apps (`comments`, `dashboard`, `files`, `settings`, `sharing`) | 17 | 4,773 | 1,995 | 2,006 | 1,963 | 1,122 | 841 |

All runs used `--base en_GB --no-config`. No run crashed, produced an unreadable
path, or reported a malformed file. The 0.7.0 placeholder parser found seven
real dropped printf arguments and also produced flattened-plural false
positives. The 0.7.1 delta removed only those plural false positives: 13
findings in core, 15 in settings, and 43 in the five-app run. Bidi and security
counts are unchanged from 0.7.0, and no new findings were introduced.

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

### Fixed in 0.7.1: flattened plural groups

Nextcloud JSON stores plural translations as string arrays under keys such as
`_%n folder_::_%n folders_`. Arabic has six forms and legitimately omits `%n`
from the zero/one/two branches; Persian and Hebrew singular branches do the
same. Comparing those arrays branch by branch produced `ExtraKey` for the
additional forms and `PlaceholderMismatch` for the omitted count.

BidiLint 0.7.1 now marks these arrays as one plural group and compares the
union of non-empty branches. Additional target forms are accepted, and a
placeholder missing from every branch is still reported. Android `<plurals>`
and gettext `msgstr[n]` groups use the same comparison. Named Android groups
still require `other`.

The field run removed 13 findings in core (one deferred placeholder finding and
12 `ExtraKey`), 15 in settings (two deferred placeholder findings and 13
`ExtraKey`), and 43 in the five-app run (five deferred placeholder findings and
38 `ExtraKey`). The remaining seven `PlaceholderMismatch` findings in settings
and the five-app run are the real dropped printf arguments above. Generic JSON
arrays and Android `<string-array>` remain indexed lists and still report extra
items. Regression coverage is `tests/BidiLint.Tests/PluralGroupTests.cs`.

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
- The 0.7.1 runs report no `ExtraKey` findings in this corpus. The additional
  plural forms that previously dominated this category are now paired as
  groups; generic arrays still keep the old behavior.
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
