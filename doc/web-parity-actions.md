# ExAV player action reassessment

**P4-01 source contract — 30 October 2026:** [Railway operation contracts,
decisions and source/text evidence](web-parity-railway-contract.md) refine the
railway routes below. No live railway operation is enabled; P4-02 fixtures are
next. See the [reciprocal railway bindings](#p4-01-railway-workflow-bindings).

**Owner audio decision — 27 October 2026:** [BGM is deferred to a future improvement project](web-audio-policy.md). TinySynth demonstrated feasibility but produced audible pops/crackles and has been removed with its player and music controls. Sound effects and six bell previews remain. Digital audio, including reimagined MIDI compositions, is allowed; BGM/MIDI playback is no longer a P2/P9 or parity exit requirement.

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
| Music and exclusions | Effects/bells required; bundled BGM/silence deferred under [owner audio policy](web-audio-policy.md). A4 factory's nine local children and temporary user-file BGM stay excluded. Debug is excluded; video is absent from stable trunk. |
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
| 013 — External music | Temporary user-file BGM/fileName remains source evidence under the owner's external-music exclusion. Effects and departure bells remain required; bundled BGM/silence are deferred under [owner audio policy](web-audio-policy.md). No automatic optional-pack inclusion or external music redistribution. |
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
| Options, presentation and audio | Map cut/pan/picking, source-time appearance, errors/duration, station labels, tree visibility, bounding boxes, effects and platform bells are individually accounted for; bundled BGM/Silence are inventoried but deferred. Native allocation/VRAM, fonts, docking and global key polling have portable dispositions. |
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

P3-14 delivers the Mountain2 portion of MAN-T09/MAN-O06/MAN-A18 and DEV-P15/P28
through [Rectangle and repair](web-parity-rectangle-terrain.md): explicit
Raise/Excavate/Repair, coordinate or two-point selection and reviewed Ctrl/Shift
semantics, with authoritative skips/loss preview and atomic confirmation. This
source-derived slice does not close terrace/map-construction or broader P3/P9
workflow/native acceptance.


## P3-16 map authoring workflow

[Delivered authoring mode and scenery controls](web-parity-authoring.md) live in
Game → Modify terrain. Owner entry/exit uses captured world/geometry confirmation;
Terrain selection → Author forests and creeks provides unowned initialization,
all six tree species × three ground treatments, and source decorative creek
routes. Existing corner/rectangle/repair/terrace/cut actions remain available
outside this mode. Registry IDs `authoring-enter`, `authoring-exit`,
`authoring-confirm`, `authoring-species`, `authoring-ground` and `authoring-preview`
join the existing terrain Confirm/Cancel and coordinate actions. EN/JA keyboard,
map targeting and narrow forms use the existing tool card and canvas lifecycle.
Owned forest purchases/booking, land acquisition and generic demolition retain
their later subsystem scope; mode entry does not bypass their rules.

## P3-17 integrated terrain and paint

[Integrated acceptance and P4 handoff](web-parity-p3-acceptance.md) reconcile the
new-world, corner/rectangle/repair, terrace/cut, forest/creek and picking/water
workflow bindings above. Their MAN/DEV IDs retain their existing meanings; no new
EXAV-SCN IDs are allocated for procedural geography. The owner-requested Paint
terrain mode immediately repeats validated quarter-corner Raise/Lower while the
left button is held. It is a new gesture extension of `terrain-action`,
`terrain-preview` and `terrain-confirm`, with `terrain-paint-stop` as its explicit
stop action. Ordinary Shift strokes keep their preview/confirmation behavior.
EN/JA help explains immediate changes, possible forest/ownership loss and stop
conditions; numeric coordinates preserve the keyboard alternative.

## P4-01 railway workflow bindings

The [railway contract](web-parity-railway-contract.md) and
[hashed review](parity/railway-review.json) own legality, actual mutation/booking
and subtype removal. CORE-03/06, EXAV-09/11/13 and the existing action map retain
these feature destinations; source-only specification is not operation delivery.
The [manual overlay](web-parity-manual.md#p4-01-railway-source-reconciliation)
contains the reciprocal assignments.

| Family / P4 operation records | Requirements and historical developer scope | Source consumer / destination |
| --- | --- | --- |
| rail: normal-route, attachment-detachment, piers | CORE-03, CORE-06, EXAV-11, EXAV-13; MAN-T02/M03/W01/W02/A03/A14; DEV-P24 costs, DEV-P28 voxel bounds | RailRoadController → SingleRailRoad/JunctionRailRoad/BridgePierVoxel; P4-06–09 |
| slope: slope, piers | CORE-03, CORE-06, EXAV-11, EXAV-13; MAN-T03/W02/A04/E12; DEV-P26 sprites, DEV-P28 bounds | SlopeRailRoadController → SlopeEntity/eight cells/stored flags; P4-10/11 |
| special: bridge, girder, tunnel, garage | CORE-03, CORE-06, EXAV-11, EXAV-13; MAN-M03/W02/A04/E12; DEV-P14 special rails, DEV-P24 costs | SpecialPurposeRailController/AbstractLineController → four concrete contribution handlers; P4-12–15. Road-derived native quote is not authoritative. |
| signal: signal | CORE-03, EXAV-11, EXAV-13; MAN-M03/E12/A12; DEV-P08 menus, DEV-P11 signal loader/side | SignalRailController → SignalRailRoad/RailSignalContribution; P4-16, P5 controller boundary |
| pole: pole | CORE-03, EXAV-11; MAN-M03/A12; DEV-P06 poles, DEV-P10 pictures | ElectricPoleContribution builder/remover → TrafficVoxel accessory; P4-17 |
| accessory: stationary-accessory | CORE-06, EXAV-11, EXAV-13; MAN-T05/M03/A12; DEV-P12 fixed railway structures, DEV-P26/28 sprites/bounds | StationaryStructPlacementController → active FixedSizeStructController → ConstructionSite → RailStationaryStructure; P4-18/P7 shared construction lifecycle |
| speed-limit: speed-limit-flat, speed-limit-slope | CORE-06, EXAV-09, EXAV-13, EXAV-ACTION-003; MAN-M03/A12/A16; DEV-P08 menu | SpeedLimit menu → flat/slope controllers/manager → per-world lists; P4-19, P5 timing |
| crossing: crossing | CORE-03, EXAV-13; MAN-E05/E11/E12/A14; DEV-P13 road format boundary, DEV-P28 bounds | TrafficVoxel.shouldHaveCrossing → RRCrossing; P4-20/P7, trusted car/platform interfaces only |

All requirements in the compact table use their full MAN/DEV identifiers in the
machine review. MAN-T02, MAN-M03, MAN-W01, MAN-W02, MAN-A03, MAN-A14; MAN-T03,
MAN-A04, MAN-E12; MAN-A12; MAN-T05; MAN-A16; MAN-E05 and MAN-E11 are the explicit
manual anchors. DEV-P06, DEV-P08, DEV-P10, DEV-P11, DEV-P12, DEV-P13, DEV-P14,
DEV-P24, DEV-P26 and DEV-P28 keep their historical identities; old prose is
context/donor material and does not override pinned ExAV callers.

[78 source text dispositions](parity/railway-localization-review.json) preserve
Japanese line/expression/hash and the existing historical English-donor records.
Owning slices add/review EN/JA resources and browser-specific cost/skip/loss,
confirmation and rejection wording; P4-22 closes translation reconciliation.
Catalog metadata stays under exact P2 contribution identities, including the 64
bundled definitions enumerated here. No invented web operation ID is registered
by this review. RAIL-D006/GEO-004 records the owner's 30 October decision to
retain source deletion of all piers below regardless of owner, with every affected
pier shown in the authoritative preview; no ownership approval gate remains.

## P4-09 normal-track workflow

[Track controls, source/translation reconciliation and verification](web-parity-normal-rail-tool.md)
implement MAN-T02/W01/A03's normal-rail route, attachment, branch and removal
subset, with DEV-P24 source booking and DEV-P28 stored voxel bounds. Registry
`rail-action`, `rail-route`, `rail-from-x/y`, `rail-to-x/y`, `rail-pick-from/to`,
`rail-preview`, `rail-level`, existing `confirm` and `cancel-draft` bind the
controls to an authoritative preview and FIFO `editRailway`. Removal includes
all lower-pier losses; explicit level/height-cut, EN/JA help, roles, receipts and
late-response guards ship together. The bounded two-route heuristic is an owner
extension. MAN-M03 speed limits and slope/special infrastructure retain their
later P4 owners; these normal-route tests do not close those requirements.

### P4-11 slope workflow

The distinct [Slope task](web-parity-slope-tool.md) implements MAN-T03/A04 with
cardinal climb/base/level selection, map and keyboard targeting, all eight ghost
members, source cost/support/loss preview and atomic confirmation/removal. Any
member, including invisible fillers and support cells, resolves the whole ramp;
normal-track removal retains its distinct source behavior. Surface/underground
art, stored ground holes, cuts, two-peer changes and exact reopen are verified
at the browser/portable scope recorded there. RAIL-TEXT-015–020 feature wording
is reconciled in EN/JA; overall P4-22/native and P5 movement gates remain open.


## P5-01 train/controller workflow bindings

**2 November 2026 — source specification only.** The
[P5-01 contract](web-parity-train-contract.md),
[operation/dependency/decision evidence](parity/train-review.json) and
[source-bound text review](parity/train-localization-review.json) supply the
current train/controller semantics. These reciprocal bindings cover actual
controls and source consumers; they do not credit runtime or browser delivery.
Historical manuals/DEV articles are workflow/donor evidence, with stable ExAV
source deciding V2 fare, capacities, movement cost, timers and registered delegation.

| Operation | Requirements | Implementing owner | Controls and source obligation |
| --- | --- | --- | --- |
| `stock` | MAN-W07, DEV-P18, DEV-P19 | P5-03 | company/type/nickname catalog; permitted length; price/native speed/fare/amenity/triprange; normal/seated/packed capacity. Preserve configured V2 fields, amenity clamp, explicit compositions and reverse/color references; one-car Param tail wins. |
| `trade` | MAN-T07, MAN-W07, MAN-A10, MAN-A11, DEV-P19, DEV-P24 | P5-06 | length × quantity quote; buy; sale quote/confirm; purchase timestamp. One purchase batch/date and one RAIL_SERVICE spending entry; nullable-date sale uses completed365-day price/(years+2). UI sale requires withdrawal while domain sell can remove placed stock. |
| `deploy` | MAN-T06, MAN-W08, MAN-A11, DEV-P14 | P5-07 | select owned train; map/coordinate tail anchor; source initial orientation; reverse selected formation; withdraw. Tail-first walk with temporary head guide, no slope zdiff, duplicate/missing/occupied rejection; native initial rr.dir1. Additional arbitrary heading preview is a browser adaptation only for a valid equivalent reversed pose. |
| `movement` | MAN-A11, DEV-P14 | P5-08 | run/pause/step; live state/reason. Guide then incoming-heading zdiff; border before reciprocal track; occupied destination including own tail blocks. Head-to-tail old-state shift is atomic. |
| `reversal` | MAN-W08, MAN-A11, DEV-P18 | P5-12 | explicit reverse; emergency-state reason. guide().opposite per Inside car then reverse array/toggle; missing connection reverses once, blocked connection does not. Preserve original car index/type identity. |
| `timing-cost` | MAN-A11, MAN-A12, DEV-P24 | P5-13 | speed-limit feedback; operating costs. Capture old Inside head before any handler mutation; positive wait returns first; Outside/native path bypasses local limits. Charge successful moves1/5/9 etc including countdown. |
| `outside` | MAN-W08, MAN-A11 | P5-14 | Outside state/countdown; return status. 100 countdown, exchange at resulting50 before chain shift, attempt after counter0, missing-track retry10; first connected candidate wins before occupancy. Return/opposite formation and forwarding follow source. |
| `simple` | MAN-T08, MAN-W09, MAN-A12, DEV-P20 | P5-16 | built-in default assignment; read-only defaults. Junction straight; only Station harbor first wait30 then0, signal/nonstation ignored. |
| `manual` | MAN-T08, MAN-W09, MAN-A12, DEV-P20 | P5-17 | left/right; Shift pass; Up depart; Down reverse; lease/status/reset. Valid-left before valid-right; first Shift0 else5, later Up0 before Down-1 else5. Do not poll server/native keyboard or persist held input. |
| `manager` | MAN-T08, MAN-W09, MAN-A12, DEV-P20 | P5-18 | new/provider; configure; rename; deep copy; delete with usages; train/group assignment. Independent shared controller identities; built-ins not deletable plugin instances; source deep-copy intent becomes typed copy, referenced deletion rejected until explicit reassignment. |
| `tat-junction` | MAN-T08, MAN-W02, MAN-A12, DEV-P20 | P5-19 | pick junction; default straight/curve; month/day/weekday/hour masks; add/replace/up/down/remove. Location-keyed junction default straight, first matching stored rule; UI sets minute wildcard-1; only missing-junction entries refreshed. |
| `tat-stop` | MAN-T08, MAN-W09, MAN-A12, DEV-P20 | P5-19 | pick harbor via station/platform/signal; pass/fixed/daily/advanced; duration; daily time; reverse on departure. Location-keyed harbor on chosen train controller; unconfigured default10; daily equality waits1440. Signals consult effective train controller, not a signal-owned override. |
| `tat-advanced` | MAN-A12, MAN-O09, DEV-P20 | P5-20 | five mask fields; eight actions; add/replace/copy; up/down/remove; confirmed clear-all. Stored order; initial pass/stop only, later go/reverse/forwarding/stop; next ten-minute boundary polling. Add duplicate mask guard differs from replace/copy/import. Sorter is commented out. |
| `turning` | MAN-A11, MAN-A12 | P5-21 | reverse2; reverse-forwarding2. checkTurn mutates before return; preserve early reversal and special one-car nonsharp return-1000, then yard effects then central sentinel handling. Re-place cars without remove/place train callbacks. |
| `groups` | MAN-W08, MAN-O08, MAN-A17 | [P5-22 delivered](web-parity-fleet-groups.md) | create/name group; move-to-group; choose parent timetable; per-train override; effective controller. Registered delegation, not a source-only alternative. Root explicit simple; null means simple fallback; reject cycles/missing references. No source group-delete action; retain no-delete disposition. |
| `diagram` | MAN-A12, MAN-O09 | P5-23 | read/append preview; portable save/export; copy rule; clear confirmation. Source BinaryFormatter RuleCollection append/save; commented SOAP inactive. Use bounded inert reader and explicit portable JSON export, not a promise of native binary writer compatibility. |
| `information-tracking` | MAN-M02, MAN-W10, MAN-A13 | P5-25 | nested select; name/state/length/capacity/passengers%; Move; continuous follow; local train labels/font/color. Information/Move distinct from map-local tracking; normal-capacity denominator can yield >100% for packed passengers; Outside reference is placed, Unplaced disables Move/follow. |
| `color-test` | MAN-M02, MAN-A10, DEV-P18, DEV-P32 | P5-25 | template; body/three line colors; day/night preview; colorMap XML copy/export; explicit apply. Reachable authored zero-capacity train remains included. Local preview/export; any shared apply uses world-local persisted override for the color-test definition, never mutable process-global registry. |

TRAIN-D001/002 specify safe null-next/delegation/graph handling; D003/004 retain
source placement and positive-stop state quirks. D005 corrects both day editors;
D006 preserves ordered TURNING/forwarding effects; D007/008 specify positive
speed and checked original-width arithmetic. D009 rejects referenced deletion;
D010 confirms signals call the passing train's effective controller. D011 records
the inactive sorter and stored execution order; D012 retains TAT Location keys.
D013–018 cover inert rule files/typed clone, leased input, atomic lifecycle,
measured-limit follow-ups, withdrawal-before-sale UI and world-local color-test
state. Detailed acceptance obligations and source bindings live in the contract.

P4's nullable signal ControllerId is reserved web metadata, not native dispatch
ownership. Select a shared train controller's settings **at** the signal; do not
introduce a signal-wide override. Simple ignores non-Station harbors, whereas
manual/TAT can wait at signals. P6 owns real platform/Station passenger/bell
integration. `.ftdd` rules remain P5; `.ftgd`/`.ftgt` world imports remain P10.
The existing TAT resource/native/optional qualifications and P11 performance
findings retain their owners; every feature still needs EN/JA/browser evidence.

## P5-04 fleet foundation

The [canonical fleet/callback contract](web-parity-fleet-state.md) implements the
state prerequisites of the P5-01 `trade`, `deploy`, `movement`, `reversal`,
`outside`, `manager`, `groups` and `information-tracking` rows. MAN-T06–08,
MAN-W07–10, MAN-A10–13/17, MAN-O08 and DEV-P14/18–20/24 now bind concrete
`FleetTrain`/`FleetCarState`, reciprocal Traffic occupancy, stable shared
controllers/ordered group references and one callback per deployed train.
The [production checks](../tests/FreeTrain.Online.Tests/FleetTests.cs) cover
independent identity poses, fallback/cycle guards, atomic rollback and coexistence
with construction. This detached foundation ships no browser control or new
EN/JA wording; the rows' existing later P5 owners retain command/UI acceptance.


## P5-05 fleet persistence foundation

The [fleet save/transport contract](web-parity-fleet-transport.md) binds the
P5-01 `trade`, `deploy`, `movement`, `reversal`, `outside`, `manager`, `groups`
and `information-tracking` rows and MAN-T06–08, MAN-W07–10, MAN-A10–13/17,
MAN-O08, DEV-P14/18–20/24 to durable stock/controller identities, original/current
car order, retained Location-keyed harbor settings and explicit scheduled callbacks.
TRAIN-D010 rejects every nonnull reserved signal-controller field; TRAIN-D012
retains absent-harbor settings and their order. Contribution identities remain
those of the source-bound P5-03 stock/provider ledger. Save9/protocol7 schemas,
C# codec and browser decoders consume the same complete fleet/queue contract.
No new feature controls or source text ship here: EN/JA controls, trading,
motion, full TAT rules and native acceptance keep their numbered P5 owners.

## P5-06 trading domain

The [trading contract and tests](web-parity-fleet-trading.md) implement the `trade`
operation's allowed formation × quantity quotes, timestamped root inventory,
one RAIL_SERVICE batch expenditure, completed365-day resale and absent-date
no-receipt rule. Sale clears Inside occupancy, removes ordered membership and
cancels only its train callback while preserving shared controllers and other
scheduled work. `ConstructionAccounts.Earn`, Save9/protocol7 and browser replay
retain distinct source revenue. MAN-T07/W07/A10/A11 and DEV-P19/P24 bind these
domain semantics; [reciprocal manual scope](web-parity-manual.md#p5-06-trading-domain)
keeps the actual EN/JA purchase/inventory/withdraw/sale controls with P5-10/11.

## P5-07 deployment domain

The [placement/withdrawal contract](web-parity-fleet-deployment.md) implements
`deploy` for MAN-T07/W07 and DEV-P19/P24: exact tail-first temporary-head
planning, source/reversed direction choice, fixed-level GEO-008 slope behavior,
atomic occupancy/native callback registration and withdrawal retaining inventory.
Curve `dir1` validation follows source guidance in both backend and browser.
The [reciprocal manual scope](web-parity-manual.md#p5-07-deployment-domain)
keeps EN/JA map/keyboard controls, confirmations and network receipt lifecycle
with P5-11; moving trains and already placed reversal remain P5-08/12.

## P5-08 Inside movement domain

The [movement contract and evidence](web-parity-fleet-movement.md) implement the
Inside portion of the source `movement`, `guide` and `timing-cost` operations:
current-rail guidance/zdiff, visible borders before reciprocal destinations,
old-state consist shifts, occupied waits and atomic callback/accounting publication.
Simple junctions choose straight; followers reuse the predecessor's actual route.
MAN-T07/W07/W10 and DEV-P19/P24 retain [reciprocal manual scope](web-parity-manual.md#p5-08-inside-movement-domain).
The existing shared Run/Pause/Step protocol drives gameplay-bound trains after
save restore. Moving art/picking remain P5-09 and browser trade/deployment remain
P5-10/11. Full reversal, timing, Outside and controller gates retain P5-12–24.

## P5-10 purchase and owned-stock browser workflows

[Browser trading and evidence](web-parity-fleet-browser.md) bind MAN-T07/W07/A10
and purchase/resale portions of bilingual workflow01 to Game → Trains. Source
company/type/name browsing, exact formation/quantity quotes, source-art previews,
owned inventory, revision-fenced naming and unplaced-only resale now use real
atomic commands and durable shared stock. Observer browsing, draft/locale/focus
safety, stale quotes and receipt deduplication retain the existing session rules.
Deployment/withdrawal controls remain P5-11; full groups/controllers/tracking,
passengers/fares, recovery/capacity and native/optional acceptance keep their
numbered owners.

[Reciprocal manual bindings](web-parity-manual.md#p5-10-purchase-and-owned-stock-browser-workflows).

## P5-11 deployment browser slice

[Deployment controls and evidence](web-parity-fleet-deployment-browser.md) bind
MAN-T06/W08/A11 and DEV-P14/P19/P24 to owned-train map/keyboard targeting,
complete authoritative footprint/heading previews, source reversal during
placement and distinct confirmed withdrawal. Real shared Run/Pause/Step connects
purchase, P4 ramp motion, car inspection and canonical continuation in EN/JA.
Stale/role/receipt/draft/camera safeguards retain the established shell boundary.
Full manager/groups/tracking, manual/TAT and Outside behavior, integrated recovery,
capacity and native/optional acceptance retain their subsequent P5 owners.
[Reciprocal manual bindings](web-parity-manual.md#p5-11-deployment-browser-slice).

## P5-27 fleet continuation and recovery

[Fleet recovery evidence](web-parity-fleet-continuation.md) verifies MAN-F01–07,
MAN-T06–08, MAN-W07–10, MAN-A10–13/17 and DEV-P14/18–20/24 through exact
Save9 continuation, EN/JA named-slot/download/reconnect and actual checkpoint
process restart. Rejected uploads and interrupted file writes preserve prior
state/files. [Reciprocal manual scope](web-parity-manual.md#p5-27-fleet-continuation-and-recovery)
retains P10 historical/full-city recovery and P5-28/29 capacity/distribution gates.

## P6-01 station workflow bindings

[P6-01 station contract](web-parity-station-contract.md) and its
[hashed source/control/decision inventory](parity/station-review.json) bind the
following requirements. These are specification handoffs, not executed station
gameplay or EN/JA delivery. MAN-D01/02/03 and DEV-D01/02/03 use stable ExAV
range8, exact cut/index arithmetic, configured fares and current upkeep; the
historical documentation formulas remain references. TRAIN-D008/012 apply;
Manual remains owner-removed. Full decisions and independently specified
acceptance are retained in the linked contract.

| Operation | Manual/developer obligations | Implementing owner | Controls and acceptance |
| --- | --- | --- | --- |
| `station-building` | MAN-T04, MAN-W03, MAN-A05, DEV-P16, DEV-P24, DEV-A06 | P6-03/P6-06/P6-07/P6-08 | station family; orientation previous/next; preview; Place/Remove; name; cost. All 35 station declarations, flat/reclaimable/bounds, immediate timers/registration, removal genre/footprint, exact name draft and peer revision checks. |
| `thin-platform` | MAN-T04, MAN-W04, MAN-A05, MAN-D02, MAN-E06, EXAV-10 | P6-09 | N/E/S/W; roofed/unroofed; length >=1 (native default Maximum100); Place/Remove. Each heading/roof/length, both draw passes and car overlap, retained traffic/accessories, occupied/stale rejection, normal rail restoration. |
| `wide-platform` | MAN-W04, MAN-W05, MAN-A05, MAN-A08, EXAV-10 | P6-10/P6-13 | wide style; length; two lane add/remove controls; Place/Remove. All headings, lane0 mandatory creation, either/both/no lanes after edits, occupied lane rejection, exact saved ownership and independent stop indexes. |
| `host-graph` | MAN-W05, MAN-A06, MAN-A08, MAN-D01, MAN-E06, EXAV-10 | P6-03/P6-11/P6-12/P6-13 | host selector; disconnect warning; name; bell selector; preview bell; Apply/Cancel. Literal rectangle (10,10,2), east,L4 => x2..22/y2..18/z1..3 plus x10..13 columns; reparent child disconnect, descendant selection, host removal/rebuild, stale host IDs and bounded cycle-safe validation. |
| `passage-stairs` | MAN-T05, MAN-W06, MAN-A09, MAN-E06 | P6-14/P6-15 | concrete/brown wood/green wood; Passage/Stairs; Place/Remove; line preview. All materials/headings/up/down/roof/width; legal empty path no fabricated floating rails; pure preview, adjacent passage removal, precise invalid index23 handling and no service/booking changes. |
| `providers` | MAN-W04, MAN-A16, DEV-P16, DEV-P22, DEV-P29, DEV-P30, DEV-P31, DEV-A05, EXAV-06 | P6-16/P7 | provider identity/location; resident versus demand/entering queries; controlled test inputs only. Independent table cells/calendar and nested truncation cases; actual provider bindings, residents distinct from demand, height/footprint scaling; no advertised fixture population editor. |
| `listeners` | MAN-A06, MAN-A16, MAN-E10, DEV-P29, DEV-P30, DEV-P31, DEV-A05 | P6-16/P6-25/P7 | current demand; waiting value; provider lifecycle. SCN-D01 creation-order corners/z/equal-distance/fifth/removal, preserve separate forward/reverse membership and load continuation; controlled land values labeled until P7. |
| `timers-logs` | MAN-A13, MAN-A14, DEV-P24, EXAV-05, DEV-P29, DEV-P31, DEV-A05, DEV-A06 | P6-04/P6-17/P6-25 | today/yesterday; averages; clock step/run; upkeep. 08:37=>09:00 station/next00:00 daily versus platform next08:37; FIFO hour/day/train equality, source 600000+720000=1320000, nonlinear weekly logs and exact timer cancel/reopen; P10 old timer conversion excluded. |
| `passenger-fare` | MAN-A07, MAN-A12, MAN-A16, MAN-E10, EXAV-04, DEV-P19, DEV-P24, DEV-D01, DEV-D02, DEV-A05 | P6-18/P6-19 | packed/normal occupancy; waiting/demand; board/alight logs; cash. EXAV-SCN010..014, 39*3*5*2=1170; above100% occupancy, zero/missing origin, repeated/drop supply, checked overflow rollback and fare before move cost. |
| `yard-controller` | MAN-W05, MAN-A07, MAN-A12, MAN-D03, MAN-E07 | P6-19/P6-22 | pick Station/platform harbor; existing Simple/TAT manager; shared group settings. Eight L4/N3..6 indices and14 yards witnesses through real hosts, first/later/pass/forward/reverse/TURNING, shared multi-platform key, signals passenger-free and Manual absent. |
| `departure-events` | MAN-M06, MAN-W05, MAN-O07, MAN-A08, MAN-A12, MAN-A15 | P6-20 | per-platform bell choice; preview; local effects/mute; gesture enable. Epoch+committed publication sequence+effect ordinal dedup; no snapshot/save history replay, join/reconnect subscription baseline, duplicate receipts/delivery/rejected callback silent, two peers hear independently eligible live events. |
| `information-list` | MAN-M02, MAN-M03, MAN-W10, MAN-A05, MAN-A13, MAN-O16, MAN-A15, MAN-E03, DEV-P16, EXAV-14 | P6-08/P6-23 | name Apply/Cancel; Information; actual waiting/demand; today/yesterday board/alight/train; sorted list; Move; station labels. EN/JA duplicate names/rename/removal/empty list, exact logs/averages, peer stale edits, narrow layout and retained selection/focus/canvas. |

## P6-25 station continuation and recovery

[P6-25 continuation evidence](web-parity-station-continuation.md) verifies
MAN-F01–07, MAN-W07–10, MAN-A06/07/12–14/16 and DEV-P24/29–31 at current
station scope: actual listener graphs, logs, passenger origins, ordered bookings,
host rebuilds and FIFO train/station timers survive Save10 reopen and checkpoint
recovery. EN/JA named-slot/download/rejected-upload/reconnect/process workflows
consume gameplay-built stations and controlled supply. Historical import remains
P10; generic city construction/providers remain P7, full history/economy P8 and
capacity/packaging/phase acceptance P6-26–28.

## P7-01 city workflow bindings

11 November 2026: [source contract](web-parity-city-contract.md),
[exact records](parity/city-review.json) and [translation dispositions](parity/city-localization-review.json).
All 209 assigned declarations, 215 controls and 19 workflow families have named
owners. Source specification only; P7-02 independent fixtures and runtime delivery
remain open. MAN-O04 road labels, MAN-O05 live-value land trading and DEV-A07
category/recipe/automatic eligibility are reconciled without adding proposed
DEV-D06 economics or moving buses. P8 retains full market/report/stadium events.

| Operation | Requirements | Owner | Controls / source contract |
| --- | --- | --- | --- |
| `property` | MAN-M05, MAN-O05, MAN-A16, MAN-A20, EXAV-11 | P7-15 | Buy/Sell; inclusive rectangle at selected z; cost/loss review; Confirm/Cancel; owned parcel inspection. |
| `land-value` | MAN-M02, MAN-W10, MAN-A13, MAN-A16, EXAV-07 | P7-06/P7-15 | Pointed-location value inspector/status; world settings consumer; no editing by inspection. |
| `fixed-buildings` | MAN-T10, MAN-M05, MAN-W11, MAN-A14, MAN-A16, DEV-A07, DEV-P16, EXAV-13 | P7-07/P7-08/P7-09/P7-12 | Definition/group/variant; supported orientation; Place/Remove; full footprint ghost; construction progress and owner inspector. |
| `variable-buildings` | MAN-T10, MAN-W11, MAN-A14, MAN-A16, DEV-A07, DEV-P22 | P7-10/P7-12 | Definition; min/max height, native display increment4 and internal /4; supported variants; Place/Remove; stack preview. |
| `half-voxel` | MAN-T10, MAN-W11, MAN-A14, MAN-A16, DEV-A07, EXAV-13 | P7-11/P7-12 | Eight definitions; front/direction; fore/back placement side; supported colors; Place/Remove; paired owner inspection. |
| `catalog` | MAN-T10, MAN-M02, MAN-A16, MAN-A20, DEV-A07, DEV-D06 | P7-12/P9 | Category/subcategory; direct definition search; supported direction/color/design/height; author information; category navigation. |
| `construction` | MAN-T10, MAN-E12, DEV-A06, EXAV-13 | P7-04/P7-07/P7-27 | Progress inspection; cancel/demolish owner; no force-completion command. |
| `ground` | MAN-M05, MAN-O05, MAN-A16, DEV-A07, EXAV-06, EXAV-11, EXAV-13 | P7-16/P7-17/P7-18/P7-19 | Static/farm/forest/rice/creek selector; rectangle or creek endpoints; authored variants; Place/Remove; cost review. |
| `fences` | MAN-M05, MAN-A14, MAN-A16, EXAV-13 | P7-20 | Fence/remover definition; line boundary side; rectangle perimeter; segment/corner preview and removal. |
| `roads` | MAN-M04, MAN-O04, MAN-E11, MAN-A14, MAN-A16, MAN-A20, DEV-P13, EXAV-11, EXAV-13 | P7-21/P7-22/P7-23 | Three bundled styles; hierarchy/description; preview pattern cycle0..2; Place/Remove; cardinal two-point route and cost. |
| `road-extensions` | MAN-O04, MAN-E17, MAN-A20, DEV-D06 | P7-24/P9 | Safe empty accessory/dummy picker; optional supported definitions only; no moving bus command. |
| `providers-feedback` | MAN-A06, MAN-A16, MAN-E10, DEV-A05, EXAV-06, EXAV-07 | P7-13/P7-14 | Residents/outgoing/incoming inspection; station waiting/demand; live value refresh; no fixture provider editor. |
| `development` | MAN-A13, MAN-A20, DEV-A07, EXAV-06, EXAV-07 | P7-25/P7-26/P7-27 | Automatic hourly search; station-score consumer; no player force-growth controls. |
| `settings` | MAN-M02, MAN-E03, MAN-A16, MAN-A20, EXAV-07 | P7-28 | 11 fields; Apply/Cancel/Save settings/Load settings; defaults, units, errors and authoritative revision. |
| `valuation` | MAN-A20, MAN-E04, EXAV-15 | P7-03/P7-07/P7-25/P8 | P7 ownership/entity-value inputs and receipt inspection; P8 subsidiary market/history/report controls. |
| `stadium-boundary` | MAN-M05, MAN-A20, EXAV-06, EXAV-15 | P7-03/P7-13/P8 | P8 stadium placement/properties; name/team/capacity, reinforce/PR, game schedule/history and results. |
