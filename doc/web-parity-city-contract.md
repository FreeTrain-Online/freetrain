# P7-01 city/development source contract — 11 November 2026

**Complete at source-specification scope.** The reviewed authority is stable ExAV
2.1.1.12/V2, Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, SVN r115,
`trunk/`. This slice changes no production code, Save10/protocol9, content identity,
limits or shipped EN/JA text. P7-02 owns independent fixtures; P7-03 onward owns
implementation. City gameplay and P7 phase acceptance remain open.

The [source contract](parity/city-review.json) pins original Git blobs, exact
line/symbol excerpts, source declarations, operation/control ownership, four
dependency chains and twenty decisions. The [translation review](parity/city-localization-review.json)
retains stable Japanese literals, catalog metadata and historical English donor
provenance with explicit classifications. The checker reads pinned Git objects;
archive working-tree corrections and this repository's historical `core/` tree
do not supersede the authority. No desktop assembly or serialized object is loaded.

## Exact scope denominator

The review covers **193 source/manifest/project files, 194 inert resources,
16 operations, 215 lexical control/action records, 209 P7-assigned declarations,
nine supporting factory registrations, nine source-code registration references and 252 authored category entries**.
These are overlapping source inventories, not additive gameplay counts. It binds
776 source literals and 324 manifest text records; the 252 category names are
separate. Every record names its implementing or cross-phase owner. The original
P0 records remain immutable; this is their consuming overlay.

| P7-assigned declaration type | Count | Consumer / disposition |
| --- | ---: | --- |
| `commercial` | 66 | Fixed city producer/content; P7-07–09/12/13. |
| `varHeightBuilding` | 55 | Variable-height construction/recipes/population; P7-10/12/13. |
| `HalfVoxelStructure` | 8 | Shared half occupancy and first-listener behavior; P7-11/12/13. |
| `land` | 66 | 17 fence declarations (16 builders and remover), two X/Y vinyl houses, bulldozer, creek, 25 farmland/static entries, 18 forest and two ricefield entries; P7-16–20, existing P3 generation retained. |
| `road` | 3 | Standard15 two-lane DH, A3 two-lane PC-9801FA and Standard15 one-lane Syosei; P7-21–23. All three declare street/shoulder; lanes2/2/1. |
| `menu` | 6 | Generic/half building, road accessory/dummy, land-value inspector and development settings registrations. Empty extension menus do not imply instances. |
| `dockingContent` | 1 | Building categories, P7-12/P9. |
| `GenericStructure` / `generatedStructure` | 1 + 1 | Hidden zero-volume `null-contrib` / `null-contrib-0:0`; no playable footprint. |
| `specialStructure` / `anonymous` | 1 + 1 | Stadium and team-name provider; P7 producer interface, P8 playable event lifecycle. |

The source includes `roadAccessory` and `DummyCar` factories/menus but **zero
bundled instance declarations**. The nine factories are support records already
in P2's denominator, not nine extra city definitions. MAN-O04's six historical
menu labels do not create missing bundled roads. The obsolete DummyCarController
is absent from selected compile items; `Bus.draw` throws and no `new Bus(` caller
was found in the pinned C# tree. Moving buses/dummy cars and the separate
Heat/RoadSimulator experiments remain outside baseline scope. Reachable
`core/world/development/LandValue.cs` is required.

Optional V2 content retains the separate [optional review](web-parity-optional-plugins.md)
and its own pins, assets, translations, licenses and execution evidence. This slice
neither imports optional content nor credits optional runtime tests. The generic
null entry is the sole bundled generated child; historical FreeTrain's 116-child
count does not transfer. CategoryTree IDs, aliases, names/icons and byid/byname/hide
metadata are real catalog data. DEV-D06's proposed density, sales, affinity and
bus models do not become implemented economic rules.

## Callers, lifecycle and accounting

Registration flows from `system` factories/PluginManager, MainWindow's land/road
commands, dynamic generic/half menus and registered LandValueInspectorPlugin and
DevelopConfigurePlugin. Direct legacy commercial/variable menu callbacks in
MainWindow are commented. The reachable generic selector has only the hidden null
entry in bundled generic content. P7-12's already-reviewed direct catalog adaptation
makes direct definitions usable; it must bind their exact loader, supported
orientation/variant and source caller contract rather than infer reachability
from class existence.

| Actual caller | Source behavior / implementing consequence |
| --- | --- |
| Reachable `MultiSelectorController.onClick` | Fixed `unitPrice` or variable `price*height` spent to Subsidiaries when a timed site starts. Positive full occupancy check. Null child skipped by volume check. Randomization after source click is UI state; a rejected browser operation cannot advance simulation RNG. |
| Legacy `FixedSizeStructController` / `VarHeightBuildingController` | Timed owned site and completion without an explicit start/completion price spend. These source-only entry witnesses are distinct from the charged selector. Variable native display increments4 and divides by4; generic selector uses internal height units directly. |
| `FixedSizeStructurePlacementController` | Its condition is inverted: legal predicate true shows error; else constructs. Preserve the source defect as evidence; the existing direct-catalog adaptation uses positive legality and rejects invalid occupancy atomically. |
| Automated Commercial/VarHeight plans | Timed sites complete to unowned structures; no invented manifest-price start booking. Fit removes old entities and builds in the same Process call. LandPlan creates immediately with owned=false. |
| Fixed/variable demolition | Subsidiaries spends `500000*sizeX*sizeY`, original Int32 multiplication before long use; cleanup/listener/callback effects belong to the same successor. |
| Half-building create/add/remove | Initial owner spends definition price. Compatible second-half add has no spend and does not rebuild listener. Removing one half preserves initial listener; removing last/whole owner spends300000 and detaches. |
| LandBuilder location wrapper | Normalize/clamp inclusive rectangle; owned operation spends selected area after subclass create, including skipped cells. Rice and X/Y vinyl-house overrides additionally spend per successfully created owned cell. Preserve both calls. |
| Fence line / rectangle | Side overload sets individual edges without area spend; rectangle via LandBuilder books selected area. Empty cells create owned DummyVoxel; existing voxels carry fence references. |
| Road build / remove | New-road cells cost `300000*heightFactor` (underwater branch0); railballast build uses RailService, others RoadService. Existing-road attach is free. Removal uses eligible count×100000×(abs(finalZ−water)+1), RoadService, skipping train cells. |
| Land buy / sell | Selector starts at exact ground then holds anchor.z and normalizes XY; later rectangle cells need not share ground. Buy runs reclaim-then-reread-then-buy in X/Y order, including entity removal side bookings. Sell credits **current** Voxel.landPrice for LandPropertyVoxel only, not purchase price. |

`ForestBuilder` actually stores tree art/patterns/value/owned state on
`MountainVoxel.setTrees`; `ForestVoxel` is an alternative source class with no
construction caller. Neither establishes an aging timer. Static/random land,
vinyl house and rice have their own provider/ownership/cost behavior. Creek uses
the endpoint-seeded decorative path already reviewed in P3. No farming economy,
variable water or category-derived population is inferred.

Construction reuses P4's StationaryConstruction implementation. Source allocates
height/X/Y voxels, chooses door state (except the southwest corner), registers
40+Next(80) one-shots and progresses empty→bone1→bone2→bone3→walled. Decorative
ground draws can consume a callback without state advancement. Work is allowed
09:00–17:59 on nonweekend days, including weekday holidays. Vertical floor gates,
cancellation, once-only completion and all draw/registration order remain P7-02/04
contracts. Station construction stays immediate. Fixed structures occupy invisible
upper voxels; variable stacks use top-before-bottom selection and authored overlays;
half structures share a source owner, not two overlapping full boxes.

## Decision register

The detailed records bind source symbols, dispositions, owners and acceptance
conditions. **No new simulation correction is authorized here.** Retain source
quirks unless an existing plan adaptation applies; any proposed semantic change
must be recorded explicitly before its runtime slice. Existing deterministic,
finite-state, pure-preview and detached atomic-publication rules apply without
creating a new approval flow.

| Decision | Disposition and owner |
| --- | --- |
| CITY-D001 | Preserve H/V mapping, read `q[h+1,v+1]` versus addQ/Rho/update at `[h,v]`, source parity/borders. Invalid source indexing rejects atomically; no offset alignment correction. P7-02/06. |
| CITY-D002 | Preserve zero q/back, bare-rho interior only, zero borders, no initialization terrain rescan, buffer swap/write order, relative10h timer, Single/double/truncation grouping. P7-02/06. |
| CITY-D003 | Preserve last-Traffic road overwrite (including null), road→sea→mountain precedence and actual change notifications; road detach is not automatically a conductivity rescan. P7-06/21/23. |
| CITY-D004 | Native preview underquotes reclaimed land. Existing P7-15 preview policy stages actual source buy traversal and all nested removal fees; reviewed quote equals actual debit or requires renewed review. P7-02/15. |
| CITY-D005 | No stored acquisition-price resale input; sell current value. Planning wording corrected. P7-15/P8 receipts. |
| CITY-D006 | No silent ULV invalidation: preserve non-null cached Cube result, retry null. If observable, stale snapshots are durable state, not a cache reconstructible from current world. P7-03/05/25. |
| CITY-D007 | Existing deterministic policy supplies stable contribution-ID price ties and station-ID plan traversal; retain actual price search/range arithmetic. P7-25/26. |
| CITY-D008 | Replace process statics with named saved world RNG/settings under P1/session policy. Isolate preview/gallery/UI draws; preserve commit constructor/door/retry bounds/order. P7-04/05/27/28 and valuation. |
| CITY-D009 | Preserve valid source FindFirst/Last/range/variable guard outcomes; n<=0 ends without selection draw. Empty-array/index faults are explicit invalid states, atomically rejected. A different empty-pack no-candidate policy needs a recorded adaptation before P7-25. |
| CITY-D010 | Atomic parse/validate of source-safe settings; no partial assignment, nonfinite power, invalid RNG bounds or corrupt minute. Derive safe domains independently before P7-28; no invented balance ranges. P7-02/28. |
| CITY-D011 | Preserve11 exposed values, preset Save/Load versus Apply, and frequency scale versus captured callback interval. F_PopAmpBase10 is not a field. No silent timer reschedule. P7-05/06/28. |
| CITY-D012 | Preserve caller-specific owned/unowned, booking/no-booking, workday/vertical/decorative-ground gates and timed completion/cancellation. P7-04/07/10/27. |
| CITY-D013 | Use the already-reviewed direct-catalog adaptation for commented/inverted/throwing native builder paths, with positive legality; hidden null is not a legal building. P7-09/10/12. |
| CITY-D014 | Preserve area versus per-success versus no-booking land/fence entry semantics, rice/vinyl extra calls, actual forest backing and no invented growth/provider. P7-16–20. |
| CITY-D015 | Preserve Commercial/HV market valuation and variable height×price distinction, including source `price +=` repricing. Real temporal value state is a P7 prerequisite; P8 full markets remain separate. P7-03/07/25/P8. |
| CITY-D016 | Preserve P6 catchment asymmetry, actual completed producers and ordered atomic passenger effects. No active bundled GlobalTrafficMonitor subscriber exists; keep the notification interface without a fabricated consumer. P7-13/14. |
| CITY-D017 | Exactly three bundled roads; empty extension APIs/menus, source-only obsolete controller and Bus stub retain dispositions. P7-21–24/P9. |
| CITY-D018 | P7 special producer interface and P8 complete stadium/game/finance/report lifecycle; no ordinary constant stadium substitute. P7-03/13/P8. |
| CITY-D019 | Half owner retains constructor's listener on second-half add/partial removal; no sum/refresh or second charge. Both creation orders and save continuation required. P7-11/13. |
| CITY-D020 | Player direct-catalog adaptation binds charged reachable selector unit/height price and timed site; legacy no-spend witnesses remain caller-qualified, automated plan uncharged. P7-07/09/10/12/27. |

Development's live source strength is `(ScoreImported + 0.1*ScoreExported)*0.2`;
budget truncates ScoreTrains. Midnight creates/updates scored-station plans;
other hours decrement budget then process the saved phase. Land20 gives the
V2 window200000–350000. Candidate `computerCannotBuild` is element presence,
separate from manual player eligibility. Preserve the source range counts
`last-first`, variable-array guard and same-call fit/remove/build. The reviewed
ULV function deduplicates entities but sums land per surface cell. Source
replacement protects owned entities and construction sites. Station scores are
nonlinear transport-log values and train-call counts, not unique trains or
arithmetic daily passenger means.

## Cross-phase dependency handoff

Consume [P6's concrete interfaces](web-parity-p7-p8-station-handoff.md): extend
controlled-only StationProvider Input/kinds/codecs through real spatial owners;
attach on source completion, detach on removal, update actual land-value metadata.
Keep outgoing demand distinct from Residents/Incoming. Preserve sphere-versus-cube
creation order and separate local/reverse station memberships (STATION-D003 /
EXAV-SCN-D01). No provider graph reconstruction may repair the missing fifth
reverse registration. Half-owner first-listener semantics are an additional
P7 lifecycle contract, not permission to refresh that graph.

Apply ordered StationPassengerEffect deposits inside the detached successful
minute before publication, exactly once, with accounts/random/queue/owners in
the same rollback boundary. GlobalTrafficMonitor's nonzero-unload notification
has no active bundled subscriber: the development subscription is commented.
Development consumes actual station statistics through its hourly timer instead.
Transport intent is not a bus simulation or durable event log.

P7 owns land/property, source value inputs, required entity repricing, construction/
removal bookings and producer/development consumers. `SubsidiaryCompany` initially
and subsequently **adds** structure price + center-sampled land area +10×profit
to existing market price. The source piecewise logarithmic Parameters.profit
is required wherever that value feeds P7. Freeze the required value/temporal
inputs and callback/random continuation before development uses them. P8 owns
full market listings/trades, finance, histories/reports and stadium season/game
execution; it cannot be approximated by fixed values in P7's dependency path.
P10 owns historical imports; P11 retains full-city/soak/native/other-engine/real
optional acceptance and all earlier capacity misses. No format or limit increase
is implied by this source review.

## Operation and MAN/DEV bindings

These rows are reciprocal with the [action overlay](web-parity-actions.md#p7-01-city-workflow-bindings)
and [manual reconciliation](web-parity-manual.md#p7-01-city-source-reconciliation).
DEV identifiers refer to the existing local developer review; its P7-01 addendum
retains DEV-A07 runtime obligations. Each operation's independent acceptance
condition is stored beside its pinned sources; these are specifications,
not P7-02 fixtures or executed results.

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

## Verification and reproduction

```sh
python3 scripts/review-city.py --check
python3 scripts/review-city.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests -p test_city_review.py -v
```

Offline verification checks thirteen prerequisite hashes, every contribution and
control against P0, exact XML metadata/flags, nine factory registrations, resource
closure, reciprocal operation/decision/MAN/DEV ownership, ten inherited scenario
expectations, all text/donor records and unshipped scope. Archive mode additionally
verifies original source/resource bytes, exact line excerpts, literals and category
metadata, and searches every pinned C# file for traffic-monitor subscribers.
Original-runtime/native/human acceptance is not established by either check.
Fresh execution results are recorded in the checklist; historical P6 counts are
not P7 evidence. EXAV-SCN-017–024/030/031 and SCN-D01 need P7-02's expanded
independent traces, including raw pixel/alpha/geometry and random draw order.

Fresh verification on 11 November: original archive and offline checks pass for
193 reviewed files/194 resources; all 11 new evidence-integrity tests pass. The
full Python suite records 238 passes and one existing owner-supplied historical
sample skip (239 tests total). P5/P6 prerequisite source checks, project boundaries
and diff whitespace pass. Commands/results/hashes are retained under
`tmp/p7-01-*`, including `p7-01-validation.json`. These runs establish review
integrity only; no browser, .NET city runtime or P7-02 fixture execution is claimed.

## P7-04 production handoff

[Generic construction lifecycle and evidence](web-parity-city-construction.md)
binds the construction row above (MAN-T10, MAN-E12, DEV-A06, EXAV-13) to
`ConstructionLifecycle`, `CityConstruction`, durable caller/handle state and
`CityConstructionTests`. It executes CITY-D008/012/020 at detached domain scope.
The exact constructor/gate/branch/FIFO fixtures remain P7-02 source evidence.
Progress inspection and cancel/demolish browser controls remain P7-07/09/12/27;
there is no force-completion action. Actual listener attachment, family
reclamation and integrated city save/authority retain their numbered owners.
No catalog translation classification or source decision is changed.
