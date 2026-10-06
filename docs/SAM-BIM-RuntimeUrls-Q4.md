# SAM-BIM runtime URLs (Q4) - SAM_Systems

PR: SAM-BIM/SAM_Systems#38. Branch `fix/sam-bim-runtime-urls-q4` -> base `sow/2026-Q4` (Q4 base `edc8d0d`). Record date: 2026-10-06.

## Current status

PR open, **not merged**. Source-only, minimum change: the "source code" menu action of one Grasshopper component opened
the HoareLea repository and now opens SAM-BIM/SAM. No `.gitmodules`, gitlink, workflow, `master`, `sow/2026-Q3` or icon-redesign change.

## Work completed

| File | Old HoareLea destination | New SAM-BIM destination |
|---|---|---|
| `Grasshopper/SAM.Analytical.Grasshopper.Systems/Component/SAMAnalyticalSystemResults.cs` (`OnSourceCodeClick`) | `https://github.com/HoareLea/SAM` | `https://github.com/SAM-BIM/SAM` |

## Why the change is required

SAM-BIM is now the authoritative development ecosystem and HoareLea is no longer the synchronised operational source, so
the component's "source code" action should open the maintained repository. It matches the shared `OnSourceCodeClick` in SAM
(`SAMCoreInspect`, `SAMCoreFilterByType`) and SAM's `AppendSourceCodeAdditionalMenuItem`, which already uses `github.com/SAM-BIM`.

## Decisions and assumptions

- `Kernel/AssemblyInfo.cs` author/contact strings are provenance metadata and are unchanged (owner decision: KEEP).
- This file already carries the SPDX header, so no header change was needed.

## Files changed

`SAMAnalyticalSystemResults.cs` (1 line) and this record.

## Validation

- `git diff --check` clean; diff reviewed.
- `msbuild SAM_Systems.sln -p:Configuration=Release` with `APPDATA`/`USERPROFILE` redirected: 0 errors;
  `SAM.Analytical.Grasshopper.Systems.gha` contains `https://github.com/SAM-BIM/SAM` and no `https://github.com/HoareLea/SAM`.
- `SAM.Analytical.Systems.Tests` were not run locally; the change touches no code path they cover. PR CI (`build`, `spdx`) green; see the PR.

## Unresolved issues, risks

- None introduced by this change.

## Exact next step

Merge into `sow/2026-Q4` after green CI (maintainer-approved). After merge, the `PROJECT_PROGRESS.md` closeout is a direct docs commit on `sow/2026-Q4` (never on this branch).
