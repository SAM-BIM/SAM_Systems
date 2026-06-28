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
| `CoolingProcess`           | `SystemCoolingCoil`   | `Setpoint` = End dry-bulb; `BypassFactor` from Efficiency/ADP; `Duty` = m·Δh |
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
```

## Usage

```csharp
using SAM.Analytical.Systems.Mollier;

// processes: ordered IMollierProcess chain, e.g. mixing → heat-recovery → cooling → heating → fan
double designAirflow = 2.5; // m3/s
SystemEnergyCentre energyCentre = Create.SystemEnergyCentre(mollierGroup, designAirflow);
// energyCentre.ToJsonObject() -> JSON -> existing Tas TPD export
```

## Status / TODO

- Grasshopper node `SAMSystemsCreateEnergyCentreByMollier` (in
  `SAM.Analytical.Grasshopper.Systems`) — pending.
- Optional overload sourcing `designAirflow` from `AirHandlingUnitResult` /
  `AnalyticalModel` — pending.
- CESBP-2025 twin-wheel validation against a manually authored Tas model — pending
  (paper deliverable; Tas export lives in the separate `SAM_Tas` repo).
- This container has no local build output for the base `SAM` / `SAM_Mollier`
  repos, so the assembly has not yet been compiled here; build per the order above.
