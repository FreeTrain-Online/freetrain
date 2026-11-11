**11 November 2026 — P6-27:** [Station delivery reconciliation](web-parity-station-delivery.md) supplies current runtime/content/control/translation bindings. Earlier source-review status below is historical. P6-28 overall acceptance and P7/P8/P10/P11 gates remain open.

# ExAV coverage, differences and delivery estimates

**Owner audio decision — 27 October 2026:** [BGM is deferred to a future improvement project](web-audio-policy.md). TinySynth demonstrated feasibility but produced audible pops/crackles and has been removed with its player and music controls. Sound effects and six bell previews remain. Digital audio, including reimagined MIDI compositions, is allowed; BGM/MIDI playback is no longer a P2/P9 or parity exit requirement.

**P2-01 implementation update — 25 October 2026:** [canonical bundled preparation](web-parity-bundled-preparation.md)
delivers reproducible pinned build inputs, converted bitmap surfaces/seasonal
picture bindings and an inert exact-ID registry. P1-01–04 foundations are
implemented; P1's final review and P2 full recipe/rendering/optional gates remain
open. Dated P0 implementation findings and estimates below retain their scope.

**Owner policy update — 22 October 2026:** [prototype Save 1 support is retired](web-parity-runtime-policy.md). Unsupported inputs report an error and preserve the current world/files; automatic and manual prototype writes are disabled. Earlier Save 1 compatibility/continuation statements below are historical and superseded. Canonical-world persistence remains open.

**P1 follow-up:** [spatial client/server integration](web-parity-spatial-integration.md) now delivers protocol 3, per-car heading reads and server-checked target alignment. Live gameplay remains explicitly in prototype/Save 1 compatibility mode.

**Final phase review — 21 October 2026:** [P0 coverage/specification is closed and P1-01 spatial primitives are implemented/tested](web-parity-phase-gates.md). P1–P11 and UX/native acceptance remain open. Earlier dated open-P0 statements below describe the pre-review state; source statuses/pins and runtime limitations retain their meaning.

**P0-07 report complete — 21 October 2026.** P0-01–06 now establish the
stable ExAV discovery, source reconciliation and scenario specifications below.
**P0-08 source inventory/specification and [P0-09 authority/scope integration](web-parity-exav-integration.md) are now complete; overall P0 and every P1–P11 exit gate remain open.** See the separate
[optional V2 review](web-parity-optional-plugins.md) for its 21 October follow-up. This report
closes reporting and provisional sizing; the existing UX handoff still governs
gameplay implementation.

Authority is **FreeTrain ExAV 2.1.1.12 / V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, SVN r115, **`trunk/`**.
The implementation inspected for this report is
`dae7480eaa22dacd3a2e43273de91abf8754873a`. Source checks read pinned Git
objects from the explicitly supplied archive, including where approved working-tree
corrections exist. Original FreeTrain, Avalonia, manuals and earlier optional
copies retain their comparison/translation roles under the
[source policy](web-feature-parity.md). Beta/alpha behavior is outside stable scope.

## Coverage and evidence levels

| Workstream | Reviewed denominator and present evidence | Remaining acceptance / phase owner |
| --- | --- | --- |
| [P0-01 discovery](web-parity-exav-inventory.md) | 1,041 hashed files; 456 C# files; 67 projects, 33 in the selected solution; 1,158 project item/reference edges | Lexical/project inclusion is not runtime reachability. Preserve source-only, authoring, native and excluded records. P0-09 scope assignment complete; P1–P11 feature acceptance pending. |
| [P0-02 content](web-parity-content.md) | 56 manifests; 482 direct declarations including 33 factories, one generated child; 347 implementation bindings; all 75 missing DLL edges mapped to qualified source/projects; 238 media, 224 referenced and 14 alternatives | Full importer, portable code registrations/effects, converted assets and runtime recipe/gallery validation. P2 and owning P3–P8 slices. |
| [P0-03 actions](web-parity-actions.md) | 298 bindings, 1,108 additional surfaces, 87 workflow families / 46 routes; all 483 contribution consumer/support decisions and 56 dependency closures | 154 of 166 UI source reviews require feature detail; 12 have explicit scope/support dispositions. Granular controls, dependency semantics, reciprocal MAN/DEV links and text classification ship with each slice. P1–P9/P11. |
| [P0-04 geometry](web-parity-geometry.md) | 98 distinct reviewed files across source/dependency/gallery work; 60 patterns, 16 support slices, 48 level / 64 slope poses, 192 attachment decisions; 152 initial rail/car compositions | Native/full-world render, placement, picking, movement and save-continuation comparisons. P1/P2/P4–P6/P10. |
| [P0-04 expanded gallery](web-parity-geometry-gallery.md) | 332 raw and 1,176 composition records; all 28 contacts inspected; six raw / 12 composition records diagnose an invalid source crop | EXAV-GAL-001 has the P6-15 portable x288 correction; original diagnostics and native/human qualifications remain separate. Expanded counts stay separate from the initial 152. |
| [P0-05 persistence](web-parity-persistence.md) | 195 candidate files, 377 declarations / 1,048 fields; 26 semantic dependencies, 24 implementation files; callbacks, custom writers and two older structural save profiles | Version/schema/pack dispatch, subsystem state, stable-runtime save fixtures, safe import, migration and continuation. P1/P2/P3–P10. |
| [P0-06 scenarios](web-parity-scenarios.md) | 31 independent cases / 38 hashed files; six bilingual workflows; three proposed city profiles, two integration companions and full-content gallery recipe | Fixture generation, exact content bindings, instantiated legality, execution, continuation, capacity and native acceptance. P1–P11. |
| [P0-09 integration](web-parity-exav-integration.md) | EXAV-01–16, eight gap dispositions, 465 stable source assignments / 73 historical-only paths, 24 scope groups and six archive profiles | Feature detail/defect choices, reproducible bundled build migration and runtime acceptance remain with P1–P11; overall P0 and UX handoff stay open. |
| [P0-08 optional V2](web-parity-optional-plugins.md) | 4,933 hashed files; 452 source manifests / 11,484 declarations / 23,417 generated children / 4,399 media; stable profile selects 450 manifests / 11,470 declarations; 21,686 text candidates | Startup preparation/loading, localized per-set credits, image/case repairs, map-capture adapter/scope, per-file conversion terms and save/runtime evidence. P2/P9/P10/P11. |

These denominators overlap and describe different things. Do not sum UI bindings
and surfaces, declarations and media, or source files across reviews into a feature
total. The 483 content records include factories, support and exclusions; they
are not 483 placeable assets. The discovery ledger retains **`inventoried`**
implementation status. Source-reviewed/specification-complete records do not
become implemented or verified when a checker passes. No aggregate ExAV runtime
completion percentage is established.

The running slice still uses the obsolete original-FreeTrain prototype: four directions and eight track shapes in
[Definitions.cs](../src/FreeTrain.Online.Simulation/Prototype/Definitions.cs), a
minute-per-tick [clock](../src/FreeTrain.Online.Simulation/Prototype/SimulationClock.cs),
the fixed 64×64×16 [reference scenario](../src/FreeTrain.Online.Content/ReferenceScenario.cs)
and the curated historical [asset importer](../scripts/import-web-content.py).
Protocol **2**, `prototype-content-1`, Save **1**, three manual slots and the
**1 MiB** save bound remain delivered values. P0's reserved ExAV names and city
budgets do not change them. Authority, sessions, recovery, panels, drafts, leases,
canvas lifecycle and bilingual shell are reusable implementation foundations;
their prototype tests do not verify ExAV geometry, economy or content.

## Reused evidence and rederived requirements

| Evidence family | Reuse justified by the ExAV review | Newly derived work / limit |
| --- | --- | --- |
| Discovery/comparison tooling | Inert Git/XML scanning, hashes, scope/status model and historical reproducibility machinery | Separate archive selector, namespace and ExAV outputs. Of 456 same-path C# comparisons, 3 are byte-identical, 425 differ and 28 have no historical path; byte differences include encoding/line endings. No blanket semantic equivalence. |
| Content/resources | Decoder/recipe machinery and narrowly matched TAT GIF/ImageStream evidence | Reconcile direct building families, V2 fields, native wrapper effects, registrations, notices and state against ExAV. Absent Straight.bmp remains a build diagnostic despite reused embedded-image evidence. |
| Geometry | Pattern records and direction indices/vectors match; eight distinct bitmap inputs for the initial gallery match, with callers reviewed separately | Regenerated ExAV pose/attachment/gallery tables; changed girder art, new passage art, speed limits, platforms, forwarding, costs and native effect boundaries. Matching art does not establish native pixels. |
| Actions/manuals | Browser destinations, UX contracts and historical MAN/DEV families remain usable scaffolding | 15 current action findings supersede old reachability/effect conclusions. Historical per-control expressions and same IDs remain donors until slice review. |
| Persistence/scenarios | Inert NRBF inspection, trace structure, draft/readiness/authority design | Stable ExAV field/callback graph, separate old-save profiles and source-derived fares, packed boarding, calendar, timer, growth, resale, finance/history expectations. Prototype arithmetic is not reused as ExAV expected behavior. |

## Difference register and unresolved decisions

The linked machine-readable registers retain individual hashes, symbols and
dispositions. Rows below group delivery obligations; they do not merge their IDs
or resolve a source defect by implication.

| Register / source difference | Required disposition and next owner |
| --- | --- |
| [Discovery](parity/exav-ledger.json): 351 shared direct IDs, 131 ExAV-only, 42 original-only | Matching IDs do not establish matching definitions. Original-only IDs are historical/compatibility differences, not missing bundled ExAV content. P0-09 scope integration complete; P10 compatibility pending. |
| [EXAV-CONTENT-001–008](parity/exav-content-rules.json) | 66 commercial / 55 variable-height definitions have development consumers; removed direct menus and builder stubs require the planned P7/P9 catalog/placement adaptation. Preserve Int64 values, seated capacity, effects and state. Source/project bindings do not establish a successful Windows build. |
| [EXAV-ACTION-001–015 / EXAV-UI-DEP-01–05](parity/exav-action-rules.json) | Include reachable station/train information, tracking, speed limits, report histories, diagram copy/files, passage materials, development tuning and group delegation. Resolve atomic validation, delegation and timetable defects in P4–P10. Old source-only/unused-selector claims do not transfer. |
| [EXAV-GEO-01–11](parity/exav-geometry-rules.json), [four dependency chains](parity/exav-geometry-dependency-review.json) | Implement eight-direction terrain/host/drawing/timing contracts, source money/cadence, forwarding/TURNING, speed and platform cases in P1–P6. Source specifications are complete; runtime/full-world fidelity remains open. |
| [EXAV-GAL-001](parity/exav-geometry-gallery-review.json) | SOUTH stair index23 selects outside its sheet. P6-15 uses the authored x288 partner in three prepared recipes with x388 correction metadata; raw diagnostics remain unchanged. [P6-27 reconciliation](web-parity-station-delivery.md) separates portable pixels/picks from unaccepted original-runtime/native/human fidelity. |
| [TRANS-001–012 / SAVE-001–006 / EXAV-SAVE-007–008](parity/exav-domain-transition.json) | P1/P2/P10 implement distinct ruleset/protocol/content/save/pack dispatch, prototype disposition and inert legacy mapping. The supplied 0.4 and owner-reported 0.6.2 saves have 12 / 2 field-name differences from stable; neither is stable-runtime continuation evidence. BZ detection survives but decompression is commented out in stable code. |
| [EXAV-SCN-D01](parity/exav-scenarios.json) | P6/P7: station creation-order cube/spherical reach mismatch and replacement listener registration need boundary/order/save cases and explicit behavior disposition. |
| EXAV-SCN-D02 | P8: bank asset recalculation uses debts for deposit totals; cancellation recalculates before removal. Preserve source outputs separately from any approved correction. |
| EXAV-SCN-D03 | P5: disconnected train paths can leave a null next state before scheduling. A passing free-reverse case does not resolve this case. |
| EXAV-SCN-D04 | P1/P8: integer intermediates/overflow and whole-year bank interest require explicit boundaries; do not substitute fractional interest or translated monetary units. |
| [EXAV-OPT-001–010](parity/exav-optional-rules.json) | P2/P9/P10/P11: explicit directory loading, separate catalogs/caches/notices, localized credits XML/button per enabled set; preserve immediate-child membership, 27 image gaps / 36 case-only edges, map-capture code gap and older-suite profiles. |
| [EXAV-DIR-001 / EXAV-ENC-001](parity/exav-baseline.json) | Owner-approved SOUTH typo correction and UTF-8 manifest/fallback-reader conversion are recorded separately from pinned source. Port these decisions; do not repin or replace evidence with corrected working files. |

Source/project gaps also stay visible: seven unresolved project items (five old
core entries and Straight.bmp in two TAT projects), two core C# files without
selected-solution membership, Windows/native dependencies and source stubs.
P0-02's mappings explain missing DLLs without removing the discovery diagnostics.
[P0-09 integration](web-parity-exav-integration.md) now assigns residual source/scope decisions; implementing slices must record
equivalent portable adaptations versus accepted semantic changes before verification.

A4/external user-file music and debug remain excluded. Effects and bells remain
required; [bundled BGM and silence controls are deferred](web-audio-policy.md). Video is absent from stable trunk. Empty road
accessory/dummy-car extension shells do not establish a working bus requirement.
No implemented Undo/Redo binding was found. The reachable balance-sheet placeholder
retains its planned computed P8 report. Exclusions, unfinished source and planned
adaptations must remain visible in release scope.

## Translation gaps

| Inventory | Current review boundary |
| --- | --- |
| XML: 977 candidates, 641 with English donor candidates | All 977 retain pending feature wording review and null proposed English; 336 have no donor candidate. These include scope/support records, not only player-visible text. |
| C#/resx/HTML: 3,671 candidates, 997 containing Japanese, 2,543 with ASCII English donor candidates | All retain pending classification/wording review; 1,128 have no donor candidate. 2,345 are unclassified C# literals and may be IDs, paths or formats. Neither number is a count of missing player translations. |
| Optional XML: 21,686 candidates under separate pack identity | P0-08 occurrence inventory only; all need feature wording review, including collection credits supplied in the optional repo. Do not combine with bundled counts. |
| Six P0-06 bilingual workflows | Representative acceptance designs only; no locale workflow was executed in this report. |

These inventories are occurrence-based, overlap and include non-presentation and
excluded text. Do not add them into a unique-string backlog or count donor matches
as approved translations. Each P1–P9 feature classifies its entries, preserves
ExAV identity/Japanese text/hash, reviews exact carryovers versus changed meaning
or new translation, and records English wording/donor/status. Review raw money and
display units, total/seated/packed capacity, actual/demand populations, speed
categories, forwarding, history periods and settings scope. Both locales need
interpolation, long-name, open-draft language-switch and layout evidence. P9-HELP
assembles the completed chapter manual; P11 reconciles the final in-scope ledger.

## Optional sets and distribution accounting

| Source family | Recorded totals and authority |
| --- | --- |
| Stable bundled ExAV | **56 manifests / 482 direct declarations / one generated child / 238 media**; current reviewed LGPL bundled denominator, including explicit exclusions. |
| Canonical non-LGPL V2, `freetrain-online-plugins-nonfree`, original `c8ec62e47a7f5089b666e57072164a0e14934466`, root `plugins/`; normalized `55ac13e7d292b1d003cf6f97a63be1675d7202f4` | **P0-08 source inventory/specification complete:** 452 manifests / 11,484 declarations / 23,417 generated children / 4,399 media. Immediate-child profile selects 450 manifests / 11,470 declarations / 23,273 generated children; nested source-only copies explain all 158 source ID conflicts. No runtime compatibility verdict. |
| Earlier AdditionalPluginSet V2 copy | Historical 452 manifests / 11,484 explicit declarations / 11,470 distinct explicit IDs, with 14 duplicate occurrences. Not a current canonical-pack total. |
| Earlier AdditionalPluginSet V1 copy | Historical 436 manifests / 11,420 explicit declarations and distinct explicit IDs. Compatibility/reference scope, not a second gameplay authority. |
| Earlier combined ExAV/community tree | Historical 508 manifests / 11,966 explicit declarations / 11,952 distinct explicit IDs. Includes optional content; not stable bundled coverage. |
| [Earlier PluginsExtra ledger](parity/historical/extra-suite-ledger.json) | 228 manifests / 6,216 direct declarations / 6,610 generated children / 2,511 media at its recorded `6886a0f` provenance; separate owner-requested compatibility evidence. |

Earlier-copy counts retain their dated research provenance under the maintained [P0-09 content boundaries](web-parity-exav-integration.md#branch-and-content-boundaries). The canonical [P0-08 review](web-parity-optional-plugins.md) now binds
source/normalization hashes, six local includes, generated identities/direction
selections, selected membership, dependencies, notices and translation candidates.
It finds 773 missing DLL edges with exact stable source/project bindings, 27
missing image edges and 36 case-only edges, plus one unbound map-capture type.
The earlier PluginsExtra suite has 82 direct IDs absent from canonical V2;
V1/older-suite compatibility remains separately profiled P10 work. The supplied
0.4 sample resolves 138 IDs in selected optional V2 and 11 in bundled ExAV, with
one unmatched legacy ID; the seven 0.6-era sample IDs are bundled. These are
identity matches, not recipe/import/continuation evidence.

Owner-directed delivery keeps plugins in their separate repo. Explicit startup
plugin-set directory arguments prepare/load portable content and add one localized
credits button per enabled set from that set's XML. Conversion/loading, credits
UI and compatibility remain implementation work. Default builds have no optional
sibling dependency; optional outputs/caches/notices/packages stay separate. Source
metadata totals must not be presented as combined playable coverage.

## Revised phase estimates

These are **low-confidence planning ranges in focused engineering days for one
engineer familiar with this codebase**, assessed on 21 October. They are judgments
from the reviewed scope/dependencies, not measured throughput, delivery dates or
the prototype's superseded 20–35-day allowance. Each feature row includes its
detailed source/translation review, implementation, meaningful fixtures, subsystem
UI and integration/fidelity correction. P9 covers remaining shared workflows/help;
P10 covers migration/legacy import beyond ordinary slice save support; P11 covers
integrated acceptance beyond slice checks, avoiding duplicate effort allowances.

| Phase | Remaining days | Sizing basis / main uncertainty |
| --- | ---: | --- |
| P0 closure (original P0-07 allowance) | 5–10 | P0-08 source/specification and P0-09 authority/scope integration are now complete; final coverage/schema/priority review and UX handoff remain. This dated allowance is retained pending re-sizing, not a fresh remaining-P0 estimate. |
| P1 spatial/time/contracts | 10–20 | Eight directions, terrain/clock/timer/random boundaries and coordinated contract/version/Save 1 dispatch. |
| P2 content/rendering | 25–45 | 483 content records / 238 media, factory/recipe conversion, native effects, traversal/picking, full runtime gallery and reproducible build-input migration. |
| P3 terrain/world creation | 10–20 | Two providers, terrain/corners/Mountain2, heightmaps, water/underground legality and save/reopen UI. |
| P4 rail infrastructure | 20–35 | 60 patterns, support/host dependencies, routes/curves/slopes/bridges/tunnels, signals/poles/speed limits and authoritative cost preview/booking. |
| P5 trains/controllers | 25–45 | Full state machine/formation identity, purchase/resale, blocking/outside return, manual/simple/delegation/TAT, TURNING and diagram validation. |
| P6 stations/passengers | 15–30 | Platform/passage families, listener migration/catchment anomalies, packed loading, fares/upkeep, information/counters and bells. |
| P7 city/development | 25–45 | Roads/land/ULV, building families and missing direct-placement adaptation, half-voxels, growth/population/construction and controlled randomness. |
| P8 economy/plugins | 20–35 | Accounts/history/fiscal rollovers, bank/stocks/subsidiaries/stadium, computed reports and explicit source-anomaly decisions. |
| P9 workflow completion | 15–30 | Remaining 87-family destinations, complete catalog/settings/view/help (BGM deferred), chapter manual and cross-feature bilingual/authority review. |
| P10 migration/import | 20–40 | Stable runtime fixture acquisition, old type/layout/pack mappings, diagram/save import, rejection/recovery and paired continuation evidence. |
| P11 integrated acceptance | 20–35 | Legal-city/catalog campaigns, long-run/soak/performance, release/default-package separation, recovery and ledger/locale/difference closure. |

The additive **bundled remaining-work envelope is 210–390 engineering days**,
including residual P0. It excludes optional-pack implementation/compatibility
beyond P0 inventory, and device/access waits. For planning only, reserve a further
**15–40 days** for optional conversion/package/startup/diagnostic/compatibility
work; this remains the dated P0-07 conditional allowance, **not a complete pack
quote**. P0-08 now identifies a map-capture adapter gap, missing/case-only image
references, large recipe/translation scope and pending per-file conversion terms.
Re-size during the first P2 optional conversion slice before committing effort.
No reliable combined total or calendar completion date is available.

Native Chromium/device/IME/assistive checks may take roughly **1–3 active days**
with devices and reviewers available, plus uncapped fixes/waiting; this is a separate
pre-implementation handoff allowance. Other-engine execution remains owner-deferred.
P0-09 source integration is complete. Close the final P0 review and UX handoff, then P1/P2 → P3 → P4 → P5/P6;
P7/P8 depend on P1 scheduling/random/accounting and integrate with railway flows.
Subsystem UI/save work ships with each feature. Re-estimate with P0-08 findings during initial P2 conversion, the first
complete terrain/rail/train/passenger slice, and the first economy/development slice,
using actual effort, instantiated recipe gaps and legal-city capacity measurements.

## UX, runtime and release gates

UX0–UX2 and UX3 implementation/automated foundations retain their dated evidence.
The [carried-forward manual sheet](../tmp/parity-p0/manual-checks.md) still records
native desktop Chromium 100/150/200% zoom, Japanese IME, physical touch, virtual
keyboard/rotation/safe area and assistive technology as unperformed.
**UX3-14/17, A-03/A-14 and UX3-GATE remain open.** Source/report work may proceed;
this report does not authorize the gameplay handoff or expand browser scope.

The [18 October P0 provenance audit](../tmp/parity-p0/provenance.json) records
129 .NET / 55 frontend / 47 Chromium checks at `a48e8fa`, matching runtime
sources at that revision and isolated final browser captures. It also records
six older UX3 source-manifest mismatches and 28 mismatched screenshots whose
original copies were not recovered. Keep those older images and performance
qualified; later automation does not restore their provenance or establish
exact-current-HEAD acceptance. No application/browser/deployment tests were rerun
for P0-07.

Historical UX3 production stress **29.67 fps** remains below the proposed
30 fps target, and is not legal ExAV city capacity. P0-06's small/medium/large
budgets and latency targets are proposals; runtime legality, memory/transfer
limits, full-catalog decode, Linux/native deployment and stable save continuation
still require measured evidence with exact implementation/content/fixture hashes.

## Reproduction and completion boundary

The P0-07 checks on 21 October all passed using the pinned archive:

```sh
python3 scripts/inventory-parity.py --source exav --source-root /path/to/freetrain-exav-archive --check
python3 scripts/reconcile-exav-content.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/reconcile-exav-actions.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-exav-geometry.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-exav-persistence.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-exav-scenarios.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
git diff --check
```

All **93 Python tests** pass. These checks reproduce source accounting/reviews,
gallery metadata and selected independent scenario arithmetic/geometry; they do
not execute ExAV assemblies or the modern game. P0-07 publishes the required
coverage/differences/translation/estimate report with optional uncertainty and
native gates intact. P0-08 has subsequently closed at source/specification
level with its own checker and tests; see its reproduction instructions.
Next: **final P0 coverage/schema/priority review and the existing UX handoff** before affected
P1/P2 gameplay work. No generated discovery status, source pin, asset, runtime
version or service limit changes in this reporting batch.

**P0-09 follow-up — 21 October 2026:** all eight explicit-source checks (discovery, content, actions, geometry, persistence, scenarios, optional V2 and integration), **105 Python tests** and `git diff --check` pass. The [integration checker](../scripts/review-exav-integration.py) also pins the inspected importer, Docker inputs and current protocol/save constants at implementation `357c6e4095e98333e619d153db679c731e87831c`. No application/browser/native execution or content conversion was performed.
