# Handover — Mollier → HVAC Systems bridge (SOW 2026-Q3, Abstract 2)

## Goal
Implement the engineering feature behind **Abstract 2**: convert a chain of psychrometric
(Mollier) processes into a connected, simulation-ready `SystemEnergyCentre` in `SAM_Systems`.
Paper title: *From Psychrometric Process to Simulatable HVAC System*.

## Current status — last updated 2026-06-28 (read this first)
- **Both PRs open, #7 CI is GREEN** (`build` + `spdx` pass) at head `a931cd0` onward.
- Work continues on branch **`claude/mollier-hvac-systems-bridge-h7w4ci`** in `SAM_Systems`
  (NOT `claude/handover-docs-qhfbk4` — that name appeared in a resumed-session header but no such
  branch exists; everything lives on the `...-h7w4ci` branch and on PR #7).
- **Codex P1 #1 (TwinWheelExample `Create` qualification)** — RESOLVED (commit `f33786f`,
  thread now outdated).
- **Codex P1 #2 (`SupplyExtract.cs:124` connector indexes)** — root cause found + FIXED in core:
  `SystemPlantRoom.CreateSystemConnection` resolved the connector indexes via `TryGetIndexes`
  (`index_*_out`) but then built the `SystemConnection` with the raw `-1` args, so connectors never
  read as occupied and every link piled onto connector 0 (mis-wires even a plain supply chain).
  Fix = build the connection with `index_1_out/index_2_out`. Backward-compatible (explicit-index
  callers get `out==in`); the bridge is the only caller of that overload today. Maintainer approved
  the core change over a bridge-only workaround.
- **CI build break (CS0012)** — RESOLVED: `SAM.Analytical.Grasshopper.Systems.csproj` now
  references `SAM.Analytical.Mollier.dll` (needed because `Create.SystemEnergyCentre`'s overload set
  includes an `AirHandlingUnitResult` param).
- **Codex P1 #3 (`SupplyExtract.cs` heat-recovery efficiencies lost to clone)** — RESOLVED.
  `SystemPlantRoom.Add` clones the component (`SAM.Core.Systems/Classes/SystemPlantRoom.cs:76`), so
  `createdExchangers`/`supplyExchangers` held the pre-Add ORIGINAL while the plant room stored a
  clone; `ApplyHeatRecoveryEfficiencies` mutated the detached original, so the stored/serialised
  exchanger had no sensible/latent effectiveness (twin-wheel recovery silently dropped). Fix:
  `ApplyHeatRecoveryEfficiencies` now takes the plant room, looks the exchanger up by Guid via
  `GetSystemComponent<SystemExchanger>`, sets the efficiencies on that stored instance and re-`Add`s
  it (re-adding under the same Guid replaces in place — `RelationCluster.TryAddObject` does
  `dictionary[guid] = object` — leaving connections intact). The twin-wheel WIRING was already
  correct: `CreateSystemConnection` resolves connectors from the live cluster by Guid, so passing the
  pre-clone original to `Connect` was fine; only the in-place mutation was lost.
- **Codex P2 #4 (`SupplyExtract.cs` single-component chain not related to its air system)** —
  RESOLVED. A 1-process chain never reached the pairwise `Connect(prev, current, …, airSystem)`, so
  the lone component was never related to its `AirSystem` and `GetSystemComponents<T>(ISystem)` (plus
  the export/conversion paths on it) saw an empty plant room. `AddChain` now relates the FIRST
  component to its air system via `Connect(airSystem, current)` when `previous == null`.
- **Runtime self-check NOW EXECUTED (first time).** Built locally (`dotnet build` of
  `SAM.Analytical.Systems.Mollier`, deps resolved from `..\..\..\SAM\build` + `..\..\..\SAM_Mollier\build`)
  and ran `TwinWheelExample.Verify` via a throwaway net8 console harness pointed at `SAM_Systems\build`.
  **All 14 checks PASS.** `Verify` was strengthened to (a) read sensible/latent effectiveness back
  from the JSON-round-tripped STORED exchanger (got 0.75 / 0.65 — directly guards P1) and (b) assert a
  single-component plant room relates its component to its air system (guards P2). Cooling bypass 0.15,
  cooling duty 53 094 W.
- **Still open / next:** push these two fixes (branch `…-h7w4ci`, do NOT open a new branch); let #7 CI
  re-run; reply to / resolve the two Codex threads. Then the SAM_Tas leg (TPD export + CESBP-2025
  validation) — out of scope of #6/#7, SAM_Tas not checked out. PR-activity subscription still off.
- **NOTE:** the authoring environment used for earlier sessions could not compile; THIS local
  environment (michaldengusiak's Windows box) has `dotnet` + prebuilt `SAM`/`SAM_Mollier` `build\`
  DLLs, so individual `.csproj` build and the bridge runs locally.

## Repos / branches / environment
- Two repos: `SAM_Systems` and `SAM_Mollier` (SAM_Tas is **not** in scope/checked out).
- Both work on branch **`claude/mollier-hvac-systems-bridge-h7w4ci`**; PR base is
  **`sow/2026-Q3`** (exists on both remotes, == master == branch base).
- The authoring environment **cannot compile** — no .NET toolchain, no base `SAM`/`SAM_Mollier`
  build DLLs. Code is written against APIs verified by reading source; local build is the gate.
- GitHub access is via MCP tools only (no `gh`), scoped to `sam-bim/sam_systems` + `sam-bim/sam_mollier`.

## PRs raised (both track the work branch, auto-update on push)
- **SAM_Systems #7** — the bridge + GH nodes + repo-wide GH description pass:
  https://github.com/SAM-BIM/SAM_Systems/pull/7
- **SAM_Mollier #6** — docs-only Grasshopper description pass:
  https://github.com/SAM-BIM/SAM_Mollier/pull/6
- #7 depends on #6's DLLs. Both non-draft. Codex review requested.
  **Not yet subscribed to PR activity** (offered; awaiting decision).

## What's implemented (SAM_Systems, new project `SAM.Analytical.Systems.Mollier`)
Location: `SAM_Systems/SAM_Systems/SAM.Analytical.Systems.Mollier/` (netstandard2.0, SDK-style,
in `SAM_Systems.sln`; project-refs Core/Analytical/Geometry.Systems; `HintPath` →
`..\..\..\SAM_Mollier\build\SAM.Core.Mollier.dll` + `SAM.Analytical.Mollier.dll` +
`..\..\..\SAM\build\` base DLLs).

- `Create/SystemComponent.cs` — process→`ISystemComponent` map. Cooling: `Setpoint`=End DBT,
  `BypassFactor` (ADP), `MinimumOffcoil`=ADP DBT, `Duty`=|m·Δh|. Heating: `Setpoint`+`Duty`.
  Fan/Humidifier/AirJunction; Exchanger set to `Simple`/`Simple`, latent flagged on humidity
  shift. (`FanProcess` tested before `HeatingProcess`.)
- `Create/SystemPlantRoom.cs` — single chain; delegates to shared `AddChain`.
- `Create/SupplyExtract.cs` — supply+extract (twin-wheel): shared `SystemExchanger` across both
  air paths (auto-connect takes path 1 then path 2); `AddChain` helper;
  `ApplyHeatRecoveryEfficiencies` post-pass pairs supply/extract HR processes in order.
- `Create/SystemEnergyCentre.cs` + `Create/SystemEnergyCentreByResult.cs` — energy-centre
  wrappers; overload sources airflow from `AirHandlingUnitResult.SupplyAirFlow`.
- `Query/` — `Duty.cs` (`Duty`, `MassFlow` via inlet density), `BypassFactor.cs`,
  `SystemComponentType.cs`, `HeatRecoveryEfficiency.cs` (`HeatRecoveryEfficiencies`,
  supply-side, 0–1).
- `Example/TwinWheelExample.cs` — builds twin-wheel + `Verify(out messages)` framework-free
  self-check.
- GH nodes in `SAM_Systems/Grasshopper/SAM.Analytical.Grasshopper.Systems/Component/`:
  `SAMSystemsCreateEnergyCentreByMollier`, `SAMSystemsCreatePlantRoomByMollier`,
  `SAMSystemsCreateComponentByMollierProcess`, `SAMSystemsMollierTwinWheelExample`. GH csproj
  got the bridge project ref + SAM_Mollier DLL refs.
- `docs/Mollier-Systems-Bridge.md` — design + verified worked example.
- **Repo-wide GH description pass**: all 24 existing `SAM.Analytical.Grasshopper.Systems`
  components got rewritten tooltips (fixed 1 empty + 1 broken + 2 copy-pasted summaries).

## SAM_Mollier changes
Docs-only GH tooltip pass on 9 genuinely-weak components (`MollierPointsByPercentage`,
`CreateMollierGroup`, `ApparatusDewPoint`, `CalculateAHU`, `CreateAHU`, `DryAirMassFlow`,
`DryAirMassFlowByMollierPoint`, `MassFlowRate`, `Psychrometrics`); fixed "Air Handlin Unit"
and "mass flor rate from valumetric" typos. No functional change.

## Key facts learned (so you don't re-derive)
- Wiring API: `SystemPlantRoom.Connect(c1, c2, out conn, system, index_1=-1, index_2=-1)` is meant
  to auto-select the first **unconnected** connector pair (enables the shared twin-wheel exchanger).
  NOTE: core `CreateSystemConnection` had a bug discarding the resolved indexes (see Current status);
  fixed to use `index_*_out`. `TryGetIndexes` returns explicit indexes unchanged and resolves `-1`.
- `ModifiableValue` has implicit `double` conversion (`Setpoint = 12.0` works).
- Efficiency values are **fractions 0–1** (template `MVRE.json` shows `0.7`); exchanger default
  `ExchangerCalculationMethod`/`ExchangerType` = `Simple`.
- `MollierGroup.GetObjects<IMollierProcess>()` preserves order. `MollierPoint` exposes
  `Enthalpy` (J/kg), `DryBulbTemperature`, `HumidityRatio`, `[MollierPointProperty.Density]`.
- `CoolingProcess.ApparatusDewPoint()` and `.Efficiency` exist;
  `AirHandlingUnitResult.TryGetValue(AirHandlingUnitResultParameter.SupplyAirFlow, out double)`
  is volumetric m³/s.
- Bridge files use **`System.Math.`** fully-qualified (no `using System;`).

## Deliberately NOT done (with reason)
- **Fan `DesignFlowRate`** — template uses `DesignConditionSizedFlowValue` with ambiguous units
  (likely l/s vs m³/s) + `DesignFlowType` semantics; risk of 1000× error. Use the existing
  `SystemEnergyCentre.ModifyFanByAirflows` node instead.
- **Tas TPD export + CESBP-2025 validation** — live in the un-checked-out `SAM_Tas`; out of scope.

## Suggested next steps
1. Push the P1 #3 (exchanger clone) + P2 #4 (single-component relation) fixes on branch `…-h7w4ci`
   (DONE in this session — do NOT branch); let #7 CI re-run; resolve the two Codex threads.
2. Merge #6 (SAM_Mollier docs) then #7 to `sow/2026-Q3` once green (#7 consumes #6's DLLs).
3. Optional polish: richer/unique component names, a supply-only example, dedupe checks.
4. When SAM_Tas is available: hand the `SystemEnergyCentre` to its TPD path; run the CESBP
   twin-wheel validation (duties + annual energy vs a manual Tas model). This is the actual Abstract 2
   paper deliverable and is out of scope of #6/#7.

(DONE this session: local build + first execution of `TwinWheelExample.Verify` — all 14 checks pass;
to re-run, build `SAM.Analytical.Systems.Mollier` then run the bridge type's `Verify(out msgs)` from a
console/GH node with the `SAM_Systems\build` dir on the probe path.)

## Commit hygiene
Branch `claude/mollier-hvac-systems-bridge-h7w4ci` in both repos. Commit trailers:
`Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` and
`Claude-Session: https://claude.ai/code/session_01DBPdu7fogKggmF47N76sXM`.
Push with `git push -u origin <branch>` (retry with backoff). Do not put the model id in
repository artifacts.
