# Unicode bidi conformance fixtures

These files are the official Unicode 17.0.0 conformance tests for UAX #9,
compressed with gzip so the repository stays small while tests remain fully
offline and deterministic.

| File | Source | Uncompressed SHA-256 |
| --- | --- | --- |
| `BidiTest.txt.gz` | <https://www.unicode.org/Public/17.0.0/ucd/BidiTest.txt> | `888bdfc8090652272d1f859cdb00ae659e2dc6c26740be61ef1d03998a687620` |
| `BidiCharacterTest.txt.gz` | <https://www.unicode.org/Public/17.0.0/ucd/BidiCharacterTest.txt> | `a3e6e905ab5afbe318a96df5401d0372a04cd73ef139ab5e3cf0ae241c255488` |

Both files were retrieved from the Unicode Consortium's
`unicode-org/unicodetools` mirror because the public UCD host throttled the
large downloads during development. They identify themselves as Unicode
17.0.0 in their headers, and the uncompressed hashes above let a future
maintainer verify a fresh download before replacing a fixture.

See <https://www.unicode.org/terms_of_use.html> for the Unicode data terms.
