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
| `FanProcess`               | `SystemFan`           | — (duty implied by pressure/efficiency) |
| `HeatingProcess`           | `SystemHeatingCoil`   | `Setpoint` = End dry-bulb; `Duty` = m·Δh |
| `CoolingProcess`           | `SystemCoolingCoil`   | `Setpoint` = End dry-bulb; `BypassFactor` from Efficiency/ADP; `MinimumOffcoil` = ADP dry-bulb; `Duty` = m·Δh |
| `HeatRecoveryProcess`      | `SystemExchanger`     | `Setpoint` = End dry-bulb; latent flagged when humidity ratio shifts (twin-wheel) |
| `HumidificationProcess`    | `SystemHumidifier`    | — |
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
  Create/SystemComponent.cs               # process → ISystemComponent (duty/setpoint/bypass)
  Create/SystemPlantRoom.cs               # ordered chain → connected plant room
  Create/SystemEnergyCentre.cs            # plant room → energy centre (top-level entry)
  Create/SystemEnergyCentreByResult.cs    # overload sourcing design airflow from AirHandlingUnitResult
  Create/SupplyExtract.cs                 # supply + extract chains; shared twin-wheel exchanger
  Query/HeatRecoveryEfficiency.cs         # supply-side sensible/latent effectiveness from both paths

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

## Status / TODO

- ✅ Core bridge (`Create.SystemComponent` / `SystemPlantRoom` / `SystemEnergyCentre`,
  `Query.Duty` / `BypassFactor` / `SystemComponentType`).
- ✅ Three Grasshopper nodes in `SAM.Analytical.Grasshopper.Systems` (energy centre,
  plant room, single component), each with comprehensive component/input/output
  descriptions. The GH project references `SAM.Core.Mollier` and
  `SAM.Geometry.Grasshopper.Mollier` (for `GooMollierProcessParam`) from the SAM_Mollier build.
- ✅ Overload sourcing `designAirflow` from `AirHandlingUnitResult.SupplyAirFlow`.
- ✅ Supply + extract overload sharing one heat-recovery exchanger across both air
  paths (twin-wheel), exposed via optional GH inputs. Pairing is order-based — review
  if a chain has multiple heat-recovery devices.
- ✅ Worked example + framework-free self-check `TwinWheelExample` (in the bridge
  assembly) and a `SAMSystems.MollierTwinWheelExample` GH node that builds the example
  and reports PASS/FAIL checks (component creation, single plant room, JSON round-trip,
  cooling duty and bypass factor).
- ⏳ CESBP-2025 twin-wheel validation against a manually authored Tas model — paper
  deliverable; Tas export lives in the separate `SAM_Tas` repo.
- ⚠️ Not yet compiled: this container has no .NET toolchain and no base `SAM` /
  `SAM_Mollier` build output. Build locally in the order above (`SAM` + `SAM_Mollier`
  before `SAM_Systems`) to validate.
