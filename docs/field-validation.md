# Field validation: Nextcloud RTL locale trees

BidiLint 0.6.1 was run against real Arabic, Persian, and Hebrew locale files
from Nextcloud server (`master`, downloaded 2026-10-26). The goal was to find
crashes, false positives, and gaps that unit fixtures miss, not to add every
Nextcloud string as a regression fixture.

## Corpus

| Run | Files | Entries | Errors | Warnings | Findings |
| --- | ---: | ---: | ---: | ---: | ---: |
| `core/l10n` (`ar`, `fa`, `en_GB`) | 3 | 1,505 | 263 | 172 | 435 |
| `apps/settings/l10n` (`ar`, `fa`, `he`, `en_GB`) | 4 | 3,318 | 976 | 807 | 1,783 |
| Five apps (`comments`, `dashboard`, `files`, `settings`, `sharing`) | 17 | 4,773 | 1,116 | 879 | 1,995 |

All runs used `--base en_GB --no-config`. No run crashed, produced an unreadable
path, or reported a malformed file.

## Triage

### Fixed: cross-app `KeyCollision`

Before 0.6.1, every culture-named JSON file used the resource set `"json"`.
That merged unrelated apps into one logical set, so ordinary shared keys such
as `Cancel` and `Rename` produced `KeyCollision` errors across app boundaries.
The five-app run now reports zero `KeyCollision` findings because each
directory is its own resource set. Regression coverage lives in
`tests/BidiLint.Tests/AdapterTests.cs`.

### Deferred: plural-branch `PlaceholderMismatch`

The core run reports one placeholder finding:

```text
translations._{count} file conflict_::_{count} file conflicts_[0]
expected [{count}], found []
```

The English base branch is `"{count} file conflict"`. The Arabic singular
branch is `"لا يوجد تعارض"` ("there is no conflict"), which legitimately does
not repeat the count. Comparing flattened plural branches one by one therefore
produces a false positive. The 0.6.1 rule is unchanged; plural-aware
placeholder comparison belongs to the 0.7 placeholder/plural AST.

### Retained: Hebrew right-to-left mark

The settings run reports one `InvisibleControl` finding for an RLM before a
maqaf and `%2$s`:

```text
ססמת ב‏־%2$s הוחלפה על ידי %1$s.
```

The mark does not change the resolved order of visible text, so the finding is
retained. Placeholder-boundary effects may need a more precise model in 0.7;
the security rule was not weakened for 0.6.1.

### Policy-consistent findings

- `MissingKey` is the largest group because the Arabic, Persian, and Hebrew
  translations do not cover every `en_GB` key. These are real coverage gaps,
  not parser failures.
- `ExtraKey` is dominated by additional plural forms. This is the same
  flattened-plural limitation documented in the README and scheduled for 0.7.
- `UnisolatedLatin` mostly reports acronyms, brand names, and code terms such
  as `IP`, `API`, `PHP`, `cron`, and product names. That matches the current
  rule policy; changing it would be a precision-policy change, not a 0.6.1
  hotfix.
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
