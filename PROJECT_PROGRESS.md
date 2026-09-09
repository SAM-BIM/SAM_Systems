# Project Progress

## Branch
`part-o/iteration3-systems-materialisation`, branched from `sow/2026-Q3` at **`9e1cd06`**.

Baseline measured before any edit, on that tip:

```text
SAM.Analytical.Systems.Tests             90/90
SAM.Analytical.Systems.Mollier.Tests   123/123
dotnet build SAM_Systems.sln -c Release   0 errors
```

Sibling `SAM` at `sow/2026-Q3` **`413215c`**, rebuilt so this repository references current binaries.
**No `SAM` production file is touched by this branch.**

Superseded branch note, retained for context:
`feature/parto-catalogue-xbc15`, branched from `sow/2026-Q3` at **`fea5055`** (the merge of PR #18).

**Stands alone.** It needs no change in `SAM`, `SAM_UI`, `SAM_Tas` or any other dependency: no production
code changed in this repository at all. Baseline it was branched from, measured before any edit:

```text
SAM.Analytical.Systems.Tests            83/83
```

Companion, test-only branches exist in `SAM` (`test/parto-real-selection-ladder`) and `SAM_UI`
(`test/parto-xbc15-capacity-ceiling`). None is a code dependency in either direction; each can merge
independently.

Everything below the *Latest* entry is superseded history retained for context.

## Last updated
2026-09-09 - **PR1 of Part O Iteration 3 (SAM #111): generic deterministic mechanical-ventilation
materialisation.** See *Latest* immediately below.

## Latest (2026-09-09): PR1 - mechanical-ventilation materialisation

`Create.MechanicalVentilation(this AdjacencyCluster, SystemEnergyCentre template, MechanicalVentilationSettings, IEnumerable<Space>)`
turns the ventilation an analytical design already states into an isolated, deterministic
`SystemEnergyCentre` built on a supplied topology template.

**The shape.** One energy centre -> **one** materialised plant room -> **N air systems, one per physical
analytical air handling unit**, each with its own deterministically re-keyed copy of the template's
air-system subgraph. The non-air plant - the electrical, hot water, heating, cooling and refrigerant
collections and the liquid side - is **shared, not copied per unit**. `SystemEnergyCentre.Add(plantRoom)`
is called **exactly once** for the whole materialisation: the energy centre keys plant rooms by guid, so
a per-unit `Add` would silently replace the previous one and lose an entire air system.

**Design airflow is the only airflow authority.** The design duty of a direction is the **sum** of that
space's design terminals of that direction - a bedroom subdivided into 7 + 8 + 15 l/s has one 30 l/s
supply leg. A Part F requirement, a selected product's capacity and an operating airflow are three other
numbers and none of them is read; two tests prove it structurally, by materialising with and without them
and comparing the graphs byte for byte. The new generic
`SystemConnectionParameter.DesignFlowRate` [l/s] carries design airflow and is never overloaded.

**Membership and bindings are guids and relations, never names.** The one name read anywhere is
`VentilationSystemParameter.SupplyUnitName`/`ExhaustUnitName`, resolved **once** through an index to
locate the `AirHandlingUnit` object - the model's own pre-existing binding, which SAM's own accessor
records as debt. That resolution is **stricter** than SAM's: a missing name, an unresolved name, a name
two units answer to, and supply and exhaust naming different units all **refuse**, where the shipped
`Find` silently takes the first match. After resolution `AirHandlingUnit.Guid` is the sole authority.

**Fail closed, with no partial-success graph.** `MechanicalVentilationMaterialisation.SystemEnergyCentre`
is null whenever `Refusals` is non-empty, enforced in the constructor rather than by convention.

**Deterministic identity.** `Query.MechanicalVentilationGuid` reuses `OverheatingScenario.Derive`'s
convention verbatim - namespace guid, schema, length-prefixed NFC components, SHA-256, version 8 - with
its own namespace and `IdentitySchema = "MechanicalVentilationMaterialisation:v1"`, pinned by a test. No
`Guid.NewGuid()` for any generated identity, and a collision map refuses rather than letting
`RelationCluster.TryAddObject` silently replace an entry. The **one stated exception** is the plant room,
which **inherits** the template's guid: re-keying it through `new SystemPlantRoom(guid, …)` would
silently downgrade a `DisplaySystemPlantRoom` to a plain one and lose its schematic routing, and it has
no analytical source object so it is outside the lineage contract. `Modify/Merge.cs:47` re-guids a plant
room on the way in, so this cannot lose one downstream.

**Lineage is one row per contributing source, not one per materialised object.** All three terminals of a
subdivided room bind to the one connection they produced (N:1), because a caller has to be able to trace
a duty back to the terminals that made it. Completeness is asserted both ways.

**Two narrow defects fixed, both in this repository:**

* `YearlySchedule(YearlySchedule)` copied **24** of 8760 values. Live in `Modify/Merge.cs:289` and both
  `ScheduleModifier` constructors via `Core.Query.Clone`, so an annual operating schedule of constant 1.0
  silently became one day of operation and every stage downstream reported success. Test written first
  and **observed failing** on current production source (hour 24 read 0.0 instead of 25) across the copy
  constructor, `Core.Query.Clone`, `ScheduleModifier` and `Modify.Merge`, with the JSON round trip
  passing as the control. Fixed to the full array length; all five pass.
* `SystemSpace`'s two copy constructors **shared** `FlowRate`/`FreshAir` where every sibling component
  clones them, and `SizedFlowValue.Value` has a public setter that `Modify.UpdateSpaceAirflows` writes in
  place. Hardened to `?.Clone()`, gated on a green full regression as the plan required - **kept**, since
  145/145 and 123/123 stayed green. The materialisation does not depend on it: it assigns brand-new flow
  values to every room it builds.

**Scaling.** Structural rather than wall-clock, following this repository's own recorded decision to
delete its stopwatch test. At 100 / 1 000 / 5 000 spaces (5 rooms per dwelling, 20 dwellings per unit, so
the 5 000-space run exercises 50 air systems, 4 000 terminals and 4 000 movements) every object count is
an exact closed-form function of the input; the shared collection count does not grow with unit count;
allocation grows linearly (ratio asserted under 8, linear is about 5 and quadratic about 25); and the
5 000-space model with every space and terminal name blanked produces the **same identities**, which a
name-scanning implementation could not.

**Not in PR1**, and deliberately: any `SAM` production change; SAM_Tas; SAM_UI; TAS/TPD/TBD/TSD; the
no-IZAM source; `ZoneTemperature`; the ResultantTemperature bridge; TM59; manufacturer behaviour; and any
modification of the shipped `MV.json`, which is a topology source and is proved byte- and
timestamp-unchanged by a test.

## Superseded (2026-09-07) - the catalogue's second real product
2026-09-07 - The catalogue holds a second real product: **Nuaire XBOXER XBC15, 190/190 l/s**. Data and tests
only; no production code changed. The 150 -> 190 l/s selection boundary is now a manufacturer fact rather
than a fixture, and automatic selection at every duty <= 150 l/s is provably unchanged.

## XBC15 CLOSEOUT COMPLETE - READY FOR FINAL EQUIPMENT-SELECTION UX

Merged into `sow/2026-Q3` on 2026-09-07, in dependency order, after native Iteration 2 acceptance PASSED:

| Repository | PR | Merge commit | `sow/2026-Q3` head |
| --- | --- | --- | --- |
| `SAM_Systems` | #19 | `61cedcf52f78ef4b029a5f6ef9980759ab329b9e` | `61cedcf52f78ef4b029a5f6ef9980759ab329b9e` |
| `SAM` | #109 | `a08c9df7c1a1513a61e17618539e037c8e6bf75c` | `a08c9df7c1a1513a61e17618539e037c8e6bf75c` |
| `SAM_UI` | #97 | `ec0d0f624c066719e219bbf2e5e30e392aaf07f0` | `ec0d0f624c066719e219bbf2e5e30e392aaf07f0` |
| `SAM_Tas` | - | untouched | `ec7f50543e123f8a734b6e27b2b16c0cf1f1edde` |

Post-merge verification on those exact heads, rebuilt in dependency order `SAM` -> `SAM_Systems` -> `SAM_UI`:

```text
SAM.sln / SAM_Systems.sln / SAM_UI.sln Release   0 errors each
SAM.Tests                                        2024 / 2024
SAM.Analytical.Systems.Tests                       90 /   90
SAM.Analytical.UI.WPF.Tests                       645 /  645
git diff --check (all four repositories)         clean
```

The catalogue deploys with both products: the installed copy at
`%APPDATA%\SAM\resources\Analytical\Systems\VentilationUnit\VentilationUnitCatalogue.JSON` is
byte-identical to the merged repository copy and carries `MRXBOXAB-ECO5-AECV` (150/150 l/s) and `XBC15`
(190/190 l/s). That is also what proves the "skip where the catalogue is not installed" guard in
`PartOPresentationTests` does not trip on this machine, so the two-product assertions really ran.

**Nothing is FROZEN.** Iterations 1a / 1b / 2 / 2B are deliberately not marked frozen: the freeze gate is
the equipment-selection UX task that follows - allowed product pool, convert-to-manual, per-dwelling manual
assignment - which adds the very product-list surface the native Iteration 2 check could not exercise.

## Latest (2026-09-07): the second real product - Nuaire XBOXER XBC15

**Status: implemented, tested, and natively accepted.**

### Native acceptance (recorded 2026-09-07)

**Iteration 2 PASSED**, observed in SAM_UI on the combined PR heads, with no TAS rerun. All three dwellings
still automatically select `Nuaire MRXBOXAB-ECO5-AECV`; design duties remain 30/30, 63/63 and 63/63 l/s;
selected equipment capacity remains 150/150 l/s; headroom remains 120/120, 87/87 and 87/87 l/s; Approved
Document F and Design airflow are still shown as separate quantities and remain consistent with the
previously accepted model; and no equipment-selection operation changed a design duty.

The "catalogue reports two selectable products" assertion is **NOT APPLICABLE to the current interface**:
SAM_UI does not expose the catalogue product list at all - `PartOIterationWindow` offers only the
`SelectVentilationUnit` on/off checkbox - so there is no surface on which a product count could be read.
The two-product fact is pinned in CI instead. The missing surface is an explicit usability gap carried into
the equipment-selection UX task that follows, not a defect of this closeout.

**Nothing is FROZEN.** This closeout is recorded as
**XBC15 CLOSEOUT COMPLETE - READY FOR FINAL EQUIPMENT-SELECTION UX**.

### What was added, and what it is

One entry, appended to `files/resources/Analytical/Systems/VentilationUnit/VentilationUnitCatalogue.JSON`:

| Field | Value |
| --- | --- |
| Identity | `Nuaire` / `XBC15` / *(no reference - the range has no cooling module)* |
| `MaximumSupplyFlowRate_Lps` / `MaximumExtractFlowRate_Lps` | **190** / **190** |
| `Rank` | 20 |
| `PerformanceTable` | 1 axis, `ExternalStaticPressure` 0/50/100/200/300/400/500/600/700 Pa; outputs `AirFlowRate` in **m3/s** and `SpecificFanPower` in **W/l/s** |
| `FlowFractionByControlTemperature` | **absent** - not `null` |

Source: Nuaire, XBOXER XBC Heat Recovery Solutions catalogue, document 029 2085 82002 (2024 edition),
page 18 - XBC15 horizontal heat exchange units, unit performance.

**THE CAPACITY IS THE FREE-AIR END OF THE PUBLISHED FAN ROW.** The catalogue tabulates a Specific Fan Power
table (both fans operating) whose 100% fan-speed row runs against external static pressure; its 0 Pa end is
0.19 m3/s = 190 l/s. That is the flow the unit delivers against no external resistance, which is what a
capacity is. Same reasoning as the MRXBOX entry, except this source states it numerically instead of drawing
it on a chart, so nothing was digitised.

**AND THE DUTY POINTS ARE NOT.** The catalogue's own example selection, 0.1 m3/s @ 100 Pa (SFP 1.1,
17 dBA @ 3 m), is a duty point against duct resistance. The 75% fan-speed row's 0.14 m3/s at 0 Pa is an
operating state of the same physical unit. Neither is a product and neither is a rating.
`TheXBC15Capacity_IsTheFreeAirEndOfItsPublishedFanRow` asserts the capacity equals the table's own 0 Pa entry
and is neither of those two numbers.

**Note the asymmetry with the MRXBOX test, which is deliberate.** For the MRXBOX the capacity had to differ
from everything on its performance table, because that table is a grid of cooling duty points and its
largest airflow (120 l/s) is not a fan limit -
`TheNuaireCapacity_IsResolvedFromTheFanCurveNotTheTablesLargestAirflow` still asserts exactly that. For the
XBC15 the capacity *is* the table's 0 Pa entry, because that table is the fan row. Writing a
"capacity != table maximum" assertion here would have been a false invariant.

### Raw data, in the source's own units

`AirFlowRate` stays in **m3/s** and `SpecificFanPower` in **W/l/s**. Nothing is converted on the way in,
because a converted figure cannot be checked against the page it came from - the rule this file has followed
since PR #15. The 75/50/25% fan-speed rows are published **ragged** (75% to 500 Pa, 50% to 200 Pa, 25% to
50 Pa) and a `VentilationUnitPerformanceTable` is a rectangular grid of strictly increasing numeric axes, so
holding them would mean interpolating or dropping published points. They stay in the catalogue page cited in
the entry's `Source`, together with the per-speed dBA@3m (26/20/<20/<20).

### The Iteration 3 boundary - nothing was invented to make the two entries match

Nuaire publishes no external or entering dry-bulb conditions, no cooling capacity, no supply air temperature,
no heat-exchanger performance table and no controller ramp for the XBC range. None was manufactured.
`FlowFractionByControlTemperature` is therefore **absent** - the legal "data not supplied" state - and never
written as `null`, which is malformed and refuses the whole catalogue.

`TheXBC15Entry_CarriesNoCoolingOrControlDataNobodyPublished` is the test that fails if somebody later
manufactures a common performance model across the two products. The two entries carry **different kinds** of
manufacturer data on purpose, and the catalogue holds each as its source states it.

### Why only this one, of the nine products offered

Selection is smallest-capable, ordered by `Size_Lps = supply + extract`. The XBC15 is size 380 against the
MRXBOX's 300, so **automatic selection is unchanged at every duty <= 150 l/s** - the MRXBOX still wins - and
the XBC15 becomes selectable only in the new 150-190 l/s band. No Iteration 2 native re-acceptance is owed.

The XBOXER Universal sizes would not have that property. UNI-X220 is 68/68 (size 136) and UNI-X580 is
130/130 (size 260), both **smaller** than the MRXBOX and both capable at the project's 30/30 and 63/63 l/s
dwelling duties, so shipping them would re-select the live project's plant downward, drop the Iteration 2B
ceiling from 150 to 68 l/s, and invalidate the licensed acceptance run of 2026-09-01. XBC25/55/65/75/85 are
larger still and order nothing today, but belong with them. All eight are deferred to their own change with
its own acceptance. The 130 l/s undersized rung is therefore proved with a controlled descriptor rather than
a shipped product, and the tests say so.

### The one thing that had to be checked before anything else

That a **1-axis** table with `m3/s` / `W/l/s` outputs validates at all - the reader's grid rules were written
against a 3-axis degC/degC/l/s table. It does, unmodified. **No validation was weakened anywhere**, and the
fail-closed rule is unchanged: `AnUnusableSecondEntry_StillRefusesTheWholeCatalogue` adds the two-product
case - a good first entry and a second with no `Rank` refuses everything rather than quietly returning the
good one.

### Tests

`SAM.Analytical.Systems.Tests/VentilationUnitCatalogueTests.cs`, **83 -> 90**.

**Repaired, not relaxed.** The file assumed a single-entry catalogue in six places. The `Nuaire()` fixture now
resolves the MRXBOX by **product identity** through a shared `Shipped(model)` helper that still asserts the
catalogue names it exactly once - so all eight call sites keep their assertions unchanged - and a sibling
`XBC15()` fixture reads the new entry the same way. Three `Assert.Single` calls over the shipped catalogue
became identity lookups plus an explicit `count_ShippedProducts` check, so a test that means "this one
product" can no longer be confused with a test that means "the whole catalogue".

`ADutyWithinTheFanCurveMaximum_SelectsNuaire_AboveItRefuses` needed **no change**: 100/100 still selects the
MRXBOX with 50 l/s of headroom, and 200/200 is still refused, because 200 is past the XBC15 too.

Seven new, in a new section K:

- `TheShippedCatalogue_HoldsBothNuaireProducts` - two entries, both identities, 150/150 and 190/190, ranks 10
  and 20, nothing unselectable, and the note that rank decides nothing here because size separates them first.
- `TheXBC15Capacity_IsTheFreeAirEndOfItsPublishedFanRow` - above.
- `TheXBC15PerformanceTable_IsTheManufacturerRowVerbatim` - all nine pressure points and all eighteen output
  values, in the source's units.
- `TheXBC15Entry_CarriesNoCoolingOrControlDataNobodyPublished` - above.
- `TheRealLadder_RefusesUnder150_KeepsMRXBOXAt150_AndReachesXBC15Above` - the 130/150/190 ladder against the
  shipped descriptors: above 130 a 130 l/s product is not a candidate and no duty is trimmed to fit; at
  150/150 the MRXBOX with zero headroom, the XBC15 capable and not chosen; at 160/160 the MRXBOX incapable
  and the XBC15 selected with 30 l/s headroom; at 190/190 exactly on the rating; at 191/191 refused, naming
  190.
- `TheTwoShippedIdentities_RoundTripAndNeverCollide` - each identity survives serialization, resolves back to
  its own template by identity, and never matches the other.
- `AnUnusableSecondEntry_StillRefusesTheWholeCatalogue` - above.

Section J's controlled two-product fixture **stays**. The separation recorded at PR #17 still holds: the
shipped catalogue states what the manufacturers published, and the algorithm is tested against fixtures built
for the purpose. What section K adds is the assertion that what the *file* says reaches that algorithm.

### Deployment - closed, with evidence

No new mechanism. The unconditional post-build `xcopy` in
`Grasshopper/SAM.Analytical.Grasshopper.Systems/SAM.Analytical.Grasshopper.Systems.csproj` copies the whole
`files/resources` tree, so the new entry rides along exactly as `SystemEnergyCentre/CapabilityIndex.JSON`
does. Verified on this machine after `dotnet build SAM_Systems.sln -c Release`: the complete two-entry
catalogue is **byte-identical to the repository source** (18,207 bytes, md5
`19e7b9a63ed09ccf9555ba7c9d395adb`) at both

```text
%APPDATA%\SAM\resources\Analytical\Systems\VentilationUnit\VentilationUnitCatalogue.JSON
%USERPROFILE%\Documents\SAM\resources\Analytical\Systems\VentilationUnit\VentilationUnitCatalogue.JSON
```

and both read back two templates through the runtime resolver. The machine-independent path assertions
(`TheCatalogue_SitsWhereTheRuntimeResolverWillLookForIt`, `TheReaderAndTheShippedFile_AgreeOnTheFileName`,
`ALegacyPersistedSetting_StillResolvesTheShippedCatalogue`) are unchanged and still pass.

### Validation

| Suite / build | Result |
| --- | --- |
| `VentilationUnitCatalogueTests` (focused, incl. 7 new) | **50 / 50** |
| `SAM.Analytical.Systems.Tests` (full) | **90 / 90** (was 83) |
| `dotnet build SAM_Systems.sln -c Release` | **0 errors** |
| `git diff --check` | clean |

Sibling repositories at these SHAs: `SAM` `c980d4c`, `SAM_UI` `8219416`, `SAM_Tas` `ec7f505`. The `SAM` and
`SAM_UI` companion branches are **test-only** and neither is required for this one to build or pass.

Note for anyone re-running this locally: `./test.ps1` runs only the Mollier tests and will pass without
executing a single ventilation-catalogue test. Invoke the test project directly.

### Issues / blockers

- None known.

### Next step

- Human review, then merge. **Do not merge automatically.**
- The eight deferred products (XBC25/55/65/75/85, UNI-X220/360/580) are their own change, and the UNI-X sizes
  need Iteration 2 native re-acceptance because they are smaller than the MRXBOX.

## Superseded (2026-09-01): the first product, and everything the catalogue seam was built for

## Current status

The catalogue seam Approved Document O Iteration 2 was left needing. `SAM.Analytical` owns the vocabulary
(`VentilationUnitTemplate`, `VentilationUnitPerformanceTable`, `FlowFractionControlCurve`) and the
selection rule; **which products exist is a fact about this repository**, so the products live here - the
same arrangement, for the same reason, as `SystemEnergyCentre/CapabilityIndex.JSON`.

### The shipped catalogue

`files/resources/Analytical/Systems/VentilationUnit/VentilationUnitCatalogue.JSON`, schema
`VentilationUnitCatalogue:v1`, holding one product:

- **Nuaire MRXBOXAB-ECO5-AECV** with the **MR-ECO-COOL-V** cooling module (the module is part of the
  identity, because the same base unit with and without it publishes different performance).
- **All 96 published points, for both published quantities**, over the brochure's own axes:
  external dry bulb `[29, 32, 34] degC` x entering dry bulb `[23, 24, 25, 26] degC` x airflow
  `[50, 60, 70, 80, 90, 100, 110, 120] l/s`, giving `SupplyAirTemperature` in degC and
  `CombinedCoolingCapacity` in kW. Values are stored in the units the brochure publishes them in and are
  never converted on the way in.
- The controller ramp as data: **22 degC -> 0.30, 26 degC -> 1.00**, with `ClampToDomain` stated **on the
  curve** because the source says "100% at 26 degrees *and above*".
- Source: `Nuaire, MRXBOX Hybrid Cooling System brochure, v.1 July 2022, pages 6-7`, carrying the
  brochure's own "typical cooling data / contact us for project-specific data" caveat.

### THE CAPACITY COMES FROM THE FAN CURVE, NEVER FROM A DUTY POINT

`MaximumSupplyFlowRate_Lps` and `MaximumExtractFlowRate_Lps` are both **150 l/s**, and the source is the
**fan static-pressure chart** on brochure pages 6-7 - *not* the cooling-duty table below it. The chart plots
three fan-speed curves, each identified by its power draw in the Electrical and Sound Data table
(Curve 1 = 319 W, Curve 2 = 167 W, Curve 3 = 77 W). Each curve's free-air (0 Pa) endpoint is the airflow that
setting delivers against no external resistance, and Curve 1 - the highest setting - ends at **150 l/s**.
Digitised from the chart's own vector paths against its axis calibration, and re-verified independently on
2026-09-01: Curve 1 -> 150.2 l/s, Curve 2 -> 119.5 l/s, Curve 3 -> 90.1 l/s, all three terminating on the
same 0 Pa horizontal.

**Curves 2 and 3 are operating states of one physical unit and are NOT catalogue entries.** Promoting them
to 120 l/s and 90 l/s "products" would manufacture three selectable sizes out of one fan, which is exactly
the confusion this section exists to prevent. One physical product, one maximum per direction.

**The 120 l/s that ends the performance table is still not a capacity.** It is the last column of a table of
sample duty points, and `TheNuaireCapacity_IsResolvedFromTheFanCurveNotTheTablesLargestAirflow` asserts the
two numbers are not equal precisely so the sources cannot be confused with each other again.

**Read it as an upper bound, not an installed duty.** 150 l/s is free air; any real installation has duct
resistance, so the flow the unit delivers on site is lower. The field is named "Maximum...FlowRate" and is
used only as a selection ceiling, which is what a free-air figure legitimately is.

**Resolving the capacity does not collapse the authority chain.** These stay four separate facts, and this
figure is only the third of them:

```
PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow
```

The Approved Document demands a rate of a room; the design realizes it; the selected product must be able to
move it; what the unit actually moves at 3pm in August is Iteration 3's question. A capacity is never written
back as a design airflow - proved on the licensed run of 2026-09-01, where connecting this catalogue to three
dwellings at 30/30, 63/63 and 63/63 l/s left all 105,120 hourly TAS values bit-identical to the same run
with no product selected.

The related EDSL / Hoare Lea thread mentions "80-90 L/s" as the *project's* assumption; that is not a
manufacturer statement and was not written here.

### What was added

| File | What |
|---|---|
| `files/resources/Analytical/Systems/VentilationUnit/VentilationUnitCatalogue.JSON` | the catalogue (new folder) |
| `SAM_Systems/SAM.Analytical.Systems/Query/VentilationUnitTemplates.cs` | `VentilationUnitTemplates`, `VentilationUnitCapacityDescriptors`, `VentilationUnitCatalogue`, `DefaultVentilationUnitDirectory`, `VentilationUnitCatalogueFileName` |
| `SAM.Analytical.Systems.Tests/VentilationUnitCatalogueTests.cs` | 30 facts |

Modified (appended to only):

| File | What |
|---|---|
| `SAM_Systems/SAM.Analytical.Systems/Enums/Parameters/AnalyticalSystemSettingParameter.cs` | `DefaultVentilationUnitFileDirectory`, `DefaultVentilationUnitDirectoryName` - appended, so no ordinal moved |
| `SAM_Systems/SAM.Analytical.Systems/Manager/ActiveSetting.cs` | defaults the directory name to `VentilationUnit` and resolves it, mirroring `SystemEnergyCentre` |

## Codex review round (2026-08-29) - PR #15, one P1 finding, fixed

Codex reviewed PR #15 (`29059626`) and posted one P1 finding, a real upgrade blocker: an existing
installation's persisted `SAM.Analytical.Systems` `Setting`, saved before `DefaultVentilationUnitFileDirectory`
/ `DefaultVentilationUnitDirectoryName` existed, is returned by `ActiveSetting.Load()` as-is - it is never
merged with `ActiveSetting.GetDefault()`. Both ventilation-unit values then read back null, and
`Query.DefaultVentilationUnitDirectory()` fell back straight to the resources root itself rather than that
root's `VentilationUnit` child - one directory too high for `VentilationUnitCatalogue.JSON` to be found in,
so `VentilationUnitTemplates()`/`VentilationUnitCapacityDescriptors()` would return `null` for any
installation upgrading straight from before this feature, even though the catalogue file itself deployed
correctly.

**Fixed** in `Query.DefaultVentilationUnitDirectory` (`VentilationUnitTemplates.cs`): when the setting's own
`DefaultVentilationUnitDirectoryName` is absent, it now falls back to the **same** default leaf
`ActiveSetting.GetDefault()` declares, read from that method rather than a second hard-coded
`"VentilationUnit"` literal - so the leaf name is declared in exactly one place. `ActiveSetting.Load()`
itself is untouched, per the review's own scope guidance: the fix is a local fallback in the one place that
needed the invariant, not a settings-migration rewrite.

The method also gained two optional parameters, `Setting setting = null` and `string resourcesDirectory =
null` (both default to the previous, unparameterised behaviour - every existing call site is unaffected).
They exist purely so a test can hand in a specific persisted-setting shape and a specific resources root
without mutating the process-wide `ActiveSetting.Setting` or depending on whatever real per-user SAM install
`Core.Query.ResourcesDirectory()` happens to find on the machine running the test - which, it turns out, no
existing test in this file did: `TheCatalogue_SitsWhereTheRuntimeResolverWillLookForIt` re-derives the
expected path and checks `File.Exists` on it directly, but never actually calls
`Query.DefaultVentilationUnitDirectory()` end to end. That gap is why this bug shipped unnoticed.

Test: `VentilationUnitCatalogueTests` section I -
`ALegacyPersistedSetting_StillResolvesTheShippedCatalogue` (a `new Setting()` with neither parameter set -
exactly `ActiveSetting.Load()`'s legacy shape - resolves to this repository's real shipped catalogue
directory and reads the real catalogue through it, proving the upgrade path end to end),
`AFreshDefaultSetting_ResolvesTheShippedCatalogueDirectory`, `AnExplicitVentilationUnitDirectory_IsUsedDirectly`,
`AnExplicitCustomDirectoryName_IsCombinedWithTheResourcesRoot` (including the leaf-not-found refusal case).

### Validation

| Suite | Result |
|---|---|
| `VentilationUnitCatalogueTests` (focused, incl. 4 new) | **34 / 34** |
| `SAM.Analytical.Systems.Tests` (full) | **74 / 74** |
| `SAM.Analytical.Systems.Mollier.Tests` | **123 / 123** |

CI against `sow/2026-Q3` may still fail until `SAM-BIM/SAM` #80 merges - **expected**, per the declared
dependency; not worked around here. New commit on top of `29059626` on
`feature/parto-iteration2-ventilation-unit-catalogue`. Not merged.

## Follow-up hardening (2026-08-31) - PR #16, two late Codex P2 findings

After PR #15 merged (`32fff611`), a final Codex review pass raised two P2 findings against the merged head.
Both confirmed and fixed on `fix/parto-catalogue-late-p2-hardening` (**PR #16**, commit `be6a132e`, later
updated to document this section per Codex's own P1 finding on that documentation gap - see below).

### 1. Catalogue schema was never enforced

`VentilationUnitTemplates()` read `Templates` without ever checking the `Schema` key - even though the
shipped catalogue already declares `"Schema": "VentilationUnitCatalogue:v1"` and a test
(`TheShippedCatalogue_DeclaresItsSchema`) already asserted the file *states* it. Nothing refused a
catalogue with a missing, wrong, or future-looking schema tag; every one of those would have been read as
if it agreed with this reader.

**Fixed:** a new public constant, `Query.VentilationUnitCatalogueSchema = "VentilationUnitCatalogue:v1"`,
and a gate in `VentilationUnitTemplates()` - checked before `Templates` is read at all - that refuses the
whole catalogue unless the `Schema` key is present, is a JSON string, and equals that constant exactly
(ordinal). A schema that looks plausible for a future version, e.g. `"VentilationUnitCatalogue:v2"`, is
refused the same way as a missing or garbled one - never silently parsed as v1.

### 2. Present-but-null optional data read the same as absent

The `PerformanceTable`/`FlowFractionByControlTemperature` checks tested the **parsed** template property
(`ventilationUnitTemplate.PerformanceTable != null && !...IsValid`). A genuinely absent JSON key and a key
written as `"PerformanceTable": null` both parse to the same null property on `VentilationUnitTemplate`, so
a present-but-null key was silently treated as the documented "no performance data" state instead of
refusing the catalogue as malformed.

**Fixed:** both checks now read the raw JSON key via `jsonObject_Template.ContainsKey(...)` first. The rule
this preserves, unchanged: an **absent** key is the legal "data not supplied" state (allowed, e.g. a
selection-only product); a key that is **present** - whether `null`, the wrong JSON shape, or an object that
does not parse into a valid `VentilationUnitPerformanceTable`/`FlowFractionControlCurve` - is malformed data
and refuses the whole catalogue, exactly as a present-but-unusable table already did before this fix.
`Rank` and the two capacity fields already followed the correct absent-vs-present-but-bad pattern and were
not touched; inspection did not find the same bug elsewhere in this catalogue's optional fields.

**No production code changed beyond `VentilationUnitTemplates.cs`.** No architecture, dependency, or
selection-rule change - the fix is two narrow reader-hardening changes in the one file the two findings
named.

### Validation

| Suite | Result |
|---|---|
| `VentilationUnitCatalogueTests` (focused, incl. 10 new: schema missing/wrong/future, present-but-null on both optional fields, absent-still-allowed regression guard, exact-v1-accepted) | **41 / 41** |
| `SAM.Analytical.Systems.Tests` (full) | **81 / 81** |
| `SAM.Analytical.Systems.Mollier.Tests` | **123 / 123** |

Built (`dotnet build SAM_Systems.sln -c Release`) and tested against SAM's currently-merged `sow/2026-Q3`
(`5433c20f`) specifically - **not** against SAM's own in-flight late-P2 hardening branch (SAM PR #82) - to
confirm this PR's dependency on SAM is unchanged and it does not require that sibling PR to merge first.

Not merged yet.

## Decisions / assumptions

1. **Each catalogue entry is a serialised `VentilationUnitTemplate`**, so the type's own tolerant
   `FromJsonObject` does the parsing and the reader then *validates* every entry. Duplicating a
   three-dimensional grid parser in the reader would have been a second place for the format to drift.
2. **One unusable entry refuses the whole catalogue** - never skipped. A product silently vanishing from a
   library is the failure that produces a smaller answer nobody notices. Refused on: no source; a
   performance grid whose values do not line up with its axes; an invalid control curve (including a
   mistyped `PerformanceDomainPolicy`, because reading a mistyped `Refuse` as a clamp is the one direction
   a typo must never take); a capacity key that is present but not a usable number; a duplicated product
   identity; a **missing or non-integer `Rank`**; an element that is not an object.
3. **A missing `Rank` refuses.** A missing rank is a unique 0, 0 sorts first, and the entry somebody forgot
   to rank becomes the preferred answer between two products of the same size - the exact trap
   `SystemCapabilityDescriptors` was hardened for.
4. **An absent capacity does not refuse and does not drop the entry.** It is a documented state; the
   template is returned in full, `Analytical.Query.CapacityDescriptors` leaves it out of the selection set,
   and `Analytical.Query.UnselectableVentilationUnitTemplates` reports it with the reason.
5. **A missing catalogue is `null`, not empty**, so "there is no catalogue" stays distinguishable from "the
   catalogue offers nothing for this duty".
6. **No `Application` eligibility field.** `CapabilityIndex.JSON` filters domestic vs commercial system
   templates; the unit catalogue has no equivalent because PR #79's selection kernel has no application
   concept to consume one. Revisit before a commercial air handling unit is catalogued.
7. **The supplied engineering spreadsheet is a transcription aid, not a specification.** The authoritative
   source for every figure in the catalogue is the Nuaire brochure. Nothing derived from the spreadsheet
   was imported - no formula, no fitted curve, no layout, and in particular no value outside the
   manufacturer's published domain. The spreadsheet was used once, as a secondary cross-check that the
   supply air temperatures were transcribed correctly, and it agreed.
8. **`JsonValue.TryGetValue`, not `GetValue<object>()` + `Core.Query.IsNumeric`.** A *parsed* JSON number is
   backed by a `JsonElement`, which is not a numeric CLR type - the `IsNumeric` form would have called
   every capacity in every file unusable and refused every catalogue on disk. The same defect exists,
   unfixed, in `SAM.Math.LinearInterpolation` and `BilinearInterpolation`; it is recorded as separate work.
   A third copy existed in `MultilinearInterpolation` and was **deleted** in the review pass along with the
   unused serialiser that needed it, so the workaround now lives only where a wire format is genuinely
   parsed: `Analytical.PerformanceJson` and `Systems.Query.IsUsableCapacity`.

## Validation

Built `SAM.sln` (Debug) then `SAM_Systems.sln` (Debug) - 0 errors, no new warnings.

| Suite | Result |
|---|---|
| `VentilationUnitCatalogueTests` (new) | **30 / 30** |
| `SAM.Analytical.Systems.Tests` (full) | **70 / 70** (was 40, plus 30) |
| `SAM.Analytical.Systems.Mollier.Tests` | **123 / 123** |
| `SAM.Tests` (in `SAM`, full) | **1583 / 1583** |
| `SAM.Analytical.Tas.TM59.Tests` (in `SAM_Tas`) | **649 / 649** |

The catalogue tests read the **repository's own** `files/resources` directory rather than a copy in the
test output, so they check what is shipped. Same walk `SystemEnergyCentreCapabilityTests` uses, and the
catalogue is deliberately not copied to the output.

`TheWholeNuaireTable_MatchesTheBrochure` checks all 96 points of both outputs against a second
transcription laid out the way the brochure lays it out. That catches a flattening or ordering mistake; it
does not catch a misreading shared by both transcriptions, and the test says so. The supply air
temperatures were independently confirmed against the engineering spreadsheet that preceded this work,
which holds the same 3 x 4 x 8 table.

## The three authorities, and which is which

```
Nuaire published table       MANUFACTURER AUTHORITY          the catalogue holds this, and only this
SAM interpolation/extrap.    explicit generic policy         Refuse | ClampToDomain | OuterCellLinearExtrapolation
legacy IES spreadsheet       HISTORICAL, NON-AUTHORITATIVE   never stored, never asserted, not reconstructed
```

The policy formerly called `LegacyLinearExtrapolation` is now
**`OuterCellLinearExtrapolation`**. The old name claimed something that is not true: comparison against the
legacy spreadsheet's own derived figures shows they **disagree** - it gives roughly 14.9 degC at
26 / 23 / 80 where SAM gives 15.1, and roughly 18.9 degC at 26 / 26 / 120 where SAM gives 19.4. The name
was chosen over the shorter `LinearExtrapolation` because it says which linear extrapolation it is -
continuation of the outermost cell, not a fit through all the points - and this codebase prefers names that
cannot be read the wrong way.

That spreadsheet is historical reference material: its derivation is unavailable, the engineer who produced
it is unavailable, and it is not being reconstructed. No polynomial was fitted and no ramp expression was
reverse-engineered. Exact compatibility with that tool, should a project ever need it, is a **separate task
requiring an authoritative specification or validated acceptance data** - not a reason to bend SAM's policy.

SAM's own extrapolation arithmetic **is** pinned, in
`VentilationUnitCatalogueTests.SAMsLinearExtrapolation_IsPinnedOutsideThePublishedDomain`, at nine points -
five below the published 29 degC external floor, four above the published 26 degC entering ceiling. Every
value was computed independently in Python before being asserted in C#; the two agree to 1e-6. The test
states in its own documentation that it pins SAM arithmetic and **not** IES compatibility.

## Deployment - closed, with evidence

`Query.DefaultVentilationUnitDirectory()` resolves `<resources>/Analytical/Systems/VentilationUnit`, where
`Analytical/Systems` is derived by `Core.Query.ResourcesDirectory(setting, assembly)` from the assembly
name `SAM.Analytical.Systems` (leading `SAM.` stripped, dots to separators) and the leaf comes from
`AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName`.

**The existing mechanism already carries it - no new mechanism was added.** The chain, traced end to end:

1. `SAM_Systems/Grasshopper/SAM.Analytical.Grasshopper.Systems.csproj` has an **unconditional**
   `Target Name="PostBuild" AfterTargets="PostBuildEvent"` that runs
   `xcopy "$(SolutionDir)\files\resources" "$(APPDATA)\SAM\resources" /Y/I/E/S` and the same to
   `%USERPROFILE%\Documents\SAM\resources`. `/E/S` is recursive, and it copies the **whole**
   `files/resources` tree - which is why `SystemEnergyCentre/CapabilityIndex.JSON` reaches installs today.
2. CI `installer.yml:702` copies `%USERPROFILE%\Documents\SAM` into `stage\user\Documents\SAM` after the
   full release build (recorded in `PLAN_SAM_TAS_SPLIT.md:228`).
3. `SAM_Installer/Build_Installer.iss:62` stages `build\user\Documents\SAM\resources\*` into
   `{userappdata}\SAM\resources`, recursively.
4. At runtime `Core.Query.ResourcesDirectory` finds it under `Documents\SAM\resources` or, failing that,
   beside the executing assembly - which is where step 1 and step 3 both put it.

**Empirical proof on this machine**, after building `SAM_Systems.sln`: the catalogue is present and
**byte-identical to the repository source** (11042 bytes, md5 `f56eabebe6529afed9dc64512c4fb222`) at both

```
%APPDATA%\SAM\resources\Analytical\Systems\VentilationUnit\VentilationUnitCatalogue.JSON
%USERPROFILE%\Documents\SAM\resources\Analytical\Systems\VentilationUnit\VentilationUnitCatalogue.JSON
```

with `SystemEnergyCentre/CapabilityIndex.JSON` sitting beside it in the same tree.

Two focused tests lock the invariant **machine-independently**, so a clean CI runner checks the same thing:
`TheCatalogue_SitsWhereTheRuntimeResolverWillLookForIt` re-derives the assembly-name segment and the setting
leaf and asserts the shipped file is at exactly that relative path (it fails if the folder moves, the
setting is renamed, or the assembly is renamed), and `TheReaderAndTheShippedFile_AgreeOnTheFileName` stops
either side of the file name being renamed alone.

## Grasshopper Seam 1 (2026-08-31) - exposing the existing catalogue and selection through Grasshopper

**Goal.** Make Iteration 2's already-implemented ventilation-unit selection usable from a normal Grasshopper
canvas, without redesigning the analytical architecture or adding a second selection algorithm. The
selection kernel (`Query.SelectSmallestCapableVentilationUnit`) and the catalogue reader
(`Query.VentilationUnitTemplates`/`VentilationUnitCapacityDescriptors`/`Analytical.Query.UnselectableVentilationUnitTemplates`)
already existed and needed no change; only a Grasshopper-facing seam was missing on either side.

**What was added here.**

| File | What |
|---|---|
| `Grasshopper/SAM.Analytical.Grasshopper.Systems/Component/SAMAnalyticalSystemVentilationUnitCatalogue.cs` (new) | Reads the catalogue via the existing `Query` API and reports it as three outputs: `ventilationUnitCapacityDescriptors` (selectable products only, as `GooObject`), `unselectableVentilationUnitTemplates` (as `GooSAMObject`, since `VentilationUnitTemplate` is a `SAMObject`) and `unselectableReasons` - aligned item-for-item with the second output. One optional `directory_` input, resolved by the existing `Query.VentilationUnitTemplates(directory)` (no path-resolution logic duplicated in Grasshopper). |
| `SAM.Analytical.Systems.Tests/VentilationUnitCatalogueTests.cs` (+2 tests, section J) | Pins the two required engineering behaviours directly against the combination the component calls. **Updated when the Nuaire capacity was resolved** - these tests now assert that the shipped Nuaire product is real (catalogue read succeeds) and **selectable** at 150 l/s supply / 150 l/s extract, with the unselectable list **empty** and that emptiness still distinguished from a failed load. Curves 2 and 3 of its fan chart stay operating states of the one physical unit, never separate products. The selection ladder itself is regression-tested against synthetic descriptors, because one real product offers no "between capacities" case: a controlled two-product fixture (100/100 and 150/150 l/s) selects the 150 l/s unit for a 115/115 l/s duty - never 100, never "nearest", and 115 is never written back as a capacity. |

**Companion change in `SAM-BIM/SAM`** (not this repository, recorded here because the two form one seam):
`SAMAnalyticalPreparePartOIteration` gained one optional input, `ventilationUnitCapacityDescriptors_`
(`Param_GenericObject`, list), passed straight into the existing 5-argument
`Modify.PreparePartOIteration(..., ventilationUnitCapacityDescriptors)` overload - unconnected, the call
behaves exactly as it did before the parameter existed (`ventilationUnitCapacityDescriptors` stays `null`,
the library's own guard clause skips selection entirely). Three previously-hidden `PartOIterationPreparation`
outputs were also exposed: `ventilationSystems`/`airHandlingUnits` (plural, `GooAnalyticalObjectParam`) and
`ventilationUnitSelections` (`GooObjectParam`, since `VentilationUnitSelection` is not an `IJSAMObject`).

**No SAM -> SAM_Systems dependency was introduced.** `SAM.Analytical.Grasshopper` never references
`SAM.Analytical.Systems`; the two components' outputs and inputs meet only as wires on a Grasshopper canvas,
matching how this catalogue seam's manufacturer/selection split already keeps the two repositories apart.

**Real Nuaire behaviour, confirmed end to end.** `MRXBOXAB-ECO5-AECV + MR-ECO-COOL-V` reaches
`SAMAnalyticalSystemVentilationUnitCatalogue`'s `ventilationUnitCapacityDescriptors` output at 150/150 l/s
with its full performance data, and `unselectableVentilationUnitTemplates` is empty - there is no absence
left to report. A canvas wiring the shipped catalogue into `SAMAnalytical.PreparePartOIteration` therefore
selects it for any dwelling duty at or below 150 l/s on both sides, and refuses above that. Verified on the
licensed run of 2026-09-01: three dwellings at 30/30, 63/63 and 63/63 l/s all select it, with 120/87/87 l/s
of headroom left deliberately untaken.

**One product is not a selection ladder.** With a single size in the shipped catalogue there is no
"between capacities" case to exercise, so smallest-capable / next-capable / refused are proved against the
controlled synthetic descriptors in `SAM.Tests/PartOVentilationUnitSelectionTests.cs` and
`TemporaryTwoProductCatalogue` here. That separation is deliberate and should stay: the shipped catalogue
states what the manufacturer published, and the algorithm is tested against fixtures built for the purpose.

**Test results.** `SAM.Analytical.Systems.Tests`: 43/43 focused (`VentilationUnitCatalogueTests`), 83/83 full
suite. `SAM.Analytical.Systems.Mollier.Tests`: 123/123 (regression, unaffected by this change). Both
`SAM.Analytical.Grasshopper.Systems` and `SAM.Analytical.Systems.Tests` build clean in Release (0 CS errors);
the project-level `dotnet build` of the Grasshopper `.csproj` alone fails its post-build deploy step with the
pre-existing `*Undefined*\files\resources` xcopy quirk when `$(SolutionDir)` is not supplied (environmental -
same quirk `SAM`'s own progress notes record; passing `-p:SolutionDir=...` or building via the `.sln`
resolves it, and CI is unaffected).

**GH acceptance.** No live-Grasshopper/Rhino test harness exists in either repository (confirmed by
inspection: the one Grasshopper-driving test project, `SAM.Core.Grasshopper.Tests`, never calls
`SolveInstance`, and neither test project here nor in `SAM` references `Grasshopper.Kernel`/`GH_IO`). Per
the brief, no new testing framework was built for this. The manual canvas for final licensed acceptance:

```
Part F
  -> SAMAnalytical.PreparePartOIteration   (_partOIteration = BasePassive, _ventilationStrategies = "MVRE")
       ventilationUnitCapacityDescriptors_ <- SAMAnalytical.SystemVentilationUnitCatalogue.ventilationUnitCapacityDescriptors
  -> outputs: ventilationUnitSelections, airHandlingUnits, ventilationSystems, refusals
```

- **Acceptance A (real installed catalogue):** leave `SAMAnalytical.SystemVentilationUnitCatalogue.directory_`
  unconnected. Expect `ventilationUnitCapacityDescriptors` to carry the Nuaire product at 150/150 l/s,
  `unselectableVentilationUnitTemplates` empty, and `PreparePartOIteration`'s `ventilationUnitSelections` to
  hold one selection per dwelling with the design duty **unchanged** by the selection.
- **Acceptance B (test catalogue):** point `directory_` at a folder holding a hand-written
  `VentilationUnitCatalogue.JSON` with two synthetic products (e.g. 100/100 and 150/150 l/s - see
  `TemporaryTwoProductCatalogue` in the tests above for the exact JSON shape). Expect the dwelling's design
  duty to be calculated by the unchanged network, the smallest capable unit selected (150 l/s for a duty
  above 100), that reference to appear in `ventilationUnitSelections`/on the analytical AHU, and
  `ventilationTerminals`' design flows to read exactly as they did before the catalogue was connected.

## Debt found during PR1 and deliberately NOT fixed

Recorded rather than migrated - none of it blocks the materialisation, and fixing any of it would widen
PR1 beyond its scope:

- `Modify.Merge` ends in an unconditional `return false` whatever it did, so its return value cannot tell
  a successful merge from a failed one. The `YearlySchedule` regression therefore asserts what the merge
  *wrote* rather than what it returned.
- `SystemEnergyCentre.Duplicate(Guid?)` mutates **`this`** rather than `result`: it enumerates
  `result.GetSystemPlantRooms()` but calls unqualified `Remove`/`Add`, which bind to the source. Avoided.
- `SystemPlantRoom.Duplicate(Guid?)` assigns **random** guids and downgrades a `DisplaySystemPlantRoom` to
  a plain `SystemPlantRoom`. Avoided.
- `AnalyticalSystemsProperties`' copy constructor **shares** its `ISchedule` instances. Tolerated: PR1
  never mutates them, and it copies the caller's schedule before it enters.
- `YearlySchedule.Values`' setter throws `NullReferenceException` on null (the null branch does not
  return) and `DivideByZeroException` on an empty array. Explicitly out of PR1 scope per the plan.
- `DailySchedule(DailySchedule)` shares `ScheduleDay` instances and copies its own empty
  `scheduleDayNames` rather than the source's.
- `Query.Duplicate(this SystemPlantRoom, ISystemSpaceComponent)` is unconditionally self-recursive.
  Nothing calls it.
- `Modify.TryConnect` throws `NotImplementedException` for any endpoint pair it does not enumerate.
  Avoided; PR1 dispatches to the typed `Connect` overloads itself.
- `RelationCluster.TryAddObject` **silently replaces** an existing entry on a guid collision - which is
  why PR1 keeps its own identity-collision map and refuses rather than trusting the store.
- `RelationCluster.Contains`/`GetGuid` fall back to a full linear equality scan when the object is absent,
  and `Core.Query.Type` performs an uncached assembly walk per type name. PR1 works around both, by adding
  before relating and by keeping `GetObjects<T>()` out of every per-space loop.
- `SystemConnection.FromJsonObject` silently drops any `ObjectReference` stored with index `-1`, so
  index-less connections do not survive a round trip. PR1 always writes explicit connector indexes.
- `SystemEnergyCentre.GetSystemPlantRoom(string)`/`(ObjectReference)` and `GetSystemEnergySource(string)`
  iterate `Dictionary.Values` with `ElementAt(i)` inside a `for`, which is O(n^2). Never called.
- `VentilationSystemParameter.SupplyUnitName` being a name rather than a relation (SAM-side, already
  recorded there). Migrating it would be a `SAM` production change, which PR1 must not make.

The PR2 blockers - the `ZoneLoads` fixed-index issue, the TPD `Simulate` false positive and the
`SystemSpaceResult` identity ambiguity - are **not** touched here; they belong to PR2 by #111's own gate.

## Issues / blockers

- ~~**Unresolved manufacturer fact.** The Nuaire unit's maximum supply and extract airflow.~~ **closed
  2026-09-01** - resolved to 150/150 l/s from the brochure's own fan static-pressure chart (Curve 1, free
  air). See *THE CAPACITY COMES FROM THE FAN CURVE* above. Remaining caveat, not a blocker: it is a free-air
  upper bound, so a project needing the *installed* duty still wants Nuaire's project-specific selection.
- **Only one selectable product exists.** The brochure covers one physical MVHR unit. Its in-line variant is
  named but explicitly publishes no performance ("contact Nuaire for performance data on the in-line
  version"), and the opposite-handed unit is a spigot arrangement of the same fan - neither is a distinct
  capability, so neither was added. Any future "which of several units" work needs a second authoritative
  product, not a second reading of this one.
- ~~Deployment unverified~~ **closed** - see *Deployment* above.
- **Legacy IES compatibility is explicitly NOT a goal of this stage** and is not an open blocker. The
  supplied workbook contains no interpolated table, no external axis below 29 degC and no ramp expression -
  see the SAM repository progress note for the full forensic result. The spreadsheet's derivation is
  unavailable and is not being reconstructed. If exact compatibility is ever required it is a separate task
  needing an authoritative specification or validated acceptance data.
- **Branch coupling.** This needs the `SAM.Analytical` from the SAM manufacturer-catalogue pull
  request. Merge order is SAM first, then this.
- **Two copies of the JSON-number workaround remain**, in different assemblies and different repositories
  (`Analytical.PerformanceJson`, internal, and `Systems.Query.IsUsableCapacity`, private). Consolidating
  them means making a JSON reader public on `SAM.Analytical`, which is a decision about that library's
  API surface and belongs with the separate fix to `LinearInterpolation` / `BilinearInterpolation` - not
  inside this feature.

## Next step

Merge the Grasshopper Seam 1 pair (this repository and `SAM-BIM/SAM`'s
`feature/parto-iteration2-gh-equipment-selection`) once both are reviewed. Seam 2 and Iteration 3 are
deliberately **not** started on this branch - see the brief this stage worked from. Beyond that, Iteration 3
starts at the read-only bridge; the Nuaire capacity that used to gate it is resolved (150/150 l/s, see
*THE CAPACITY COMES FROM THE FAN CURVE* above). Given an
`AirHandlingUnit` carrying a `VentilationUnitReference`, resolve its template from this catalogue and
report the hourly supply airflow and leaving-air temperature the template and its control curve imply for
a supplied entering-temperature series. Read-only, no TAS, no model writes - so the control aggregation
question (which temperature drives one unit serving several rooms) has to be answered explicitly before
anything depends on the answer.
