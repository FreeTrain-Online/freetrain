# ExAV player action reassessment

**P0-03 reassessment executed — 20 October 2026; closure remains open.** Current
source is stable ExAV **2.1.1.12**, archive commit
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk/`.
The source-reviewed differences below replace the corresponding historical
conclusions. The remaining original-FreeTrain workflows are donor candidates,
not approved ExAV behavior. No application behavior changed in this batch.

[Maintained findings](parity/exav-action-rules.json) and the separate
[generated review](parity/exav-action-review.json) preserve source hashes,
registry evidence, candidate destinations, contribution bindings and text.
The [discovery ledger](parity/exav-ledger.json) remains discovery evidence;
this review does not rewrite it or promote its implementation statuses.

## Accounting and review status

| Evidence | Count / meaning |
| --- | --- |
| UI source files | 166; 158 require player-action field/dependency closure; 8 have explicit excluded, experiment or authoring scope |
| Event/command bindings | All 298 accounted for, retaining namespaced discovery IDs |
| Additional surfaces | All 1,108 accounted for, including controls, delegates, shortcuts, custom input and dialog results |
| Contributions | All 482 direct declarations plus one generated child; current P0-02 type/project/source bindings joined, historical routes retained only as donors |
| Reviewed ExAV differences | 14 findings, `EXAV-ACTION-001`–`014`, with pinned source tokens/hashes and phase/destination obligations |
| C#/resx/HTML text candidates | 3,671; 997 contain Japanese; 2,543 have an ASCII English donor candidate |
| Unclassified C# literals | 2,345 of the 3,671; include IDs, paths, formats and non-presentation values, not 2,345 missing translations |
| XML presentation inventory | Separate P0-02 inventory: 977 entries / 641 English donor candidates; do not add these counts as unique strings |

Counts are lexical records, not distinct player capabilities or approved
translations. Every action has a source-file candidate workflow or explicit
scope disposition. Exact historical surface-expression matches retain the old
per-control effect as a **donor**, with field/dependency review still pending.
Contribution identity matching similarly does not prove a consumer. No default
mapping is called implemented, verified or semantically unchanged.

## Findings that change the action specification

All paths in this table are relative to pinned ExAV `trunk/`. Machine-readable
findings supply exact tokens, hashes and destinations. `missing:` and
`disposition:` are planning labels, not web operation IDs.

| Finding | ExAV evidence and browser obligation |
| --- | --- |
| 001 — Station information/list | `Station.onRightClick` and StationPropertyDialog's Information button open live name, boarding/alighting/train-call counters today/yesterday, and actual waiting passengers versus demand. The station-list manifest registers **dockingContent**, consumed by DockingContribution; it is not registered through the spare StationListPlugIn class. Its sorted list updates on world/station changes and double-click moves maps. Use stable station IDs and accessible list actions in P6/P9. |
| 002 — Train information/tracking | `system/plugin.xml` registers TrainInformationWindow through dockingContent. It offers nested train/group selection, name/state, car count, capacity, passenger percentage and Move. `MapView` separately creates `views/map/TrainTrackingWindow`, with continuous-tracking checkbox. Existing inspector/find-train covers only part of these P5/P9 workflows. The old core project path gap does not erase the current manifest route. |
| 003 — Speed limits | SpeedLimit's menu initializes the subsystem and supplies level section, slope section and orphan cleanup. Add/remove, endpoints, low/medium/high choices and slope directions need P4/P5/P9 workflows. Exact values/movement and slope boundary cases remain P0-04/06; these choices are distinct from simulation-clock speed. |
| 004 — Terrain editing 2 | Registered Mountain2 specialStructure supplies rectangular raise/excavate and Ctrl repair of mismatched corners. Preserve it separately from ordinary corner editing and heightmap import. Provide an explicit accessible repair action in P3/P9. |
| 005 — Report history | AccountSummaryWindow has today/yesterday, this/last month and this/last year. Context actions open 31-day and 12-month histories, with sales/expenditure/balance, genre selection and font choice. P8/P9 must expose these; P0-05/06 owns rollover/state fixtures. A Close label without a handler is still a gap. |
| 006 — Timetable copy/files | Manager adds controller copying; advanced station rules add copy, confirmed clear-all, `.ftdd` read/append/save, duplicate time-mask checking on Add and forwarding/reverse variants. Copy/import bypasses and sorted display versus execution order need fixtures. Adapt BinaryFormatter clone/file routes to validated authoritative commands and drafts. The day-from-month edit defect still requires disposition. |
| 007 — Platform/passage choices | Wide, slim roofed and slim unroofed radios replace the old slim checkbox; station/platform costs and charges are explicit. Concrete/brown-wood/green-wood passage selection **is consumed** in previews/outlooks: historical ACTION-009's unused-selector claim is superseded. Empty-voxel construction gaps remain separate. P4/P6. |
| 008 — Options | Train labels, poles, station/train font/color, bankruptcy notification and liquidity increments 10^10/10^11/10^12 join existing options. Fonts/visibility adapt to local presentation; bankruptcy changes require shared authority. Native allocation remains a portable disposition. P1/P8/P9; no local preference may change shared economy rules. |
| 009 — Direct buildings | MainWindow's commercial/variable-height entry commands are commented out; generic picker enumerates GenericStructureContribution. The 66 commercial/55 variable-height declarations remain required content with development consumers. Direct placement needs an explicit adaptation; surviving classes are insufficient reachability evidence. Variable-height builder/remover stubs remain. P7/P9. |
| 010 — Road extension defect | ExAV's RoadAccessory preview correctly declares `drawer`; historical ACTION-006's undeclared-variable claim does not transfer. No bundled RoadAccessory/DummyCar instance types establish working bus simulation. Empty/null selection and obsolete controller questions remain explicit. |
| 011 — Generic navigation | CategoryTreeWnd uses the Japanese menu label; historical ACTION-005's exact English-label claim does not transfer. Label/index lookup is still brittle, and category callbacks depend on a subscriber. Preserve stable identities while localizing presentation. P7/P9. |
| 012 — Save UI | ExAV warns for `.ftgd` and changes filters; BZ detection survives while the BZip2InputStream wrapping line is removed. Recognized extensions do not prove import support. P0-05/P10 must reconcile formats, compression and continuation. |
| 013 — External music | Temporary user-file BGM/fileName remains source evidence under the owner's external-music exclusion. Bundled BGM, silence, effects and departure bells remain required. No automatic optional-pack inclusion or external music redistribution. |
| 014 — Development tuning | `system/plugin.xml` **registers `{develop-config}`** and DevelopConfigurePlugin merges its menu. Historical ACTION-007's source-only classification is superseded. Apply, Cancel, Save settings and Load settings operate development/land-value parameters; source preferences are separate from the world and require explicit reload. P7/P9/P10 must define shared authority, validation and continuation policy. Source Apply can partially mutate before parse failure; use validated atomic commands. |

## MAN-* and DEV-* reconciliation

The historical map's 45 MAN-T/M/W/O family links are preserved under
`historicalManualWorkflowCandidates`, without silently approving them. Findings
link affected families; reciprocal per-action links remain a P0-03 closure task.
Read [Word/manual](web-parity-manual.md) and [online/manual](web-parity-online-manual.md)
through this current overlay:

- MAN-M02/MAN-W10 need the ExAV train/station information, station-list and
  separate tracking routes; MAN-T04/MAN-W03–06/MAN-O07/O16 need current station
  counters, roof choices and consumed passage materials. Older illustrations are partial.
- MAN-T08/MAN-W09/MAN-O09 need controller/rule copying, `.ftdd` files and extra
  station actions. MAN-T09/MAN-O06 needs Mountain2 as well as the ordinary corner
  editor; MAN-T02/T03/MAN-M03 needs speed-limit tools. These are additions to the
  workflow families, not claims that the manuals describe ExAV's new fields.
- MAN-M06 needs ExAV bankruptcy options; the View/settings families also need
  registered development tuning. MAN-A20's accounting/menu exercise includes all
  six report periods and both history windows (MAN-M02/MAN-W10/MAN-O03).
  These are pending feature cases, not executed journeys.
- DEV-P02–22/P25–27 and DEV-A01 join the P0-02 factory/type bindings to actual
  consumers; the current review retains all declarations, including support-only
  factories/art and the one generated child. Dynamic consumer decisions are still
  pending per declaration; same-ID historical routes are not closure evidence.
- DEV-D01–03's original passenger/fare/cost findings must **not** supply ExAV
  expected numbers: packed/seated capacity, configurable fare/amenities, calendar
  and changed costs need P0-04–06 derivation. DEV-D04/05's rendering rules remain
  P0-02/P0-04 evidence with their own source/dependency checks.
- DEV-D06/P35/P39–42 proposals do not create bus/economy/consist/fare features.
  Conversely, a documented proposal is no reason to exclude ExAV's registered
  tuning window or source-supported fare fields. ExAV code determines the boundary.

## English/Japanese text and UI reference

Text entries retain source file/hash, a literal/statement occurrence key or resx
resource key, original text/hash, source expression/line where available, donor
revision/path/hash/text, comparison and explicit review status. C# escapes remain
source spellings; this is not a C# evaluator. Conservative HTML text-node and resx
string candidates include support/authoring text; binary resources are omitted.
String concatenations, indirect resources and runtime-generated messages still
need semantic classification. The inventory does not claim every literal is
player-visible or every donor means the same thing.

Proposed English remains null until reviewed with its feature. The maintained
rules add provisional terminology for total versus seated capacity, actual versus
demand, speed categories, out-of-service operation and daily/monthly financial
history. Preserve Japanese text, IDs and data. Current web localization is hashed
as a donor/reference; no translated gameplay code is imported.

Avalonia `freetrain-modern/main` was inspected at
`82bf78c3e1902805626b4444e6e8b8d160a89a5b`: `MainWindow.Layout.cs`'s toolbar,
rail type/height context controls, searchable catalog and option groups;
`MainWindow.Catalog.cs`; and `PluginLocalizationCatalog.cs`. Exact path/hash
references are in the rules/review. These inform browser workflow and wording;
they do not define ExAV gameplay or authorize replacing the delivered web shell.

## Reproduction, validation and remaining work

```sh
python3 scripts/inventory-parity.py --source exav --source-root /path/to/freetrain-exav-archive --check
python3 scripts/reconcile-exav-actions.py --source-root /path/to/freetrain-exav-archive
python3 scripts/reconcile-exav-actions.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests -v
```

Generation requires an explicit source repository and reads the configured commit,
not HEAD or corrected working files. It checks the discovery file hashes, finding
tokens and delivered operation IDs, and hashes current inputs/registry/localization.
The separate output is byte reproducible. Ordinary tests do not discover siblings.

Fresh validation for this batch: **73 Python tests passed**, including six new
action-review checks. ExAV discovery, content and action `--check` commands passed,
as did `git diff --check`. No desktop/browser/runtime acceptance was run.

**P0-03 remains open** until the 158 player-source reviews have per-control effects
and dependency/registration closure, all dynamic routes and reciprocal MAN/DEV
links have dispositions, and conservative text candidates have presentation/donor
classification. Wording delivery, executable scenarios, gameplay, browser and
native/device acceptance remain with their respective feature phases. P0-04–09
and the overall P0 gate also remain open. The explicit remaining-closure list in
the rules/review is the next-work queue, not an automatic completion assignment.

## Historical original-FreeTrain review

The rest of this document preserves the 19 October original-source review.
Its counts, reachability claims, ACTION-* differences and verification refer to
that source only. In particular, its development-tuning, passage-selector,
English-menu-label and undeclared-drawer findings are superseded above for ExAV.

Historical P0-03 source mapping completed 19 October 2026 against original FreeTrain `1f755ca` and the
delivered operation registry at `8e4c6d4`. This closed the original-FreeTrain action-inventory item,
not P0, gameplay parity, P9 workflow delivery or native/device UX acceptance.
The [port plan](../tmp/web-feature-parity-port-plan.md) keeps those gates separate.

## Maintained review and generated evidence

[`parity/historical/action-map.json`](parity/historical/action-map.json) is the maintained review input.
Its schema version 2 records 75 workflow destinations/dispositions, 180 pinned
source reviews, all 248 original event/command binding decisions, 985 additional
source-surface decisions, all 509 declaration/generated-child routes and reciprocal
links for all 45 `MAN-T*`, `MAN-M*`, `MAN-W*` and `MAN-O*` requirements.
Every baseline root also has an explicit disposition.
All 26 post-manual release groups have action-level source/workflow links; the
pinned baseline's 15 `whatsnew.txt` notes have individual UI/content/no-functional-
change dispositions. These are not verification of each historical bug fix.

[`parity/historical/action-review.json`](parity/historical/action-review.json) is reproducible output.
The original binding IDs remain in [`base-ledger.json`](parity/historical/base-ledger.json),
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
