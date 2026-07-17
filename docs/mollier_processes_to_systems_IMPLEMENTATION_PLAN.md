# Mollier Processes to Systems — Implementation Plan

> Generated: 2026-07-10 | Target: `sow/2026-Q3` | SAM-BIM fork only

---

## A. Executive Summary

**Recommended architecture:** Option A — Bridge assembly inside `SAM_Systems`. This project already exists as `SAM.Analytical.Systems.Mollier` (merged from PR #8). It references Systems projects directly and consumes `SAM_Mollier` DLLs via `HintPath`. This avoids circular references, keeps ownership clear in `SAM_Systems`, and has been proven buildable.

**What PR #8 delivers:** A working bridge that converts `IMollierProcess` chains into connected, display-native `SystemEnergyCentre` objects with support for single-chain, supply+extract (twin-wheel), template merging, room-side arrangement, and outside-air junctions. The self-check in `TwinWheelExample.Verify()` passes 25+ assertions.

**What this plan adds:** Production-grade refinements: structured diagnostics, proper unit tests, fan psychrometric integration, humidifier setpoints, two-row layout, CI/build hardening, liquid-system auto-injection, and validated Tas export pathway. These are enhancements on top of a working core, not a rewrite.

**Minimum deliverable:** The existing bridge code (merged) + the new additions below, all on `sow/2026-Q3`, with passing tests and validated Tas TPD export readiness.

---

## B. Current Repo Baseline

### Branch Status

| Repo | Current Branch | Status |
|------|---------------|--------|
| `SAM_Systems` | `sow/2026-Q3` (PR #8 merged via fast-forward) | Clean |
| `SAM_Mollier` | `sow/2026-Q3` | Clean |
| `SAM` | `sow/2026-Q3` | Clean |

### Key Projects Discovered

| Project | Location |
|---------|----------|
| `SAM.Core.Systems` | `SAM_Systems/SAM_Systems/SAM.Core.Systems/` |
| `SAM.Analytical.Systems` | `SAM_Systems/SAM_Systems/SAM.Analytical.Systems/` |
| `SAM.Geometry.Systems` | `SAM_Systems/SAM_Systems/SAM.Geometry.Systems/` |
| `SAM.Analytical.Systems.Mollier` | `SAM_Systems/SAM_Systems/SAM.Analytical.Systems.Mollier/` |
| `SAM.Core.Mollier` | `SAM_Mollier/SAM_Mollier/SAM.Core.Mollier/` |
| `SAM.Analytical.Mollier` | `SAM_Mollier/SAM_Mollier/SAM.Analytical.Mollier/` |
| `SAM.Core` | `SAM/SAM/SAM.Core/` |

### Relevant Classes (confirmed from repository inspection)

**Mollier side (`SAM_Mollier`):**
- `IMollierProcess` — marker interface, extends `IMollierCurve` — `SAM.Core.Mollier/Interfaces/IMollierProcess.cs`
- `IMollierCurve` — `Start` (MollierPoint), `End` (MollierPoint), `Pressure` (double), `ChartDataType`
- `MollierProcess` — abstract base — `SAM.Core.Mollier/Classes/MollierProcess.cs`
- **`FanProcess : HeatingProcess`** — inherits HeatingProcess directly
- `HeatingProcess : MollierProcess`
- `CoolingProcess : MollierProcess` — has `Efficiency` (double, 0–1), `ApparatusDewPoint()` method
- `HeatRecoveryProcess : MollierProcess`
- `HumidificationProcess : MollierProcess` (abstract)
- `AdiabaticHumidificationProcess : HumidificationProcess`
- `IsothermalHumidificationProcess : HumidificationProcess`
- `SteamHumidificationProcess : IsothermalHumidificationProcess`
- `MixingProcess : MollierProcess`
- `MollierGroup : Collection<IMollierGroupable>` — ordered collection, nested groups via `GetObjects<T>(includeNestedObjects)`
- `MollierPoint` — `DryBulbTemperature` [°C], `HumidityRatio` [kg/kg], `Pressure` [Pa], `Enthalpy` [J/kg] (computed), `Density` [kg_MoistAir/m³] (via `MollierPointProperty.Density` indexer)

**Systems side (`SAM_Systems`):**
- `SystemEnergyCentre : SystemEnergyCentre<SystemPlantRoom>` — `SAM.Core.Systems/Classes/SystemEnergyCentre.cs`
- `SystemPlantRoom : SAMObject, ISystemSpatialObject` — `SAM.Core.Systems/Classes/SystemPlantRoom.cs` (1854 lines)
- `AirSystem : FluidSystem` — `SAM.Analytical.Systems/Classes/System/AirSystem.cs`
- `SystemFan` — Pressure (double), OverallEfficiency (ModifiableValue), DesignFlowRate (SizedFlowValue)
- `SystemHeatingCoil` — Setpoint (ModifiableValue), Efficiency (ModifiableValue), Duty (ISizableValue)
- `SystemCoolingCoil` — Setpoint (ModifiableValue), BypassFactor (ModifiableValue), Duty (ISizableValue), MinimumOffcoil (ModifiableValue)
- `SystemExchanger` — SensibleEfficiency/LatentEfficiency (ModifiableValue), ExchangerLatentType (enum), Setpoint (ModifiableValue), two air paths (In/Out 1 and 2)
- `SystemSprayHumidifier : SystemHumidifier` — Setpoint (ModifiableValue), Effectiveness (ModifiableValue), WaterFlowCapacity (ISizableValue)
- `SystemSteamHumidifier : SystemHumidifier` — Duty (ISizableValue), Setpoint (ModifiableValue)
- `SystemHumidifier` — **abstract** (PR #8 change)
- `SystemAirJunction : SystemJunction<AirSystem>`
- `SystemDamper` — DesignFlowRate (SizedFlowValue), Capacity (double)
- `SystemSpace` — temperature/RH setpoints, area, volume
- `SystemConnection` — `SAM.Core.Systems/Classes/SystemConnection.cs`
- `DisplaySystemPlantRoom : SystemPlantRoom` — overrides `CreateSystemConnection` for polyline routing
- `DisplaySystemEnergyCentre : SystemEnergyCentre<DisplaySystemPlantRoom>`

### Relevant Tests

- **No formal unit test project exists.** The only test mechanism is `TwinWheelExample.Verify()` in `SAM.Analytical.Systems.Mollier/Example/TwinWheelExample.cs`.
- `SAM` repo has `SAM.Tests` (xunit on `net8.0`). `SAM_Systems` has no equivalent.

### Tas Export Path

The TPD export lives in `SAM_Tas` (separate repo, not available locally). Bridge serializes `SystemEnergyCentre` to JSON → Grasshopper component `SAMSystems.CreateTPDByTSDAndSystemEnergyCentre` (in `SAM_Tas`) consumes it. No bridge changes needed — emitted types match hand-authored models.

### Uncertainties

- `SAM_Tas` repo not available — cannot verify TPD export end-to-end
- `SAM_Systems.sln` may not yet include the Mollier project (needs verification)

---

## C. Critique of Previous Plan (PR #8 Baseline)

| Assumption | Verdict | Evidence |
|---|---|---|
| "Create a new `SAM.Analytical.Systems.Mollier` assembly" | **Implemented** | Project exists with 12 files |
| "Project-ref Systems, DLL-ref Mollier" | **Correct** | `.csproj` lines 48–51 confirm |
| "DLLs at `..\..\..\SAM_Mollier\build\`" | **Correct but fragile** | Path confirmed in `.csproj` |
| "FanProcess derives from HeatingProcess" | **Correct** | `FanProcess : HeatingProcess` at `SAM.Core.Mollier/Classes/FanProcess.cs:11` |
| "Process-to-component mapping table" | **Correct** | `is FanProcess` tested before `is HeatingProcess` in `Create/SystemComponent.cs:39` |
| "Duty = \|ṁ·Δh\|, ṁ = airflow·ρ(inlet)" | **Correct** | `Query/Duty.cs:43` uses `System.Math.Abs(massFlow * (end.Enthalpy - start.Enthalpy))` |
| "BypassFactor = (T_off−T_adp)/(T_on−T_adp), clamped [0,1]" | **Correct** | `Query/BypassFactor.cs:42-51` |
| "Fan duty from pressure/efficiency" | **Incomplete** | `SystemFan` created but `Pressure`, `OverallEfficiency`, `DesignFlowRate` NOT set from Mollier states |
| "Humidification: moisture change + duty" | **Incomplete** | Humidifier created but `Setpoint`, `Effectiveness`, `Duty` NOT derived |
| "SystemHumidifier → abstract" | **Correct, intentional** | Done. Breaks old JSON with `_type: SystemHumidifier`. Accepted trade-off. |
| "JSON round-trip" | **Correct** | Verified by `TwinWheelExample.Verify()`: `ToJsonObject()` → `new SystemEnergyCentre(jsonObject)` |
| "Tas TPD compatibility" | **Likely correct** | Same types as hand-authored models. Not verified end-to-end. |
| "Single AirSystem for supply+extract" | **Correct** | `SupplyExtract.cs:44` creates one `AirSystem` |
| "Room as DisplayAirSystemGroup" | **Correct** | `RoomGroup.cs:79-133` |
| "Outside air junctions" | **Correct** | `OutsideAirJunction.cs` caps supply intake + extract discharge |
| "Clear diagnostics" | **Incomplete** | Uses `List<string>` only. No structured model, no severity levels. |
| **Missing: structured diagnostics model** | Not addressed | |
| **Missing: unit test project** | Not addressed | |
| **Missing: humidifier psychrometric derivation** | Not addressed | |
| **Missing: fan pressure derivation** | Not addressed | |
| **Missing: two-row layout** | Partially addressed | Documented as "still open" in schematic preview plan |
| **Missing: liquid system auto-injection** | Partially addressed | Template merge exists but requires external Plantroom-Only.json |
| **Missing: CI/build script** | Not addressed | |
| **Missing: solution registration** | Needs verification | Mollier project may not be in `.sln` |

---

## D. Recommended Final Architecture

### Assembly/Project Layout
```
SAM_Systems/
  SAM_Systems.sln                              ← ADD SAM.Analytical.Systems.Mollier (+ Tests)
  SAM_Systems/
    SAM.Core.Systems/                          ← SystemPlantRoom, SystemEnergyCentre, SystemConnection
    SAM.Analytical.Systems/                    ← SystemFan, SystemCoolingCoil, ..., DisplaySystemEnergyCentre
    SAM.Geometry.Systems/                      ← DisplaySystemPlantRoom, DisplaySystemConnection
    SAM.Analytical.Systems.Mollier/            ← THE BRIDGE (existing, to be enhanced)
      Create/
        SystemComponent.cs                     ← MODIFY: humidifier setpoints, fan pressure
        SystemPlantRoom.cs                     ← MODIFY: diagnostics overload
        SystemEnergyCentre.cs                  ← MODIFY: diagnostics, liquid injection
        SystemEnergyCentreByResult.cs
        SystemEnergyCentreByTemplate.cs
        SupplyExtract.cs                       ← MODIFY: diagnostics overload
        OutsideAirJunction.cs
        RoomGroup.cs
        LiquidSystem.cs                        ← NEW: auto-inject liquid systems
      Query/
        SystemComponentType.cs
        Duty.cs
        BypassFactor.cs
        HeatRecoveryEfficiency.cs
        HumidifierProperties.cs                ← NEW: derive humidifier setpoints
        FanPressure.cs                         ← NEW: derive fan pressure
      Classes/
        ConversionDiagnostic.cs                ← NEW: structured diagnostic model
        ConversionResult.cs                    ← NEW: result wrapper
      Example/
        TwinWheelExample.cs
      SAM.Analytical.Systems.Mollier.csproj
    SAM.Analytical.Systems.Mollier.Tests/      ← NEW: test project
      Create/SystemComponentTests.cs
      Create/SystemPlantRoomTests.cs
      Create/SupplyExtractTests.cs
      Query/DutyTests.cs
      Query/BypassFactorTests.cs
      Query/HeatRecoveryEfficiencyTests.cs
      Query/SystemComponentTypeTests.cs
      Query/HumidifierPropertiesTests.cs
      Query/FanPressureTests.cs
      Json/RoundTripTests.cs
      Diagnostics/ConversionDiagnosticTests.cs
      Integration/TasExportReadinessTests.cs
      SAM.Analytical.Systems.Mollier.Tests.csproj
  Grasshopper/
    SAM.Analytical.Grasshopper.Systems/
      Component/
        SAMSystemsCreateEnergyCentreByMollier.cs
        SAMSystemsCreatePlantRoomByMollier.cs
        SAMSystemsCreateComponentByMollierProcess.cs
        SAMSystemsMollierTwinWheelExample.cs
```

### Dependency Direction
```
SAM (base) → SAM_Mollier → SAM_Systems → SAM.Analytical.Systems.Mollier
                                              └── Mollier DLLs (HintPath)
```

### Namespace
`SAM.Analytical.Systems.Mollier`

### Build Order
1. `SAM.sln` → `SAM\build\SAM.Core.dll`, etc.
2. `SAM_Mollier.sln` → `SAM_Mollier\build\SAM.Core.Mollier.dll`, etc.
3. `SAM_Systems.sln` → `SAM_Systems\build\SAM.Analytical.Systems.Mollier.dll`

### Reference Strategy
- SAM base + SAM_Mollier: `HintPath` DLL references (already in `.csproj`)
- Systems projects: `ProjectReference` (already in `.csproj`)
- Test project: `ProjectReference` to `SAM.Analytical.Systems.Mollier` + `PackageReference` to `xunit 2.9.2`

### Serialization
No changes needed. All emitted types implement `ToJsonObject()`/`FromJsonObject()` via SAM's native `System.Text.Json`-based serialization.

### Tas Export
Bridge outputs structurally identical `SystemEnergyCentre` to hand-authored models. No bridge changes needed for Tas compatibility.

---

## E. Phased Implementation Roadmap

### Phase 1: Branch Setup & Solution Registration (XS, 1 session, Low risk)

Create `feature/mollier-bridge-enhancements` from `sow/2026-Q3` (already has PR #8 merged). Add Mollier project and test project to `SAM_Systems.sln`. Verify build.

**Files:** `SAM_Systems.sln`, new test `.csproj`

**Acceptance:**
- `dotnet build SAM_Systems.sln -c Debug` succeeds (SAM + SAM_Mollier pre-built)
- `SAM.Analytical.Systems.Mollier` is in solution

---

### Phase 2: Structured Diagnostics Model (S, 1 session, Low risk)

Replace `List<string>` reports with typed `ConversionDiagnostic` model with severity levels.

**New files:**
- `Classes/ConversionDiagnostic.cs` — enum `DiagnosticSeverity` {Info, Warning, Error}, class `ConversionDiagnostic`, static class `DiagnosticCodes`
- `Classes/ConversionResult.cs` — wraps `List<ConversionDiagnostic>` with `HasErrors`/`HasWarnings`

**Modified files:**
- `Create/SystemComponent.cs` — add overload with `out List<ConversionDiagnostic>`
- `Create/SystemPlantRoom.cs`, `Create/SupplyExtract.cs`, `Create/SystemEnergyCentre.cs` — same pattern

**Acceptance:**
- Every skipped/unsupported process produces a `ConversionDiagnostic`
- Severity levels correctly assigned (Info/Warning/Error)
- Existing API preserved (old overloads call new ones, discard diagnostics)

---

### Phase 3: Humidifier Setpoint & Psychrometric Derivation (S, 1 session, Low risk)

Derive humidifier properties from Mollier process states.

**New file:** `Query/HumidifierProperties.cs`

**Modified file:** `Create/SystemComponent.cs` — set humidifier Setpoint, Effectiveness, Duty

**Key logic:**
- `SystemSprayHumidifier`: Setpoint = End **relative humidity** [%, 0–100] (not dry-bulb), Effectiveness = (W_end − W_start) / (W_sat − W_start) clamped [0,1]
- `SystemSteamHumidifier`: Setpoint = End **relative humidity** [%, 0–100] (not dry-bulb), Duty = |ṁ·(h_end−h_start)| computed **unconditionally** — the isothermality gate (`|ΔT|<0.01`) was removed: library-built steam processes are near-isothermal but not exactly so (injected steam carries sensible heat, ΔT ≈ +0.3 K), so the gate never fired and Duty was never set
- WaterFlowCapacity (spray) = ṁ·(W_end − W_start) [kg/s]
- Use `MollierPoint.SaturationMollierPoint()` for W_sat
- Setpoint is relative humidity (not dry-bulb or humidity ratio) because SAM humidifier
  setpoints pass straight through to Tas TPD (`SAM_Tas Convert.ToTPD` maps
  `SprayHumidifier`/`SteamHumidifier` setpoints verbatim) and TPD humidifiers control
  downstream %RH under their default flags

**Acceptance:**
- Spray humidifier Setpoint (relative humidity) and Effectiveness set
- Steam humidifier Setpoint (relative humidity) and Duty set
- Tests verify against hand-calculated psychrometrics

---

### Phase 4: Fan Pressure Derivation (XS, 1 session, Low risk)

Compute fan pressure rise from temperature pickup.

**New file:** `Query/FanPressure.cs`

**Modified file:** `Create/SystemComponent.cs` — set `SystemFan.Pressure`, `OverallEfficiency`, `DesignFlowRate`

**Key logic:**
- ΔT = End.DryBulb − Start.DryBulb
- ΔP = η_fan · ρ_inlet · cp_air · ΔT (default η_fan = 0.7) — **efficiency multiplies**,
  not divides. This is the exact inverse of `SAM.Core.Mollier Query.PickupTemperature`
  (ΔT = SFP/(ρ·cp), SFP = ΔP/η), so `ΔP == η_fan · SFP · 1000` [Pa] regardless of inlet
  state. (An earlier `ΔP = ρ·cp·ΔT/η_fan` formulation had efficiency dividing, which
  overstates pressure by a factor of 1/η².)
- `cp_air` is read from `SAM.Core.Mollier Query.SpecificHeatCapacity_Air(start)`
  (returned in kJ/kg·K, ×1000 for J/kg·K) — **not** a hardcoded 1010 constant
- `SystemFan.DesignFlowRate` = designAirflow [m³/s] × 1000, i.e. **litres per second**,
  with `DesignFlowType = FlowRateType.Value` (Tas TPD fan flow values are l/s; SFP
  itself is W/(l/s); TPD template code uses values like `DesignFlowRate.Value = 150`)

**Acceptance:**
- Fan pressure and efficiency set when FanProcess has valid states
- NaN return when process/states/efficiency are invalid, or ΔT ≤ 0, or inlet density/cp
  are unavailable

---

### Phase 5: Two-Row Layout for Supply+Extract (M, 2 sessions, Medium risk)

Supply chain on top row (L→R), extract on bottom row (R→L or L→R), shared exchanger spanning both rows.

**Modified file:** `SAM_Systems/SAM.Analytical.Systems/Create/DisplaySystemEnergyCentre.cs`

**Acceptance:**
- Supply on one row, extract on another
- Shared exchanger at column spanning both rows
- Connection polylines route between rows
- Fallback to single-row if two-row cannot be resolved

---

### Phase 6: Liquid System Auto-Injection (M, 2 sessions, Medium risk)

Auto-generate minimal liquid systems when no template provided.

**New file:** `Create/LiquidSystem.cs`

**Modified files:** `Create/SystemEnergyCentre.cs`, `Create/SupplyExtract.cs`

**Key logic:**
- HeatingProcess exists → add heating LTHW system + boiler
- CoolingProcess exists → add cooling CHW system + chiller
- Wire coil liquid connectors to corresponding system

**Acceptance:**
- Output `SystemEnergyCentre` has `SystemEnergySource` entries when coils present —
  **delivered**: a `SystemEnergySource` "Natural Gas" (CO2Factor 0.216, PeakCost 0.05,
  PEF 0) is added when a boiler is injected, and an `ElectricalEnergySource` "Grid
  Supplied Electricity" (CO2Factor 0.519, PeakCost 0.13, PEF 0) when a chiller is
  injected — values mirror the reference `MVRE.json` project. The boiler/chiller are
  stamped with `SystemObjectParameter.EnergySourceName` (chiller also gets
  `FanEnergySourceName`) so `SAM_Tas` can resolve the matching `SystemEnergySource` by
  name. Energy sources are added by the top-level entry points (they live on
  `SystemEnergyCentre`, not the plant room), not by the injector itself.
- Coil liquid connectors wired to plant, loop closed (plant equipment → coil → … → coil
  → plant equipment)
- Does NOT overwrite user-provided template — the template path skips injection
  entirely
- Graceful no-op when no template and no coils are present (nothing to inject; this is
  not tied to MOLLIER-011, which in the delivered diagnostics contract means "MollierGroup
  contains zero processes" — see Section N)

---

### Phase 7: Unit Test Project (L, 3 sessions, Medium risk)

Create `SAM.Analytical.Systems.Mollier.Tests` with xunit (30+ tests).

**New files:** Test project + test files as listed in Section K.

**Acceptance:**
- All test categories in Section K passing
- `dotnet test` succeeds
- Test project in solution

---

### Phase 8: Tas Export Structural Validation (M, 1 session, Medium risk)

Prove bridge output is structurally Tas-compatible.

**New test files:** `Integration/TasExportReadinessTests.cs`, `Integration/MVRE_ComparisonTests.cs`

**Acceptance:**
- All air components have In+Out connectors
- No dangling connectors except room/outside-air boundaries
- JSON serializes without exceptions
- Structural comparison to MVRE.json passes

---

### Phase 9: Documentation & Examples (S, 1 session, Low risk)

Update `docs/Mollier-Systems-Bridge.md` with new features. Add summer cooling-only and winter heating+humidification examples.

**Acceptance:**
- README updated with Phase 2–6 features
- At least 3 worked examples documented

---

### Phase 10: Build Script & Final Verification (S, 1 session, Low risk)

**New files:** `build.ps1`, `test.ps1`

**Acceptance:**
- `build.ps1` succeeds on clean checkout
- `test.ps1` runs all tests with 100% pass
- `TwinWheelExample.Verify(out messages)` returns true

---

## F. Core Algorithm Pseudocode

```
// === PROCESS CLASSIFICATION ===
ClassifyProcess(process):
  if null or UndefinedProcess or SpecificProcess → null
  if FanProcess        → typeof(SystemFan)         // BEFORE HeatingProcess!
  if HeatingProcess    → typeof(SystemHeatingCoil)
  if CoolingProcess    → typeof(SystemCoolingCoil)
  if HeatRecovery      → typeof(SystemExchanger)
  if AdiabaticHumid.   → typeof(SystemSprayHumidifier)
  if Humidification    → typeof(SystemSteamHumidifier)
  if MixingProcess     → typeof(SystemAirJunction)
  → null + MOLLIER-001 diagnostic

// === COMPONENT CREATION ===
CreateComponent(process, airflow, out diagnostics):
  type = ClassifyProcess(process)
  if type is null: return null

  component = new type(nameFromProcess(process))

  // Heating/Cooling: setpoint + duty
  if HeatingCoil: component.Setpoint = End.T; component.Duty = |ṁ·Δh|
  if CoolingCoil: component.Setpoint = End.T; adp = ADP(); component.MinimumOffcoil = adp.T;
                  component.BypassFactor = clamp((End.T-adp.T)/(Start.T-adp.T), 0, 1)
                  component.Duty = |ṁ·Δh|

  // Fan: pressure from temperature rise (efficiency MULTIPLIES; cp from the inlet state, not a constant)
  if Fan: ΔT = End.T - Start.T; if ΔT <= 0: ΔP = NaN
          else: ΔP = 0.7·ρ(Start)·cp_air(Start)·ΔT
          component.Pressure = ΔP; component.OverallEfficiency = 0.7
          component.DesignFlowRate = airflow·1000 [l/s]; component.DesignFlowType = Value

  // Spray humidifier: relative-humidity setpoint + effectiveness + water flow
  if SprayHumidifier: component.Setpoint = End.RH [%]
      W_sat = saturation(End.T, Pressure); component.Effectiveness = clamp((End.W-Start.W)/(W_sat-Start.W), 0, 1)
      component.WaterFlowCapacity = ṁ·(End.W-Start.W)

  // Steam humidifier: relative-humidity setpoint + duty (unconditional - see Corrections)
  if SteamHumidifier: component.Setpoint = End.RH [%]; component.Duty = |ṁ·Δh|

  // Exchanger: setpoint + latent flag
  if Exchanger: component.Setpoint = End.T; Simple method/type;
      if |Start.W-End.W|>1e-6: component.ExchangerLatentType = HumidityRatio

  return component

// === DUTY ===
Duty(process, airflow):
  if null or NaN(airflow): return NaN
  ρ = Start[Density]; if NaN(ρ): return NaN + MOLLIER-005
  ṁ = airflow · ρ
  return |ṁ · (End.Enthalpy - Start.Enthalpy)|

// === BYPASS FACTOR ===
BypassFactor(process):
  adp = ApparatusDewPoint()
  denom = Start.T - adp.T; if |denom|<1e-9: return NaN + MOLLIER-007
  return clamp((End.T-adp.T)/denom, 0, 1)

// === CHAIN WIRING ===
WireChain(plantRoom, processes, airflow, airSystem, exchangers):
  for each process:
    if HeatRecovery and exchangers has next: current = exchangers.next()
    else: (current, diag) = CreateComponent(process, airflow)
    if current is null: continue
    plantRoom.Add(current)
    if previous: Connect(prev.Out, cur.In, airSystem)  // explicit index = unconnected Out/In
    else: Connect(airSystem, current)  // first component
    previous = current

// === SYSTEM ENERGY CENTRE (supply+extract) ===
CreateSystemEnergyCentre(supply, extract, supplyAF, extractAF, name, template):
  plantRoom = new SystemPlantRoom(name)
  airSystem = new AirSystem("Air System")
  supplyExchangers = []
  WireChain(plantRoom, supply,  supplyAF,  airSystem, null,              supplyExchangers)
  WireChain(plantRoom, extract, extractAF, airSystem, supplyExchangers, null)
  ApplyHeatRecoveryEfficiencies(plantRoom, supply, extract, supplyExchangers)
  AddBoundaryJunction(plantRoom, airSystem, firstSupply, In,  "Junction Fresh Air")
  AddBoundaryJunction(plantRoom, airSystem, lastExtract, Out, "Junction Exhaust Air")
  AddRoom(plantRoom, airSystem, lastSupply, firstExtract, roomCondition)
  displayPlantRoom = ToDisplay(plantRoom)

  if template: result = MergeAirSystems(template, displayPlantRoom, name)
  else:
    result = new SystemEnergyCentre(name)
    InjectLiquidSystems(result, supply, extract)  // Phase 6
    result.Add(displayPlantRoom)
  return result
```

---

## G. Data Model and API

### Existing APIs (from PR #8 — preserved)

```csharp
// Single process → component
public static ISystemComponent SystemComponent(this IMollierProcess mollierProcess, double designAirflow = double.NaN)

// Single chain → plant room
public static SystemPlantRoom SystemPlantRoom(this IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, string name, string airSystemName)
public static SystemPlantRoom SystemPlantRoom(this MollierGroup mollierGroup, double designAirflow, string name)

// Supply + extract → plant room
public static SystemPlantRoom SystemPlantRoom(IEnumerable<IMollierProcess> supply, IEnumerable<IMollierProcess> extract, double supplyAF, double extractAF, string name)

// Plant room → energy centre
public static SystemEnergyCentre SystemEnergyCentre(this IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, string name)
public static SystemEnergyCentre SystemEnergyCentre(this MollierGroup mollierGroup, double designAirflow, string name)
public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> supply, IEnumerable<IMollierProcess> extract, double supplyAF, double extractAF, string name)
public static SystemEnergyCentre SystemEnergyCentre(this IEnumerable<IMollierProcess> mollierProcesses, AirHandlingUnitResult result, string name)
public static SystemEnergyCentre SystemEnergyCentre(this MollierGroup mollierGroup, AirHandlingUnitResult result, string name)
public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> supply, IEnumerable<IMollierProcess> extract, double supplyAF, double extractAF, string name, SystemEnergyCentre template)
public static SystemEnergyCentre SystemEnergyCentre(IEnumerable<IMollierProcess> mollierProcesses, double designAirflow, string name, SystemEnergyCentre template)

// Query
public static double Duty(this IMollierProcess mollierProcess, double designAirflow)
public static double MassFlow(this IMollierProcess mollierProcess, double designAirflow)
public static double BypassFactor(this CoolingProcess coolingProcess)
public static System.Type SystemComponentType(this IMollierProcess mollierProcess)
public static void HeatRecoveryEfficiencies(this HeatRecoveryProcess supply, HeatRecoveryProcess extract, out double sensible, out double latent)
```

### Proposed New APIs

```csharp
// === DIAGNOSTICS OVERLOADS (Phase 2) ===
// All existing Create methods gain an overload with out List<ConversionDiagnostic>
public static ISystemComponent SystemComponent(this IMollierProcess mollierProcess, double designAirflow, out List<ConversionDiagnostic> diagnostics)
// ... same pattern for SystemPlantRoom, SystemEnergyCentre, SupplyExtract overloads

// === HUMIDIFIER DERIVATION (Phase 3) ===
public static void HumidifierProperties(this HumidificationProcess process, double designAirflow,
    out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity)

// === FAN PRESSURE (Phase 4) ===
public static double FanPressureRise(this FanProcess fanProcess, double fanEfficiency = 0.7)

// === DIAGNOSTIC CLASSES (Phase 2) ===
public enum DiagnosticSeverity { Info, Warning, Error }

public class ConversionDiagnostic
{
    public DiagnosticSeverity Severity { get; }
    public string Code { get; }
    public string Message { get; }
    public IMollierProcess SourceProcess { get; }
}

// Delivered constant names/meanings (Classes/ConversionDiagnostic.cs) — see Section N
// for the canonical code table. Every code has exactly one meaning/severity across all
// emission sites.
public static class DiagnosticCodes
{
    public const string UnsupportedProcess               = "MOLLIER-001"; // Warning
    public const string NullProcess                      = "MOLLIER-002"; // Warning
    public const string InvalidProcessState               = "MOLLIER-003"; // Warning
    public const string AirflowNaN                        = "MOLLIER-004"; // Warning
    public const string NullProcessChain                  = "MOLLIER-005"; // Error
    public const string ApparatusDewPointNotAvailable      = "MOLLIER-006"; // Info
    public const string BypassFactorInvalid                = "MOLLIER-007"; // Info
    public const string HeatRecoveryEfficiencyNotAvailable = "MOLLIER-008"; // Warning
    public const string ConnectionFailed                   = "MOLLIER-009"; // Warning
    public const string DisplaySymbolMissing                = "MOLLIER-010"; // Info
    public const string NoProcessesInChain                 = "MOLLIER-011"; // Error
    public const string ChainEmpty                         = "MOLLIER-012"; // Error
    public const string HeatRecoveryCountMismatch           = "MOLLIER-013"; // Warning
    public const string LiquidInjectionFailed               = "MOLLIER-014"; // Error
}
```

### API Philosophy: Return null vs Throw vs Diagnostic

Severities below are the canonical ones from Section N; that table governs.

| Situation | Behavior |
|-----------|----------|
| `null` process chain or `MollierGroup` | Return `null` + `MOLLIER-005` Error |
| `null` process within a chain | Skip + `MOLLIER-002` Warning, continue chain |
| Chain produced no components | Return `null` + `MOLLIER-012` Error |
| Unsupported process type | Skip + `MOLLIER-001` Warning, continue chain |
| Invalid psychrometric start/end state | Component created without derived values + `MOLLIER-003` Warning |
| Cannot compute duty (NaN airflow) | Component created without duty + `MOLLIER-004` Warning |
| ADP unavailable | `MinimumOffcoil` unset + `MOLLIER-006` Info |
| Denominator ≈ 0 in bypass factor | By-pass = `NaN`, unset + `MOLLIER-007` Info |
| Connector wiring fails, or falls back to automatic selection | Continue + `MOLLIER-009` Warning |
| No symbol for display object | Skip display + `MOLLIER-010` Info |
| Liquid loop left partially wired | Continue + `MOLLIER-014` Error |

---

## H. External / Native / Library Bridge Additions

**None required.** The bridge uses SAM's native `System.Text.Json` serialization, `SAM_Mollier` DLLs via `HintPath`, existing `SystemPlantRoom.Connect`, and existing `DisplaySystemManager` for symbols. No Tas SDK, native DLLs, or third-party bridges needed.

---

## I. Wrapper / Integration Layer Plan

- **SAM_Systems factories:** The bridge is a producer of `SystemEnergyCentre` — consistent with existing `Create.*` conventions.
- **Serialization:** All types inherit from `SAMObject → ParameterizedSAMObject` with `ToJsonObject()`/`FromJsonObject()`. No changes needed.
- **Tas TPD export:** Bridge outputs structurally identical `SystemEnergyCentre` to hand-authored models. Downstream component `CreateTPDByTSDAndSystemEnergyCentre` in `SAM_Tas` consumes it directly.
- **Grasshopper:** Existing components already work. Display-native output enables direct preview.

---

## J. UI / Component / API Consumer Plan

### C# API (primary)
```csharp
using SAM.Analytical.Systems;

// Simple: MollierGroup → SystemEnergyCentre
SystemEnergyCentre ec = mollierGroup.SystemEnergyCentre(2.5);

// With diagnostics:
SystemEnergyCentre ec2 = processes.SystemEnergyCentre(2.5, out var diagnostics);

// Supply + extract (twin-wheel):
SystemEnergyCentre ec3 = Create.SystemEnergyCentre(supply, extract, 2.5, 2.3, "AHU-1");

// With plantroom template:
SystemEnergyCentre ec4 = Create.SystemEnergyCentre(supply, extract, 2.5, 2.3, "AHU-1", template);
```

### Grasshopper Components (already exist)
- `SAMSystems.CreateEnergyCentreByMollier` — processes → `SystemEnergyCentre`
- `SAMSystems.CreatePlantRoomByMollier` — processes → `SystemPlantRoom`
- `SAMSystems.CreateComponentByMollierProcess` — single process → component + duty + bypass factor
- `SAMSystems.MollierTwinWheelExample` — worked example with PASS/FAIL output

### Command-line / Batch
Not in scope. C# API enables scripting.

---

## K. Testing and Validation Plan

### Test Project: `SAM.Analytical.Systems.Mollier.Tests` (xunit, net8.0)

**Delivered: 123 tests, all passing, 0 skipped, across 17 files** (verified by executed
test run). Supersedes the original 11-file/30+-test estimate below — `HeatRecoveryEfficiencyTests.cs`,
`HumidifierPropertiesTests.cs` and `FanPressureTests.cs` were planned but not yet written
when this plan was first drafted; they now exist, and four more files were added beyond
the original plan (`TwinWheelTopologyTests.cs`, `TwoRowLayoutTests.cs`, `LiquidLoopTests.cs`,
`TwinWheelVerifyTests.cs`).

| Test File | Count | Coverage | Expected Results |
|-----------|------:|----------|-----------------|
| `Create/SystemComponentTests.cs` | 13 | Fan returns SystemFan (tested before Heating, since FanProcess derives from it); Heating sets Setpoint+Duty; Cooling sets Setpoint+BypassFactor+MinimumOffcoil+Duty; fan sets efficiency+pressure+design flow rate; adiabatic→SprayHumidifier; steam→SteamHumidifier; Mixing→AirJunction; null/undefined→null; NaN airflow→component without duty | All pass |
| `Create/SystemPlantRoomTests.cs` | 8 | Single chain: component count, AirSystem membership, connections present; empty chain→null; NaN airflow still wires; MollierGroup overload | All pass |
| `Create/SupplyExtractTests.cs` | 7 | Supply+extract share a single AirSystem; shared exchanger is a single instance; heating coils present; room Space component; energy-centre creation; both chains empty→null | All pass |
| `Create/TwinWheelTopologyTests.cs` | 6 | Exactly one AirSystem; exactly one shared exchanger; both air paths connected; sensible+latent efficiency set; fans have pressure+efficiency; exchanger survives JSON round-trip | All pass |
| `Create/TwoRowLayoutTests.cs` | 6 | Exactly two rows used; supply row left-to-right; extract row placed in reverse; shared exchanger drawn once on the supply row; air connections routed between rows; short supply-only chain falls back to single row | All pass |
| `Create/LiquidLoopTests.cs` | 8 | Exactly one boiler + one chiller injected; heating+cooling liquid systems created; coil liquid In/Out connected; boiler/chiller loops closed; boiler related to the heating system, chiller to the cooling system; energy sources created and linked by name; template path does not inject duplicate plant | All pass |
| `Query/DutyTests.cs` | 7 | Known enthalpy pairs (heating, cooling) match hand calculation; scales with airflow; NaN airflow→NaN; null process→NaN; MassFlow valid/NaN cases | All pass |
| `Query/BypassFactorTests.cs` | 5 | Result in [0,1]; matches hand calculation; NaN for null process; clamps to 0 and to 1; non-zero for partial cooling | All pass |
| `Query/SystemComponentTypeTests.cs` | 9 | Heating→HeatingCoil; Cooling→CoolingCoil; Fan→Fan; HeatRecovery→Exchanger; adiabatic→SprayHumidifier; steam→SteamHumidifier; Mixing→AirJunction; null/Undefined→null | All pass |
| `Query/HeatRecoveryEfficiencyTests.cs` | 4 | Efficiencies invert the factory percentages; sensible-only leaves latent NaN; null process leaves both NaN; efficiencies clamped to [0,1] | All pass |
| `Query/HumidifierPropertiesTests.cs` | 10 | Spray AND steam setpoint = end-state relative humidity (not dry-bulb); spray effectiveness matches hand calculation; water-flow capacity matches mass balance; steam duty set unconditionally for library-built (near-isothermal) processes; negative humidity change clamps effectiveness to 0; saturated start (zero denominator) leaves effectiveness NaN; NaN airflow leaves flows NaN but still sets setpoint; null process leaves everything NaN | All pass |
| `Query/FanPressureTests.cs` | 6 | Exact inverse of `PickupTemperature`; ΔP equals efficiency × specific fan power × 1000; matches hand calculation with efficiency multiplying; scales linearly with efficiency; NaN for invalid inputs; NaN for non-positive temperature rise | All pass |
| `Json/RoundTripTests.cs` | 6 | ToJson→FromJson preserves heating-coil setpoint, cooling-coil bypass factor, fan pressure, plant-room component count, exchanger efficiency, humidifier concrete type | All pass |
| `Diagnostics/ConversionDiagnosticTests.cs` | 15 | Every MOLLIER-001…014 code emitted at its trigger (unsupported process, NaN airflow, empty chain, null MollierGroup, null process chain, null process, invalid state, template overload, HR count mismatch, ADP/bypass-factor unavailable); severities correct; code format and meaning uniqueness | All pass |
| `Integration/TasExportReadinessTests.cs` | 7 | PlantRoom has an AirSystem; all air components have In+Out connectors; no dangling connectors except boundaries; JSON serializes without exceptions and round-trips; JSON contains expected type identifiers; every connection has both endpoints; at least one AirSystem-typed connection | All pass |
| `Integration/MVRE_ComparisonTests.cs` | 5 | Loads the real `MVRE.json` fixture (hard-fails with a clear message if not copied to test output — it is not silently skipped); plant-room counts; component-type counts (type-level, not count-for-count: MVRE's full liquid plant is not asserted against the bridge's minimal one); connector-type counts; air-side topology shape | All pass |
| `Integration/TwinWheelVerifyTests.cs` | 1 | `TwinWheelExample.Verify()` returns true with every check line PASS | All pass |

---

## L. CI / Build / Packaging Plan

### Build Order
```powershell
# 1. SAM base
dotnet build SAM/SAM.sln -c Debug

# 2. SAM_Mollier
dotnet build SAM_Mollier/SAM_Mollier.sln -c Debug

# 3. SAM_Systems (includes Mollier bridge)
dotnet build SAM_Systems/SAM_Systems.sln -c Debug
```

### Test Command
```powershell
dotnet test SAM_Systems/SAM_Systems/SAM.Analytical.Systems.Mollier.Tests/SAM.Analytical.Systems.Mollier.Tests.csproj
```

### Solution Update
Add `SAM.Analytical.Systems.Mollier.csproj` and `SAM.Analytical.Systems.Mollier.Tests.csproj` to `SAM_Systems.sln`.

### CI Risks
- Build order must be enforced: SAM → SAM_Mollier → SAM_Systems
- `HintPath` references require sibling repo build output at build time
- No NuGet packages — all DLL references are local

---

## M. Performance and Tolerances

### Complexity
- Process classification: O(1)
- Chain wiring: O(n) for n processes
- Overall: O(n) per `Create.SystemEnergyCentre` call
- Typical AHU (3–8 processes): <1 ms
- Large MollierGroup (100+): <10 ms

### Tolerances

| Quantity | Value | Source |
|----------|-------|--------|
| Temperature | 0.001 °C | `Tolerance.MacroDistance` |
| Humidity ratio | 1e-6 kg/kg | Latent flag check |
| Enthalpy | 1 J/kg | Unit scale |
| Duty | 1 W | Rounding |
| Bypass factor denominator | 1e-9 | `Tolerance.MicroDistance` |
| Density | 0.001 kg/m³ | `Tolerance.MacroDistance` |

### Error Handling
- **Never throw** from Create/Query — return null or NaN with diagnostics
- Only throw on programming errors (internal invariant violations), not on bad data

---

## N. Diagnostics and Reporting

Canonical, as delivered (`Classes/ConversionDiagnostic.cs`) — each code has exactly one
meaning and severity across every emission site in the bridge:

| Code | Constant | Severity | Meaning |
|------|----------|----------|---------|
| `MOLLIER-001` | `UnsupportedProcess` | Warning | Process type maps to no system component; skipped |
| `MOLLIER-002` | `NullProcess` | Warning | Null process encountered; skipped |
| `MOLLIER-003` | `InvalidProcessState` | Warning | Process start or end MollierPoint null/invalid; derived setpoints and duties not set |
| `MOLLIER-004` | `AirflowNaN` | Warning | Design airflow is NaN; duties/flows not set |
| `MOLLIER-005` | `NullProcessChain` | Error | Process chain or MollierGroup argument is null |
| `MOLLIER-006` | `ApparatusDewPointNotAvailable` | Info | ADP could not be computed; MinimumOffcoil not set |
| `MOLLIER-007` | `BypassFactorInvalid` | Info | Bypass factor NaN/invalid; not set |
| `MOLLIER-008` | `HeatRecoveryEfficiencyNotAvailable` | Warning | Paired heat-recovery processes yielded no usable sensible or latent effectiveness |
| `MOLLIER-009` | `ConnectionFailed` | Warning | Connector operation failed, or directional wiring fell back to automatic selection |
| `MOLLIER-010` | `DisplaySymbolMissing` | Info | No display symbol / no symbol library; display promotion skipped the component |
| `MOLLIER-011` | `NoProcessesInChain` | Error | MollierGroup contains zero processes |
| `MOLLIER-012` | `ChainEmpty` | Error | Chain produced zero system components |
| `MOLLIER-013` | `HeatRecoveryCountMismatch` | Warning | Supply/extract heat-recovery counts differ; surplus gets its own exchanger |
| `MOLLIER-014` | `LiquidInjectionFailed` | Error | Liquid injection failed or the loop was left partially wired |

---

## O. Risks and Mitigations

| Risk | Level | Mitigation |
|------|-------|------------|
| Build dependency fragility (HintPath) | High | Document build order; `build.ps1`; CI must build all three repos |
| SystemHumidifier abstract breaks old JSON | Medium | Accepted trade-off for `sow/2026-Q3`; document migration |
| SAM_Tas repo unavailable for TPD validation | Medium | Structural JSON comparison to MVRE.json; defer end-to-end |
| FanProcess carries no pressure/gauge (only ΔT) | Low | Derive ΔP from ΔT + assumed η_fan=0.7; expose η_fan parameter |
| MixingProcess has no properties beyond topology | Low | Skip ratio propagation; topology captures mixing point |
| Two-row layout shared exchanger complexity | Medium | Fallback to single-row if unresolved |
| Liquid auto-injection may surprise users | Medium | Only when no template; keep minimal; MOLLIER-011 diagnostic |
| No unit test framework in SAM_Systems | High | Add xunit test project (matches SAM.Tests convention) |

---

## P. AI Implementation Prompt Sequence

> **Historical record — do not implement from this section.** These are the original prompts as issued,
> retained to show how the work was commissioned. Several carry the pre-audit errors corrected in
> Section R (notably Prompt 4's `ΔP = ρ·cp·ΔT/η` with a hardcoded `1010`, and the dry-bulb humidifier
> setpoints). Where they conflict with Sections E, F, N or R, those sections govern.

### Prompt 1: Branch Setup & Solution Registration
```
Task: Prepare SAM_Systems for Mollier bridge enhancements.

Prerequisites: SAM (sow/2026-Q3) and SAM_Mollier (sow/2026-Q3) are built and their DLLs exist in their build/ directories.

Actions:
1. Ensure SAM_Systems is on branch `sow/2026-Q3` (PR #8 already merged via fast-forward).
2. Create feature branch `feature/mollier-bridge-enhancements` from `sow/2026-Q3`.
3. Open `SAM_Systems.sln`. Add the existing project `SAM_Systems\SAM.Analytical.Systems.Mollier\SAM.Analytical.Systems.Mollier.csproj` to the solution if not already present.
4. Verify `dotnet build SAM_Systems.sln -c Debug` succeeds from the SAM_Systems root.
5. Report build result and any missing references.

Do NOT modify any .cs files. Only .sln and branch operations.
```

### Prompt 2: Diagnostic Model
```
Task: Add structured diagnostics to SAM.Analytical.Systems.Mollier.

Create: `SAM_Systems\SAM.Analytical.Systems.Mollier\Classes\ConversionDiagnostic.cs`
- Enum `DiagnosticSeverity` { Info, Warning, Error }
- Class `ConversionDiagnostic` with properties: Severity, Code (string), Message (string), SourceProcess (IMollierProcess, nullable)
- Static class `DiagnosticCodes` with const strings MOLLIER-001 through MOLLIER-013

Create: `SAM_Systems\SAM.Analytical.Systems.Mollier\Classes\ConversionResult.cs`
- Class `ConversionResult` with List<ConversionDiagnostic>, bool HasErrors, bool HasWarnings

Modify each Create file (SystemComponent.cs, SystemPlantRoom.cs, SupplyExtract.cs, SystemEnergyCentre.cs):
- Add overload with `out List<ConversionDiagnostic>` parameter
- Existing overload calls new one, discards diagnostics
- Emit MOLLIER-001 (unsupported), MOLLIER-004 (NaN airflow), MOLLIER-012 (empty chain)
- Return null (not throw) when chain is empty

Do NOT modify Grasshopper files.
```

### Prompt 3: Humidifier Setpoint Derivation
```
Task: Derive humidifier properties from Mollier process psychrometrics.

Create: `SAM_Systems\SAM.Analytical.Systems.Mollier\Query\HumidifierProperties.cs`
- Static method `HumidifierProperties(HumidificationProcess process, double designAirflow, out double setpoint, out double effectiveness, out double duty, out double waterFlowCapacity)`
- setpoint = End.DryBulbTemperature
- Adiabatic: effectiveness = (W_end - W_start) / (W_sat(T_end) - W_start), clamped [0,1]
- Water flow = massFlow * (W_end - W_start) [kg/s]
- Isothermal/steam: if |T_end - T_start| < 0.01°C, duty = |massFlow * (h_end - h_start)| [W]
- Use `MollierPoint.SaturationMollierPoint()` for W_sat
- Use `Duty()` and `MassFlow()` from existing Query/Duty.cs

Modify: `Create/SystemComponent.cs` (lines 120-131, the HumidificationProcess branch)
- Call HumidifierProperties and set the corresponding SystemHumidifier subtype properties

Do NOT modify other Create files. Do NOT modify Grasshopper files.
```

### Prompt 4: Fan Pressure Derivation
```
Task: Derive fan pressure rise from FanProcess temperature pickup.

Create: `SAM_Systems\SAM.Analytical.Systems.Mollier\Query\FanPressure.cs`
- Static method `FanPressureRise(FanProcess fanProcess, double fanEfficiency = 0.7)` → double [Pa]
- ΔT = End.DryBulbTemperature - Start.DryBulbTemperature
- ΔP = density(Start) * 1010.0 * ΔT / fanEfficiency  (1010 J/kg·K = cp_air)
- Guard: return NaN if start/end invalid or ΔT <= 0

Modify: `Create/SystemComponent.cs` (lines 39-43, the FanProcess branch)
- Call FanPressureRise, set SystemFan.Pressure, OverallEfficiency = fanEfficiency
- Set SystemFan.DesignFlowRate = new SizedFlowValue(designAirflow) when valid

Do NOT modify other files.
```

### Prompt 5: Two-Row Layout
```
Task: Implement two-row auto-layout for supply+extract plant rooms.

Modify: `SAM_Systems\SAM.Analytical.Systems\Create\DisplaySystemEnergyCentre.cs`
- Detect when a single plant room has one AirSystem serving both supply and extract chains.
- Place supply components on top row (y=0), extract on bottom row (y=-DisplaySystemRowStep, currently 0.8).
- Supply: left-to-right in flow order.
- Extract: right-to-left (reverse order) for mirror layout, or left-to-right if simpler.
- Shared components (SystemExchanger with two air paths): place on supply row; route second air-path connectors to extract row.
- Fallback to current single-row layout if two-row placement cannot be resolved.
- Preserve existing constants DisplaySystemColumnStep=1.0 and DisplaySystemRowStep=0.8.

Do NOT create new files. Do NOT modify Mollier bridge files. Only modify this one file.
```

### Prompt 6: Liquid System Auto-Injection
```
Task: Auto-generate minimal liquid systems when no template is provided.

Create: `SAM_Systems\SAM.Analytical.Systems.Mollier\Create\LiquidSystem.cs`
- Private/internal method `InjectLiquidSystems(SystemPlantRoom systemPlantRoom, IEnumerable<IMollierProcess> supply, IEnumerable<IMollierProcess> extract)` returning `List<ConversionDiagnostic>`
- Search codebase for PlantSystem, LiquidSystem, Boiler, Chiller types in SAM.Analytical.Systems namespace first.
- If any HeatingProcess exists: create heating liquid system + boiler, wire to all SystemHeatingCoil liquid connectors.
- If any CoolingProcess exists: create cooling liquid system + chiller, wire to all SystemCoolingCoil liquid connectors.
- If no suitable liquid system types found: emit MOLLIER-011 diagnostic, return empty list.

Modify: `Create/SystemEnergyCentre.cs` and `Create/SupplyExtract.cs`
- In the `else` branch (when template is null), call InjectLiquidSystems before Add(displayPlantRoom).
- Forward any diagnostics.

Do NOT modify Grasshopper files. Search for existing plant system types before creating new ones.
```

### Prompt 7: Unit Test Project
```
Task: Create a unit test project for the Mollier bridge.

Create: `SAM_Systems\SAM.Analytical.Systems.Mollier.Tests\SAM.Analytical.Systems.Mollier.Tests.csproj`
- Target net8.0, ProjectReference to SAM.Analytical.Systems.Mollier
- PackageReference: xunit 2.9.2, Microsoft.NET.Test.Sdk 17.11.1, xunit.runner.visualstudio
- Output to ..\..\build_tests\

Create these test files:
1. Create/SystemComponentTests.cs — Fan→SystemFan (not HeatingCoil), Heating→Setpoint+Duty, Cooling→Setpoint+BypassFactor, Exchanger→LatentType, Humidifier→correct concrete subtype, Mixing→AirJunction, null/undefined→null
2. Query/DutyTests.cs — known enthalpy pairs, zero Δh, NaN airflow, invalid states
3. Query/BypassFactorTests.cs — in [0,1], clamp, zero denominator→NaN
4. Query/SystemComponentTypeTests.cs — all mappings correct
5. Query/HumidifierPropertiesTests.cs — spray effectiveness, steam duty
6. Query/FanPressureTests.cs — ΔP matches formula, NaN on invalid
7. Create/SystemPlantRoomTests.cs — chain wiring, Out→In direction, fresh-air/exhaust junctions
8. Create/SupplyExtractTests.cs — shared exchanger, room creation, DisplayAirSystemGroup
9. Json/RoundTripTests.cs — ToJson→FromJson preserves counts, duties, exchanger efficiencies
10. Diagnostics/ConversionDiagnosticTests.cs — all diagnostic codes
11. Integration/TasExportReadinessTests.cs — In+Out connectors, no dangling, JSON serializable

Add test project to SAM_Systems.sln.

Verify: `dotnet test SAM_Systems\SAM.Analytical.Systems.Mollier.Tests\SAM.Analytical.Systems.Mollier.Tests.csproj`

Do NOT modify production code. Only create test files.
```

### Prompt 8: Documentation Update
```
Task: Update bridge documentation.

Modify: `docs/Mollier-Systems-Bridge.md`
- Add Phase 2-6 features: diagnostics, humidifier derivation, fan pressure, two-row layout, liquid injection
- Add diagnostic model section with code table
- Add summer cooling-only worked example (no heat recovery)
- Add winter heating+humidification worked example
- Update file listing with new files (ConversionDiagnostic.cs, HumidifierProperties.cs, FanPressure.cs, LiquidSystem.cs, Tests)
- Update Status/TODO: mark new items as ✅ done

Do NOT modify .cs files.
```

### Prompt 9: Build Script & Final Verification
```
Task: Add build and test scripts.

Create: `build.ps1` at SAM_Systems root:
- Build SAM.sln (if needed), then SAM_Mollier.sln, then SAM_Systems.sln in order
- Report success/failure per repo

Create: `test.ps1` at SAM_Systems root:
- Run dotnet test on Mollier test project
- Report results

Run build.ps1 and test.ps1.
Fix any build issues.
Ensure ALL tests pass.
Ensure `TwinWheelExample.Verify(out var messages)` returns true (call from test or manually).

Commit: "feat(mollier-bridge): diagnostics, humidifier/fan derivation, two-row layout, liquid systems, tests"
```

---

## Summary of Changes vs. PR #8

| PR #8 Has | This Plan Adds |
|-----------|----------------|
| Working core bridge | Structured diagnostics (severity levels, canonical codes) |
| `List<string>` reports | Typed `ConversionDiagnostic` + `out` overloads on every entry point |
| `SystemHumidifier` abstract | Humidifier setpoint/effectiveness/duty derivation |
| Fan creates `SystemFan` | Fan pressure derivation from ΔT |
| Single-row auto-layout | Two-row layout for supply+extract |
| Template merge | Auto-injection of minimal liquid systems + default energy sources |
| `TwinWheelExample` self-check | Full xunit test suite (123 tests across 17 files) |
| Informal build | `build.ps1`, `test.ps1`, solution registration |
| — | Tas export structural validation tests |

---

## Repository Workflow (Memory)

- **Work exclusively in SAM-BIM fork.** Never touch upstream HoareLea.
- **Quarterly cadence:** Open `sow/2026-Q3` from `master` at quarter start. Work entire quarter on this branch.
- **End of quarter:** Raise PR from `sow/2026-Q3` to upstream `HoareLea/master`.
- **Current state:** PR #8 merged into `sow/2026-Q3` via fast-forward. SAM_Mollier and SAM are on `sow/2026-Q3`.
- **Q3 delivery:** All 10 phases delivered. 12 commits on `feature/mollier-bridge-enhancements`,
  starting with `46d08f6` (the initial P1–P10 delivery) and continuing through a later
  diagnostics-contract, physics-correction and test-suite audit (see the Corrections
  note at the end of this document).

---

## Q. Delivery Status (2026-07-10)

### Phase Completion

| Phase | Description | Status | Files Created | Files Modified |
|-------|------------|--------|--------------|---------------|
| **P1** | Branch Setup & Solution Registration | ✅ Pre-existing (PR #8) | — | `SAM_Systems.sln` |
| **P2** | Structured Diagnostics Model | ✅ Pre-existing (PR #8) + Enhanced (P6) | `Classes/ConversionDiagnostic.cs`, `Classes/ConversionResult.cs` | 4 Create files |
| **P3** | Humidifier Setpoint Derivation | ✅ Pre-existing (PR #8) | `Query/HumidifierProperties.cs` | `Create/SystemComponent.cs` |
| **P4** | Fan Pressure Derivation | ✅ Delivered | `Query/FanPressure.cs` | `Create/SystemComponent.cs` |
| **P5** | Two-Row Layout | ✅ Delivered | — | `Create/DisplaySystemEnergyCentre.cs` |
| **P6** | Liquid System Auto-Injection | ✅ Delivered | `Create/LiquidSystem.cs` | `Create/SystemEnergyCentre.cs`, `Create/SupplyExtract.cs`, `Create/SystemEnergyCentreByTemplate.cs` |
| **P7** | Unit Test Project (62 → 75 tests) | ✅ Delivered | 11 test files + `.csproj` | `SAM_Systems.sln` |
| **P8** | Tas Export Structural Validation | ✅ Delivered | `Integration/TasExportReadinessTests.cs`, `Integration/MVRE_ComparisonTests.cs` | — |
| **P9** | Documentation & Examples | ✅ Delivered | — | `docs/Mollier-Systems-Bridge.md` |
| **P10** | Build Script & Final Verification | ✅ Delivered | `build.ps1`, `test.ps1`, `Integration/TwinWheelVerifyTests.cs` | `Example/TwinWheelExample.cs` |

### Verification

| Check | Result |
|-------|--------|
| `build.ps1` (SAM → SAM_Mollier → SAM_Systems) | All 3 solutions OK |
| `dotnet test` | **75 passed, 0 failed, 0 skipped** |
| `TwinWheelExample.Verify()` | 27/27 PASS lines |
| `docs/Mollier-Systems-Bridge.md` | Updated with P4-P9 features, 3 worked examples, file listing |
| `docs/mollier_processes_to_systems_IMPLEMENTATION_PLAN.md` | This document — delivery status recorded |

### New Files Delivered (24 files)

*Corrected count — verified against `git diff --name-status sow/2026-Q3...feature/mollier-bridge-enhancements`.
`Classes/ConversionResult.cs` (listed here previously) was created and later deleted
within this branch, so it nets to neither added nor modified and is not shipped; see the
Corrections note below.*

| Directory | Files |
|-----------|-------|
| `SAM.Analytical.Systems.Mollier/Query/` | `FanPressure.cs`, `HumidifierProperties.cs` |
| `SAM.Analytical.Systems.Mollier/Create/` | `LiquidSystem.cs` |
| `SAM.Analytical.Systems.Mollier/Classes/` | `ConversionDiagnostic.cs` |
| `SAM.Analytical.Systems.Mollier.Tests/` | `.csproj` + 17 test `.cs` files: `Create/LiquidLoopTests.cs`, `Create/SupplyExtractTests.cs`, `Create/SystemComponentTests.cs`, `Create/SystemPlantRoomTests.cs`, `Create/TwinWheelTopologyTests.cs`, `Create/TwoRowLayoutTests.cs`, `Diagnostics/ConversionDiagnosticTests.cs`, `Integration/MVRE_ComparisonTests.cs`, `Integration/TasExportReadinessTests.cs`, `Integration/TwinWheelVerifyTests.cs`, `Json/RoundTripTests.cs`, `Query/BypassFactorTests.cs`, `Query/DutyTests.cs`, `Query/FanPressureTests.cs`, `Query/HeatRecoveryEfficiencyTests.cs`, `Query/HumidifierPropertiesTests.cs`, `Query/SystemComponentTypeTests.cs` |
| Root | `build.ps1`, `test.ps1` |

### Modified Files (15 files)

| File | Change |
|------|--------|
| `.gitignore` | Build/test output ignores |
| `Grasshopper/SAM.Analytical.Grasshopper.Systems/Component/SAMAnalyticalSystemResults.cs` | Unrelated compatibility fix (MinCompatibleVersion/ObsoleteSeverity, SPDX header) — not part of the Mollier bridge feature |
| `SAM_Systems.sln` | Added test project |
| `Create/OutsideAirJunction.cs` | `AddBoundaryJunction` gained a diagnostics parameter |
| `Create/RoomGroup.cs` | `AddRoom`/`AddDisplayAirSystemGroup` gained a diagnostics parameter |
| `Create/SupplyExtract.cs` | Liquid injection, diagnostics |
| `Create/SystemComponent.cs` | Fan pressure + efficiency, humidifier properties, diagnostics |
| `Create/SystemEnergyCentre.cs` | Liquid injection, diagnostics, energy-source attachment |
| `Create/SystemEnergyCentreByResult.cs` | Diagnostics overload |
| `Create/SystemEnergyCentreByTemplate.cs` | Liquid injection + energy-source attachment when template=null |
| `Create/SystemPlantRoom.cs` | Diagnostics overload |
| `Example/TwinWheelExample.cs` | Air-side-only display checks; `FanProcessBySpecificFanPower` workaround for the upstream `Create.FanProcess` defect |
| `SAM.Analytical.Systems/Create/DisplaySystemEnergyCentre.cs` | Two-row supply/extract layout |
| `docs/Mollier-Systems-Bridge.md` | Full update — diagnostics, physics and file/test-count corrections |
| `docs/mollier_processes_to_systems_IMPLEMENTATION_PLAN.md` | Delivery status (this section) + Corrections note |

---

## R. Corrections (2026-07-17)

A documentation and code audit against the delivered `feature/mollier-bridge-enhancements`
branch (12 commits) found this plan had drifted from the implementation in several
places. Summary of what changed and why, for anyone reconciling earlier discussions
against the current document:

- **Fan efficiency placement was inverted.** The plan (Phase 4, Section F) and the
  originally-delivered code both had `ΔP = ρ·cp·ΔT/η_fan` (efficiency dividing). This
  overstates pressure by a factor of `1/η²` and is physically backwards — a less
  efficient fan should yield a *smaller* recoverable pressure for a given temperature
  pickup, not a larger one. Corrected to `ΔP = η_fan·ρ·cp·ΔT` (efficiency multiplying),
  the exact inverse of `SAM.Core.Mollier Query.PickupTemperature`. `cp_air` is also no
  longer a hardcoded `1010 J/kg·K`; it is read from the process's own inlet state via
  `Query.SpecificHeatCapacity_Air`.
- **Humidifier setpoint units were wrong.** The plan and the originally-delivered code
  set `Setpoint = End.DryBulbTemperature` for both spray and steam humidifiers (and an
  earlier draft of this plan described the spray setpoint as a target humidity ratio).
  Neither matches how Tas TPD actually consumes the setpoint: `SAM_Tas Convert.ToTPD`
  passes humidifier setpoints straight through, and TPD humidifiers control downstream
  **relative humidity** under their default flags. Corrected to
  `Setpoint = End.RelativeHumidity` [%, 0–100] for both humidifier types.
- **The steam duty isothermality gate was dead code.** The plan and original
  implementation only set `SystemSteamHumidifier.Duty` when `|ΔT| < 0.01`. Library-built
  `SteamHumidificationProcess`s are near-isothermal but not exactly so (injected steam
  carries sensible heat, ΔT ≈ +0.3 K), so this gate never fired and Duty was never set in
  practice. The gate has been removed; duty is now computed unconditionally.
- **Diagnostic codes have been realigned to the delivered contract.** The plan's Section
  G/N code list (drafted before implementation) named and numbered several codes
  differently from what was actually built (e.g. separate `NoStartState`/`NoEndState`
  vs. the single delivered `InvalidProcessState`; `NoLiquidSystemTemplate` vs. the
  delivered `LiquidInjectionFailed`; several severities differed). Sections G and N now
  reproduce the canonical, delivered `DiagnosticCodes` table verbatim (MOLLIER-001…014).
- **`ConversionResult` was removed.** It was created during the initial delivery but
  never constructed or referenced anywhere in the bridge or its tests, and has been
  deleted as dead code. The diagnostics API is the `out List<ConversionDiagnostic>`
  overloads, which now exist on every top-level entry point, including the
  template-based and `AirHandlingUnitResult`-based ones (old signatures preserved as
  wrappers).
- **Default energy sources were added.** Phase 6's acceptance criterion ("output has
  `SystemEnergySource` entries") was not yet true when this plan's delivery status
  (Section Q) was first recorded. It is now: template-less liquid injection adds a
  `SystemEnergySource` "Natural Gas" and an `ElectricalEnergySource` "Grid Supplied
  Electricity" (values mirroring `MVRE.json`) at the energy-centre level.
- **The MVRE comparison fixture was never actually loaded.** The original comparison
  test had a fixture-resolution gap, so the comparison against `MVRE.json` was not
  genuinely exercised. `Integration/MVRE_ComparisonTests.cs` now resolves the fixture
  from the test output directory and hard-fails with a clear message if it is not
  present, rather than silently passing without comparing anything. The comparison
  itself remains intentionally type-level/air-side structural, not count-for-count — see
  the Limitations section of `docs/Mollier-Systems-Bridge.md`.
- **Test suite grew from the planned 11 files / 30+ tests to 17 files / 123 tests**
  (Section K), all passing, 0 skipped.
- **File counts corrected:** 24 files added / 15 modified vs. `sow/2026-Q3` (Section Q
  had recorded 21 new / 10 modified); 12 commits on the branch (Section Q had recorded a
  single commit `46d08f6`, which is in fact the first of the 12).

This note supplements, and does not replace, Section Q's delivery-status table, which
remains a dated snapshot (2026-07-10) of the original P1–P10 delivery and is left as-is
pending a final verification pass.
