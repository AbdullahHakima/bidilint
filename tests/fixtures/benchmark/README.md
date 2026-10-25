# 100k-entry benchmark fixture

The benchmark workload is generated deterministically by
`tests/BidiLint.Tests/BenchmarkTests.cs` instead of checking 500 generated JSON
files into the repository. It models a localization tree with:

- 250 independent resource sets;
- one `locales/en` base and one `locales/ar` target per set;
- 200 keys per culture file;
- 100,000 entries across 500 files in total.

The timed section covers file discovery, JSON parsing, base/target comparison,
RTL detection, and linting. Fixture generation and test startup are excluded.
The fixture uses benign Arabic values, so the run should produce no findings.

## Budget

The CI gate is **five seconds** for the 100,000-entry workload on a
GitHub-hosted runner. The test also fails if the fixture no longer has 500 files
or 100,000 entries, or if it starts producing findings. The 1.0 target remains
three seconds on a developer laptop and peak memory under 256 MB; the 0.6 gate
is deliberately looser to avoid flaky shared-runner failures.

On 2026-10-26 the workload completed in about 1.8 seconds locally.
