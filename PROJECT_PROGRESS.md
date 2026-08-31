# Project Progress

## Branch
The ventilation unit catalogue feature itself is **merged**: `feature/parto-iteration2-ventilation-unit-catalogue`
went in as PR #15 (`32fff611`), onto `SAM-BIM/SAM_Systems` `sow/2026-Q3`, after the SAM manufacturer-catalogue
dependency below merged first.

Current work is a narrow **follow-up hardening branch**, `fix/parto-catalogue-late-p2-hardening`, cut from
the current `sow/2026-Q3` (`32fff611`) - see the *Follow-up hardening* section below. Raised as **PR #16**
against `sow/2026-Q3`. **Not merged yet.**

**Depended on `SAM-BIM/SAM`** for the ventilation unit catalogue feature (now merged, both sides). The
current hardening branch depends only on the SAM API surface already merged into SAM's `sow/2026-Q3`
(`5433c20f`) - it does not require SAM's own late-P2 hardening follow-up (SAM PR #82) to merge first, and
was built and tested against SAM's `sow/2026-Q3` specifically to confirm that independence.

## Last updated
2026-08-31 - late Codex P2 hardening follow-up (PR #16): catalogue schema enforcement and the
present-but-null vs. absent optional-data distinction.

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
