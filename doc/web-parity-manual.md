# Manual evidence: ExAV and historical FreeTrain references

**P4-01 source contract — 30 October 2026:** [Railway source reconciliation](#p4-01-railway-source-reconciliation)
now binds the track/infrastructure/accessory/manual journeys to explicit source
legality, booking and removal rules. This is specification evidence; P4 gameplay
and P4-02 executable fixtures remain open.

**P3-07 delivery — 29 October 2026:** [Heightmap and fixed-map controls/evidence](web-parity-heightmap.md) add the terrain-loader/provider portions of MAN-M01/O02/E02/A02/A19 and DEV-P09/P17. Bilingual upload, dimensions/water, image/result preview, cancellation, exact Owner confirmation and Save6 reopen without input image are delivered within temporary32² admission. Other authoring/capacity/native journeys remain open.

**P3-06 delivery — 29 October 2026:** [New world controls and evidence](web-parity-empty-world.md) deliver the empty-map parts of MAN-M01/O02/E02/A02/A19 through the existing Game panel, bilingual fields, candidate preview, Owner confirmation and Save6 reopening. Heightmap/provider selection and the other P3 authoring journeys remain open.

**Owner audio decision — 27 October 2026:** [BGM is deferred to a future improvement project](web-audio-policy.md). TinySynth demonstrated feasibility but produced audible pops/crackles and has been removed with its player and music controls. Sound effects and six bell previews remain. Digital audio, including reimagined MIDI compositions, is allowed; BGM/MIDI playback is no longer a P2/P9 or parity exit requirement.

**Baseline and authority — 21 October 2026:** the port targets **FreeTrain ExAV
2.1.1.12 / V2**, archive Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
SVN r115, **`trunk/`**. Its source and the approved decisions in the
[authority/scope reconciliation](web-parity-exav-integration.md) determine
behavior. The ExAV wiki in `tmp/online-manual-exav/` is the preferred available
player-documentation reference, subject to version and source reconciliation.
It is incomplete and covers several incompatible ExAV lines.

**All earlier findings are non-authoritative for ExAV.** The February 2003 Word
manual, the original FreeTrain online manual in `tmp/online-manual/`, their
screenshots, release history, source comparisons and derived `MAN-*` claims are
historical evidence. They do not prescribe ExAV fields, defaults, prices,
algorithms, limitations, feature membership or acceptance results. Missing ExAV
instructions do **not** make an older description normative. Even a matching
feature name or repeated sentence needs current source/dependency review.

Use the evidence in this order: pinned stable ExAV source plus approved port
adaptations; version-qualified ExAV documentation; older FreeTrain documentation
as a comparison or illustration candidate. Record contradictions and gaps rather
than filling them with assumptions. Beta 2.2 passenger behavior, Prototype 3 time
and speed settings, optional packs and V1 compatibility remain separate from
stable bundled scope. Existing browser UX and multiplayer requirements retain
their own authority under the [web UX contract](web-game-ux.md).

`MAN-T*`, `MAN-M*`, `MAN-W*`, `MAN-O*` and `MAN-D*` keep their identities for
traceability; retaining them does not approve their old semantics. `MAN-A*` remain
pending journey candidates whose expected results must come from current ExAV
specifications. New `MAN-E*` entries below are documentation findings, not runtime
operation IDs or completed action mappings. The existing [action review](web-parity-actions.md#man--and-dev--reconciliation)
reconciles the earlier workflow families; this documentation review closes no
implementation, runtime, save-continuation or acceptance gate.

## P3-01 terrain source reconciliation

**27 October 2026:** [the terrain contract](web-parity-terrain-contract.md) resolves
P3 source details for MAN-M01/O02/E02/A02/A19 (New and heightmaps),
MAN-T09/M05/O06/A14/A18 (corner, rectangle/repair, terraces/cuts),
MAN-M02/W11/D02 (terrain targeting/cuts) and MAN-A16 (authority/drafts).
The [reciprocal workflow bindings](web-parity-actions.md#p3-01-terrain-workflow-bindings)
and [77 independent vectors](parity/terrain-fixtures.json) separate source
behavior, browser adaptations and new generation rules. The owner retains a
single global water level for this checklist. Source corner Shift repeats on
movement; Mountain2 Shift changes its target profile and Ctrl selects repair;
forest manual eligibility has no water test; intentional cuts/terraces may have
discontinuous corners. Older prose cannot override these specified rules.
MAN-A02/A14/A16/A18/A19 browser journeys and MAN-D02 cross-system cut acceptance
remain open with P3-05–17/P4/P7/P9/P10 as applicable. This closes source
reconciliation for the terrain slice, not any journey or native observation.

## ExAV documentation reviewed

Reviewed **all 15 top-level saved HTML articles** in `tmp/online-manual-exav/`
on 21 October 2026 as UTF-8. Article text was separated from atwiki navigation,
advertising, tag/edit widgets and footers; embedded scripts were not executed.
The [input inventory](parity/exav-manual-review.json) records local filenames,
raw SHA-256, canonical URLs and the articles' own last-update strings. Links in
this table identify the saved articles' origins; this was an offline review,
not a check of today's live wiki. These are individual 2009–2010 page dates,
not proof of the illustrated game build or capture date.

| Input ID / saved article | Article last updated | Review coverage |
| --- | --- | --- |
| EXAV-DOC-P01 / [FAQ plugin作成編](https://w.atwiki.jp/freetrain-exav/pages/43.html) | 2009-03-30 | Symmetric/asymmetric car graphics and slope-frame diagnosis |
| EXAV-DOC-P02 / [FAQ plugin導入編](https://w.atwiki.jp/freetrain-exav/pages/26.html) | 2010-05-08 | V1/V2 packs, engine compatibility and installation |
| EXAV-DOC-P03 / [FAQエラー対応編](https://w.atwiki.jp/freetrain-exav/pages/59.html) | 2010-03-29 | Save incompatibility, missing content and stale-window load failures |
| EXAV-DOC-P04 / [FAQバージョンアップ編](https://w.atwiki.jp/freetrain-exav/pages/25.html) | 2010-05-08 | Fork identity, upgrade promises, removed tools and future plans |
| EXAV-DOC-P05 / [FAQ全般編](https://w.atwiki.jp/freetrain-exav/pages/33.html) | 2010-08-24 | View/speed limits, transport exclusions, bankruptcy and map-size anecdotes |
| EXAV-DOC-P06 / [FAQ列車運転編](https://w.atwiki.jp/freetrain-exav/pages/29.html) | 2010-05-02 | Orientation, exits, timetable restrictions, signals, groups and passengers |
| EXAV-DOC-P07 / [FAQ導入編](https://w.atwiki.jp/freetrain-exav/pages/24.html) | 2010-08-26 | Branch/time-scale distinctions, bundled versus additional content and startup diagnosis |
| EXAV-DOC-P08 / [FAQ建物建設編](https://w.atwiki.jp/freetrain-exav/pages/34.html) | 2009-05-14 | Generic versus older building menus, construction/removal bugs and ground cover |
| EXAV-DOC-P09 / [FAQ経営編](https://w.atwiki.jp/freetrain-exav/pages/91.html) | 2010-05-08 | Finance caveats, V2 improvements, report categories and money width |
| EXAV-DOC-P10 / [FAQ道路建設編](https://w.atwiki.jp/freetrain-exav/pages/30.html) | 2009-03-30 | Road anchors, development attributes, decorative infrastructure and crossings |
| EXAV-DOC-P11 / [FAQ鉄道建設編](https://w.atwiki.jp/freetrain-exav/pages/31.html) | 2010-04-27 | Platforms, poles/roofs, passage materials, removal hazards and catchments |
| EXAV-DOC-P12 / [FAQ鉄道車両編](https://w.atwiki.jp/freetrain-exav/pages/28.html) | 2010-05-08 | Formation restrictions, lighting, resale and plugin modification guidance |
| EXAV-DOC-P13 / [マニュアル/ツールバー](https://w.atwiki.jp/freetrain-exav/pages/140.html) | 2010-04-20 | Task shortcuts, pause/speed and automatic/day/night controls |
| EXAV-DOC-P14 / [マニュアル/メインメニュー](https://w.atwiki.jp/freetrain-exav/pages/141.html) | 2010-04-20 | Seven menu families, additional windows, options and compatibility routes |
| EXAV-DOC-P15 / [用語集](https://w.atwiki.jp/freetrain-exav/pages/32.html) | 2009-03-05 | Night colors/overrides, terraces/cuttings and timetable stop markers |

This collection contains two manual overview pages, twelve FAQ pages and a
glossary. It contains no detailed ExAV station, track, purchase or controller
manual chapters, changelog, complete options reference or complete parameter
specification. Linked pages, plugin downloads and test saves were not supplied
as reviewed articles. The snapshots also have broken local image references:
33 article image references were counted, 25 of which do not resolve verbatim
within the saved tree; downloaded `_002`/`_003` variants exist for some. The
inventory preserves this distinction without assuming variant identity. Two
local toolbar variants were inspected and show the extra appearance controls;
this is no claim of a complete ExAV screenshot review. Older illustrations remain
historical and cannot certify omitted ExAV controls.

## ExAV findings and reconciliation

The evidence column reports claims in the saved ExAV articles; it does not mean
they were verified in stable 2.1.1.12. P01–P15 abbreviate the EXAV-DOC input IDs
above. Current-source conclusions link to the existing maintained reviews;
no native application was run for this review.

| ID / inputs / earlier references | ExAV documentation evidence | Disposition for the port |
| --- | --- | --- |
| MAN-E01 / P13–14 / MAN-T01–10, MAN-O01 | The toolbar repeats the ten familiar clock/construction/train task groups and adds automatic/day/night appearance buttons. P13 says automatic switches at 06:00/18:00. | Preserve task reachability and expose appearance modes through the current UI. Clock speed, train speed and appearance are separate controls. Treat the wiki times as a source-check candidate; the toolbar is not a complete ExAV feature inventory. P2/P9. |
| MAN-E02 / P05/P14 / MAN-M01, MAN-O02 | New offers an empty map with name/three dimensions or a grayscale BMP terrain loader with width/depth/height/water level. File has Open, Save, plugin list and Exit; terrain loading is described inside New. | Keep MAN-A02/A19 aligned with the [current two-provider action review](web-parity-actions.md). Do not require the 2003 separate terrain-import menu entry. Loader limits and validation need source rules; P05's large-map examples are anecdotes, not supported capacities. P3/P10. |
| MAN-E03 / P14 / MAN-M02, MAN-W10–11, MAN-O03 | View additionally lists subsidiaries, bankbook, building categories, holdings, station list, development tuning, bank, pole visibility, securities and train tracking. Multiple map windows can show different places/trains; tracking selects one train per view. | [EXAV-ACTION-001/002/014](web-parity-actions.md) distinguish station/train information, one-time Move, station lists and continuous tracking. Development settings have Apply/Cancel/Save/Load and are separate from world saves. Pole visibility synchronizes with Options. Native multiwindow layout needs an explicit browser disposition. P5–P9/P10. |
| MAN-E04 / P09/P14 / MAN-M02, MAN-W10, MAN-O03 | P14 describes only today/this month/this year sales tabs and calls the balance sheet unimplemented with incorrect values. P09 warns that categories may be placeholders and says several costs/subsidiary receipts are absent, while explicitly announcing improved V2 economics. | This wiki is internally dated/mixed. Do not omit costs, finance or subsidiaries on this basis. [EXAV-ACTION-005](web-parity-actions.md) establishes six periods and 31-day/12-month histories; [EXAV-02/11/15](web-parity-exav-integration.md) supplies current economics/finance. The computed balance sheet is an existing P8 adaptation, not established native behavior. P8/P9. |
| MAN-E05 / P13–14 / MAN-M03–05, MAN-T02/03/09 | Railway retains the familiar tools; Roads now has a plugin-tree construction tool, accessory tool and dummy-car placement. Construction adds generic building categories and land trading; older building/rental menus are described as compatibility routes. | Older two-/six-road menu lists cannot determine current membership. [EXAV-ACTION-003/004/009/010/011](web-parity-actions.md) adds speed limits and Mountain2, distinguishes direct-building consumers/stubs, and records empty road-accessory/dummy-car extensions. Required content follows current bundled/optional inventories. P3/P4/P7/P9. |
| MAN-E06 / P06/P11 / MAN-W03–06, MAN-O07 | FAQs explain back-to-back slim island platforms, pole plugins used as roofs/girders, wide-platform visual limitations, concrete/brown-wood/green-wood passage choices from beta 0.8, and an underground-platform workaround of laying rails first. | [EXAV-ACTION-007](web-parity-actions.md) establishes wide/slim-roofed/slim-unroofed controls and consumed materials. [EXAV-10](web-parity-exav-integration.md) and [host review](web-parity-geometry-dependencies.md#platform-host-connections) replace the old 5/3 host ranges with range 8 and exact enumeration. FAQ visual limitations and underground defects require per-tool review, not unconditional browser restrictions. P4/P6. |
| MAN-E07 / P06/P15 / MAN-W02/09, MAN-O09 | Detailed rules can physically turn/reverse or forward empty stock; station rules apply across its platforms. FAQs propose signals beyond platforms for direction-specific waits or stops without passenger exchange. Basic signal graphics share behavior; automatic block and pass-monitor features require additional plugins. The glossary shows stop markers holding trains off-platform. | Use [EXAV-08](web-parity-exav-integration.md), [EXAV-ACTION-006](web-parity-actions.md) and P5/P6 source semantics for shared identities, forwarding, copying, clear/import/append/export and rule ordering. Do not infer that all signal plugins are equivalent or bundled. Timed waits are distinct from optional block/monitor logic. P4/P5/P6/P10. |
| MAN-E08 / P06 / MAN-W08, MAN-O08 | Placement clicks change travel direction; withdraw/redeploy can change formation orientation. Map-exit return reverses formation and may use the neighboring return track. The FAQ says unwanted groups cannot be deleted. | Preserve these as concrete journeys, with exact heading/exit timing from [current geometry](web-parity-geometry.md). Group deletion is a documentation claim to check. Registered delegation is established separately by [EXAV-ACTION-015](web-parity-actions.md); the FAQ's silence cannot remove it. Resolve null/root/cycle/reparenting defects in P5/P10. |
| MAN-E09 / P05/P12 / MAN-W07/08 | Train formations depend on plugin choices; the FAQ claims a 100-car ceiling but admits the total train-count limit is unknown. It dates resale refunds to beta 2.0.3.4 / alpha 3.0.2.1 and describes 1/2, 1/3, 1/4… depreciation with age. | V2 fare, amenity and seated capacity are required beyond old purchase details ([EXAV-03](web-parity-exav-integration.md)). Use reviewed manifests for allowed formations; do not turn the unknown train count into a limit. [EXAV-SCN-026](web-parity-scenarios.md) defines completed-365-day resale boundaries and missing purchase-date behavior. P2/P5/P8/P10. |
| MAN-E10 / P06/P07/P11 / MAN-W04/05, MAN-D01/03, MAN-O07 | P06 repeats “platform at least as long as train” and explains actual versus demanded waiting passengers using 30% boarding. P11 describes catchments as radius 10 for versions 1–2, radius 25 for Prototype 3, plus a special stadium range. P07 identifies partial alighting as experimental 2.2. | None of these prose numbers is an algorithm. [Current yard arithmetic](web-parity-geometry.md) still admits some one-car-overlength stops. [EXAV-SCN-010–020](web-parity-scenarios.md) and [EXAV-04–06](web-parity-exav-integration.md) specify packing, demand/depletion and asymmetric listener/catchment behavior. Keep beta passenger cohorts out of stable scope. P5/P6/P7. |
| MAN-E11 / P10 / MAN-M04, MAN-O04 | Roads start from flat ground/terraces or rails and extend straight; decorative slope roads are buildings, so the FAQ proposes temporary elevated rails/terraces as anchors. Roads do not grow automatically. Development depends on road attributes; untyped decorative surfaces do not contribute. | Review actual road construction/development consumers and metadata in [content](web-parity-content.md) and [actions](web-parity-actions.md), including occupied targets and source accounting. Decorative monorail paths, supports and dummy crossings do not establish another simulated transport system. P4/P7. |
| MAN-E12 / P08/P11 / MAN-M05, MAN-W01/02, MAN-O05 | The FAQs distinguish generic versus legacy building menus and report hazards involving in-progress demolition, partial slope removal and signals/road surfaces under trains. Several fixes are explicitly dated beta 0.7/0.7.1; one passage still reports unresolved crashes. | Use these as regression candidates for [EXAV-13/D06](web-parity-exav-integration.md) and operation-specific occupancy/dependency checks. A bug or old workaround is not a parity requirement to crash or leave broken entities. Current direct-placement adaptation and source stubs remain explicit. P3/P4/P7. |
| MAN-E13 / P02/P03/P04/P07 / MAN-M01/07, MAN-W12, MAN-O12/14 | ExAV is a fork, with no original/EX save compatibility promised. V1/V2 plugin families differ; old V1 definitions do not directly load as V2. A compatibility table separately versions signal/map-capture plugins. Save-compatibility promises span older releases and exclude Prototype 3. | [Current persistence](web-parity-persistence.md) separates engine, ruleset, pack and save identities. Wiki promises cannot certify 2.1.1.12 import/continuation, shared V2 identity cannot certify 2.2/3.x saves, and historical “cannot convert” wording does not cancel the authorized port importer. Current bundled and [optional](web-parity-optional-plugins.md) source inventories govern membership. P2/P9/P10. |
| MAN-E14 / P03/P07/P08 / MAN-W12, MAN-A21 | Load failures may involve open timetable/train/station/funds/category windows, absent or damaged plugins, or corrupt saves. Startup advice diagnoses V1/V2 mismatch, missing dependencies, stale DLL paths and an empty generic catalog. | Preserve useful content/version/error diagnosis; make world replacement atomic and invalidate stale drafts/inspectors. Empty catalog controls need safe behavior. Do not reproduce DLL-copy instructions, ASCII-only install paths, native crashes or “close windows first” as browser requirements. P2/P7/P9/P10. |
| MAN-E15 / P01/P12/P15 / MAN-W07, MAN-O10 | Car-light pixels are baked into sprites, not switched by leading/trailing position. The glossary documents night-emissive colors and day/night/season overrides. Authoring FAQ distinguishes 12/24 graphic cases and warns about symmetric/asymmetric slope ordering. | Treat this as rendering/authoring evidence; use the [ExAV geometry/gallery](web-parity-geometry.md) and content recipes for actual frame/override dispatch. The FAQ's 12/24 cases are not a replacement for current direction/frame indexing. Wiki redistribution advice does not establish an asset license; existing source/notices review governs. P2/P5/P7/P11. |
| MAN-E16 / P05/P07/P14 / MAN-M06, MAN-O10/11 | P14 labels train-name display as beta and train-speed units as Prototype 3; P07 separately describes five Prototype 3 time-scale choices. P05 describes four train speed classes and version-1+ speed-limit sections; it reports bankruptcy notice followed by replenishment/continuation. | Article labels do not decide stable membership: [EXAV-ACTION-008](web-parity-actions.md) confirms stable train labels and bankruptcy options. Prototype 3 time scales remain excluded; approximate km/h equivalents are not clock formulas. Economy changes require shared authority; local visibility/fonts remain presentation settings. P1/P4/P5/P8/P9. |
| MAN-E17 / P04/P05/P09/P14 / MAN-M02/04/07 | The FAQ explicitly denies playable buses, moving dummy cars, freight/material transport, coupling/uncoupling, cab view, zoom and rotation. Recorder is unsupported; map capture requires an ExAV-specific additional plugin. It also lists future plans and placeholder report categories. | Separate stable absence, optional functionality, future announcements and approved browser adaptations. In particular, existing browser zoom is a UX adaptation; it need not be removed to match the FAQ. Keep current [scope/exclusions](web-parity-exav-integration.md#branch-and-content-boundaries); neither category labels nor linked addons create bundled requirements. P2/P5/P7/P9/P11. |

## Documentation gaps and feature handoff

Every older `MAN-*` statement below carries historical status, including ones
for which this ExAV collection has no replacement. For each implementing slice,
link the requirement to the current ExAV symbol/hash, registration and consumer,
record an approved adaptation or unresolved difference, and derive expected
results before accepting the journey. The [source-scenario specification](web-parity-scenarios.md)
is the current numeric reference; source specification completion and executed
acceptance are different statuses.

The gaps most likely to mislead implementation are:

- **Detailed forms and numeric boundaries:** the saved ExAV manual does not
  enumerate complete station/purchase/timetable/options fields. Old screenshots
  cannot establish completeness. Include roof variants, counters, V2 metadata,
  controller/rule files, all report periods and histories from current reviews.
- **Outdated statements within the ExAV wiki:** finance, passenger, platform-length,
  group and catchment prose needs source reconciliation even when no old manual
  is involved. Preserve the page/version claim and the current decision separately.
- **Unreviewed branches/content:** neither linked changelog/addon names nor wiki
  installation counts revise the pinned bundled/optional denominators. Prototype
  3, experimental 2.2 and older V1 saves retain separate compatibility identities.
- **Help and acceptance:** P9-HELP should use ExAV task vocabulary and delivered
  browser illustrations. Reuse old text only after current-rule review; show
  unresolved documentation gaps in planning, and publish instructions only for
  implemented behavior. All MAN-A cases below remain unexecuted.

## Historical original-FreeTrain review

The following retains the 19 October 2026 review and its IDs as historical
comparison evidence. Imperative language in the old tables describes proposed
port work from that review, subject to the ExAV qualifications above; it does
not grant those descriptions current specification authority. Original source
paths refer to the older snapshot unless explicitly linked to an ExAV review.

Added 19 October 2026 during P0, after source/content reconciliation P0-02.
Use chapters **2–4** of the [user manual](http://www.kohsuke.org/freetrain/wordDoc.lzh) (Word format)
together with the [later archived online manual comparison](web-parity-online-manual.md)
as historical player-workflow evidence for the original game's structure, features,
and toolbar/dialog workflows. Chapter 1's installation and platform requirements
are outside this review. This is historical comparison material, not an ExAV
specification or evidence of delivered behavior or running the Windows application.

The input is Japanese Markdown with 49 embedded PNG references. SHA-256 of the
complete file: `f200f03f6bd49682bb31f7686c2a197e00c415533710a16c6aef92b991ba5634`.
Its published last-update date is **2 February 2003**, as identified by the owner
and confirmed by the [original download listing](https://www.kohsuke.org/freetrain/downloads.html),
which credits Neon for the Word manual. This dates the documentation, not the
pinned game's full feature set. Section numbers below follow its table of
contents; the exported body's nested numbering restarts and is not reliable.
The original remains in ignored `tmp/`; this maintained document preserves the
historical findings without shipping the manual or its screenshots as game assets.

**Online-manual supplement — 19 October 2026:** all 16 supplied HTML pages in
`tmp/online-manual/` have now been compared with this Word baseline. Most gameplay
pages were edited in 2008, with older footbridge and later support pages; preserve
each page's modification and capture dates separately. The [detailed comparison](web-parity-online-manual.md)
adds `MAN-O01`–`MAN-O16`, records missing images/chapters and the conflicting wiki
Word-download date, and extends acceptance with `MAN-A17`–`MAN-A22`. Its
[hash/provenance inventory](parity/historical/online-manual-review.json) preserves the exact
local inputs. The tables below retain the Word-era candidates; both older manuals are
non-authoritative for ExAV and remain subordinate to the current review above.

The older manual supplies historical workflow and information-architecture
candidates. Current ExAV source and approved decisions supply behavior and scope;
original-source comparisons cannot substitute for that review. Existing debug,
video and external-music exclusions retain their separate decisions. Manual
omissions do not exclude current source features or the authorized optional suite.

The retained `MAN-*` IDs are historical cross-reference identities. The
[P0-03 source/action reconciliation](web-parity-actions.md) maps all 45 earlier
toolbar/menu/window/online families to current source workflows/dispositions;
that mapping does not approve old field/numeric semantics. Implementation
verification and acceptance remain pending. New MAN-E findings above record
this documentation comparison separately from that completed family mapping.

## Post-manual release history

**Historical chronology only:** reviewed 19 October 2026 from the original
FreeTrain [post-cutoff releases](https://www.kohsuke.org/freetrain/downloads.html).
These 26 groups are not an ExAV changelog or a current feature checklist;
individual-fix reconciliation remains open.
The historical P0-03 map assigns each group's UI/action consequences in
`doc/parity/historical/action-map.json#releaseActionLinks`. Those links preserve
original-source provenance; current source specifications and runtime regression
acceptance retain their separate statuses.

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

Two historical original-source checks illustrate why this must remain a
reconciliation. Their conclusions are not automatically ExAV findings:

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

**Historical toolbar/menu evidence:** these are older workflow candidates,
not normative ExAV requirements. Apply MAN-E01–05/16–17 and current action/source
dispositions before reusing any row. Missing ExAV instructions do not validate it.

Manual §2.1, `toolbar.jpg` / embedded `image5`, depicts a single compact top
toolbar. Its icons are shortcuts to menu commands and task windows, rather than
one button per field or construction variant. Preserve this relationship when
extending the existing web command bar/tool strip; exact Windows chrome and icon
positions are presentation references, not required browser coordinates.

| ID | Historical original shortcut, in displayed order | Earlier browser candidate / phase; ExAV review required |
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

Manual §2.2 has seven historical menu families. Each retained entry needs a
current source binding or explicit disposition even if it shares a destination.
This does not require restoring original-only menus, catalog membership or old
field semantics. The browser need not reproduce seven desktop menus.

| ID / manual section | Historically documented entries and outcomes | Earlier port candidate / disposition; ExAV review required |
| --- | --- | --- |
| MAN-M01 / §2.2.1 File | New (world name, width, depth, height); Open; Import terrain; Save; Installed plugins; Exit | P3/P9/P10. Distinguish new-world configuration, terrain import and full-game restore. Plugin/dependency information is distinct from building catalog browsing. Exit → participant Leave is an adaptation, not host shutdown. |
| MAN-M02 / §2.2.2 View | Map; Sales report; Train information; Debug window; Land value inspection; Test train color settings; Height cut; Height-cut window | P3/P5/P7/P8/P9. Keep each capability accounted for; archived debug window remains explicitly excluded. Train recoloring is separately source-reviewed, not covered by a global display tint. |
| MAN-M03 / §2.2.3 Railway | Track; Slopes; Stations; Footbridges; Signals; Accessories; Train placement; Train purchase; Timetables; Bridges; Girder elevated track; Tunnels; Engine sheds | P4/P5/P6. Signals accept timetable settings but do not board/alight passengers. Specialized infrastructure needs its own choices/rules even if destinations share a panel. |
| MAN-M04 / §2.2.4 Roads | One-lane road; Two-lane road | P7. Retain distinct construction styles and source placement rules; this is not evidence for a completed bus simulation. |
| MAN-M05 / §2.2.5 Construction | Buildings (provisional); Terrain grading (provisional); Rental buildings; Ground cover; Terraces/cuttings; Soccer stadium | P3/P7/P8. Shift continues the selected terrain operation. Terraces/cuttings are restricted to mountain sides. “Provisional” wording does not by itself prove a source-only stub. |
| MAN-M06 / §2.2.6 Settings | Music (plugin MIDI and other music files); Sound effects; Options (error-message presentation, station-name visibility) | P2/P9. Effects and source options require individual mappings; [BGM playback is deferred](web-audio-policy.md). External A4 integration and user-file music equivalent retain the existing exclusion. Do not claim that locale/scheme plus one WAV covers these settings. |
| MAN-M07 / §2.2.7 Help | Online help; About (including free video memory) | P9. Extend help to delivered game systems; retain Credits/License. Legacy VRAM/platform details need an explicit portability disposition, not invented browser VRAM reporting. |

## Focused windows and map operations

**Historical window evidence:** form fields, bounds and interactions below are
from the original manual; they are not a complete ExAV UI specification. See
MAN-E03–16 for changed or conflicting descriptions and current source owners.

The original-manual screenshots were inspected in the earlier review, including toolbar `image5`, rail `image13`,
station tabs `image27`/`image29`, platform properties `image34`, footbridge `image36`,
purchase `image42`, placement `image43` and timetable manager `image47`.
Visual observations below refer to those embedded images in the supplied manual;
they are original illustrations, not fresh original-runtime captures.

| ID / manual section | Historical fields, actions and claimed behavior | Original-source starting points / earlier phase candidates |
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

Paths without a directory in the source column are relative to the historical
`core/controllers/rail/`. They are original-source starting points, not verified
ExAV path/behavior bindings or complete designer/dynamic/plugin-form coverage.
In particular, the old `TrainTrackingWindow.cs` reference does not identify both
current information and continuous-tracking routes (MAN-E03).

## Browser adaptation contract

Apply current ExAV task scope through the delivered [web UX contract](web-game-ux.md).
These browser adaptation requirements come from that contract, independently of
historical gameplay prose. Subsystem controls/fields must first be reconciled
with current source. This section adds no unavailable controls to the current
[operation registry](../web/src/lib/game-ui.ts).

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
| MAN-D01 | **Superseded original-source numbers:** old manual/source described normal 5/slim 3. Stable ExAV uses range **8**, an endpoint-expanded inclusive rectangle at z ±1 plus full-height footprint columns. Constructor auto-selection requires a direct Station; properties can select an already connected platform. | Use [EXAV-DEP-008–010](web-parity-geometry-dependencies.md#platform-host-connections), not the old constants. P6 fixtures must cover candidate order, vertical hosts, host removal and child disconnection on reparenting. No Euclidean cutoff or automatic nearest-host rule follows from either manual. |
| MAN-D02 | Both the older manual and ExAV P14 say height “at or above” the cut disappears. The earlier original-source comparison found that level included; repeating the wiki wording does not settle this boundary. | Use the current [ExAV geometry and picking review](web-parity-geometry.md) for cut/render/picking predicates and per-tool legality. P3/P4/P7 must derive boundary/footprint fixtures from those bindings and record portable gesture decisions. |
| MAN-D03 | Old §3.2.4 and ExAV P06 both say an overlong train passes. The [current ExAV yard review](web-parity-geometry.md) independently confirms `(platformLength + trainLength)/2 - 1` / `(platformLength - trainLength)/2` and range checks: platform 5/train 6 yields valid 4/0 by C# truncation. | Current source arithmetic, not either manual's simplified rule, supplies P5/P6 expected results absent an approved change. Cover both directions and one-/two-car-overlength boundaries; do not introduce a prose-derived hard length check. |
| MAN-D04 | Word manual is a February 2003 snapshot; the later original wiki and 2003 releases still predate the current baseline. The ExAV wiki is also incomplete and mixes branch/version claims. | Use current ExAV registrations/consumers and source-specified behavior. Historical releases, future plans and wiki omissions cannot set stable scope. Missing manual chapters remain documentation gaps rather than implied old-rule defaults. |
| MAN-D05 | §§2.2/4 mention archived debug tools, external/user-file music, Windows memory/setup and historical support URLs. | Keep explicit exclusions/portability dispositions; bells, useful help and ordinary content/error diagnosis remain required; bundled MIDI playback is deferred. Chapter 4 is support guidance, not an instruction to revive native dependencies or obsolete external services. |

Original-source observations retain their historical provenance. Only the
explicitly linked current ExAV reviews establish revalidated source conclusions;
this document establishes no runtime result. Resolve remaining semantic questions
through the current difference registers before declaring parity.

## Acceptance cases to add with the relevant slices

These are **unexecuted journey candidates**, not normative old-manual assertions.
The table is updated below where current reviews already establish ExAV changes;
all remaining fields, restrictions and outcomes still require per-slice ExAV
source review. Keep the stable MAN-A identities, link MAN-E evidence where
relevant, and use [current ExAV scenarios](web-parity-scenarios.md) for expected
arithmetic/state boundaries. Historical `doc/parity/historical/scenarios.json`
seeds and MAN-A wording cannot override those specifications. Browser authority,
accessibility and safe-replacement requirements have their separate UX contract.

| ID | Candidate player journey; expected results require current ExAV bindings | Phase |
| --- | --- | --- |
| MAN-A01 | Open every in-scope toolbar shortcut through its menu equivalent and verify identical task/options; pause and source speed choices reflect confirmed time. Narrow English/Japanese layouts keep all delivered tasks reachable. | P1/P9 + each task slice |
| MAN-A02 | Create a named world with reviewed dimension bounds through empty/terrain-loader New providers (MAN-E02); save/restore without replacement on invalid input. A separate 2003 File import entry is not required. | P3/P10 |
| MAN-A03 | Choose rail start/end, inspect ghost/legal cost, cancel without mutations; commit route, then remove it. Reject obstacles and unsupported right-angle curves; exercise underground cut. | P4 |
| MAN-A04 | Join ground/sloped/elevated track and exercise supports, bridge/girder/tunnel geometry and legal targets under current ExAV rules. Include flat/slope speed-limit sections, removal and continuation (MAN-E05/16). | P4 |
| MAN-A05 | Build/orient/name stations and wide/slim-roofed/slim-unroofed platforms; verify source-derived length/placement/removal boundaries, material/roof appearance and underground picking. Cover current station information and counters (MAN-E03/06). | P6 |
| MAN-A06 | Connect/reconnect platforms using current range-8 enumeration, direct-station automatic selection and explicit connected-platform hosts. Cover vertical candidates, reparented child disconnection, host removal and no station callbacks when disconnected. Use MAN-D01's current dependency review. | P6 |
| MAN-A07 | Compare shorter/equal/one-car-longer formations approaching both directions under a stopping timetable; record the MAN-D03 decision and exact integer-boundary results. | P5/P6 |
| MAN-A08 | Configure independent platform names and bells; extend normal-platform rail connections while slim has no extension; save/reload and verify membership, bells and names. | P6 |
| MAN-A09 | Exercise passage/stair placement and removal using current legality; compare concrete/brown-wood/green-wood previews/outlooks and separate appearance from station host/service logic (MAN-E06). Resolve EXAV-GAL-001 before visual acceptance. | P6 |
| MAN-A10 | Browse current catalog metadata and permitted formations, including V2 fare/amenity/seated and packed capacity; inspect authoritative totals before purchase. Verify inventory/accounting once and persistence (MAN-E09). | P5/P8 |
| MAN-A11 | Group/name formations, assign controllers, deploy/orient/withdraw/redeploy and sell. Verify formation versus travel direction, outside/return behavior, exact resale anniversaries and missing purchase dates; invalid targets leave inventory/funds intact (MAN-E08/09). | P5 |
| MAN-A12 | Manage shared manual/TAT controllers; copy controllers/rules, confirm clear-all, read/append/save validated rule files and exercise source order/masks/reverse/empty-stock actions. Verify reference/deletion/continuation behavior and signal stops without passenger exchange; optional block/monitor logic stays pack-qualified (MAN-E07). | P4/P5/P6 |
| MAN-A13 | Inspect train/station data and lists, frame versus continuously track the selected train, and retain stable selection. Verify all six report periods, 31-day/12-month histories, genre settings, rollover and land-value inspection against current source (MAN-E03/04). | P5/P7/P8/P9 |
| MAN-A14 | Exercise cut slider/gesture, inclusive boundary, underground picking and tall-building/construction constraints; test documented pan modifiers or record their portable gesture dispositions. Terrain repeat and mountain-only terrace cases are covered. | P3/P4/P7/P9 |
| MAN-A15 | Review current labels/fonts/poles/error options and pole-menu synchronization; separate local preferences from shared bankruptcy/economy controls. Exercise effects/bells with browser gesture handling (BGM deferred) and current save/content/version help (MAN-E13/14/16). | P2/P6/P9/P10 |
| MAN-A16 | Run purchase/deploy/connect/timetable workflows through authority, stale revision, dirty draft, disconnect and world-replacement cases; observer browsing remains available, no old draft replays and local navigation never pauses another player. Verify keyboard/touch and English/Japanese field access. | Every relevant slice |
| MAN-A17 | Create and name nested train groups, assign group timetables, choose parent inheritance or a per-train override, move trains between groups, change the group timetable and save/reload effective controller identity. ExAV registers delegation (EXAV-ACTION-015); resolve null/root/cycle/defect cases during P5; offer keyboard/touch Move to group alongside any drag/drop. | P5/P10 |
| MAN-A18 | Target terrain corners in Raise and Dig, temporarily reverse with Ctrl, repeat with Shift plus mouse movement, release modifiers/focus without further mutations, and verify legal elevation, obstruction and water-level boundaries. Do not derive a timer-repeat or flooding algorithm from the wiki prose. | P3/P9 |
| MAN-A19 | Reach empty-world and terrain-loader creation through their reconciled New workflow; verify name/dimensions and terrain height/water-level settings, cancel without replacement, then save/reload. Account for terrain import without requiring the old duplicate File-menu entry. | P3/P10 |
| MAN-A20 | Account for current bank/stocks/subsidiaries, land trading, development tuning, road catalog/empty extension shells and appearance modes through actual source consumers or explicit adaptations. Verify the balance-sheet placeholder disposition and computed P8 adaptation separately; old road counts and FAQ finance omissions do not determine scope (MAN-E01/04/05/11/17). | P0-03/P2/P7/P8/P9 |
| MAN-A21 | Load with missing, incompatible or updated content definitions; diagnose required identities before replacing the world, preserve the file/current state, then recover with the matching content set. Help explains why removing a required plugin breaks saves; historical stock-plugin deletion advice is not the browser recovery path. | P0-05/P9/P10 |
| MAN-A22 | Review Help against delivered ExAV behavior and resolved MAN-E gaps, including inheritance, modifiers, stop/catchment diagnosis, appearance and pack compatibility. Verify localized narrow/desktop navigation; use reviewed browser illustrations and revalidated older text, keeping beta/Prototype 3 and outdated finance guidance out of stable instructions. | P9-HELP/P11 |

P0-03 workflow/scope mapping and P0-04/P0-06 source specifications are recorded
in the current [action](web-parity-actions.md), [geometry](web-parity-geometry.md)
and [scenario](web-parity-scenarios.md) reviews. This document does not reopen or
complete those source gates. Granular feature reconciliation, implementation,
executable fixtures, native/device UX acceptance and gameplay handoff remain
with their owning slices; overall P0/runtime acceptance is not established here.

**P1 spatial follow-through — 21 October 2026:** [client/server integration](web-parity-spatial-integration.md) delivers four/eight-direction target alignment, server bounds/projection checks and per-car heading reads. This supports MAN-A03 without closing its P4 multi-cell route, legality, cost, obstacles or removal acceptance. Source H/V rendering/picking fixtures support MAN-E16; full terrain/content/controller disambiguation remains with its owning slices.

**P1-02 delivery — 22 October 2026:** MAN-T01/MAN-A01 now have source-calendar
display and confirmed Run/Pause/Step in English/Japanese, with narrow-menu access
and Observer enforcement. [Evidence](web-parity-clock.md) also covers MAN-A16 local
navigation without shared-time changes. Source speed choices and complete menu
equivalence remain P9; these manual requirements are only partially satisfied.

## P4-01 railway source reconciliation

The [maintained railway contract](web-parity-railway-contract.md) and
[hashed source/decision review](parity/railway-review.json) refine the manual's
railway summaries against stable ExAV callers. CORE-03, CORE-06, EXAV-09,
EXAV-11 and EXAV-13 remain source requirements, not completed gameplay. The
[action overlay](web-parity-actions.md#p4-01-railway-workflow-bindings) is reciprocal.

| Workflow | Applicable manual anchors | Source refinement / next runtime owner |
| --- | --- | --- |
| Normal rail, attachment/detachment, piers | MAN-T02, MAN-M03, MAN-W01, MAN-W02, MAN-A03, MAN-A14 | Inclusive fixed-Z sign-vector routing; open/well-connected attachment; no-op demolition charges; actual usable start and terrain/water; P4-06–09. Browser authoritative removal preview and Confirm/Cancel adapt the manual's no-removal-ghost/direct-click workflow. |
| Slope and paired geometry | MAN-T03, MAN-W02, MAN-A04, MAN-E12 | Four rail positions/eight occupied cells; cardinal climb, per-position ground art, stored flags, explicit car checks; P4-10/11. Terrain slopes do not create rail slopes. |
| Bridge, girder, tunnel, garage | MAN-M03, MAN-W02, MAN-A04, MAN-E12 | Distinct predicates and booking: bridge need not cross water; girder offers eight headings; tunnel/garage removal excludes destination, garage repeats subtotal spend. No endpoint or economic fix approved; P4-12–15. |
| Signal and pole | MAN-M03, MAN-E12, MAN-A12 | Signal straight-pattern replacement/restoration and actual pass dispatch; pole cardinal pattern eligibility/single accessory slot. Source calls no money spend for either. P4-16/17; moving signal behavior/controller editing stays P5. |
| Railway accessories | MAN-T05, MAN-M03, MAN-A12 | Full fixed-size ground footprint, invisible upper occupancy, active pending ConstructionSite/completion path and inherited SUBSIDIARIES demolition. P4-18/P7; not P6 cosmetic passage/stair parts. |
| Speed limits | MAN-M03, MAN-A12, MAN-A16 | EXAV-ACTION-003 adds flat/slope sections and cleanup beyond the historical menu summary. TrafficVoxel eligibility, per-world lists and overlap rules; P4-19, prior-head scheduling P5. |
| Crossings/protection | MAN-E05, MAN-E11, MAN-E12, MAN-A14 | Source permits road/rail coexistence and creates crossings only under exact perpendicular/empty-slot conditions. P4-20 trusted interface fixtures; P7 playable roads, P5 trains and P6 platforms remain later. |

Historical developer anchors retain their original scope: DEV-P06 poles, DEV-P08
menus, DEV-P10 pictures, DEV-P11 signal side/loader, DEV-P12 fixed railway
structures, DEV-P13 road boundary, DEV-P14 special rails, DEV-P24 costs,
DEV-P26 sprites and DEV-P28 voxel boundaries. Native dialogs and historical
English text are donors, not authority for new prices or hidden build rules.
[The translation ledger](parity/railway-localization-review.json) assigns each
reviewed source string/donor to the owning playable slice and P4-22; it does not
claim shipped wording. For GEO-004/RAIL-D006 the owner confirmed source teardown
of all piers below regardless of owner on 30 October, to avoid confusing stuck
states for operators. P4-07/10/20 must preview every affected pier and retain that
cleanup behavior; no owner-only restriction or pending approval remains.

## P4-09 normal-track browser adaptation

MAN-T02/W01/A03 now have a playable normal-rail browser path: the
[Track task](web-parity-normal-rail-tool.md) and
[reciprocal action bindings](web-parity-actions.md#p4-09-normal-track-workflow).
Two map points or keyboard XY coordinates share a construction plane detected
from the start by default, with an explicit manual-level override;
server legality preserves the usable-start, stored-bounds, water, endpoint and
source cost rules. Explicit Confirm/Cancel replaces native immediate drawing,
with exact demolition/pier loss preview. Local height cuts remain view settings
and never choose a different construction level. The reviewed owner extension
compares two shortest legal candidates; source route order remains selectable.
EN/JA text and dedicated Chromium two-client/save/reopen cases accompany the
domain/session implementation. Moving trains, speed limits and special/sloped
rail retain their separate owners and acceptance gates.
