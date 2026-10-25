# BidiLint rule catalog

This file is generated from `RuleCatalog` and checked by a test.

| Rule | Default | Category | Fixable | Since | Baseline |
| --- | --- | --- | --- | --- | --- |
| [`BaseCultureMissing`](#baseculturemissing) | error | structure | no | 0.3.0 | no |
| [`BidiControl`](#bidicontrol) | error | bidi | no | 0.6.0 | yes |
| [`BidiOverride`](#bidioverride) | error | security | no | 0.6.0 | no |
| [`CultureNotFound`](#culturenotfound) | error | discovery | no | 0.3.0 | no |
| [`DuplicateKey`](#duplicatekey) | error | format | no | 0.3.0 | no |
| [`EmptyValue`](#emptyvalue) | warning | structure | no | 0.1.0 | yes |
| [`ExtraKey`](#extrakey) | warning | structure | no | 0.1.0 | yes |
| [`InvisibleControl`](#invisiblecontrol) | error | security | no | 0.6.0 | no |
| [`IsolateDepth`](#isolatedepth) | warning | bidi | no | 0.6.0 | yes |
| [`KeyCollision`](#keycollision) | error | format | no | 0.3.0 | no |
| [`MalformedFile`](#malformedfile) | error | format | no | 0.3.0 | no |
| [`MissingKey`](#missingkey) | error | structure | no | 0.1.0 | yes |
| [`MixedNumerals`](#mixednumerals) | warning | bidi | no | 0.1.0 | yes |
| [`NoLocaleFiles`](#nolocalefiles) | error | discovery | no | 0.3.0 | no |
| [`NonStringValue`](#nonstringvalue) | warning | format | no | 0.3.0 | no |
| [`Normalization`](#normalization) | warning | unicode | no | 0.6.0 | yes |
| [`NumberAdjacency`](#numberadjacency) | warning | bidi | no | 0.6.0 | yes |
| [`PathNotFound`](#pathnotfound) | error | discovery | no | 0.3.0 | no |
| [`PlaceholderMismatch`](#placeholdermismatch) | error | placeholder | no | 0.1.0 | yes |
| [`PresentationForm`](#presentationform) | warning | unicode | no | 0.6.0 | yes |
| [`StaleBaseline`](#stalebaseline) | warning | configuration | no | 0.5.0 | no |
| [`StaleSuppression`](#stalesuppression) | warning | configuration | no | 0.5.0 | no |
| [`Tatweel`](#tatweel) | warning | unicode | no | 0.6.0 | yes |
| [`UnbalancedIsolates`](#unbalancedisolates) | error | bidi | no | 0.1.0 | yes |
| [`UnisolatedLatin`](#unisolatedlatin) | warning | bidi | no | 0.1.0 | yes |
| [`UnreadablePath`](#unreadablepath) | error | discovery | no | 0.3.0 | no |
| [`UnsupportedFile`](#unsupportedfile) | error | discovery | no | 0.3.0 | no |

## BaseCultureMissing

**Base culture missing** · error · structure · since 0.3.0

The configured base culture does not exist for a resource set, so that set cannot be compared.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## BidiControl

**Bidi control** · error · bidi · since 0.6.0

Legacy bidi embeddings or overrides are unbalanced, ineffective across an isolate, or exceed the Unicode nesting limit. Directional marks are handled by the security rules.

Suppress with an `ignore` entry, or change the severity under `rules.BidiControl`.

## BidiOverride

**Bidi override** · error · security · since 0.6.0

A left-to-right or right-to-left override (LRO/RLO) is visible in the value. Override sequences can reorder or hide text and are the basis of Trojan Source attacks; use isolates instead.

This is a security rule. Waive a specific finding only with an `ignore` entry that names this rule and includes a reason. It cannot be disabled under `rules` or suppressed by a baseline.

## CultureNotFound

**Culture not found** · error · discovery · since 0.3.0

The filename or metadata does not contain a usable BCP-47 culture.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## DuplicateKey

**Duplicate key** · error · format · since 0.3.0

The same key is defined more than once in one file. The first value is used, which makes the later value dead and surprising.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## EmptyValue

**Empty value** · warning · structure · since 0.1.0

The value is empty or contains only whitespace.

Suppress with an `ignore` entry, or change the severity under `rules.EmptyValue`.

## ExtraKey

**Extra key** · warning · structure · since 0.1.0

A key exists only in the translated resource set. It may be dead text left behind by a redesign.

Suppress with an `ignore` entry, or change the severity under `rules.ExtraKey`.

## InvisibleControl

**Invisible control** · error · security · since 0.6.0

Visible text contains a BOM, a disallowed C0/C1 control, a zero-width joiner or non-joiner without a legitimate shaping or emoji context, or a directional mark that does not affect resolved order.

This is a security rule. Waive a specific finding only with an `ignore` entry that names this rule and includes a reason. It cannot be disabled under `rules` or suppressed by a baseline.

## IsolateDepth

**Isolate depth** · warning · bidi · since 0.6.0

Bidi isolates are nested more deeply than the recommended limit, or an isolate contains no visible content.

Suppress with an `ignore` entry, or change the severity under `rules.IsolateDepth`.

## KeyCollision

**Key collision** · error · format · since 0.3.0

Flattening or merging produces the same logical key twice. Split or rename the conflicting entries.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## MalformedFile

**Malformed file** · error · format · since 0.3.0

The file could not be decoded or parsed. Fixing the syntax is required before linting can continue.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## MissingKey

**Missing key** · error · structure · since 0.1.0

A key exists in the base resource set but not in this translated resource set.

Suppress with an `ignore` entry, or change the severity under `rules.MissingKey`.

## MixedNumerals

**Mixed numerals** · warning · bidi · since 0.1.0

One string uses more than one numeral system, or violates the configured numeral policy. Western, Arabic-Indic, and Extended Arabic-Indic digits are distinguished.

Suppress with an `ignore` entry, or change the severity under `rules.MixedNumerals`.

## NoLocaleFiles

**No locale files** · error · discovery · since 0.3.0

No supported locale files were discovered under the selected paths.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## NonStringValue

**Non-string value** · warning · format · since 0.3.0

A JSON value is not a string and cannot be analyzed as user-visible locale text.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## Normalization

**Non-NFC text** · warning · unicode · since 0.6.0

A visible text element is not in Unicode NFC, or the value mixes composed and decomposed forms. Normalize values at the source so search, comparison, and rendering are stable.

Suppress with an `ignore` entry, or change the severity under `rules.Normalization`.

## NumberAdjacency

**Number adjacency** · warning · bidi · since 0.6.0

Two or more logically adjacent numeric tokens resolve into a different visual order, so the numbers can appear swapped in RTL text.

Suppress with an `ignore` entry, or change the severity under `rules.NumberAdjacency`.

## PathNotFound

**Path not found** · error · discovery · since 0.3.0

A target path or configured file does not exist.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## PlaceholderMismatch

**Placeholder mismatch** · error · placeholder · since 0.1.0

The placeholders in a translated value differ from the base value.

Suppress with an `ignore` entry, or change the severity under `rules.PlaceholderMismatch`.

## PresentationForm

**Arabic presentation form** · warning · unicode · since 0.6.0

Arabic Presentation Forms-A or -B (U+FB50-U+FDFF and U+FE70-U+FEFE) are present. These compatibility characters should be stored as standard Arabic letters so normalization, search, and shaping remain stable.

Suppress with an `ignore` entry, or change the severity under `rules.PresentationForm`.

## StaleBaseline

**Stale baseline entry** · warning · configuration · since 0.5.0

A baseline entry no longer matches any eligible finding.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## StaleSuppression

**Stale suppression** · warning · configuration · since 0.5.0

An ignore rule did not match a finding, matched only findings that were already suppressed, or expired.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## Tatweel

**Arabic tatweel** · warning · unicode · since 0.6.0

Arabic tatweel (U+0640) is present. It elongates glyphs but is not a letter; keep it only for intentional kashida and configure the rule when that policy is deliberate.

Suppress with an `ignore` entry, or change the severity under `rules.Tatweel`.

## UnbalancedIsolates

**Unbalanced isolates** · error · bidi · since 0.1.0

A bidi isolate opener has no matching PDI, or a PDI has no opener. The finding points at the unmatched control.

Suppress with an `ignore` entry, or change the severity under `rules.UnbalancedIsolates`.

## UnisolatedLatin

**Unisolated Latin run** · warning · bidi · since 0.1.0

A Latin brand, command, URL, code term, or other left-to-right run is not isolated inside RTL text, uses the wrong isolate direction, or crosses an isolate boundary.

Suppress with an `ignore` entry, or change the severity under `rules.UnisolatedLatin`.

## UnreadablePath

**Unreadable path** · error · discovery · since 0.3.0

A file or directory could not be read. Check permissions, locks, and broken links.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## UnsupportedFile

**Unsupported file** · error · discovery · since 0.3.0

An explicitly selected file is not in a supported locale format.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.
