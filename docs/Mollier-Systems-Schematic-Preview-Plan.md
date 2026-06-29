# Mollier → HVAC System bridge: schematic preview plan

## Problem

Running `SAMSystems.MollierTwinWheelExample` (or the underlying
`SAM.Analytical.Systems.Mollier.TwinWheelExample.Create` /
`Verify`) produces a valid `SystemEnergyCentre`, the self-check passes, but the
Grasshopper viewport shows **no preview**. Loading the bundled example
`files/resources/Analytical/Systems/SystemEnergyCentre/MVRE.json` via `FromJson`
**does** preview.

## Root cause (confirmed in code)

Preview is driven entirely by **display geometry**. In
`GooSystemObject.DrawViewportWires`
(`Grasshopper/SAM.Analytical.Grasshopper.Systems/Classes/GooSystemObject.cs`):

```csharp
if (Value is IDisplaySystemObject)
    sAMGeometry2DObject = Query.SAMGeometry2Dobject((IDisplaySystemObject)Value);
if (sAMGeometry2DObject != null)
    DrawViewportWires(...);
```

Only objects that are `IDisplaySystemObject` **and** carry 2D geometry are drawn.

- **`MVRE.json`** is a fully laid-out schematic authored in SAM_UI: its members
  are `DisplaySystemAirJunction`, `DisplaySystemExchanger`, `DisplaySystemFan`, …,
  each holding a `SystemGeometryInstance` (symbol + location) plus
  `DisplaySystemConnection` polylines. → previews.
- **The bridge** (`SAM.Analytical.Systems.Mollier/Create/SupplyExtract.cs`) only
  builds **logical** components — `SystemExchanger`, `SystemCoolingCoil`,
  `SystemHeatingCoil`, `SystemFan`, `SystemConnection` — which are *not*
  `IDisplaySystemObject` and have no geometry. → nothing to draw.

This is not a regression: the bridge never produced display geometry. `Verify`
passes because it only inspects the logical model. To preview, the output must be
converted to a `DisplaySystemEnergyCentre` with auto-laid-out symbols and
connections.

## Symbol library (resolved)

A complete symbol library already exists and will be used as the default — **no
extraction tool needed** (supersedes the earlier plan's Step 1):

```
files/resources/Analytical/Systems/SAM_DisplaySystemManager.JSON
```

It deserialises to a `DisplaySystemManager` whose `SystemGeometrySymbolManager`
maps ~70 component types → `SystemGeometrySymbol` (each symbol carries its
`DisplaySystemConnector` locations, which is what connection routing needs).

### Coverage vs. what the bridge emits

The bridge maps Mollier processes to component types (`Create.SystemComponent`).
`SystemHumidifier` is being made **abstract** (see below), so the
`HumidificationProcess` branch must emit a concrete subtype — each of which
already has a symbol:

| Mollier process                 | Component (target)        | In symbol library? |
|---------------------------------|---------------------------|--------------------|
| `FanProcess`                    | `SystemFan`               | ✅ yes |
| `HeatingProcess`                | `SystemHeatingCoil`       | ✅ yes |
| `CoolingProcess`                | `SystemCoolingCoil`       | ✅ yes |
| `HeatRecoveryProcess`           | `SystemExchanger`         | ✅ yes |
| `MixingProcess`                 | `SystemAirJunction`       | ✅ yes |
| `AdiabaticHumidificationProcess`| `SystemSprayHumidifier`   | ✅ yes |
| `IsothermalHumidificationProcess` / `SteamHumidificationProcess` | `SystemSteamHumidifier` | ✅ yes |
| (`SystemDirectEvaporativeCooler`) | — adiabatic alternative | ✅ yes |

**With the abstract refactor + subtype mapping, coverage is 6/6 (no missing
symbols).** The twin-wheel example uses only the first five, so it previews
fully regardless.

### Refactor: make `SystemHumidifier` abstract

`SystemHumidifier` (`SAM.Analytical.Systems/Classes/SystemComponent/SystemHumidifier.cs`)
is currently a **concrete** `SystemComponent, IAirSystemComponent`. It should be
the abstract base for its three concrete subtypes, which all already exist with
their own symbols and `Duplicate` overrides:

- `SystemDirectEvaporativeCooler : SystemHumidifier, IAirSystemComponent`
- `SystemSprayHumidifier : SystemHumidifier`
- `SystemSteamHumidifier : SystemHumidifier`

Ripple of making the base abstract:

1. **`SystemHumidifier.cs`** — `public abstract class`; make its constructors
   `protected`; remove the base `Duplicate` override (line 90) that does
   `new SystemHumidifier(...)` (or mark it `abstract`). The three subtypes
   already override `Duplicate`, so this is safe.
2. **Bridge `Create.SystemComponent`** (`SystemComponent.cs:120-123`) — replace
   `new SystemHumidifier("Humidifier")` with the concrete subtype chosen by the
   `HumidificationProcess` subtype (table above). `AdiabaticHumidificationProcess`
   → `SystemSprayHumidifier`; `IsothermalHumidificationProcess` (incl.
   `SteamHumidificationProcess`) → `SystemSteamHumidifier`.
3. **`Query.SystemComponentType`** (`SystemComponentType.cs:47-50`) — currently
   returns `typeof(SystemHumidifier)`; return the matching concrete type instead,
   so the symbol-manager lookup resolves to a real (non-abstract) symbol.
4. **`Create.DisplayObject<T>`** — already has branches for all three concrete
   subtypes and **no** base-`SystemHumidifier` branch, so it is already correct
   for an abstract base; no change needed.

No JSON resource change is required: all three concrete humidifier symbols are
already present in `SAM_DisplaySystemManager.JSON`.

## Plan

### Step 1 — Default symbol manager loader
- Add `Query.DefaultDisplaySystemManager()` in `SAM.Analytical.Systems` that
  loads and caches `SAM_DisplaySystemManager.JSON` and returns a
  `DisplaySystemManager`. Resolve the resource path the same way the existing
  `SystemEnergyCentre` example resources are resolved.

### Step 2 — Auto-layout converter
New file `SAM.Analytical.Systems/Create/DisplaySystemEnergyCentre.cs`:
- `Create.DisplaySystemPlantRoom(this SystemPlantRoom, DisplaySystemManager)` and
  `Create.DisplaySystemEnergyCentre(this SystemEnergyCentre, DisplaySystemManager = null)`
  (null → default library from Step 1).
- Algorithm:
  1. For each `AirSystem`, find the chain head and walk
     `GetOrderedSystemComponents(head, system, Direction.Out)` for flow order.
  2. Assign each component a `Point2D` — one **row per air system** (supply at
     `y = 0`, extract at `y = -Δ`), `x = index · step`. A shared twin-wheel
     `SystemExchanger` (present on both chains) is placed once, on a column
     spanning both rows.
  3. Convert each logical component → display object via the existing
     `Create.DisplayObject<IDisplaySystemObject>(component, location, manager)`;
     add to a new `DisplaySystemEnergyCentre` / `DisplaySystemPlantRoom`.
  4. Re-create each `SystemConnection` as a `DisplaySystemConnection`: read the two
     endpoints' **world** connector points via
     `SystemGeometryInstance.GetPoint2D(systemType, connectionIndex, direction)`
     and build an orthogonal polyline, preserving the original connection's
     component references and indexes.
- Components whose type has no symbol are **skipped and reported** (return a
  `List<string>` log) rather than throwing — graceful degradation for the
  `SystemHumidifier` gap above.

### Step 3 — Implement display connection wiring
- Replace `throw new NotImplementedException()` in
  `DisplaySystemPlantRoom.CreateSystemConnection`
  (`SAM.Geometry.Systems/Classes/DisplaySystemPlantRoom.cs:41`) with a real
  implementation that builds a `DisplaySystemConnection` polyline between the two
  display components' connector world points. This is the correct home for the
  routing logic and makes `Connect(...)` work for display plant rooms generally.

### Step 4 — Expose it
- New Grasshopper component **`SAMSystems.CreateDisplaySystemEnergyCentre`**
  (input: `SystemEnergyCentre`, optional `DisplaySystemManager`; outputs:
  `DisplaySystemEnergyCentre`, `report`) so logical and schematic stay separable
  and any bridge output can be made previewable.
- Add an optional `_display_` boolean (default `true`) plus an extra
  `displaySystemEnergyCentre` output to `SAMSystems.MollierTwinWheelExample`, so
  the example previews out-of-the-box **without** changing the logical
  `Create`/`Verify` contract the smoke test depends on.

### Step 5 — Verify
- Extend `TwinWheelExample.Verify` with display checks: converter yields a
  `DisplaySystemEnergyCentre`; every component became an `IDisplaySystemObject`;
  every component has a non-null `Query.SAMGeometry2Dobject`; connection count is
  preserved.
- Manual: drop the example in Grasshopper → the schematic now draws; confirm
  `Bake By Type` works.
- SPDX headers on every new `.cs`; CI green; address Codex review.

## Touch list

| Area | File | Change |
|------|------|--------|
| `SAM.Analytical.Systems` | `Create/DisplaySystemEnergyCentre.cs` | **new** converter |
| `SAM.Analytical.Systems` | `Query/DefaultDisplaySystemManager.cs` | **new** loader/cache |
| `SAM.Analytical.Systems` | `Classes/SystemComponent/SystemHumidifier.cs` | make **abstract**; protected ctors; drop base `Duplicate` |
| `SAM.Analytical.Systems.Mollier` | `Create/SystemComponent.cs` | map `HumidificationProcess` subtype → concrete humidifier |
| `SAM.Analytical.Systems.Mollier` | `Query/SystemComponentType.cs` | return concrete humidifier type (not abstract base) |
| `SAM.Geometry.Systems` | `Classes/DisplaySystemPlantRoom.cs` | implement `CreateSystemConnection` |
| `SAM.Analytical.Systems.Mollier` | `Example/TwinWheelExample.cs` | optional display output + checks |
| Grasshopper | `Component/SAMSystemsCreateDisplaySystemEnergyCentre.cs` | **new** node |
| Grasshopper | `Component/SAMSystemsMollierTwinWheelExample.cs` | `_display_` input + display output |

`Create/DisplayObject.cs` needs **no** change — it already handles all three
concrete humidifier subtypes and no base type. No resource change either: all
three humidifier symbols already exist in `SAM_DisplaySystemManager.JSON`.

## Risks

- **`SystemHumidifier` → abstract** — verify nothing else instantiates the base
  type or relies on it being concrete (deserialization registration, any
  reflection/`Activator` paths). Only known callers are its own `Duplicate` and
  the bridge factory, both updated above.
- **Twin-wheel shared exchanger** placement spanning two rows is the trickiest
  geometry: both air-path connectors must route cleanly off the single device.
- **Connector metadata** on auto-built display objects must carry the right
  `SystemType` / `ConnectionIndex` / `Direction` for `GetPoint2D` lookups to
  resolve; if a symbol's connectors don't match the bridge's connection indexes,
  routing falls back to the component centroid.

## Suggested order

Step 1 → Step 3 → Step 2 → Step 4 → Step 5. (Loader and the connection-wiring
primitive first, since the converter depends on both.)

---
Generated by Michal Dengusiak and CodeClaude
