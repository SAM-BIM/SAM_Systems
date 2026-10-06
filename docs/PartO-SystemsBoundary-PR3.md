<!-- SPDX-License-Identifier: LGPL-3.0-or-later -->
<!-- Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors -->

# Part O PR-3 (SAM_Systems half): `Create.MechanicalVentilation` honours a stated ventilation system scope

**Status (30 Sep 2026): implemented and tested. [SAM-BIM/SAM_Systems#34](https://github.com/SAM-BIM/SAM_Systems/pull/34) is open against
`sow/2026-Q3` and is NOT merged. Merge it first; [SAM-BIM/SAM_UI#153](https://github.com/SAM-BIM/SAM_UI/pull/153) calls the new overload.**

- Branch `feature/parto-pr3-systems-scope-2026-09-30`, from `sow/2026-Q3` `aadb1b1`.
- Architecture authority: SAM_UI `documentation/PartO-ModelStateArchitecture.md`, step 4 (PR-3). Nothing here
  contradicts it.
- Cross-repo record, the real-model replay and the SAM_UI wiring: SAM_UI `documentation/PartO-SystemsBoundary-PR3.md`.
- Depends on SAM `sow/2026-Q3` with PR-1 (SAM#171) and PR-2 (SAM#172), both merged. No SAM change.

## Root cause

SAM decides which ventilation systems a Part O Systems-route run assesses (`Query.PartOSystemsMaterialisationScope`).
SAM_Systems had no way to be told. It read every `VentilationSystem` of the cluster it was handed, in two places:

1. **D2** (`Create/MechanicalVentilation.cs`) enumerated every system and resolved each one's unit, so the model's
   unit-less `NV 1`/`UV 1` refused ("names no air handling unit"). Any malformed system anywhere refused the call too.
2. **D10**, the reconciliation's duty cross-check (`Create/MechanicalVentilationReconcile.cs`), called SAM's
   `AirHandlingUnitDesignDuty`. That sums every system naming the unit, over the whole model. So a system left out
   that names a scoped unit would add its duty back and refuse a correct design.

PR-1 made both callers hand over a working copy with the other systems removed. That is correct, but SAM_Systems
itself did not enforce it.

## The change (additive)

```csharp
//New overload. The existing 4-parameter method now delegates to it with null, and is unchanged.
public static MechanicalVentilationMaterialisation MechanicalVentilation(this AdjacencyCluster adjacencyCluster,
    SystemEnergyCentre systemEnergyCentre_Template, MechanicalVentilationSettings mechanicalVentilationSettings,
    IEnumerable<Space> spaces, IEnumerable<Guid> guids_VentilationSystem)
```

- **`null`** means every ventilation system. That is the legacy call, byte-identical.
- **Stated**, the scope is a set, so a repeated guid counts once. Only the stated systems reach D2. A system that is
  not stated is never read: its unit, terminals and related spaces are not visited. Only its `Guid` (in D2) and its
  `SupplyUnitName` (D10's name match) are read. Units, member spaces and transfer air follow from the stated systems,
  as before. A unit that only an excluded system names (`AHU1`) is not materialised, and settings for it refuse.
- **D10 scoped.** The cross-check sums SAM's own `VentilationSystems(unit)` / `VentilationSystemDesignDuty`, still
  found by name and independent of D2's grouping, but over the stated systems only.
- **Refusals** (new):
  - an empty scope: "names no system";
  - `Guid.Empty`: "names a system with no identity";
  - a guid that is not a ventilation system of the model (unknown, a unit, a space): "… is not a ventilation system of
    the model", one refusal per such guid.
- **Unchanged:**
  - every refusal of a stated system, with the same message text;
  - the model-wide identity preconditions (a space, unit or transfer movement with no guid);
  - the settings' all-or-nothing rules.
- **Identities unchanged.** The scope is not a component of any derived guid. Scoped on the whole model gives the same
  graph, bindings and notes as unscoped on the model with the other systems removed (tested).

No Part O, dwelling or effective-duty rule is added here. SAM decides; this seam processes what it is told.

## Files

- `SAM_Systems/SAM.Analytical.Systems/Create/MechanicalVentilation.cs`: the overload, D2 scoping,
  `TryGetVentilationSystems`.
- `SAM_Systems/SAM.Analytical.Systems/Create/MechanicalVentilationReconcile.cs`: the scoped D10 roll-up.
- `SAM_Systems/SAM.Analytical.Systems/Classes/MechanicalVentilationContext.cs`: `Guids_VentilationSystem`.
- `SAM.Analytical.Systems.Tests/MechanicalVentilationSystemScopeTests.cs` (new, 34 cases).
- `SAM.Analytical.Systems.Tests/MechanicalVentilationMixedCoolingTests.cs`: the `Mixed(Guid)` fixture helper is now
  `internal` so the new tests reuse the real mixed settings.

## Evidence

- **Fixture.** It has the real model's shape. `Modify.AddMechanicalSystems` builds `NV 1` (Flat 1), `UV 1` (corridor)
  and `MV 1 → AHU1` (Flats 2 and 3). Beside them sit Part O's `MVHR Flat 2`/`MVHR Flat 3`, each with its own unit,
  terminals and transfer air. Flat 3 is guidance-cooled.
- **Required cases:**

  | # | Case | Test |
  |---|---|---|
  | 1-3 | NV / UV / `MV 1 → AHU1` outside the scope. Each refuses when read, and is never read when scoped. | `AnUnrelatedScaffoldSystemOutsideTheScope_IsNeverRead` (3 rows) |
  | 4 | A malformed system outside the scope does not refuse. Eight malformations: no unit, missing unit, ambiguous unit, supply ≠ exhaust, terminal with 0 or 2 spaces, no duty, negative duty. | `AMalformedSystemOutsideTheScope_DoesNotRefuse` (8) |
  | 5 | The same malformations inside the scope refuse, with the legacy message text | `AMalformedSystemInsideTheScope_StillRefuses_WithTheSameMessage` (8) |
  | 6 | Both dwelling systems materialise, each on its own unit | `TwoRequestedDwellingSystems_BothMaterialise_EachOnItsOwnUnit`, `Scoped_TheRealModelShape_…` |
  | 7 | Dependencies are kept: the unit named, a transfer-only hall, the transfer air | `TheRequiredDependenciesOfAnIncludedSystem_AreRetained` |
  | 8 | An unrelated unit is not pulled in, and its settings refuse | `Scoped_TheRealModelShape_…`, `AUnitOnlyAnExcludedSystemNames_IsNotPartOfTheCall` |
  | 9 | Empty, `Guid.Empty`, unknown, non-ventilation, duplicate, null and refused-result behaviour | 7 facts |
  | 10 | Iteration 3: stating every system of the working copy is the legacy call (B0 MV, B1+ MVRE unit settings, Mixed) | `StatingEverySystemOfTheWorkingCopy_IsTheLegacyCall_ForEveryIteration3Variant` |
  | — | D10: an excluded system naming a scoped unit adds nothing | `AnExcludedSystemNamingAScopedUnit_AddsNothingToIt` |
  | — | Scoped equals unscoped on the working copy | `Scoped_IsExactlyTheUnscopedCall_OnTheModelWithTheOtherSystemsRemoved` |

- **Mutation checks.** All were killed, and the sources were restored byte-identical (sha1 checked):

  | Mutation | Failed |
  |---|---|
  | M1 D2 validates the scope but keeps every system (the pre-PR behaviour) | 26 / 34 |
  | M2 D10 sums the whole model (the pre-PR reconcile) | 1 / 34 |
  | M3 an unknown guid is silently ignored | 2 / 34 |
  | M4 an empty scope is treated as the legacy call | 1 / 34 |
  | M5 `Guid.Empty` is skipped | 1 / 34 |
  | M6 a null scope is treated as empty (breaks the legacy call) | 133 / 303 |
  | M7 the scope is not recorded for D10 | 1 / 34 |

- **Suites:**
  - `SAM.Analytical.Systems.Tests` **303/303** (269 before, +34);
  - `SAM.Analytical.Systems.Mollier.Tests` 123/123;
  - `SAM_Systems.sln` Release: 0 errors.
  - SAM_Tas (unchanged) `SAM.Analytical.Tas.TM59.Tests`, rebuilt against this branch: 1023/1023.
- **Real model** (SAM_UI record): the whole materialised `-Cleaned.sam` refuses on `UV` unscoped. With SAM's scope,
  exactly `MVHR Flat 2` and `MVHR Flat 3` are processed, and `NV 1`/`UV 1`/`MV 1`/`AHU1` are on the model but unread.
  The graph is identical to PR-1's working copy, and the source SHA is unchanged.

## Compatibility

Source- and binary-compatible. The 4-parameter signature is unchanged, and calls with up to 4 arguments bind to it.
A literal `null` fifth argument selects the new overload, which is the same legacy call. No settings or JSON shape
changed.

## Risks / not done

- The model-wide identity preconditions stay model-wide, on purpose. A `Guid.Empty` space, unit or transfer movement
  anywhere still refuses. They are integrity checks, not systems. No real model has shown one.
- Legacy messages say `ventilationSystem.Name`, the type name (`'UV'`), not `FullName` (`'UV 1'`). This is unchanged,
  because the message text is pinned. It could be improved in PR-6.

## Next step

Owner review → merge this PR → re-run SAM_UI PR-3's CI (it clones SAM_Systems' default branch) → merge SAM_UI PR-3
→ closeouts in both repos.
