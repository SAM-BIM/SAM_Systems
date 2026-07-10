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
| `FanProcess`               | `SystemFan`           | `Pressure` = FanPressureRise (ΔP from ΔT); `OverallEfficiency` = 0.7; `DesignFlowRate` = design airflow |
| `HeatingProcess`           | `SystemHeatingCoil`   | `Setpoint` = End dry-bulb; `Duty` = m·Δh |
| `CoolingProcess`           | `SystemCoolingCoil`   | `Setpoint` = End dry-bulb; `BypassFactor` from Efficiency/ADP; `MinimumOffcoil` = ADP dry-bulb; `Duty` = m·Δh |
| `HeatRecoveryProcess`      | `SystemExchanger`     | `Setpoint` = End dry-bulb; latent flagged when humidity ratio shifts (twin-wheel) |
| `HumidificationProcess`    | `SystemHumidifier`    | Adiabatic → `SystemSprayHumidifier` (setpoint, effectiveness, water-flow capacity); Steam → `SystemSteamHumidifier` (setpoint, duty) |
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

Fan pressure rise [Pa] is derived from the temperature pickup across a `FanProcess`:

```
ΔT = End.DryBulbTemperature − Start.DryBulbTemperature
ΔP = ρ_inlet · cp_air · ΔT / η_fan
```

where `cp_air = 1010 J/kg·K`, `ρ_inlet` is the moist-air density at the process inlet,
and the default fan total efficiency `η_fan` is 0.7. Returns `NaN` when the start/end
are invalid or `ΔT ≤ 0`.

`SystemComponent` for `FanProcess` calls `FanPressureRise`; when the result is valid,
sets `SystemFan.Pressure`, `SystemFan.OverallEfficiency` (= 0.7), and
`SystemFan.DesignFlowRate` from the design airflow.

### Humidifier derivation (`Query.HumidifierProperties`)

Humidification processes derive four properties for the control model:

- **Setpoint** — the end-point dry-bulb temperature for steam humidifiers,
  or the target humidity ratio for adiabatic (spray) humidifiers.
- **Effectiveness** — for adiabatic humidifiers, the ratio of actual humidity-ratio
  rise to the maximum possible (saturation).
- **Duty** [W] — humidification thermal load, `|ṁ · (h_end − h_start)|`.
- **Water-flow capacity** [kg/s] — mass rate of water evaporated, `ṁ · Δω`.

Adiabatic humidification processes map to `SystemSprayHumidifier`; isothermal (steam)
map to `SystemSteamHumidifier`. All values return `NaN` when the process or endpoints
are invalid.

### Liquid system auto-injection (`InjectLiquidSystems`)

When no energy-centre template is provided, the bridge auto-generates minimal liquid
systems so the output is simulation-ready without manual plant definition:

- **Heating:** if any `SystemHeatingCoil` exists in the plant room, a `LiquidSystem`
  ("Heating Hot Water") and `SystemBoiler` are created and daisy-chain wired to all
  heating-coil liquid connectors (boiler→coil→…→coil→boiler).
- **Cooling:** if any `SystemCoolingCoil` exists, a `LiquidSystem` ("Chilled Water")
  and `SystemAirSourceChiller` are created and wired to all cooling-coil liquid connectors.

`InjectLiquidSystems` is called from every `SystemEnergyCentre` entry point where no
template is supplied. When injection cannot be performed (e.g. missing assembly), a
`MOLLIER‑014` (`LiquidInjectionFailed`) warning diagnostic is emitted.

### Diagnostic model

Every bridge `Create` method with a diagnostic `out` parameter produces a
`List<ConversionDiagnostic>` with structured `Severity` / `Code` / `Message` entries:

| Code | Severity | Meaning |
|------|----------|---------|
| `MOLLIER-001` | Warning | Unsupported process type (mapped to null) |
| `MOLLIER-004` | Warning | Design airflow is NaN; duties will not be set |
| `MOLLIER-005` | Error | Process chain is null |
| `MOLLIER-012` | Error | Process chain is empty; no components were created |
| `MOLLIER-014` | Warning | Could not inject heating/cooling liquid system |

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
  Classes/ConversionResult.cs             # bridge output wrapper with diagnostics

SAM_Systems/SAM.Analytical.Systems.Mollier.Tests/
  SAM.Analytical.Systems.Mollier.Tests.csproj   # net8.0; xunit 2.9.2; references bridge + Mollier/base DLLs
  Create/SystemComponentTests.cs          # per-process mapping + fan-before-heating + null/unsupported
  Create/SystemPlantRoomTests.cs          # single-chain wiring, component count, connector direction
  Create/SupplyExtractTests.cs            # twin-wheel shared exchanger, room creation
  Query/DutyTests.cs                      # known enthalpy changes, NaN inputs
  Query/BypassFactorTests.cs              # ADP, clamp, zero denominator
  Query/SystemComponentTypeTests.cs       # type classification
  Json/RoundTripTests.cs                  # ToJson→FromJson preserves component data
  Diagnostics/ConversionDiagnosticTests.cs # all diagnostic codes emitted correctly
  Integration/TasExportReadinessTests.cs   # structural validation for Tas TPD export
  Integration/MVRE_ComparisonTests.cs      # comparison against reference MVRE.json

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

`Create.SystemPlantRoom(supply, extract, supplyAirflow, extractAirflow)` wires the
supply chain onto a supply `AirSystem` and the extract chain onto an extract
`AirSystem`. A `SystemExchanger` exposes two air paths (connection indexes 1 and 2);
because `SystemPlantRoom.Connect` auto-selects the first *unconnected* connector pair,
the supply chain consumes air path 1 and the extract chain then consumes air path 2 of
the **same** exchanger instance. Heat-recovery devices are reused across the two chains
paired in order, so a latent + sensible twin-wheel is modelled as one device rather than
two. The Grasshopper node exposes optional `_extractMollierProcesses_` / `_extractAirflow_`
inputs for this case.

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
- ✅ Fan pressure derivation (`Query.FanPressureRise`) — ΔP from ΔT pickup across a
  `FanProcess`, with density, cp_air, and fan efficiency.
- ✅ Humidifier derivation (`Query.HumidifierProperties`) — setpoint, effectiveness,
  duty, and water-flow capacity for adiabatic (spray) and isothermal (steam) processes.
- ✅ Liquid system auto-injection (`InjectLiquidSystems`) — boiler + chiller liquid
  loops wired to heating/cooling coils when no template is supplied.
- ✅ Two-row supply/extract auto-layout in `DisplaySystemEnergyCentre` — supply row
  (top, left-to-right), extract row (bottom, right-to-left), shared exchangers on
  supply row with downward-routed second air paths.
- ✅ Structured diagnostic model (`ConversionDiagnostic` / `DiagnosticCodes`) —
  MOLLIER‑001…014 codes surfaced through all bridge `Create` methods and Grasshopper
  nodes.
- ✅ xUnit test project (`SAM.Analytical.Systems.Mollier.Tests`) — 74 tests covering
  component mapping, duty/bypass/fan-pressure queries, chain wiring, supply+extract,
  JSON round-trip, diagnostics, Tas export readiness, and MVRE comparison.
- ⏳ CESBP-2025 twin-wheel validation against a manually authored Tas model — paper
  deliverable; Tas export lives in the separate `SAM_Tas` repo.
