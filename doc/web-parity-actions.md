# Baseline player action reconciliation

P0-03 source mapping completed 19 October 2026 against legacy `1f755ca` and the
delivered operation registry at `8e4c6d4`. This closes the action-inventory item,
not P0, gameplay parity, P9 workflow delivery or native/device UX acceptance.
The [port plan](../tmp/web-feature-parity-port-plan.md) keeps those gates separate.

## Maintained review and generated evidence

[`parity/action-map.json`](parity/action-map.json) is the maintained review input.
Its schema version 2 records 75 workflow destinations/dispositions, 180 pinned
source reviews, all 248 original event/command binding decisions, 985 additional
source-surface decisions, all 509 declaration/generated-child routes and reciprocal
links for all 45 `MAN-T*`, `MAN-M*`, `MAN-W*` and `MAN-O*` requirements.
Every baseline root also has an explicit disposition.
All 26 post-manual release groups have action-level source/workflow links; the
pinned baseline's 15 `whatsnew.txt` notes have individual UI/content/no-functional-
change dispositions. These are not verification of each historical bug fix.

[`parity/action-review.json`](parity/action-review.json) is reproducible output.
The original binding IDs remain in [`base-ledger.json`](parity/base-ledger.json),
now with reviewed workflow, reachability, mapping status, phase and manual links.
Their implementation status remains **inventoried**. Missing gameplay destinations
use `missing:`; explicit adaptations, exclusions, source-only and supporting code
use `disposition:`. Neither notation declares a runtime operation. `operations`
contains only actual IDs from `web/src/lib/game-ui.ts`, including partial entry
points whose missing behavior is described explicitly.

The additional audit scans pinned core/plugin/library C# for constructed input
controls, delegate targets, custom input hooks, shortcuts and dialog results. It
also accounts for links/selection behavior in the two embedded HTML documents.
This includes fields read only on Apply, constructor/context callbacks, tree
drag/drop, label editing, custom index/color selectors and virtual mouse/key hooks
missed by the original six-event scanner. Source control properties are retained
as expressions; a designer minimum/default is not automatically a domain rule.

Counts describe review records, not unique capabilities. Some surfaces are redraw,
scheduler or listener plumbing; several controls lead to one workflow. Discovery
is lexical, with manual source/registration/consumer review supplying reachability
and dispositions. It is not a C# semantic parser or evidence of running Windows.
Source hashes and explicit decisions prevent new discovered items from silently
receiving a default mapping.

```sh
python3 scripts/inventory-parity.py
python3 scripts/inventory-parity.py --check
python3 scripts/inventory-parity.py --action-report tmp/parity-p0/action-reconciliation.md
python3 -m unittest discover -s scripts/tests -v
```

Generation needs only this checkout and Python's standard library. It reads no
manual archive, temporary authoring helper, legacy assembly or sibling repository.
The optional suite retains its separate P0/content/permission gates.

## Source routes and browser destinations

| Source capability | Mapping / later delivery |
| --- | --- |
| Static menus, toolbars, four clock speeds, Open/Save, file drop and MRU | Existing clock/save/session destinations are credited as partial or adapted. New-world providers, historical files and speed/calendar semantics remain P1/P3/P10. MRU/file-drop outcomes adapt through the save/file workflow. |
| New-world providers | Exactly two registered base providers: empty-world dimensions/name and terrain-loader image/dimensions/height/water. `TerrainOnlyNewGameContribution` is an extension class/factory, not another declared base provider. |
| Railway construction | Existing rail/remove/Confirm/Cancel is partial. Slope, special rails, accessory catalogs, electric poles, signals, station/platform construction and cosmetic passage/stairs have named P4/P6 destinations. |
| Train management | Purchase catalog, owned inventory, group moves/sale/deployment and independent timetable management are separate missing P5 workflows. Source context commands, label editing and map reversal are mapped; add accessible Move to group alongside drag/drop. |
| Controllers | Exactly two registered contribution types, manual and TAT. The built-in simple controller is an engine default; group delegation is separately source-only. TAT map picking, station/junction configuration and ordered advanced rules have distinct destinations. Leased browser manual input is partial coverage. |
| Structures, ground and roads | Generic category/design/color/direction/random/height controls, fixed/variable/half-voxel builders, land cover/trading, terraces/cuttings, road styles/accessories/dummy cars and stadium operations have P3/P7/P8 destinations or explicit empty/source-only dispositions. |
| Reports and finance | Sales report periods, font/category ordering, subsidiaries, bank terms/amounts/asset selections, stock quantities/trades/chart scales and portfolio/bankbook windows are mapped. The balance-sheet template is a source placeholder. |
| Options, presentation and audio | Map cut/pan/picking, source-time appearance, errors/duration, station labels, tree visibility, bounding boxes, bundled BGM/Silence, effects and platform bells are individually accounted for. Native allocation/VRAM, fonts, docking and global key polling have portable dispositions. |
| Support and content | Installed-plugin details/dependencies, error/version/recovery information, train-color XML copy/export, Help and Credits/License are distinct mappings. Full source content/runtime acceptance remains P2/P9/P10/P11. |

`MainWindow.OnLoad` merges registered menus, adds special-rail/special-structure
handlers and one shared road construction menu. `DockingContribution` constructs
manifest-directed menu routes for its window classes. Timetable New enumerates
registered controller contributions; New World enumerates new-game contributions;
music and construction selectors enumerate their owning contribution lists.
All 393 declarations and 116 generated children are joined to these routes or
classified as factory/art/state support with no independent action. Artwork and
factories are not counted as additional player commands.

## Source gaps and explicit dispositions

The machine-readable difference register `ACTION-001`–`ACTION-014` retains the
findings and phase owners. These are source findings, not silent fixes:

- **Group inheritance:** the implementation says it does not work and its World
  registration is commented out. Group assignment/reparenting remains reachable;
  P5 must resolve inheritance/fallback/override semantics explicitly.
- **Map wheel:** `MapViewWindow.OnMouseWheel` changes height cut without checking
  Shift. The manual's Shift gesture is not the exact pinned-source binding.
- **Road membership:** three base definitions exist: dh two-lane 15-sprite,
  pc-9801fa two-lane and syosei single-lane. Wiki sidewalk variants are absent or
  unidentified external definitions, not three invented baseline contributions.
- **TAT edit defect:** both advanced rule forms restore the day selector from
  `rule.month`. P5 and independent boundary fixtures must address the discrepancy.
- **Generic construction:** the menu contribution rebinds toolbar index 10;
  `CategoryTreeWnd.ShowForm` relies on an exact English menu label and an unchecked
  lookup. Preserve the capability while adapting that brittle navigation.
- **Empty/broken road extensions:** road-accessory and dummy-car factories/menus
  have no base instance declarations. The compiled road-accessory form references
  an undeclared `drawer`; an inconsistent obsolete dummy controller is not in its
  project's compile list. Empty extension shells do not establish bus simulation.
- **Development tuning:** the parameter dialog and plugin class exist but no
  baseline manifest registers `DevelopConfigurePlugin`. This is source-only UI;
  the reachable LandValue/development simulation remains P7 scope.
- **Profiles:** `Core.parseProfile` returns an unpopulated set. This source gap
  does not cancel the owner's separately required optional-pack build/run path.
- **Footbridges:** the type selector is not consumed, and empty-voxel passage
  creation branches are incomplete. Working slim-platform cosmetic parts and the
  broader manual expectation remain distinguishable.
- **Timetable references and mutation:** Delete leaves a reference-handling TODO;
  advanced rules and platform lane buttons mutate immediately in source. Browser
  authority/epoch/revision/draft behavior needs explicit transactional adaptations.
- **Balance sheet:** the menu is reachable, but `balanceSheet.html` contains literal
  sample values, no computed report or SOAP binding. The plan's full P8 report is
  still required; the placeholder is not verified accounting functionality.
- **Alternate fixed-size builder:** its legality test uses the opposite branch to
  the main construction controller. Preserve the source defect for recipe/use and
  fixture review rather than treating it as another player feature.

Unwired station-page selectors/list, terrain brush-size selector, report graph
and Close placeholders, and an unused tuning textbox have per-control source-only
dispositions. Error presentation, station labels and the registered bounding-box
option are not implicitly excluded with the archived debug-window plugin.

External A4/user-file music and archived debug/video remain explicitly excluded.
No implemented Undo/Redo binding was found; generic command dispatch does not
create an Undo/Redo requirement. Other baseline roots are inventoried as game,
reference, build, content-authoring or historical documentation inputs; experiments
and supplemental forks do not silently add player capabilities.

## Verification and remaining gates

All 35 Python checks pass: the existing 25 inventory/content checks and ten action
coverage tests. New tests reject missing/duplicate reviews, new fields without
events, unassigned contributions, unknown operations, stale source/registry
evidence and missing manual/root links. Base/content/rail inventory reproducibility
remains a separate check from running game scenarios.

The [manual workflow oracle](web-parity-manual.md) and
[online supplement](web-parity-online-manual.md) now have a complete action-source
cross-reference in the maintained map. `MAN-A*` are still pending acceptance cases;
source/prose numeric differences remain P0-04/P0-06. Per-release numeric/fix and save
reconciliation remains in P0-04/P0-05/P0-06, alongside later phase delivery.
No runtime application, browser, performance or native/device acceptance is
established by this source/tooling batch. P0-04 is next; P0 as a whole stays open.
