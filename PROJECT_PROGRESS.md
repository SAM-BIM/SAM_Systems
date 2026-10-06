# Project Progress - SAM_Systems (2026-Q4)

## Branch

`sow/2026-Q4` - bootstrapped 2026-10-06 from `master` `d5f239e4`. Frozen Q3 record: `sow/2026-Q3` @ `4fe7d8a0` (not modified).

## Last updated

2026-10-06 (Q4 operational cleanup).

## Current status

Q4 branch cut from `master` `d5f239e4`, which is the exact commit pinned in SAM_Deploy's frozen Q3 baseline (`v20261006.1`). Bootstrap added only internal docs (this file, `AGENTS.md`). No product source changed. No Q4 product work has started.

## Q4 priorities

Not yet set by the owner. Record them here at the first Q4 planning pass. Known carry-over work is listed below.

## Known carry-over work

- **SAM Grasshopper icon redesign - PR #32** (`feature/sam-gh-icon-redesign` @ `4c32610d`, open, base `sow/2026-Q3`, not merged). Analysed 2026-10-06: the branch carries only its own 6 icon-only commits (`bad9d31`, `5822f5e`, `ca0cc8d`, `028cca0`, `0a15f74`, `4c32610`) on top of Q3 commit `fbef48ff`. Those commits are not reachable from `sow/2026-Q4` (Q4 is built on the promoted `master` line), so a plain retarget would list 91 commits. Replaying exactly those commits onto `sow/2026-Q4` @ `054fade6` is conflict-free (verified commit-by-commit with `git merge-tree`; identical to the net-diff merge). Planned action: rebase-onto Q4 as a new branch + PR, then close this one; owner-approved controlled task, not yet executed.
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

## Q4 operational cleanup (2026-10-06)

- Reviewed every active Q2/Q3 reference in this repository on `sow/2026-Q4` (workflow branch filters, dependency-branch resolution, `.gitmodules`/validation, docs). Historical Q2/Q3 mentions (feature documentation records, the frozen Q3 section below) are intentionally unchanged.
- Changed (`054fade`): removed the dead `$candidates += 'sow/2026-Q2'` fallback from the dependency-branch resolution in `.github/workflows/build.yml`. No dependency repository has a `sow/2026-Q2` branch, so the entry never matched and resolution already fell through to the default branch; behaviour is unchanged (PR head ref, current sow ref, then the dependency's default branch) and no per-quarter edit is needed.
- Checked, no action: the `github.repository_owner == 'SAM-BIM'` build guard (intentional; its comment names HoareLea only to explain why the guard exists), CODEOWNERS (SAM-BIM owners), and workflow secrets (no HoareLea-named secret). The local `upstream` (HoareLea) remote is preserved.
- Carry-over: **SAM Grasshopper icon redesign - PR #32** (`feature/sam-gh-icon-redesign` @ `4c32610d`, open, base `sow/2026-Q3`, not merged). Analysed 2026-10-06: the branch carries only its own 6 icon-only commits (`bad9d31`, `5822f5e`, `ca0cc8d`, `028cca0`, `0a15f74`, `4c32610`) on top of Q3 commit `fbef48ff`. Those commits are not reachable from `sow/2026-Q4` (Q4 is built on the promoted `master` line), so a plain retarget would list 91 commits. Replaying exactly those commits onto `sow/2026-Q4` @ `054fade6` is conflict-free (verified commit-by-commit with `git merge-tree`; identical to the net-diff merge). Planned action: rebase-onto Q4 as a new branch + PR, then close this one; owner-approved controlled task, not yet executed.
- Full cross-repository record, migration table and owner decisions: `SAM_Deploy:sow/2026-Q4` `PROJECT_PROGRESS.md`.

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
