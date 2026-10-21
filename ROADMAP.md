# BidiLint Roadmap

## 0.1.0 (foundation)

- JSON locale files, one per culture, nested objects flattened
- Structural checks: missing keys, extra keys, placeholder mismatch, empty values
- Bidi checks for RTL strings: unisolated Latin runs, mixed numeral systems,
  unbalanced isolates
- `--strict` CI exit code, bundled sample with real-world mistakes

## Next

- `.resx` support
- JSON output (`--format json`) for editors and dashboards
- Auto-fix for unisolated Latin runs (wrap in FSI..PDI)

## Later

- Number-adjacency rules (numbers glued to Arabic words)
- Config file: per-project base culture, ignored keys, allowed Latin terms
  (product names a team decides to leave unisolated)
