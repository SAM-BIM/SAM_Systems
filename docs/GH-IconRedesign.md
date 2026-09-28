# SAM Grasshopper icon redesign — SAM_Systems PR record

Branch `feature/sam-gh-icon-redesign`, based on `sow/2026-Q3` @ `fbef48f`. PR: (to be opened).
Propagates the SAM icon design system from SAM-BIM/SAM#166 (head `cf4d924a`, open, not merged) to this repository.

## Current status
All **38** Grasshopper objects in this repo (30 components + 8 params) use redesigned icons: **38 / 38**.
Built and validated; ready for review. **Not merged.**

## Work completed
- `design/grasshopper-icons/`: the shared SAM-BIM icon kit. `icons.py`, `render.py`, `sam_classify.py` and `ICON_DESIGN_SYSTEM.md` are vendored **verbatim** from SAM#166 (hash-checked). `icons_ext.py` and `ICON_DESIGN_SYSTEM_EXT.md` are the frozen SAM-BIM extension v1 (identical in every SAM-BIM repo). `tools/repo_rules.py` holds this repo's explicit decisions.
- **Inventory**: `tools/inventory.py` parses C# source (every non-abstract class declaring `ComponentGuid`).
- **Manifest** (source of truth): `manifest.json` / `manifest.csv` — per object: GUID, class, source, project, object glyph, operation, modifiers, icon id, resource, glyph/badge origin.
- **Generation**: 36 canonical SVGs → 24×24 PNGs; review sheet `review/contact_sheet.png` (native 24 px on GH normal / orange-warning / dark bodies + 3×) and `review/REVIEW.md`.
- **Integration**: each project's existing mechanism; only the icon token inside each `Icon` getter changes.

| Project | Objects | Icon resources | Mechanism |
|---|---|---|---|
| `SAM.Analytical.Grasshopper.Systems` | 36 | 34 | resx / Bitmap |
| `SAM.Core.Grasshopper.Systems` | 2 | 2 | resx / Bitmap |

## Design reuse
- **Reused SAM object families (11)**: `ahu`, `airflow`, `fan`, `geometry2D`, `object`, `relation`, `result`, `settings`, `space`, `system`, `thermometer`
- **New SAM-BIM ext v1 families used (4)**: `connector`, `energyCentre`, `plantRoom`, `processHeatRecovery`
- **Verbs**: `add`, `convert`, `create`, `display`, `get`, `import`, `merge`, `modify`, `set`, `sort`, `update`, `value` (all SAM)
- Distinct icons: **36** (25 on SAM glyphs, 11 on ext glyphs). Icon ids shared with SAM render pixel-identically to SAM's.

## Decisions and assumptions
- Grammar, palette, badge families and construction rules are unchanged (SAM#166). No text, no new colours.
- Qualifier variants (`…By<X>`) share an icon intentionally (see `review/REVIEW.md`).
- Interop direction: external → SAM = import ↓, SAM → external = export ↑.
- System components (coils, fans, exchangers) use SAM's `ahu` (equipment object) glyph; ISystem keeps `system`.
- `SystemEnergyCentre` / `SystemPlantRoom` get the ext glyphs `energyCentre` / `plantRoom`; connectors get `connector`.
- Connected*/Ordered* queries draw the object they return (components, groups, objects), per SAM's subject rule.
- Legacy icon resources are kept (still referenced by context menus / AssemblyInfo); no GUID, name, nickname, category, subcategory, parameter or behaviour change.

## Files changed
- New: `design/grasshopper-icons/**`, `<project>/Resources/Icons/SAM_GH_*.png`, `docs/GH-IconRedesign.md`.
- Modified: 38 component/param `.cs` files (one icon token each), 2× `Resources.resx`, 2× `Resources.Designer.cs`. No csproj change.

## Validation
| Check | Result |
|---|---|
| `tools/classify.py` | 38 classified, 0 unclassified |
| `tools/build.py` identical-pixel collision check | 0 groups (36 distinct icons; 1 intentional shared icon: `energyCentre_create` ×3 qualifier variants) |
| Icon ids shared with SAM#166 vs SAM's `png/24` | 11/11 byte-identical |
| `tools/integrate.py` re-parse | 38/38 objects reference their `SAM_GH_*` resource; every PNG exists |
| `tools/check_source.py` vs `origin/sow/2026-Q3` | vendored files OK; 38 icon-token swaps, 0 non-icon C# changes; ComponentGuid set unchanged (38) |
| `dotnet build SAM_Systems.sln -c Debug` | Build succeeded, 0 errors |
| `tools/check_assemblies.py build` | both assemblies embed every required 24×24 icon (34 + 2) → OK |
| `tests/GhIconTest` (real Rhino 8 / Grasshopper, Rhino.Testing) | 38/38 objects load by GUID, name/category match, icon = manifest PNG (max diff 1 level, premultiplied-alpha rounding) |
| `SAM.Analytical.Systems.Tests` / `SAM.Analytical.Systems.Mollier.Tests` | 268/268 and 123/123 passed |
| Visual review (`review/contact_sheet.png`, 24 px on normal / warning / dark bodies) | all icons legible; no collisions |

## Unresolved issues / risks
- Built against sibling SAM checked out on `feature/sam-gh-icon-redesign` (SAM#166); API-neutral, so the result is the same on `sow/2026-Q3`.
- Building a single csproj (outside the solution) hits the pre-existing post-build `xcopy "$(SolutionDir)…"` error; building the solution is clean. Not caused by this PR.

## Recommended next step
Review this PR (compare `review/contact_sheet.png`), then merge by the maintainer. After merge, add the `PROJECT_PROGRESS.md` closeout entry on `sow/2026-Q3` with the merge SHA. SAM#166 (the reference design system) remains open.
