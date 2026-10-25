# 0.6 RTL fixture corpus

This corpus is an end-to-end fixture for the 0.6 bidi engine, security rules,
and Unicode normalization rules. Each top-level directory is an independent
resource set with its own `locales/en` base, so Hebrew, Persian, and Urdu are
compared against their own English source instead of sharing one merged base.

| Directory | Culture | Keys | What it exercises |
| --- | --- | ---: | --- |
| `rules` | `ar` | 26 | Every 0.6 rule, including three separate `InvisibleControl` cases |
| `hebrew` | `he` | 6 | Hebrew linting, mixed numerals, and visual-order reordering |
| `persian` | `fa` | 6 | Persian linting, Extended Arabic-Indic numerals, and a legitimate ZWNJ |
| `urdu` | `ur` | 6 | Urdu linting, visual-order reordering, and a contextless ZWJ |

The full corpus contains 8 files and 88 entries. It produces 8 errors and 14
warnings; every diagnostic is attributable to a key ending in `.positive`.
Every key ending in `.benign` is a counterexample that must not produce a 0.6
finding.

## Rule coverage

| Rule | Positive key | Benign counterpart |
| --- | --- | --- |
| `UnisolatedLatin` | `unisolatedLatin.positive` | `unisolatedLatin.benign` |
| `MixedNumerals` | `mixedNumerals.positive` | `mixedNumerals.benign` |
| `UnbalancedIsolates` | `unbalancedIsolate.positive` | `unbalancedIsolate.benign` |
| `BidiControl` | `legacyControl.positive` | `legacyControl.benign` |
| `IsolateDepth` | `isolateDepth.positive` | `isolateDepth.benign` |
| `NumberAdjacency` | `numberAdjacency.positive` | `numberAdjacency.benign` |
| `PresentationForm` | `presentationForm.positive` | `presentationForm.benign` |
| `Normalization` | `normalization.positive` | `normalization.benign` |
| `Tatweel` | `tatweel.positive` | `tatweel.benign` |
| `BidiOverride` | `security.override.positive` | `security.override.benign` |
| `InvisibleControl` | `security.bom.positive`, `security.joiner.positive`, `security.mark.positive` | `security.bom.benign`, `security.joiner.benign`, `security.mark.benign` |

The language-specific sets add positive and benign `UnisolatedLatin` cases in
Hebrew, Persian, and Urdu, plus `MixedNumerals` and `NumberAdjacency` cases.
The Persian and Urdu sets also exercise legitimate and contextless joiners.

Controls and invisible characters are stored as JSON `\uXXXX` escapes so the
fixture remains reviewable. Do not replace them with literal invisible
characters.

## Golden outputs

`tests/golden/rtl-corpus.txt` and `tests/golden/rtl-corpus.json` are rendered
from the corpus with `ElapsedMilliseconds = 0`. The version in the golden JSON
is pinned to the package version. The `RtlCorpusTests` test checks both the
coverage invariants above and byte-for-byte normalized golden output, so a rule
message, span, ordering, or summary change is visible in review.
