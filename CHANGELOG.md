# Changelog

## [0.2.0] - 2026-10-21

- `.resx` support: satellite files by name (`Strings.ar.resx`), a neutral
  `Strings.resx` counts as the base culture, non-string resources skipped.
  JSON and .resx merge per culture (JSON wins on duplicate keys).
- `--format json`: machine-readable report (files, counts, findings) for
  tooling. Sample .resx pair under `samples/resx-storefront`.

## [0.1.0] - 2026-10-21

- First release: JSON locale loading, structural checks against a base culture,
  and bidi checks (unisolated Latin runs, mixed numerals, unbalanced isolates)
  for RTL strings. `--strict` exit code for CI. Sample locale pair under
  `samples/storefront`.
