# Project Progress

## Branch
`feature/parto-iteration2-ventilation-unit-catalogue`, cut from `sow/2026-Q3` at **`208379d`** (unchanged
integration tip). Raised as a pull request against `sow/2026-Q3`. **Not merged.**

**Depends on `SAM-BIM/SAM`.** This needs the `SAM.Analytical` manufacturer-template vocabulary added on
`feature/parto-iteration2-manufacturer-catalogue` (based on SAM `sow/2026-Q3` at `ce95bc5b`). It will not
build against an older `SAM.Analytical`, so **the SAM pull request must merge first**.

## Last updated
2026-08-28 - the manufacturer ventilation unit catalogue and its reader.

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

### THE CAPACITY IS DELIBERATELY ABSENT

`MaximumSupplyFlowRate_Lps` and `MaximumExtractFlowRate_Lps` are **not stated**, so the Nuaire entry is a
complete record of published performance and is **not selectable**. The brochure states no maximum supply
or extract airflow. Its 50-120 l/s figures are the duty points the selection tables are published at, and
the fan-curve chart's axis reaching 125 l/s is an axis tick. **Neither is a statement about the fan, and
neither has been promoted into the gap.** `UnresolvedCapacityNote` on the entry says what would resolve it.
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

## Issues / blockers

- **Unresolved manufacturer fact.** The Nuaire unit's maximum supply and extract airflow. Until sourced -
  a Nuaire technical datasheet, a fan selection, or the project-specific selection Nuaire ask for - the
  product cannot be selected by Iteration 2. Intended behaviour, not a bug.
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

Iteration 3, first step: resolve the Nuaire capacity, then build the read-only bridge - given an
`AirHandlingUnit` carrying a `VentilationUnitReference`, resolve its template from this catalogue and
report the hourly supply airflow and leaving-air temperature the template and its control curve imply for
a supplied entering-temperature series. Read-only, no TAS, no model writes - so the control aggregation
question (which temperature drives one unit serving several rooms) has to be answered explicitly before
anything depends on the answer.
