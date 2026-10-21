# BidiLint Roadmap

## 0.1.0 (foundation)

- JSON locale files, one per culture, nested objects flattened
- Structural checks: missing keys, extra keys, placeholder mismatch, empty values
- Bidi checks for RTL strings: unisolated Latin runs, mixed numeral systems,
  unbalanced isolates
- `--strict` CI exit code, bundled sample with real-world mistakes

## 0.2.0

- `.resx` support: satellite files by name (`Strings.ar.resx`), a neutral
  `Strings.resx` counts as the base culture, non-string resources skipped
- JSON and .resx merge per culture (JSON wins on duplicate keys)
- `--format json` for editors and CI tooling

## Next

- Auto-fix for unisolated Latin runs (wrap in FSI..PDI)
- Number-adjacency rules (numbers glued to Arabic words)

## Later

- Config file: per-project base culture, ignored keys, allowed Latin terms
  (product names a team decides to leave unisolated)
