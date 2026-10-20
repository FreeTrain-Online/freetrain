# Original manual: player workflow oracle

**ExAV reassessment pending — 20 October 2026:** this original manual mapping records the earlier original-FreeTrain source/documentation and its stated revision. It is historical input, not current ExAV completion or authority. [The current source policy](web-feature-parity.md) selects stable ExAV archive `trunk/` (2.1.1.12), including bundled plugins; P0 must validate retained findings and replace changed ones against that source. Existing original-source JSON, counts and fixtures retain their old provenance until regenerated. Carry English translations onto ExAV semantics as features are ported.

P0-01's [stable ExAV discovery ledger](web-parity-exav-inventory.md) now provides
the separate pinned source paths/hashes for reconciliation. The MAN-* behavior
claims and source comparisons below still require ExAV P0-03/P0-04 review.

Added 19 October 2026 during P0, after source/content reconciliation P0-02.
Use chapters **2–4** of the [user manual](http://www.kohsuke.org/freetrain/wordDoc.lzh) (Word format)
together with the [later archived online manual comparison](web-parity-online-manual.md)
as historical player-workflow evidence for the original game's structure, features,
and toolbar/dialog workflows. Chapter 1's installation and platform requirements
are outside this review. This is a specification input, not delivered web behavior
or evidence of running the Windows application.

The input is Japanese Markdown with 49 embedded PNG references. SHA-256 of the
complete file: `f200f03f6bd49682bb31f7686c2a197e00c415533710a16c6aef92b991ba5634`.
Its published last-update date is **2 February 2003**, as identified by the owner
and confirmed by the [original download listing](https://www.kohsuke.org/freetrain/downloads.html),
which credits Neon for the Word manual. This dates the documentation, not the
pinned game's full feature set. Section numbers below follow its table of
contents; the exported body's nested numbering restarts and is not reliable.
The original remains in ignored `tmp/`; this maintained document preserves the
derived requirements without shipping the manual or its screenshots as game assets.

**Online-manual supplement — 19 October 2026:** all 16 supplied HTML pages in
`tmp/online-manual/` have now been compared with this Word baseline. Most gameplay
pages were edited in 2008, with older footbridge and later support pages; preserve
each page's modification and capture dates separately. The [detailed comparison](web-parity-online-manual.md)
adds `MAN-O01`–`MAN-O16`, records missing images/chapters and the conflicting wiki
Word-download date, and extends acceptance with `MAN-A17`–`MAN-A22`. Its
[hash/provenance inventory](parity/historical/online-manual-review.json) preserves the exact
local inputs. The tables below retain the Word requirements; read them together
with the online supplements rather than treating the early menu list as final.

The manual supplies expected player outcomes and information architecture. The
current pinned ExAV source supplies exact algorithms, geometry, state and bundled
features; the original-source comparisons below require ExAV reconciliation. Reconcile disagreements explicitly; do not
silently replace either with the current prototype. Existing scope decisions
(archived debug/video tools and external A4/user-file music exclusions) still
apply. A manual omission does not exclude source-implemented bank, stocks,
development, other plugins or the owner's optional content suite.

The `MAN-*` IDs below are stable requirement references for P0-03's action map
and later acceptance fixtures. All are **documented expectations**; the [P0-03 source/action reconciliation](web-parity-actions.md)
now links all 45 toolbar/menu/window/online families to source and destinations/dispositions.
Implementation verification and acceptance fixtures remain pending. They are not
new runtime operation IDs. Source bindings, destinations and scope/difference
dispositions now live in the action map; add fixture evidence as implementation
proceeds. This manual reference alone is not P0-03 or P0-06 completion evidence.

## Post-manual release history

Reviewed 19 October 2026: [post-cutoff releases](https://www.kohsuke.org/freetrain/downloads.html).
All 26 release groups follow; individual-fix reconciliation remains open.
P0-03 now assigns each group's UI/action consequences to reviewed source workflows
in `action-map.json#releaseActionLinks`. Numeric/fix/save regression verification
remains P0-04/P0-05/P0-06 work.

| 2003 date | Change areas |
| --- | --- |
| 02-03 | Seasons, forests, buildability, roads |
| 02-13 | Splash, seasonal assets, tooling |
| 02-15 | Previews, seasons, assets, revenue |
| 02-19 | Terrain seasons, initialization, development |
| 02-21 | Crossings |
| 02-25 | Land trading, crossings, packaging |
| 02-27 | Poles, accounting, terrain loaders |
| 03-04 | Winter assets, cuts, tunnels |
| 03-08 | Train rendering, world exits |
| 03-11 | Return/service, construction fixes |
| 03-18 | Assets, development, platform connections |
| 03-22 | Occlusion, previews, footprints |
| 04-01 | Memory, SOAP, subsidiaries, development |
| 04-19 | Subsidiaries, assets, train fixes |
| 04-20 | Subsidiary trading, construction fixes |
| 04-22 | Subsidiaries, growth, tooling |
| 04-25 | Compositions, saves, cuts |
| 04-29 | Construction, saves, water, timetables, reports |
| 05-04 | Plugin profiles, color fixes |
| 05-08 | Banking, save structures |
| 05-17 | Banking, dialogs, construction, load fixes |
| 05-31 | Error guidance, placement, timetable/rendering fixes |
| 06-01 | Rendering overhaul |
| 09-07 | Heat-based land values, hosting |
| 09-09 | Stocks, rendering, startup |
| 09-14 | Recent saves, startup diagnostics |

Reconciliation against the pinned source remains required. P0-03 should follow
each dated entry into reachable commands, forms, contributions and options;
P0-04/P0-06 should capture changed boundaries and regression expectations;
P0-05/P10 should account for serializer changes. Later entries can explain an
old instruction without proving when a particular source line changed. Do not
infer that every documented fix exists identically in the pinned revision, or
restore an obsolete bug to match an early illustration. Use the chronology when
writing P9-HELP so the adapted manual describes the delivered game.

Two source checks illustrate why this must remain a reconciliation:

- `core/world/World.cs` constructs `core/world/development/LandValue.cs`, whose
  implementation describes heat conductivity and registers periodic updates and
  voxel-change listeners. This reachable baseline implementation belongs in P7,
  separately from the deferred `experiments/Heat/` project. Review its state,
  scheduler, parameters and interaction with development, not just the experiment.
- `core/framework/Core.cs` calls `parseProfile`, but that retained method only
  scans paths and returns an unpopulated set. Record this source gap in P0-03
  rather than claiming a complete historical profile workflow or implementing
  one solely because it appears in the release history. The separately authorized
  optional-pack build/run support retains its own delivery requirements.

## Toolbar and menu structure

Manual §2.1, `toolbar.jpg` / embedded `image5`, depicts a single compact top
toolbar. Its icons are shortcuts to menu commands and task windows, rather than
one button per field or construction variant. Preserve this relationship when
extending the existing web command bar/tool strip; exact Windows chrome and icon
positions are presentation references, not required browser coordinates.

| ID | Original shortcut, in displayed order | Browser requirement / phase |
| --- | --- | --- |
| MAN-T01 | Time: click icon to pause; adjacent ▼ selects progression speed | Clock control group with confirmed pause/run and source speed choices; existing Step remains available. P1/P9; run/pause alone is partial coverage. |
| MAN-T02 | Track construction | Rail task controls and map targeting, shared with Railway menu. P4. |
| MAN-T03 | Sloped track | Distinct slope workflow, with elevation/preview; do not bury it in a flat tile-shape list. P4. |
| MAN-T04 | Station construction | Station-building and platform tabs within a station task panel. P6. |
| MAN-T05 | Accessories | Rail accessory catalog and placement/removal. P4. |
| MAN-T06 | Train placement | Owned train inventory, timetable assignment and deployment/removal. P5. |
| MAN-T07 | Train purchase | Available rolling stock catalog, composition/quantity/cost and purchase. P5. |
| MAN-T08 | Timetables | Independent controller/timetable manager and configuration. P5. |
| MAN-T09 | Terrain manipulation | Terrain mode/options and map editing. P3. |
| MAN-T10 | Building construction | Constructible catalog, variants and placement/removal; current browse-only catalog is partial. P7. |

Manual §2.2 has seven menu families. Every entry below needs a source binding or
explicit disposition, even when it shares a toolbar destination. These are
navigation families to preserve in the browser action map; changing the existing
Game/View shell to seven desktop menus is not a prerequisite.

| ID / manual section | Documented entries and outcomes | Port work / disposition |
| --- | --- | --- |
| MAN-M01 / §2.2.1 File | New (world name, width, depth, height); Open; Import terrain; Save; Installed plugins; Exit | P3/P9/P10. Distinguish new-world configuration, terrain import and full-game restore. Plugin/dependency information is distinct from building catalog browsing. Exit → participant Leave is an adaptation, not host shutdown. |
| MAN-M02 / §2.2.2 View | Map; Sales report; Train information; Debug window; Land value inspection; Test train color settings; Height cut; Height-cut window | P3/P5/P7/P8/P9. Keep each capability accounted for; archived debug window remains explicitly excluded. Train recoloring is separately source-reviewed, not covered by a global display tint. |
| MAN-M03 / §2.2.3 Railway | Track; Slopes; Stations; Footbridges; Signals; Accessories; Train placement; Train purchase; Timetables; Bridges; Girder elevated track; Tunnels; Engine sheds | P4/P5/P6. Signals accept timetable settings but do not board/alight passengers. Specialized infrastructure needs its own choices/rules even if destinations share a panel. |
| MAN-M04 / §2.2.4 Roads | One-lane road; Two-lane road | P7. Retain distinct construction styles and source placement rules; this is not evidence for a completed bus simulation. |
| MAN-M05 / §2.2.5 Construction | Buildings (provisional); Terrain grading (provisional); Rental buildings; Ground cover; Terraces/cuttings; Soccer stadium | P3/P7/P8. Shift continues the selected terrain operation. Terraces/cuttings are restricted to mountain sides. “Provisional” wording does not by itself prove a source-only stub. |
| MAN-M06 / §2.2.6 Settings | Music (plugin MIDI and other music files); Sound effects; Options (error-message presentation, station-name visibility) | P2/P9. Bundled music/effects and source options require individual mappings. External A4 integration and user-file music equivalent retain the existing exclusion. Do not claim that locale/scheme plus one WAV covers these settings. |
| MAN-M07 / §2.2.7 Help | Online help; About (including free video memory) | P9. Extend help to delivered game systems; retain Credits/License. Legacy VRAM/platform details need an explicit portability disposition, not invented browser VRAM reporting. |

## Focused windows and map operations

The screenshots were inspected directly, including toolbar `image5`, rail `image13`,
station tabs `image27`/`image29`, platform properties `image34`, footbridge `image36`,
purchase `image42`, placement `image43` and timetable manager `image47`.
Visual observations below refer to those embedded images in the supplied manual;
they are original illustrations, not fresh original-runtime captures.

| ID / manual section | Fields, actions and expected behavior | Source starting points / phase |
| --- | --- | --- |
| MAN-W01 / §3.1.1 Track | Small window with track preview, start-point instruction, cost, Lay/Remove. Choosing a start shows a translucent planned route. Underground construction uses height cut. Removal follows the route interaction; the manual says no removal ghost. Obstacles and right-angle curves are disallowed. | `core/controllers/rail/RailRoadController.cs`, `core/world/rail/RailPattern.cs`; P4. Keep the web's explicit commit/cancel and read-only authoritative legality/cost query; record any removal-preview adaptation. |
| MAN-W02 / §3.1.2–6 Infrastructure | Junction direction is chosen per train/controller, default straight; use timetable settings. Slopes change rail height; terrain slopes alone do not create sloped rail. Elevated routes inherit starting height and need a usable start; supports are automatic. Bridges have four directions and no curves/junctions; girder elevated track cannot be at ground level or curve/branch. Tunnels cut raised terrain, with four directions and no curves/junctions. | `core/world/rail/`, slope/special-rail controllers and TAT junction forms; P4/P5. These are manual summaries to refine against source, not complete legality algorithms. |
| MAN-W03 / §3.2.1–3 Station buildings | Station consists of station building plus separately placed/connected platform(s), not a single renamed track tile. Station tab has type dropdown, previous/next orientation, visual preview and Place/Remove. Removal does not require matching the currently selected type. Pick a building to edit its name. | `PlatformController.cs`, `StationPropertyDialog.cs`, `core/world/rail/Station.cs`; P6. |
| MAN-W04 / §3.2.4 Platforms | Platform tab has four orientation choices, Slim checkbox, length 1–100 and Place/Remove. Platforms can be elevated, but cannot be on curves/junctions or bridges/girder track. Removal does not require matching selected type. Manual says an overlong train passes regardless of timetable. | `PlatformController.cs`, `core/world/rail/{ThinPlatform,FatPlatform,YardRailRoad}.cs`; P6. See MAN-D03 before encoding the length boundary. |
| MAN-W05 / §3.2.5–6 Platform properties | Pick platform → name, station/host selector, disconnected warning and per-platform departure-bell selector. Connect to a station or an already connected platform. Normal range is summarized as 5 voxels, slim as 3. Unconnected platforms are passed without a stop. Normal platforms have track-connection extension controls; slim platforms do not. | `PlatformPropertyDialog.cs`, `core/world/rail/{Platform,ThinPlatform,FatPlatform}.cs`; P6. See MAN-D01 for exact host enumeration. |
| MAN-W06 / §3.3 Footbridges | Type selector, Passage/Stairs part choice and Place/Remove. Build passage first with translucent preview, then stairs connecting to platforms. Only slim platforms, at right angles; placement uses platform height. The bridge is cosmetic: it does not determine whether a station functions. | `StationPassagewayController.cs`, `core/world/rail/PassagewayRail.cs`; P6. Do not confuse cosmetic footbridges with logical platform-host connections. |
| MAN-W07 / §3.4 Purchase | Wide catalog tree grouped by railway/company; detail area shows type/name, train preview, author, description, four speed categories (low/medium/high/very high), cars per formation × formation count, total cost and capacity per formation; Purchase and Close. Train plugins extend the available catalog. | `TrainTradingDialog.cs`, train contributions/manifests; P5. Preserve provenance/details and computed totals before committing a purchase; this catalog is distinct from owned trains. |
| MAN-W08 / §3.5 Placement | Tree of all owned trains with group creation and sale available through context actions. Selected train has editable name (visible in screenshot), assigned timetable and displayed type. Place on track, click train to set travel direction; Remove returns it from the map. | `TrainPlacementController.cs`, `core/world/rail/{Train,TrainGroup}.cs`; P5. Expose group/sell actions to keyboard and touch as well as any context menu; distinguish sale from withdrawing deployment. |
| MAN-W09 / §3.6 Timetable manager | Named list with controller type and New, Configure, Delete, Rename, Close. New chooses configuration method; manual driving and A-Train-style methods are documented. Double-click configures; single-click rename is an original convenience. Timetables are created/managed independently and assigned to trains. | `TrainControllerDialog.cs`, `core/world/rail/TrainController.cs`, manual/TAT plugins; P5. Existing per-train simple/manual selection is partial; preserve shared controller identities/configuration. |
| MAN-W10 / §2.2.2 Reports and tracking | Sales report switches Day/Month/Year using tabs, with display settings via context menu. Train information selects a train and displays name, state and passenger count/occupancy; Move frames it in the active map. Land-value inspection reports value at the pointed location in the status bar. | `core/world/accounting/AccountSummaryWindow.cs`, `core/controllers/rail/TrainTrackingWindow.cs`, development/land inspectors; P5/P7/P8/P9. Existing cash summary, object inspector and Find train cover only parts. |
| MAN-W11 / §2.2.2 Map and height cut | Left or right drag scrolls; Shift or Ctrl doubles distance, together quadruple it. Shift+wheel changes height cut; dedicated slider also changes it. Cut limits displayed height and construction affected by that height, including tall buildings. | `core/views/QuarterViewDrawer.cs`, map views/controllers and `HeightCutWindow.cs`; P3/P4/P7/P9. Record gestures, cut boundary and placement policy separately from current wheel zoom and construction-level selection. |
| MAN-W12 / §4.1–4.3 Help and diagnosis | Explain incompatible saves and operation failures; useful bug report contains version, reproduction steps, error details/stack trace and optional screenshot. Glossary defines timetable, plugin, height, junction, voxel and elevated construction. | Existing help/feedback and persistence diagnostics; P9/P10. Portable error details and support guidance remain in scope; Windows installation paths, MSCOREE errors, Print Screen tutorial and obsolete submission sites are historical context, not new game features. |

Paths without a directory in the source column are relative to
`core/controllers/rail/`. These are starting points, not a claim of complete
designer/dynamic/plugin-form reconciliation.

## Browser adaptation contract

Apply the original task grouping through the delivered [web UX contract](web-game-ux.md).
This section specifies future subsystem UX; it does not add unavailable controls
to the current [operation registry](../web/src/lib/game-ui.ts).

- Menu and toolbar routes open the same task and preserve a single set of options.
  Keep icon labels/tooltips and English/Japanese names; compact toolbar overflow
  must retain access to slope, station, purchase, placement and timetable workflows.
- Construction is a compact nonmodal task surface over the persistent map, using
  `PanelFrame` and the tool-card boundary. Keep type/variant, orientation, length,
  mode and preview/cost together. A form's controls configure its tool; map targeting
  begins only after an explicit transition out of form interaction, following the
  existing targeting/focus contract. The panel cannot resize/remount the canvas.
- Station building and platform tabs belong together; selecting an existing object
  opens its properties. Properties use stable object identity, epoch/revision and
  Apply/Discard/Keep editing, including host/bell/name changes and conflict handling.
- Purchase uses a catalog/list plus details; deployment uses owned inventory plus
  selected-train fields; timetable management uses a named controller list plus
  configuration. On narrow screens these become bounded, internally scrolling
  sheets with list/detail navigation and retained selection. Windows window sizes
  are references for information density, not browser minimum dimensions.
- Use explicit, keyboard/touch-accessible buttons for group/sale, report settings,
  Configure and Rename. Retain useful double-click/context conveniences only as
  secondary paths. Preserve Cancel/Close, focus return and Escape ordering.
- Local navigation/tool entry never changes shared time. Clock speeds, purchase,
  connection edits, sale and timetable changes need server authority, role/conflict
  policy, explicit results and save support with their first playable slice.
  Read-only browsing must remain useful to observers.
- Keep prospective controls out of the delivered registry until their workflows
  work. Existing Run/Pause/Step, Inspect/Find train, saves, Settings, Session,
  Credits/License, gestures and accessibility remain accounted for during changes.

## Differences and source questions to carry forward

| ID | Evidence / finding | Required disposition |
| --- | --- | --- |
| MAN-D01 | §§3.2.1/6 say normal 5/slim 3; §3.2.5 mentions 5 generically. Source `FatPlatform.HOST_RANGE=5`, `ThinPlatform.HOST_RANGE=3`, but `Platform.listHosts` searches an endpoint-expanded rectangle at z ±1 and also scans every height directly over the platform footprint. Only already station-connected platforms qualify as hosts. | P0-03/P6: preserve manual intent and exact source enumeration, including vertical cases. Do not implement a simple Euclidean 3D distance cutoff from prose. No semantic fix is accepted by this note. |
| MAN-D02 | Manual says height “at or above” the setting disappears; `QuarterViewDrawer.draw` begins its traversal at `Z=heightCutHeight`, including that level. It also documents cut-related construction restrictions beyond the current local display switch. | P0-04/P3/P4/P7: specify inclusive cut, picking and per-tool legality independently. Check height-boundary/footprint cases; preserve current authority and shared-world policy while recording any adaptation. |
| MAN-D03 | §3.2.4 says every train longer than a platform passes. `YardRailRoad.getStopTimeSpan` derives stop index with integer arithmetic: `(platformLength + trainLength)/2 - 1` or `(platformLength - trainLength)/2`, then range-checks it. For platform 5/train 6, C# truncation yields 4/0, both in range. This source boundary does not implement the prose rule literally. | P0-04/P6: add both-direction boundary fixtures and a semantic-difference decision before implementation. Keep the manual's expected outcome and source's actual calculation visible; do not silently introduce a length check or call the source equivalent. |
| MAN-D04 | Manual was last updated 2 February 2003. Its illustrations and two documented timetable methods are an early snapshot; the post-manual release index above and pinned source supply later coverage. | P0-03: reconcile every later release group with reachable in-scope source features, dynamic contributions/forms and configuration options. Retain explicit dispositions for source gaps, fixes and supplemental code; do not shrink the baseline to the manual or treat a changelog announcement alone as verified implementation. |
| MAN-D05 | §§2.2/4 mention archived debug tools, external/user-file music, Windows memory/setup and historical support URLs. | Keep explicit exclusions/portability dispositions; bundled MIDI, bells, useful help and ordinary content/error diagnosis remain required. Chapter 4 is support guidance, not an instruction to revive native dependencies or obsolete external services. |

The source observations above were checked in the retained baseline files; they
are source-derived comparisons, not original-runtime results. Resolve each open
semantic question through the plan's difference register before declaring parity.

## Acceptance cases to add with the relevant slices

These are fixture obligations, **not executed tests**. Derive precise numeric
expectations from pinned source and the difference decisions above. Reuse these
MAN-* IDs in the ExAV scenario ledger as P0-06 adds executable fixtures.
`doc/parity/historical/scenarios.json` retains the earlier source-derived seeds;
revalidate them against ExAV before carrying their expectations forward.

| ID | Player journey and observable acceptance | Phase |
| --- | --- | --- |
| MAN-A01 | Open every in-scope toolbar shortcut through its menu equivalent and verify identical task/options; pause and source speed choices reflect confirmed time. Narrow English/Japanese layouts keep all delivered tasks reachable. | P1/P9 + each task slice |
| MAN-A02 | Create named world with all three dimensions; import terrain through its separate workflow; save/restore it without replacing it on invalid input. | P3/P10 |
| MAN-A03 | Choose rail start/end, inspect ghost/legal cost, cancel without mutations; commit route, then remove it. Reject obstacles and unsupported right-angle curves; exercise underground cut. | P4 |
| MAN-A04 | Join ground/sloped/elevated track; inherit start elevation and verify automatic supports; exercise legal/illegal bridge, girder and tunnel directions, terrain and curve/junction cases. | P4 |
| MAN-A05 | Build/orient/name station building and normal/slim platforms; verify length endpoints, elevated placement and blocked curve/junction/special-rail targets. Remove an existing type while another catalog type is selected. | P6 |
| MAN-A06 | Connect/reconnect platforms to a building or already connected platform; test normal/slim range edges, out-of-range choices, vertical hosts and disconnected warnings after host removal. No stop/passengers at an unconnected platform. Use MAN-D01's source algorithm. | P6 |
| MAN-A07 | Compare shorter/equal/one-car-longer formations approaching both directions under a stopping timetable; record the MAN-D03 decision and exact integer-boundary results. | P5/P6 |
| MAN-A08 | Configure independent platform names and bells; extend normal-platform rail connections while slim has no extension; save/reload and verify membership, bells and names. | P6 |
| MAN-A09 | Place passage and stairs on slim platforms at right angles; reject normal/incorrect orientation cases; station service is unchanged by cosmetic footbridge removal. | P6 |
| MAN-A10 | Browse company catalog, preview metadata, set cars × formations and inspect capacity/total before purchase; owned inventory and accounting change exactly once and persist. | P5/P8 |
| MAN-A11 | Group/name owned formations, assign timetable, deploy/orient/withdraw, redeploy and sell using distinct actions. Invalid/occupied track leaves inventory and funds intact. | P5 |
| MAN-A12 | Create/name/configure manual and TAT timetables independently, assign one to multiple formations, change route/stop settings, and exercise deletion/references under source rules; save/reload maintains shared identity. Signals accept control without passenger exchange. | P4/P5/P6 |
| MAN-A13 | Inspect selected train's status/passengers/occupancy and frame that exact train; compare sales report Day/Month/Year and display settings against independent accounting totals; inspect pointed land value. | P5/P7/P8/P9 |
| MAN-A14 | Exercise cut slider/gesture, inclusive boundary, underground picking and tall-building/construction constraints; test documented pan modifiers or record their portable gesture dispositions. Terrain repeat and mountain-only terrace cases are covered. | P3/P4/P7/P9 |
| MAN-A15 | Map station-name visibility/error options; choose bundled MIDI/effects and per-platform bells with browser gesture handling; help explains implemented systems and incompatible saves, and exposes useful error/version details. | P2/P6/P9/P10 |
| MAN-A16 | Run purchase/deploy/connect/timetable workflows through authority, stale revision, dirty draft, disconnect and world-replacement cases; observer browsing remains available, no old draft replays and local navigation never pauses another player. Verify keyboard/touch and English/Japanese field access. | Every relevant slice |
| MAN-A17 | Create and name nested train groups, assign group timetables, choose parent inheritance or a per-train override, move trains between groups, change the group timetable and save/reload effective controller identity. Resolve MAN-O08's source reachability/null/root/cycle cases first; offer keyboard/touch Move to group alongside any drag/drop. | P0-03/P5/P10 |
| MAN-A18 | Target terrain corners in Raise and Dig, temporarily reverse with Ctrl, repeat with Shift plus mouse movement, release modifiers/focus without further mutations, and verify legal elevation, obstruction and water-level boundaries. Do not derive a timer-repeat or flooding algorithm from the wiki prose. | P3/P9 |
| MAN-A19 | Reach empty-world and terrain-loader creation through their reconciled New workflow; verify name/dimensions and terrain height/water-level settings, cancel without replacement, then save/reload. Account for terrain import without requiring the old duplicate File-menu entry. | P3/P10 |
| MAN-A20 | Map balance sheet, land trading, six documented road choices and automatic/Always day/Always night to actual source contributions, pack membership and browser destinations or explicit dispositions. Keep missing road definitions, placeholder descriptions and archived recorder/debug exclusions visible. | P0-03/P2/P7/P8/P9 |
| MAN-A21 | Load with missing, incompatible or updated content definitions; diagnose required identities before replacing the world, preserve the file/current state, then recover with the matching content set. Help explains why removing a required plugin breaks saves; historical stock-plugin deletion advice is not the browser recovery path. | P0-05/P9/P10 |
| MAN-A22 | Review chapter-tabbed Help against both manual versions and resolved source differences, including group inheritance, terrain modifiers, station no-stop diagnosis, appearance modes and pack compatibility. Verify localized narrow/desktop chapter/section navigation and replace absent wiki chapters/images with reviewed browser content. | P9-HELP/P11 |

P0-03 source/action/configuration mapping is complete; see the
[action reconciliation](web-parity-actions.md). Keep P0-04 (numeric/source
boundaries) and P0-06 (independent executable cases) open.
Native/device UX acceptance and gameplay handoff remain separate prerequisites.
