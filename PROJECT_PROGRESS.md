# Project Progress - SAM_Systems (2026-Q4)

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `d5f239e4`. Frozen Q3 record: `sow/2026-Q3` @ `4fe7d8a0` (not modified).

## Last updated

2026-10-06 (Q4 bootstrap).

## Current status

Q4 branch cut from `master` `d5f239e4`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`). No product source changed. No Q4 product work has started.

## Q4 priorities

Not yet set by the owner. Record them here at the first Q4 planning pass. Known carry-over work is listed below.

## Known carry-over work

- **SAM Grasshopper icon redesign - PR #32** (`feature/sam-gh-icon-redesign`, open, base `sow/2026-Q3`). Known Q4 carry-over. Preserved untouched at bootstrap: not merged, not retargeted. The branch is based on Q3 history (6 commits unique to it vs `sow/2026-Q3`), and `sow/2026-Q3` is not an ancestor of `master`/`sow/2026-Q4`; retargeting needs an explicit rebase-onto decision (carry only its own commits, do not pull Q3 history into Q4).
- Branch `codex/part-o-cooling-control-room` - Q3 complete: all commits already in sow/2026-Q3.

## Repository-specific next steps

- Await Q4 planning. Open PRs for Q4 work against `sow/2026-Q4`.
- Follow the continuity convention in `AGENTS.md` for every PR and closeout.

## Decisions / assumptions

- Q4 base is `master` `d5f239e4`; the internal files were recovered from `sow/2026-Q3` into this branch only, never onto `master`.
- Q4 history intentionally does not contain the Q3 branch history (the maintained `master` is the promoted Q3 line, which is not a descendant of `sow/2026-Q3`); the frozen `sow/2026-Q3` branch is the permanent record.
- Historical Q2/Q3 content below is kept as evidence; its branch names, SHAs and next steps describe Q3 and are not current instructions.

## Validation

- Bootstrap verified 2026-10-06: `sow/2026-Q4` was created at exactly `d5f239e4` and the push was a normal (non-forced) branch creation.

## Issues / blockers

- None at bootstrap.

## Next step

- Owner to set Q4 priorities; then start the first Q4 task from this branch.

---

# Historical record - 2026-Q3 (frozen)

Source: last revision of the file on `sow/2026-Q3`, commit `5cd9ee1` (the file was removed from the Q3 tip by `7d4d972`; `sow/2026-Q3` tip is `4fe7d8a0`). Preserved verbatim except that heading levels are shifted down one. Everything below describes Q3 and is not a current instruction.

## SAM_Systems Part O PR1 progress

Base: `sow/2026-Q3`. PR1 merged as SAM-BIM/SAM_Systems#35 at `301d0fa9a8b2e8b4a6c40f81c8cf45cfa253cddf` on 2026-10-04. Local base updated.

### Completed
Removed largest-supply room heuristic; guidance settings carry explicit CoolingStatSpaceGuid and reject missing, unserved or unsupplied rooms before graph creation. No airflow or equipment physics changed.

### Files changed
MechanicalVentilationGuidanceSettings, Create.MechanicalVentilationGuidanceCooling, MechanicalVentilationGuidanceCooling, guidance/mixed cooling/system scope tests; this progress file.

### Validation
Focused guidance tests: 11 passed; mixed cooling/scope: 46 passed; broader mechanical ventilation/unit suite: 260 passed; PR Windows build and SPDX passed.

### Next step
No unresolved PR1 issues. Stop after PR1; do not start PR2 without a new request.
