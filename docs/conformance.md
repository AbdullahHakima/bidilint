# Unicode bidi conformance

BidiLint pins Unicode 17.0.0 and implements the Unicode Bidirectional Algorithm
(UAX #9) for one paragraph. This page records the conformance subset, how it is
verified, and what is intentionally outside the engine.

## Verified suite

The repository checks in the official Unicode 17.0.0 conformance files,
compressed with gzip:

| File | Cases exercised | Uncompressed SHA-256 |
| --- | ---: | --- |
| `BidiTest.txt.gz` | 770,241 | `888bdfc8090652272d1f859cdb00ae659e2dc6c26740be61ef1d03998a687620` |
| `BidiCharacterTest.txt.gz` | 91,707 | `a3e6e905ab5afbe318a96df5401d0372a04cd73ef139ab5e3cf0ae241c255488` |

The `BidiTest` count is the number of class-sequence and paragraph-direction
combinations exercised; the `BidiCharacterTest` count is the number of
code-point test lines. No conformance lines are skipped. The test harness
asserts both totals so an accidentally truncated fixture fails the build.

`BidiConformanceTests` verifies, for every case:

- the paragraph level for explicit and auto directions;
- the resolved embedding level of every scalar;
- the set of characters removed by rule X9;
- the L2 visual order of the remaining characters.

The compressed fixtures, their checksums, and the tests run on every
`dotnet test` invocation. See
[`tests/BidiLint.Tests/Conformance/README.md`](../tests/BidiLint.Tests/Conformance/README.md)
for provenance.

## Implemented scope

The resolver covers the core UAX #9 rules for a single paragraph:

- X1-X10: paragraph level, explicit embeddings, overrides, isolates, and
  removal of bidi controls from the resolved sequence;
- W1-W7: weak type resolution;
- N0-N2: paired brackets, neutral runs, and isolate boundaries;
- I1-I2: implicit levels;
- L1: whitespace and isolate resets at the end of the paragraph;
- L2: visual reordering.

It uses the pinned Unicode 17.0.0 bidi classes, paired-bracket data, and
script data, iterates Unicode scalar values (including supplementary-plane
characters), and supports automatic, explicit LTR, and explicit RTL paragraph
directions. The linter invokes it on each visible locale value after masking
markup and placeholders, then maps the resolved evidence back to the value's
source span.

## Exclusions

These are outside the engine and outside the Unicode conformance suite:

- **Paragraph segmentation.** Each locale value is resolved as one paragraph.
  CR, LF, and other paragraph separators are not used to split a value into
  independently resolved paragraphs. Callers that analyze prose should split
  first.
- **Line breaking and line-level L1.** BidiLint does not perform line layout,
  wrapping, or per-display-line trailing-whitespace resets. L1 is applied at the
  paragraph level only.
- **Rendering.** Glyph shaping, font fallback, combining-mark reordering, and
  mirroring (L3-L4), CSS layout, DOM direction, and browser-specific line
  layout are not modeled. A clean result means the string's bidi structure is
  sound, not that a particular renderer will draw it identically.
- **Higher-level protocols.** The resolver does not implement CSS
  `unicode-bidi`, HTML `dir` processing, plaintext mode, or application-level
  bidi overrides. The linter recognizes `<bdi>`, `<code>`, `dir="ltr"`, and
  configured wrappers as mitigation for its own rules; that markup heuristic is
  separate from UAX #9.
- **Malformed UTF-16 and format errors.** Invalid encodings and malformed files
  are reported by the format layer before bidi analysis. They are not part of
  the conformance suite.
- **Rule heuristics.** `UnisolatedLatin`, `MixedNumerals`, `BidiControl`,
  `BidiOverride`, `InvisibleControl`, and the other lint rules are layered on
  top of resolved levels. Their policy choices have their own tests and are not
  claims of UAX #9 conformance.

The 0.6 fixture corpus at
[`tests/fixtures/rtl-corpus`](../tests/fixtures/rtl-corpus) exercises every 0.6
rule with a positive and a benign counterpart across Arabic, Hebrew, Persian,
and Urdu resource sets. Golden text and JSON reports are checked in under
[`tests/golden`](../tests/golden).
