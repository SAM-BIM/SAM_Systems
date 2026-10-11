# Icon migration validation — 2026-10-11

Base: `sow/2026-Q4`. Scope: SAM-BIM only. Reference: [SAM#179](https://github.com/SAM-BIM/SAM/pull/179); [SAM#166](https://github.com/SAM-BIM/SAM/pull/166) is the closed historical design source.

## Changes and decisions

- Grasshopper manifest coverage: 38 concrete objects. GUIDs, APIs, namespaces, component metadata and behavior are preserved.
- Reuse the frozen SAM glyph/palette/badge grammar and frozen SAM-BIM extension v1. Preserve plugin identity, external integration logos, severity symbols and historical attribution.
- Source comparison against current base: 38 icon resource token swaps; 0 other production C# changes, apart from standard SPDX headers and additive resource properties.
- Validated 38 manifest resource references; 0 errors.

## Fresh validation

- VS MSBuild Restore,Build, Release: **PASS**. Scratch APPDATA/USERPROFILE; current sibling SAM-BIM dependencies, OCCT native build skipped.
- Regression suites: SAM_Systems-0.trx: passed 305, failed 0, total 305; SAM_Systems-1.trx: passed 123, failed 0, total 123
- Re-parsed compiled component declarations: 38/38 manifest rows; 0 coverage/canonical 24x24 RGBA PNG errors.
- Built assembly resource presence and exact embedded PNG bytes checked.

Next step: verify final-head GitHub checks and merge only if green and safe under the owner authorization. No force-push or branch deletion. Hosted Rhino smoke is tracked separately; prior historical results are not fresh runs.
