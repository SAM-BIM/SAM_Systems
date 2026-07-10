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
- `SystemSprayHumidifier`: Setpoint = End dry-bulb, Effectiveness = (W_end − W_start) / (W_sat − W_start) clamped [0,1]
- `SystemSteamHumidifier`: Setpoint = End dry-bulb, Duty = |ṁ·(h_end−h_start)| when isothermal
- WaterFlowCapacity (spray) = ṁ·(W_end − W_start) [kg/s]
- Use `MollierPoint.SaturationMollierPoint()` for W_sat

**Acceptance:**
- Spray humidifier Setpoint and Effectiveness set
- Steam humidifier Setpoint and Duty set
- Tests verify against hand-calculated psychrometrics

---

### Phase 4: Fan Pressure Derivation (XS, 1 session, Low risk)

Compute fan pressure rise from temperature pickup.

**New file:** `Query/FanPressure.cs`

**Modified file:** `Create/SystemComponent.cs` — set `SystemFan.Pressure`, `OverallEfficiency`, `DesignFlowRate`

**Key logic:**
- ΔT = End.DryBulb − Start.DryBulb
- ΔP = ρ_inlet · cp_air(1010 J/kg·K) · ΔT / η_fan (default η_fan = 0.7)
- `SystemFan.DesignFlowRate` from designAirflow [m³/s]

**Acceptance:**
- Fan pressure and efficiency set when FanProcess has valid states
- NaN return when states invalid or ΔT ≤ 0

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
- Output `SystemEnergyCentre` has `SystemEnergySource` entries when coils present
- Coil liquid connectors wired to plant
- Does NOT overwrite user-provided template
- Graceful fallback when no template and no coils (MOLLIER-011)

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

  // Fan: pressure from temperature rise
  if Fan: ΔT = End.T - Start.T; ΔP = ρ·cp·ΔT/0.7; component.Pressure = ΔP; component.DesignFlowRate = airflow

  // Spray humidifier: effectiveness
  if SprayHumidifier: component.Setpoint = End.T;
      W_sat = saturation(End.T, Pressure); component.Effectiveness = clamp((End.W-Start.W)/(W_sat-Start.W), 0, 1)

  // Steam humidifier: duty when isothermal
  if SteamHumidifier: component.Setpoint = End.T;
      if |Start.T-End.T|<0.01: component.Duty = |ṁ·Δh|

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

public static class DiagnosticCodes
{
    public const string UnsupportedProcess    = "MOLLIER-001";
    public const string NoStartState          = "MOLLIER-002";
    public const string NoEndState            = "MOLLIER-003";
    public const string NoDesignAirflow       = "MOLLIER-004";
    public const string InvalidDensity        = "MOLLIER-005";
    public const string BypassFactorClamped   = "MOLLIER-006";
    public const string AdpUnavailable        = "MOLLIER-007";
    public const string EfficiencyClamped     = "MOLLIER-008";
    public const string ConnectFailed         = "MOLLIER-009";
    public const string NoSymbolForType       = "MOLLIER-010";
    public const string NoLiquidSystemTemplate= "MOLLIER-011";
    public const string EmptyProcessChain     = "MOLLIER-012";
    public const string DuplicateExchangerSkew= "MOLLIER-013";
}
```

### API Philosophy: Return null vs Throw vs Diagnostic

| Situation | Behavior |
|-----------|----------|
| `null` process or empty chain | Return `null` + `MOLLIER-012` |
| Unsupported process type | Skip + `MOLLIER-001` Warning, continue chain |
| Invalid psychrometric state (NaN) | Skip + `MOLLIER-002`/`MOLLIER-003` Error |
| Cannot compute duty (NaN airflow) | Component created without duty + `MOLLIER-004` Warning |
| Denominator ≈ 0 in bypass factor | By-pass = `NaN` + `MOLLIER-007` Warning |
| Connector wiring fails | Skip connection + `MOLLIER-009` Error |
| No symbol for display object | Skip display + `MOLLIER-010` Warning |

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

| Test File | Tests | Expected Results |
|-----------|-------|-----------------|
| `SystemComponentTypeTests.cs` | FanProcess returns Fan NOT HeatingCoil; Heating→HeatingCoil; Cooling→CoolingCoil; each humidifier subtype→correct concrete; Mixing→AirJunction; Undefined/Specific→null; null→null | All pass |
| `SystemComponentTests.cs` | Fan creates SystemFan; Heating sets Setpoint+Duty; Cooling sets Setpoint+BypassFactor+MinimumOffcoil+Duty; Exchanger sets LatentType when humidity shifts; adiabatic→SprayHumidifier; steam→SteamHumidifier; undefined→null; NaN airflow→component without duty | All pass |
| `DutyTests.cs` | Known enthalpy pairs match hand calculation; zero Δh→0; NaN airflow→NaN; invalid state→NaN | All pass |
| `BypassFactorTests.cs` | Result in [0,1]; matches hand calculation; zero denominator→NaN; clamp below zero→0; clamp above one→1; null→NaN | All pass |
| `HeatRecoveryEfficiencyTests.cs` | Sensible in (0,1]; matches formula; latent when humidity changes; NaN when sensible-only; null→NaN | All pass |
| `HumidifierPropertiesTests.cs` | Spray: effectiveness in [0,1]; Steam: duty matches |ṁ·Δh|; NaN when invalid | All pass |
| `FanPressureTests.cs` | ΔP matches ρ·cp·ΔT/η; NaN when invalid state | All pass |
| `SystemPlantRoomTests.cs` | Single chain: correct component count, Out→In direction walkable; empty chain→null; single component relates to air system; fresh-air/exhaust junctions present | All pass |
| `SupplyExtractTests.cs` | Supply+extract share single AirSystem; shared exchanger has two air paths wired; efficiencies set; room Space/Damper/Group Junctions created; DisplayAirSystemGroup created | All pass |
| `RoundTripTests.cs` | ToJson→FromJson preserves plant room count, component count, duties, bypass factor, exchanger efficiencies; display geometry survives round-trip | All pass |
| `ConversionDiagnosticTests.cs` | MOLLIER-001 through MOLLIER-013 emitted correctly; severity levels correct | All pass |
| `TasExportReadinessTests.cs` | All air components have In+Out; no dangling connectors except boundaries; JSON serializes; structural comparison to MVRE.json passes | All pass |

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

| Code | Severity | Condition |
|------|----------|-----------|
| `MOLLIER-001` | Warning | Unsupported/undefined process skipped |
| `MOLLIER-002` | Error | Process has no valid start state |
| `MOLLIER-003` | Error | Process has no valid end state |
| `MOLLIER-004` | Warning | No design airflow; duties unset |
| `MOLLIER-005` | Error | Cannot compute inlet air density |
| `MOLLIER-006` | Warning | Bypass factor clamped to bounds |
| `MOLLIER-007` | Warning | ADP unavailable; bypass = NaN |
| `MOLLIER-008` | Warning | Recovery effectiveness clamped |
| `MOLLIER-009` | Error | Connector wiring failed |
| `MOLLIER-010` | Warning | No display symbol for type |
| `MOLLIER-011` | Info | No liquid system template; air-only |
| `MOLLIER-012` | Error | Empty process chain |
| `MOLLIER-013` | Warning | Supply/extract HR count mismatch |

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
| Working core bridge | Structured diagnostics (severity levels, codes) |
| `List<string>` reports | Typed `ConversionDiagnostic` + `ConversionResult` |
| `SystemHumidifier` abstract | Humidifier setpoint/effectiveness/duty derivation |
| Fan creates `SystemFan` | Fan pressure derivation from ΔT |
| Single-row auto-layout | Two-row layout for supply+extract |
| Template merge | Auto-injection of minimal liquid systems |
| `TwinWheelExample` self-check | Full xunit test suite (30+ tests across 11 files) |
| Informal build | `build.ps1`, `test.ps1`, solution registration |
| — | Tas export structural validation tests |

---

## Repository Workflow (Memory)

- **Work exclusively in SAM-BIM fork.** Never touch upstream HoareLea.
- **Quarterly cadence:** Open `sow/2026-Q3` from `master` at quarter start. Work entire quarter on this branch.
- **End of quarter:** Raise PR from `sow/2026-Q3` to upstream `HoareLea/master`.
- **Current state:** PR #8 merged into `sow/2026-Q3` via fast-forward. SAM_Mollier and SAM are on `sow/2026-Q3`.
