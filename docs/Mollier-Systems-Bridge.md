# Mollier → HVAC System bridge (`SAM.Analytical.Systems.Mollier`)

## Context

Conceptual HVAC design begins on the psychrometric (Mollier) chart, where an
engineer sketches the sequence of air-state changes — mixing, heat recovery,
cooling, heating, humidification — that condition a space. Turning that conceptual
sequence into a dynamic, simulatable system model has been a manual, error-prone
step.

This bridge automates it: a chain of `IMollierProcess` (from `SAM_Mollier`) is
converted directly into a connected, simulation-ready `SystemEnergyCentre` (in
`SAM_Systems`). The result serialises to JSON and is handed to the existing Tas
TPD export path for annual simulation — making the Mollier chart an executable
design artefact rather than a static drawing.

It is the engineering feature behind Abstract 2 of `sow/2026-Q3`
(*From Psychrometric Process to Simulatable HVAC System*).

## Design

A new assembly **`SAM.Analytical.Systems.Mollier`** couples the two mature models
without a circular reference: it lives in `SAM_Systems`, project-references the
Systems projects, and consumes `SAM.Core.Mollier` / `SAM.Analytical.Mollier` as
built DLLs from the sibling `SAM_Mollier` repo.

> **Build order:** build `SAM` (base) and `SAM_Mollier` before `SAM_Systems`, so
> the Mollier DLLs exist at `..\..\..\SAM_Mollier\build\` for the `HintPath`
> references.

### Per-process → component mapping (`Create.SystemComponent`)

| Mollier process            | SAM_Systems component | Derived values |
|----------------------------|-----------------------|----------------|
| `FanProcess`               | `SystemFan`           | `Pressure` = FanPressureRise (ΔP = η·ρ·cp·ΔT); `OverallEfficiency` = 0.7; `DesignFlowRate` = design airflow × 1000 [l/s] |
| `HeatingProcess`           | `SystemHeatingCoil`   | `Setpoint` = End dry-bulb; `Duty` = m·Δh |
| `CoolingProcess`           | `SystemCoolingCoil`   | `Setpoint` = End dry-bulb; `BypassFactor` from Efficiency/ADP; `MinimumOffcoil` = ADP dry-bulb; `Duty` = m·Δh |
| `HeatRecoveryProcess`      | `SystemExchanger`     | `Setpoint` = End dry-bulb; latent flagged when humidity ratio shifts (twin-wheel) |
| `HumidificationProcess`    | `SystemHumidifier`    | `Setpoint` = End relative humidity [%, 0–100] for both; Adiabatic → `SystemSprayHumidifier` (+ effectiveness, water-flow capacity); Steam → `SystemSteamHumidifier` (+ duty, computed unconditionally) |
| `MixingProcess`            | `SystemAirJunction`   | — |
| `Undefined` / `Specific`   | (skipped)             | — |

`FanProcess` is tested before `HeatingProcess` because it derives from it.

### Duties from intensive states

Mollier processes are intensive-state only, so duties require a **design airflow**.
The primary `Create` methods take an explicit volumetric airflow `[m³/s]`; mass
flow is `airflow · ρ(inlet)` using the moist-air density at the process Start, and
duty is `|ṁ · (h_end − h_start)|` (`Query.Duty`). Bypass factor is
`(T_off − T_adp) / (T_on − T_adp)` clamped to `[0,1]` (`Query.BypassFactor`),
using `CoolingProcess.ApparatusDewPoint()` (which already folds in efficiency).

### Fan pressure derivation (`Query.FanPressureRise`)

Fan pressure rise [Pa] is derived from the temperature pickup across a `FanProcess`. All
fan input power ends up as heat in the air stream, so the pickup temperature is
`ΔT = SFP / (ρ·cp)`, where the Specific Fan Power is `SFP = ΔP / η`. Solving for the
pressure gives the bridge's actual formula — **efficiency multiplies**:

```
ΔT = End.DryBulbTemperature − Start.DryBulbTemperature
ΔP = η_fan · ρ_inlet · cp_air · ΔT
```

where `η_fan` is the fan total (overall) efficiency [0..1] (default 0.7), `ρ_inlet` is
the moist-air density at the process inlet, and `cp_air` is the moist-air specific heat
capacity at the inlet state — read from `SAM.Core.Mollier Query.SpecificHeatCapacity_Air`
(returned in kJ/kg·K, converted ×1000 to J/kg·K), **not** a hardcoded constant. A less
efficient fan needs more input power — and therefore heats the air more — for the same
pressure rise, so a given temperature pickup corresponds to a *smaller* pressure rise at
lower efficiency (the earlier `ΔP = ρ·cp·ΔT/η` formulation had this inverted, overstating
pressure by a factor of `1/η²`).

`FanPressureRise` is the exact inverse of `SAM.Core.Mollier Query.PickupTemperature`
(`ΔT = SFP/(ρ·cp)` with `SFP = ΔP/η`), so the invariant `ΔP == η_fan · SFP · 1000` [Pa]
holds regardless of the inlet state used to construct the process.

Returns `NaN` when: the process is null; the efficiency is `NaN`, zero or negative; the
start/end states are null or invalid; the temperature rise is not strictly positive
(`ΔT ≤ 0` — a fan always heats the air); or the inlet density or specific heat capacity
are unavailable.

`SystemComponent` for `FanProcess` calls `FanPressureRise`; when the result is valid, it
sets `SystemFan.Pressure` and `SystemFan.OverallEfficiency` (= 0.7). It also sets
`SystemFan.DesignFlowRate` to the design airflow converted to **litres per second**
(`m³/s × 1000`) with `DesignFlowType = FlowRateType.Value`, matching Tas TPD's fan flow
units (Specific Fan Power is quoted in W/(l/s)).

### Humidifier derivation (`Query.HumidifierProperties`)

Humidification processes derive four properties for the control model:

- **Setpoint** — the end-state **relative humidity** [%, 0–100], for **both** spray
  (adiabatic) and steam humidifiers. SAM humidifier setpoints pass straight through to
  Tas TPD (`SAM_Tas Convert.ToTPD` maps `SprayHumidifier`/`SteamHumidifier` setpoints
  verbatim), and TPD humidifiers control downstream relative humidity under their
  default flags (the TPD template code sets, e.g., `sprayHumidifier.Setpoint.Value = 90`).
  Setpoint is **not** an end dry-bulb temperature and **not** a target humidity ratio on
  either humidifier type.
- **Effectiveness** [0..1] — adiabatic (spray) only: the ratio of actual humidity-ratio
  rise to the maximum possible (saturation), clamped to `[0,1]`.
- **Water-flow capacity** [kg/s] — adiabatic (spray) only: mass rate of water
  evaporated, `ṁ · Δω`.
- **Duty** [W] — steam only: humidification thermal load, `|ṁ · (h_end − h_start)|`,
  computed **unconditionally**. Library-built `SteamHumidificationProcess`s are
  near-isothermal but not exactly so (the injected steam carries sensible heat,
  typically `ΔT ≈ +0.3 K`), so an isothermality gate on `|ΔT| < 0.01` would never fire —
  it has been removed; duty is always set when the states and airflow are valid.

Adiabatic humidification processes map to `SystemSprayHumidifier`; steam (isothermal)
processes map to `SystemSteamHumidifier`. All values return `NaN` when the process or
endpoints are invalid.

### Liquid system auto-injection (`InjectLiquidSystems`)

When no energy-centre template is provided, the bridge auto-generates minimal liquid
systems so the output is simulation-ready without manual plant definition:

- **Heating:** if any `SystemHeatingCoil` exists in the plant room, a `LiquidSystem`
  ("Heating Hot Water") and a `SystemBoiler` ("Boiler") are created.
- **Cooling:** if any `SystemCoolingCoil` exists, a `LiquidSystem` ("Chilled Water")
  and a `SystemAirSourceChiller` ("Chiller") are created.

Each loop is daisy-chained and **closed**: `boiler.Out → coil.In → coil.Out → … →
coil.Out → boiler.In` (equivalently for the chiller). Coils expose liquid connectors
distinct from their air connectors — `SystemHeatingCoil` liquid In/Out are connector
indexes 3/4, `SystemCoolingCoil` liquid In/Out are 2/3, and the boiler/chiller liquid
In/Out are both 0/1 — but the injector never assumes fixed positions: it locates each
component's first *unconnected* liquid connector of the required direction dynamically
via `SystemPlantRoom.Indexes(component, LiquidSystem, Unconnected, direction)`.

#### Default energy sources

The template-less path also returns two default **`SystemEnergySource`** objects that
the caller adds at the **energy-centre level** (energy sources live on
`SystemEnergyCentre`, not inside the plant room), mirroring the values in the reference
`MVRE.json` project:

| Name | Type | CO2 factor | Peak cost | PEF |
|------|------|-----------:|----------:|----:|
| `Natural Gas` | `SystemEnergySource` | 0.216 | 0.05 | 0 |
| `Grid Supplied Electricity` | `ElectricalEnergySource` | 0.519 | 0.13 | 0 |

The boiler is stamped with `SystemObjectParameter.EnergySourceName` = `"Natural Gas"`;
the chiller with both `EnergySourceName` and `FanEnergySourceName` = `"Grid Supplied
Electricity"`. `SAM_Tas` resolves fuel sources by these names (`Convert.ToTPD` for
`BoilerPlant`/`Chiller` reads `EnergySourceName`; `FuelSource.cs` maps a
`SystemEnergySource` to a TPD `FuelSource`, flagging it Electrical when the source is an
`ElectricalEnergySource`).

Because energy sources belong to the `SystemEnergyCentre` rather than the
`SystemPlantRoom`, `InjectLiquidSystems` only wires the plant and *returns* the default
sources — every top-level entry point (`Create.SystemEnergyCentre` and its
supply+extract and `AirHandlingUnitResult` overloads) adds them to the energy centre it
constructs via an internal `AddSystemEnergySources` helper, not the plant-room-level
injector itself.

**Template path:** when a `template` energy centre is supplied, injection is skipped
**entirely** — the template already owns its plant, liquid loops and energy sources;
the bridge only merges the air side into it (proven by
`LiquidLoopTests.TemplatePath_DoesNotInject_DuplicatePlant`).

`InjectLiquidSystems` is called from every `SystemEnergyCentre` entry point where no
template is supplied. When injection cannot be performed or a loop is left partially
wired, a `MOLLIER‑014` (`LiquidInjectionFailed`) **error** diagnostic is emitted.

### Diagnostic model

Every bridge `Create` method with a diagnostic `out` parameter produces a
`List<ConversionDiagnostic>` with structured `Severity` / `Code` / `Message` entries.
Codes are canonical and stable — each has exactly one meaning and severity across every
emission site in the bridge (`Classes/ConversionDiagnostic.cs`):

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

`ConversionDiagnostic` carries a reference to the source `IMollierProcess` (or null
for chain-level diagnostics). Diagnostics accumulate across the conversion pipeline
and are surfaced in the Grasshopper nodes for user feedback.

### Chain → topology

`Create.SystemPlantRoom` maps each process in order, then wires the components
sequentially along one `AirSystem` using `SystemPlantRoom.Connect(prev, cur, out _, airSystem)`
so Out→In connectors chain in process order. `Create.SystemEnergyCentre` wraps the
plant room in a `SystemEnergyCentre`. Overloads accept either an ordered
`IEnumerable<IMollierProcess>` or a `MollierGroup`.

JSON round-trip needs no new code — the emitted components and `SystemEnergyCentre`
already serialise via their existing `ToJsonObject()`/`FromJsonObject()`.

## Files

```
SAM_Systems/SAM.Analytical.Systems.Mollier/
  SAM.Analytical.Systems.Mollier.csproj   # netstandard2.0; refs Systems projects + Mollier/base DLLs
  Query/SystemComponentType.cs            # process → component System.Type classifier
  Query/Duty.cs                           # Duty(process, airflow), MassFlow(process, airflow)
  Query/BypassFactor.cs                   # BypassFactor(coolingProcess) from ADP
  Query/FanPressure.cs                    # FanPressureRise(fanProcess, efficiency) from ΔT pickup
  Query/HeatRecoveryEfficiency.cs         # supply-side sensible/latent effectiveness from both paths
  Query/HumidifierProperties.cs           # HumidifierProperties(process, airflow, out setpoint/effectiveness/duty/water)
  Create/SystemComponent.cs               # process → ISystemComponent (duty/setpoint/bypass/fan-pressure)
  Create/SystemPlantRoom.cs               # ordered chain → connected plant room
  Create/SystemEnergyCentre.cs            # plant room → energy centre (top-level entry)
  Create/SystemEnergyCentreByTemplate.cs  # merge air systems into template energy centre
  Create/SystemEnergyCentreByResult.cs    # overload sourcing design airflow from AirHandlingUnitResult
  Create/SupplyExtract.cs                 # supply + extract chains; shared twin-wheel exchanger; boundary junctions; room creation
  Create/OutsideAirJunction.cs            # AddBoundaryJunction helper
  Create/RoomGroup.cs                     # AddRoom (space + damper + group junctions), DisplayAirSystemGroup
  Create/LiquidSystem.cs                  # InjectLiquidSystems; auto-create heating/cooling liquid loops

SAM_Systems/SAM.Analytical.Systems/
  Create/DisplaySystemEnergyCentre.cs     # auto-layout schematic; two-row supply/extract; connection routing

SAM_Systems/SAM.Analytical.Systems.Mollier/
  Classes/ConversionDiagnostic.cs         # DiagnosticSeverity, ConversionDiagnostic, DiagnosticCodes

SAM_Systems/SAM.Analytical.Systems.Mollier.Tests/            # 123 tests across 17 files
  SAM.Analytical.Systems.Mollier.Tests.csproj   # net8.0; xunit 2.9.2; references bridge + Mollier/base DLLs
  Create/SystemComponentTests.cs          # (13) per-process mapping, fan-before-heating, fan efficiency/pressure/design-flow, null/undefined/NaN-airflow
  Create/SystemPlantRoomTests.cs          # (8) single-chain wiring, component count, AirSystem membership, connections present, empty chain, NaN airflow
  Create/SupplyExtractTests.cs            # (7) single shared AirSystem, twin-wheel shared exchanger, room Space, energy-centre creation
  Create/TwinWheelTopologyTests.cs        # (6) one AirSystem, one shared exchanger, both air paths connected, sensible+latent efficiency, exchanger JSON round-trip
  Create/TwoRowLayoutTests.cs             # (6) two-row split, supply left-to-right, extract reversed, shared exchanger drawn once, routed connections, single-row fallback
  Create/LiquidLoopTests.cs               # (8) boiler+chiller injection, closed loops, coil liquid In/Out wired, energy sources created and linked by name, template path skips injection
  Query/DutyTests.cs                      # (7) known enthalpy changes, scales with airflow, NaN inputs
  Query/BypassFactorTests.cs              # (5) ADP, clamp, zero denominator
  Query/SystemComponentTypeTests.cs       # (9) type classification incl. both humidifier subtypes, null/undefined
  Query/HeatRecoveryEfficiencyTests.cs    # (4) inverts factory percentages, sensible-only leaves latent NaN, clamped to [0,1]
  Query/HumidifierPropertiesTests.cs      # (10) spray/steam setpoint = end RH (not dry-bulb), effectiveness, water-flow mass balance, steam duty set unconditionally
  Query/FanPressureTests.cs               # (6) exact inverse of PickupTemperature, ΔP == η·SFP·1000 invariant, hand calculation, NaN for invalid/non-positive ΔT
  Json/RoundTripTests.cs                  # (6) ToJson→FromJson preserves setpoint/bypass factor/pressure/component count/exchanger efficiency/humidifier type
  Diagnostics/ConversionDiagnosticTests.cs # (15) every diagnostic code emitted at its trigger, severities, unique meanings, code format
  Integration/TasExportReadinessTests.cs   # (7) structural validation for Tas TPD export (connectors, no dangling, JSON round-trip)
  Integration/MVRE_ComparisonTests.cs      # (5) type-level/air-side structural comparison against reference MVRE.json (not count-for-count)
  Integration/TwinWheelVerifyTests.cs      # (1) TwinWheelExample.Verify() returns true with all-PASS lines

Grasshopper/SAM.Analytical.Grasshopper.Systems/
  Component/SAMSystemsCreateEnergyCentreByMollier.cs     # Mollier processes → SystemEnergyCentre
  Component/SAMSystemsCreatePlantRoomByMollier.cs        # Mollier processes → SystemPlantRoom
  Component/SAMSystemsCreateComponentByMollierProcess.cs # one process → component + duty + bypass
```

### Grasshopper components

| Component | Inputs | Outputs |
|-----------|--------|---------|
| `SAMSystems.CreateEnergyCentreByMollier` | supply processes, supply airflow, (extract processes, extract airflow), name | `SystemEnergyCentre` |
| `SAMSystems.CreatePlantRoomByMollier` | supply processes, supply airflow, (extract processes, extract airflow), name | `SystemPlantRoom` |
| `SAMSystems.CreateComponentByMollierProcess` | one process, design airflow | `SystemComponent`, duty [W], bypass factor |

All components live in the SAM ▸ Systems tab and carry full per-input/per-output
descriptions (units, intent, twin-wheel behaviour) in their tooltips.

### Twin-wheel (supply + extract)

`Create.SystemPlantRoom(supply, extract, supplyAirflow, extractAirflow)` wires **both**
the supply chain and the extract chain onto a single, shared `AirSystem` — one combined
`AirSystem` carries both sides of the AHU (supply and extract), not two separate
supply/extract systems. A `SystemExchanger` exposes two air paths (connection indexes 1
and 2); because `SystemPlantRoom.Connect` auto-selects the first *unconnected* connector
pair, the supply chain consumes air path 1 and the extract chain then consumes air path
2 of the **same** exchanger instance. Heat-recovery devices are reused across the two
chains paired in order, so a latent + sensible twin-wheel is modelled as one device
rather than two. When the supply and extract chains carry different counts of
heat-recovery processes, surplus extract-side processes fall back to their own exchanger
instead of sharing one (`MOLLIER-013`). The Grasshopper node exposes optional
`_extractMollierProcesses_` / `_extractAirflow_` inputs for this case.

With both air paths known, each shared exchanger's **sensible and latent effectiveness**
are derived (supply-side definition, fractions 0–1) via `Query.HeatRecoveryEfficiencies`
and written to the `SystemExchanger` (`ExchangerCalculationMethod`/`ExchangerType` = `Simple`).
In supply-only mode the exchanger is still created and flagged latent-capable when the
process shifts humidity, but efficiencies are left for the user to set (a single process
cannot determine effectiveness without the exhaust-side inlet).

### Two-row supply/extract layout

When a single plant room has one `AirSystem` serving both supply and extract chains,
`DisplaySystemEnergyCentre` detects the flow split by locating the `SystemSpace`
(room) component and lays out the schematic in two rows:

- **Supply row (y=0):** components before the room, left-to-right in airflow order.
- **Extract row (y=−0.8):** components after the room, **right-to-left** flow for
  schematic symmetry — the exhaust end sits under the fresh-air side so the loop reads
  naturally. The first extract component aligns vertically with the room on the supply row.
- Shared components (`SystemExchanger` with two air paths) are placed once on the
  supply row; their second air-path connectors route down to the extract row.
- Falls back to the original single-row layout if the two-row split cannot be resolved
  (supply-only, extract-only, multiple systems).

The row step of 0.8 symbol units matches the vertical spacing of the two air paths on
a shared twin-wheel exchanger symbol.

## Usage

```csharp
using SAM.Analytical.Systems.Mollier;

// processes: ordered IMollierProcess chain, e.g. mixing → heat-recovery → cooling → heating → fan
double designAirflow = 2.5; // m3/s
SystemEnergyCentre energyCentre = Create.SystemEnergyCentre(mollierGroup, designAirflow);
// energyCentre.ToJsonObject() -> JSON -> existing Tas TPD export
```

## Worked example (twin-wheel AHU)

Reconstructing a twin-wheel (latent + sensible recovery) air-handling unit as a Mollier
process chain and auto-generating the `SystemEnergyCentre`. The process factories are
extension methods on `MollierPoint`, and each process `End` feeds the next `Start`.
Both `SAM.Core.Mollier` and the bridge expose a `Create` class, so the bridge call is
fully qualified to avoid ambiguity.

```csharp
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SAM.Core.Mollier;     // MollierPoint, process types + factory extension methods
using SAM.Core.Systems;     // SystemEnergyCentre

const double pressure = 101325; // Pa (sea-level standard)

// Design states
MollierPoint outdoor = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(32, 40, pressure); // summer
MollierPoint room    = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(24, 50, pressure);

// Supply chain: heat recovery -> cooling -> reheat -> fan
HeatRecoveryProcess supplyHR = outdoor.HeatRecoveryProcess(room, 0.75, 0.65); // sensible 0.75, latent 0.65
CoolingProcess      cooling  = supplyHR.End.CoolingProcess(13, 0.85);         // off-coil 13 C, efficiency 0.85
HeatingProcess      reheat   = cooling.End.HeatingProcess(16);               // reheat to 16 C
FanProcess          fan      = reheat.End.FanProcess(0.8);                   // specific fan temperature rise

var supply = new List<IMollierProcess> { supplyHR, cooling, reheat, fan };

// Extract chain: same wheel, exhaust side (shared exchanger, air path 2), then extract fan
HeatRecoveryProcess extractHR  = room.HeatRecoveryProcess(outdoor, 0.75, 0.65, exhaust: true);
FanProcess          extractFan = extractHR.End.FanProcess(0.8);
var extract = new List<IMollierProcess> { extractHR, extractFan };

// Bridge: chains -> connected, simulation-ready SystemEnergyCentre
double supplyAirflow = 2.5;  // m3/s
double extractAirflow = 2.3; // m3/s
SystemEnergyCentre energyCentre = SAM.Analytical.Systems.Mollier.Create.SystemEnergyCentre(
    supply, extract, supplyAirflow, extractAirflow, "Twin-Wheel AHU");

// JSON round-trip; hand off to the existing Tas TPD export for annual simulation
JsonObject jsonObject = energyCentre.ToJsonObject();
```

The single `supplyHR`/`extractHR` map to one shared `SystemExchanger` (supply on air
path 1, extract on air path 2); the humidity-ratio shift flags it as latent-capable.

## Worked example (summer cooling-only AHU)

A simple supply-only plant room for summer sensible cooling of outdoor air to a supply
condition. No extract chain — the bridge creates a boundary junction at the intake.

```csharp
using System.Collections.Generic;
using SAM.Core.Mollier;
using SAM.Core.Systems;

const double pressure = 101325; // Pa

// Summer design: outdoor 32 °C / 40 % RH → cool to 13 °C off-coil (efficiency 0.85)
MollierPoint outdoor = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(32, 40, pressure);
CoolingProcess  cooling = outdoor.CoolingProcess(13, 0.85);
FanProcess      fan     = cooling.End.FanProcess(0.8);

var supply = new List<IMollierProcess> { cooling, fan };

// Bridge: single chain
SystemEnergyCentre energyCentre = SAM.Analytical.Systems.Mollier.Create.SystemEnergyCentre(
    supply, designAirflow: 2.0);
```

The bridge produces a `SystemPlantRoom` with a `SystemCoolingCoil` (setpoint 13 °C,
bypass factor from efficiency/ADP), a `SystemFan` (pressure derived from temperature rise),
and a `Junction Fresh Air` boundary. The `SystemEnergyCentre` wraps it ready for Tas TPD
export.

## Worked example (winter heating + humidification)

A supply-only plant room for winter pre-heat, steam humidification, and reheat:

```csharp
using System.Collections.Generic;
using SAM.Core.Mollier;
using SAM.Core.Systems;

const double pressure = 101325; // Pa

// Winter design: outdoor 0 °C / 80 % RH → pre-heat to 20 °C → humidify (+0.005 kg/kg) → reheat to 25 °C
MollierPoint outdoor  = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(0, 80, pressure);
HeatingProcess                preheat    = outdoor.HeatingProcess(20);
SteamHumidificationProcess    humidify  = preheat.End.SteamHumidificationProcess_ByHumidityRatioDifference(0.005);
HeatingProcess                reheat    = humidify.End.HeatingProcess(25);
FanProcess                    fan       = reheat.End.FanProcess(0.6);

var supply = new List<IMollierProcess> { preheat, humidify, reheat, fan };

// Bridge: single chain
SystemEnergyCentre energyCentre = SAM.Analytical.Systems.Mollier.Create.SystemEnergyCentre(
    supply, designAirflow: 1.8);
```

The bridge produces a `SystemPlantRoom` with two `SystemHeatingCoil` components
(pre-heat + reheat, each with their own setpoint from the end dry-bulb), a
`SystemSteamHumidifier` (setpoint and duty derived from the humidity-ratio shift),
and a `SystemFan`. A `Junction Fresh Air` boundary caps the intake.

## Limitations

- **Structural validation only, not simulated results.** `SAM.Analytical.Systems.Mollier.Tests`
  proves the bridge's *structure* — correct component types, connector wiring (every
  air component has In/Out connectors, no dangling connectors except boundaries), and
  JSON round-trip fidelity. It does not run a Tas simulation and compare energy or
  comfort *results*. The Tas TPD export itself lives in the separate `SAM_Tas` repository
  and is not exercised by this test suite; `Integration/TasExportReadinessTests.cs` only
  checks that the emitted `SystemEnergyCentre` has the shape the exporter expects.
- **The MVRE comparison is type-level/air-side structural, not count-for-count.**
  `Integration/MVRE_ComparisonTests.cs` loads the reference `MVRE.json` project and
  compares it against a bridge-generated twin-wheel example, but only checks things like
  "both sides have at least one of each expected air-side component type" and "both
  models have exactly one AirSystem / one shared exchanger" — it does not assert equal
  counts of every component type. MVRE is a complete, hand-authored energy centre with a
  full liquid plant (multiple boilers, a chiller, an air-source heat pump, pumps,
  controllers, sensors, a PV panel) that the bridge's auto-injected liquid loop (a single
  `SystemBoiler`/`SystemAirSourceChiller` pair, see above) does not attempt to reproduce;
  only the air side is compared.
- **Upstream `SAM_Mollier` `Create.FanProcess(MollierPoint, double)` defect.** The
  two-argument overload assigns the pickup *rise* as the End point's *absolute* dry-bulb
  temperature instead of adding it to the inlet temperature (a 16 °C inlet ends up around
  0.65 °C — a negative temperature rise, from which `Query.FanPressureRise` cannot
  recover a pressure). The four-argument sibling does not have this problem. This is a
  known upstream defect, to be fixed in `SAM_Mollier` separately. Until then, code that
  needs a physically valid fan pressure rise from a specific fan power should build the
  End state itself from `Query.PickupTemperature(MollierPoint, sfp)` added to the inlet
  temperature — see the private `FanProcessBySpecificFanPower` helper in
  `Example/TwinWheelExample.cs` for the pattern. With that workaround both example fans
  correctly yield 560 Pa (= 0.7 × 0.8 × 1000, i.e. `η · SFP · 1000`).

## Status / TODO

- ✅ Core bridge (`Create.SystemComponent` / `SystemPlantRoom` / `SystemEnergyCentre`,
  `Query.Duty` / `BypassFactor` / `SystemComponentType`).
- ✅ Three Grasshopper nodes in `SAM.Analytical.Grasshopper.Systems` (energy centre,
  plant room, single component), each with comprehensive component/input/output
  descriptions. The GH project references `SAM.Core.Mollier` and
  `SAM.Geometry.Grasshopper.Mollier` (for `GooMollierProcessParam`) from the SAM_Mollier build.
- ✅ Overload sourcing `designAirflow` from `AirHandlingUnitResult.SupplyAirFlow`.
- ✅ Supply + extract overload sharing one heat-recovery exchanger across both air
  paths (twin-wheel), exposed via optional GH inputs.
- ✅ Worked example + framework-free self-check `TwinWheelExample` (in the bridge
  assembly) and a `SAMSystems.MollierTwinWheelExample` GH node that builds the example
  and reports PASS/FAIL checks.
- ✅ Fan pressure derivation (`Query.FanPressureRise`) — `ΔP = η·ρ·cp·ΔT` from the ΔT
  pickup across a `FanProcess` (efficiency multiplies), using SAM.Core.Mollier's own
  inlet-state `cp` (not a hardcoded constant); the exact inverse of `Query.PickupTemperature`.
- ✅ Humidifier derivation (`Query.HumidifierProperties`) — end-state relative-humidity
  setpoint [%, 0–100] for both spray and steam; effectiveness and water-flow capacity for
  adiabatic (spray); duty (computed unconditionally, no isothermality gate) for steam.
- ✅ Liquid system auto-injection (`InjectLiquidSystems`) — boiler + chiller liquid
  loops wired to heating/cooling coils when no template is supplied, plus default
  `SystemEnergySource`/`ElectricalEnergySource` entries ("Natural Gas", "Grid Supplied
  Electricity") added at the energy-centre level.
- ✅ Two-row supply/extract auto-layout in `DisplaySystemEnergyCentre` — supply row
  (top, left-to-right), extract row (bottom, right-to-left), shared exchangers on
  supply row with downward-routed second air paths.
- ✅ Structured diagnostic model (`ConversionDiagnostic` / `DiagnosticCodes`) —
  MOLLIER‑001…014 codes surfaced through all bridge `Create` methods and Grasshopper
  nodes.
- ✅ xUnit test project (`SAM.Analytical.Systems.Mollier.Tests`) — 123 tests across 17
  files covering component mapping, duty/bypass/fan-pressure/humidifier/heat-recovery
  queries, chain wiring, supply+extract, twin-wheel topology, two-row layout, liquid-loop
  injection, JSON round-trip, diagnostics, Tas export readiness, and MVRE comparison.
- ⏳ CESBP-2025 twin-wheel validation against a manually authored Tas model — paper
  deliverable; Tas export lives in the separate `SAM_Tas` repo.
