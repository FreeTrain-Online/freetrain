# ExAV player action reassessment

**P0-03 workflow/scope reconciliation complete — 20 October 2026.** Current
source is stable ExAV **2.1.1.12**, archive commit
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk/`.
The owner accepted workflow/consumer scope closure now, with detailed review
inside each implementation slice. Current source routes and phase ownership are
reviewed; historical per-control effects remain donors until reviewed with their
feature. This closes the revised P0-03 task, not P0 or gameplay parity.
No application behavior changed in this batch.

[Maintained findings](parity/exav-action-rules.json) and the separate
[generated review](parity/exav-action-review.json) preserve source hashes,
registry evidence, reviewed scope destinations, contribution consumers and text.
The [discovery ledger](parity/exav-ledger.json) remains discovery evidence;
this review does not rewrite it or promote its implementation statuses.

## Accounting and review status

| Evidence | Count / meaning |
| --- | --- |
| UI source files | 166; 154 retain feature-detail obligations; 12 have explicit support, source-only, excluded, experiment or authoring dispositions |
| Event/command bindings | All 298 accounted for, retaining namespaced discovery IDs |
| Additional surfaces | All 1,108 accounted for, including controls, delegates, shortcuts, custom input and dialog results |
| Contributions | All 482 direct declarations plus one generated child have explicit maintained consumer/support/scope decisions; P0-02 implementation/project bindings retained |
| Workflows and consumer groups | 87 workflow families assigned to 46 source-backed route groups, each with destination/disposition and phase owner; these are not 87 delivered features |
| Bundled plugin dependencies | All 56 manifests have resolved direct/transitive bundled dependency IDs; no missing target or cycle |
| Reviewed ExAV differences | 15 findings, `EXAV-ACTION-001`–`015`, with pinned source tokens/hashes and phase/destination obligations |
| C#/resx/HTML text candidates | 3,671; 997 contain Japanese; 2,543 have an ASCII English donor candidate |
| Unclassified C# literals | 2,345 of the 3,671; include IDs, paths, formats and non-presentation values, not 2,345 missing translations |
| XML presentation inventory | Separate P0-02 inventory: 977 entries / 641 English donor candidates; do not add these counts as unique strings |

Counts are review records, not distinct player capabilities or approved
translations. Every binding/surface has a workflow assignment or explicit scope
disposition. Exact historical expression matches retain their old effects only
as donors: scope assignment does not approve those semantics. All contribution
consumer assignments are explicit maintained records; matching an old ID no longer
supplies the decision. Implementation status remains `inventoried`.

## Registration, consumer and dependency decisions

The maintained rules assign every declaration by coverage ID, manifest hash,
contribution type and expected implementation name. The generator rejects missing,
stale or unknown assignments. Source tokens and file hashes bind each route;
P0-02 class/project/recipe bindings remain attached to the same identities.

| Source route | Scope and phase decision |
| --- | --- |
| Plugin.loadContributions → factory.load → PluginManager | Factory definitions register types; pictures, sprite factories/loaders, palettes, car definitions and templates are support to owning recipes, not separate commands. P2/P5/P7. |
| MainWindow.menus → mergeMenu | Speed limits, bank/stocks, generic/half-voxel tools, road extension shells, poles/signals, land-value/color tools and development tuning each have assigned workflows. No registration is inferred from a surviving class alone. |
| DockingContribution → manifest class/window creator | Nine declarations, including excluded debug, retain manifest-directed routes. StationList, TrainInformationWindow, reports, controller management, categories, bankbook, holdings and subsidiaries are explicitly accounted for. Native docking adapts to the existing browser panels. |
| MainWindow.specialRails / specialStructures | Four special rails and three special structures route to their handlers/providers: bridge/girder/tunnel/garage and Mountain2/terrace/stadium. P3/P4/P5/P7/P8. |
| NewWorldDialog / controller manager | Exactly two declared new-game providers and two declared train-controller providers. Engine simple/delegation singletons are accounted separately; they are not extra manifest providers. P3/P5. |
| Construction/property selectors | Land/fence/random wrappers, roads, rail accessories, station buildings, poles/signals, half-voxels, departure bells and report genres remain options within their owning workflows. P3–P8. |
| Commercial / variable-height definitions | Development consumes 66 + 55 direct definitions; removed direct menu bindings and builder stubs have a planned P7 direct-placement/catalog adaptation. Required content is retained. |
| Generic catalog | Registered picker is reachable, but bundled generic content consists of null-contrib and its one generated child. Do not treat the direct building families as generic picker options. P7 owns the populated catalog adaptation. |
| Road accessory / dummy-car shells | Registered menus, zero bundled instance definitions: retain empty-extension/API disposition. No working bus requirement follows. The obsolete DummyCarController is uncompiled/source-only. |
| StationListPlugIn | Compiled spare menu class has no selected manifest/production instantiation; classify its duplicate route source-only. The actual StationList docking route stays in scope. |
| Music and exclusions | Bundled BGM/silence/effects/bells required. A4 factory's nine local children and temporary user-file BGM stay excluded. Debug is excluded; video is absent from stable trunk. |
| Static/unsupported surfaces | Balance sheet is a reachable placeholder with planned computed P8 report; profile parsing remains incomplete; no implemented Undo/Redo binding was found. These dispositions do not cancel the owner's separate optional-pack support. |

The generated dependency closure resolves all 56 manifest dependency sets within
bundled content, including generic → structure.c477 and forest → farmland. This is
source/load topology, not proof that the Windows build or runtime succeeds.
`EXAV-UI-DEP-01`–`05` assign remaining build/interface/state obligations:

- Preserve P0-02's source/project bindings and five resource-project diagnostics,
  including TAT's missing Straight.bmp; old core Compile omissions remain visible.
- EntityBuilderContribution and TwoDimStructure lack selected-project membership.
  Required behavior/recipes stay in scope; portable interfaces/rendering are
  resolved in P1/P2/P4/P7 rather than silently assuming desktop buildability.
- Windows docking, DirectXWrapper, global keyboard polling, fonts/allocation and
  embedded browser controls require portable adaptations within the delivered
  shell, authority, focus, draft/readiness and lease contracts.
- Delegation defects, building entry/stubs, empty extensions, development Apply,
  timetable serialization and the static balance sheet have named phase owners.
  Detailed rule/save/transaction closure follows P0-04–06 and owning slices.

## Findings that change the action specification

All paths in this table are relative to pinned ExAV `trunk/`. Machine-readable
findings supply exact tokens, hashes and destinations. `missing:` and
`disposition:` are planning labels, not web operation IDs.

| Finding | ExAV evidence and browser obligation |
| --- | --- |
| 001 — Station information/list | `Station.onRightClick` and StationPropertyDialog's Information button open live name, boarding/alighting/train-call counters today/yesterday, and actual waiting passengers versus demand. The station-list manifest registers **dockingContent**, consumed by DockingContribution; it is not registered through the spare StationListPlugIn class. Its sorted list updates on world/station changes and double-click moves maps. Use stable station IDs and accessible list actions in P6/P9. |
| 002 — Train information/tracking | `system/plugin.xml` registers TrainInformationWindow through dockingContent. It offers nested train/group selection, name/state, car count, capacity, passenger percentage and Move. `MapView` separately creates `views/map/TrainTrackingWindow`, with continuous-tracking checkbox. Existing inspector/find-train covers only part of these P5/P9 workflows. The old core project path gap does not erase the current manifest route. |
| 003 — Speed limits | SpeedLimit's menu initializes the subsystem and supplies level section, slope section and orphan cleanup. Add/remove, endpoints, low/medium/high choices and slope directions need P4/P5/P9 workflows. Values/movement and slope boundaries are specified by [P0-04](web-parity-geometry.md); independent cases remain P0-06; these choices are distinct from simulation-clock speed. |
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
| 015 — Group delegation | Non-temporary World registers DelegationTrainControllerImpl, and TrainPlacementController binds World.trainControllers. Historical ACTION-001's unregistered/source-only disposition is superseded. The defect warning remains: resolve null/root fallback, assignment/reparenting, cycles and continuation in P5/P10. |

## MAN-* and DEV-* reconciliation

The historical map's 45 MAN-T/M/W/O family links are preserved under
`historicalManualWorkflowCandidates`, without silently approving them. Findings
link affected families; granular reciprocal action/source links are mandatory
within the implementing slice, rather than a prerequisite for P0-03 closure.
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
  These are pending feature cases, not executed journeys. MAN-O08/MAN-A17 must
  use EXAV-ACTION-015's registered delegation route and retain its defect cases.
- DEV-P02–22/P25–27 and DEV-A01 join the P0-02 factory/type bindings to actual
  consumers; the current review retains all declarations, including support-only
  factories/art and the one generated child. All declarations now have current
  consumer/support decisions; detailed authoring-format and per-control links
  follow the relevant slice. Same-ID historical routes are not semantic evidence.
- DEV-D01–03's original passenger/fare/cost findings must **not** supply ExAV
  expected numbers: packed/seated capacity, configurable fare/amenities, calendar
  and changed costs need P0-04–06 derivation. DEV-D04/05's rendering rules remain
  P0-02/P0-04 evidence with their own source/dependency checks.
- DEV-D06/P35/P39–42 proposals do not create bus/economy/consist/fare features.
  Conversely, a documented proposal is no reason to exclude ExAV's registered
  tuning window or source-supported fare fields. ExAV code determines the boundary.

## P3-01 terrain workflow bindings

**27 October 2026, specified:** [terrain contracts and decisions](web-parity-terrain-contract.md)
and [source/workflow/slice mappings](parity/terrain-review.json) now bind the
new-world, terrain, rectangle/repair, terrace/cut, forest/creek and ground-picking
families. MAN-M01/O02/E02/A02/A19 map to P3-06/07; MAN-T09/M05/O06/A14/A18 to
P3-13/14/15; MAN-M02/W11/D02 to P3-05 and later cross-system cut acceptance;
MAN-A16 supplies authority/draft behavior for every affected slice. EXAV-13 maps
to the corner/rectangle/terrace source rules and explicit atomic publication policy.

The historical developer IDs retain precise meanings for this slice:
**DEV-P07** land builders, **DEV-P09** new-game providers, **DEV-P10** pictures,
**DEV-P15** special structures, **DEV-P17** fixed terrain-map providers and
**DEV-P28** voxel boundaries. The [contract's reciprocal table](web-parity-terrain-contract.md#workflow-and-text-handoff)
links them to actual source consumers and P3 runtime owners. These historical
pages are format/context donors, not gameplay authorities. The source-bound
translation review and independent fixtures are specified; the operation registry
and browser workflows remain unimplemented and no runtime coverage credit changes.

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

## Incremental feature completion

**Owner-approved staging — 20 October 2026:** P0-03 establishes what is in scope,
how it is registered/consumed, its browser destination or disposition, and phase
ownership. The 154 remaining source-detail records and conservative text candidates
are feature work, not abandoned reviews. Every implementing slice must:

1. Resolve its controls/actions, defaults, validation and configuration scope
   against ExAV, and complete the detailed dependency review needed by that behavior.
2. Attach reciprocal MAN/DEV requirement/source/workflow links for that feature,
   recording prose/source differences, exclusions and proposal/authoring scope.
3. Classify relevant C#/resx/HTML/XML strings as presentation or non-presentation
   with reasons, review English donor meaning, and deliver English/Japanese text,
   interpolation and layout checks while preserving IDs and source data.
4. Record authority/draft/atomicity adaptations and source defects, then verify
   relevant functional/content/save/recovery cases with independent expectations.
5. Update review and implementation evidence separately. Deferred details cannot
   be counted complete because P0 scope closed; P9/P11 reconcile all residual work.

These requirements are also machine-readable in `sliceCompletionRequirements`.
P0-04/P0-05 source specifications and P0-06 scenarios subsequently completed;
[P0-07 reporting and provisional sizing](web-parity-report.md) completed on
21 October. P0-08 [optional source/specification](web-parity-optional-plugins.md)
is also complete. [P0-09 source integration](web-parity-exav-integration.md) subsequently completed; the overall P0 gate stays open, as do native/device and runtime gates.
No grand per-control specification or full translation pass is required before
starting feature implementation.

## Reproduction and validation

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

Fresh validation: **77 Python tests passed**, including ten action-review checks.
ExAV discovery, content and action `--check` commands and `git diff --check` passed.
No desktop/browser/runtime acceptance is claimed. The generator also rejects
unassigned contribution consumers, changed implementations and unresolved/cyclic
bundled dependency IDs. Granular detail is tracked by the slice requirements above;
`remainingClosure` is empty for the revised P0-03 scope task.

## Historical original-FreeTrain review

The rest of this document preserves the 19 October original-source review.
Its counts, reachability claims, ACTION-* differences and verification refer to
that source only. In particular, its group-delegation, development-tuning, passage-selector,
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
established by this source/tooling batch. P0-04 source specification is now complete; P0-05 source specification is now complete; P0-06 is next and P0 as a whole stays open.
