# BidiLint rule catalog

This file is generated from `RuleCatalog` and checked by a test.

| Rule | Default | Category | Fixable | Since | Baseline |
| --- | --- | --- | --- | --- | --- |
| [`BaseCultureMissing`](#baseculturemissing) | error | structure | no | 0.3.0 | no |
| [`CultureNotFound`](#culturenotfound) | error | discovery | no | 0.3.0 | no |
| [`DuplicateKey`](#duplicatekey) | error | format | no | 0.3.0 | no |
| [`EmptyValue`](#emptyvalue) | warning | structure | no | 0.1.0 | yes |
| [`ExtraKey`](#extrakey) | warning | structure | no | 0.1.0 | yes |
| [`KeyCollision`](#keycollision) | error | format | no | 0.3.0 | no |
| [`MalformedFile`](#malformedfile) | error | format | no | 0.3.0 | no |
| [`MissingKey`](#missingkey) | error | structure | no | 0.1.0 | yes |
| [`MixedNumerals`](#mixednumerals) | warning | bidi | no | 0.1.0 | yes |
| [`NoLocaleFiles`](#nolocalefiles) | error | discovery | no | 0.3.0 | no |
| [`NonStringValue`](#nonstringvalue) | warning | format | no | 0.3.0 | no |
| [`PathNotFound`](#pathnotfound) | error | discovery | no | 0.3.0 | no |
| [`PlaceholderMismatch`](#placeholdermismatch) | error | placeholder | no | 0.1.0 | yes |
| [`StaleBaseline`](#stalebaseline) | warning | configuration | no | 0.5.0 | no |
| [`StaleSuppression`](#stalesuppression) | warning | configuration | no | 0.5.0 | no |
| [`UnbalancedIsolates`](#unbalancedisolates) | error | bidi | no | 0.1.0 | yes |
| [`UnisolatedLatin`](#unisolatedlatin) | warning | bidi | no | 0.1.0 | yes |
| [`UnreadablePath`](#unreadablepath) | error | discovery | no | 0.3.0 | no |
| [`UnsupportedFile`](#unsupportedfile) | error | discovery | no | 0.3.0 | no |

## BaseCultureMissing

**Base culture missing** · error · structure · since 0.3.0

The configured base culture does not exist for a resource set, so that set cannot be compared.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

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

One string uses more than one numeral system, or violates the configured numeral policy.

Suppress with an `ignore` entry, or change the severity under `rules.MixedNumerals`.

## NoLocaleFiles

**No locale files** · error · discovery · since 0.3.0

No supported locale files were discovered under the selected paths.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## NonStringValue

**Non-string value** · warning · format · since 0.3.0

A JSON value is not a string and cannot be analyzed as user-visible locale text.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## PathNotFound

**Path not found** · error · discovery · since 0.3.0

A target path or configured file does not exist.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## PlaceholderMismatch

**Placeholder mismatch** · error · placeholder · since 0.1.0

The placeholders in a translated value differ from the base value.

Suppress with an `ignore` entry, or change the severity under `rules.PlaceholderMismatch`.

## StaleBaseline

**Stale baseline entry** · warning · configuration · since 0.5.0

A baseline entry no longer matches any eligible finding.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## StaleSuppression

**Stale suppression** · warning · configuration · since 0.5.0

An ignore rule did not match a finding, matched only findings that were already suppressed, or expired.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## UnbalancedIsolates

**Unbalanced isolates** · error · bidi · since 0.1.0

A bidi isolate opener has no matching PDI, or a PDI has no opener.

Suppress with an `ignore` entry, or change the severity under `rules.UnbalancedIsolates`.

## UnisolatedLatin

**Unisolated Latin run** · warning · bidi · since 0.1.0

A Latin brand, command, URL, or code term is not isolated inside RTL text.

Suppress with an `ignore` entry, or change the severity under `rules.UnisolatedLatin`.

## UnreadablePath

**Unreadable path** · error · discovery · since 0.3.0

A file or directory could not be read. Check permissions, locks, and broken links.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.

## UnsupportedFile

**Unsupported file** · error · discovery · since 0.3.0

An explicitly selected file is not in a supported locale format.

This is a load, format, or configuration diagnostic. It cannot be suppressed or reconfigured; fix the input and rerun BidiLint.
