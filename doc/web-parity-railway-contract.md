# P4-01 railway source contract — 30 October 2026

**P4-01 is complete at source-contract scope. [P4-02 independent fixtures](web-parity-railway-fixtures.md)
are complete; [P4-03 detached mixed state](web-parity-railway-state.md) is now delivered.
[P4-04 detached atomic booking](web-parity-railway-booking.md) is delivered.
Next: P4-05 mixed-world saves, wire contracts and dispatch.**
This specifies railway behavior; it adds no live handler, command, UI, save or
protocol version. P4 remains open. The separate synthetic reference loop and the
accepted zero-rail terrain profile retain their invariants.

Authority is stable ExAV **2.1.1.12/V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The
[hashed review](parity/railway-review.json) binds raw source blobs, symbols and
line excerpts to the existing discovery ledger. Its operation records specify
legality, mutation, booking and removal separately. The
[text review](parity/railway-localization-review.json) preserves Japanese source
and historical English donors. Desktop code was read, not executed.

The review covers **66 pinned files, 14 operation families and 14 decisions**.
Its **64 bundled definitions** comprise four special rails, two signals, four
poles and 54 stationary accessories. **78 source-text records** include 39
historical English donor candidates and 39 requiring new translations; donor
presence is not wording approval.

Reuse the [P0 geometry tables](web-parity-geometry.md),
[dependency review](web-parity-geometry-dependencies.md),
[action routes](web-parity-actions.md#p4-01-railway-workflow-bindings),
[manual reconciliation](web-parity-manual.md#p4-01-railway-source-reconciliation)
and [P3 support handoff](web-parity-p3-acceptance.md#p4-terrain-and-support-handoff).
The new review supplements these records without changing their source pins or
granting runtime parity credit. P4-02 now makes the independent expectations
executable without manufacturing fixtures from production output.

## Shared state, geometry and transactions

Use canonical eight-direction XYZ/HV and actual stored terrain/occupancy, with
the distinct storage and train-visible border predicates. Validate every selected
and paired cell before reads or allocations; no rectangular XY approximation,
provenance-based ground inference, global corner flattening or water flood model.
The normal route walks one sign-vector step toward its destination at fixed Z;
it can change heading once, and is not a shortest-path solver. Special rails
snap to a straight four-way axis; girder track alone offers eight-way snapping.
Normal rail has 12 patterns, junctions 16; slopes retain patterns 28–59 and their
eight-cell ownership. Existing serialized P0 tables remain normative.

Source `TrafficVoxel` composes rail, road, one accessory and one car. Transparency
does not mean reclaimability. Its rail/road setters only assert no car; its generic
entity removal can withdraw an entire train. Those are **not** authorization to
evict cars during P4 construction. The planned service must enforce protected
occupancy before mutation, including paired cells and restoration targets.

The draw order is road-before, accessory-before, rail-before, car, rail-after,
accessory-after. Roads, crossing state and accessories are components, not
independent whole-voxel owners that can overwrite one another. Clearing a rail
does not inherently clear a road or pole: source removes the TrafficVoxel only
when rail, road and accessory are all absent. Car alone is not part of that empty
test, another reason to enforce car protection at the command boundary.

Every portable preview is read-only: no cash, allocator, terrain, clock, RNG,
controller or listener mutation. It includes exact changed/unchanged/skipped cells,
losses and operation-specific booking, including charges for source no-ops. One
Confirm recomputes the plan under current role, epoch, revision, content and world
state; publish geometry, ownership, allocation and booking together or publish
nothing. Preserve source local skips where specified. A rejected command charges
nothing; a legal source no-op may still cost money. These are different outcomes.
No generic “number of changed cells × rail price” helper supplies this contract.
Shared time continues through tools/panels; session FIFO, participant receipts,
late-response fences and uncertain-outcome no replay remain required.

## Normal rail, attachment and removal

First-point picking accepts an existing rail or `z <= ground`; the second point
uses the first Z. An explicit construction level must obey this usable-start
contract. A same-point preview is empty/zero; same-point commits are rejected
before direction lookup (GEO-003, P4-06). Reject noncoplanar input and invalid
patterns instead of relying on desktop assertions or an unbounded route walk.

For each inclusive route cell, reject a car, a nonreclaimable non-Traffic obstacle,
or `ground <= z <= water && ground < water`. A reclaimable entity adds its value
to the quote and is removed as an entity, not just erased at the selected voxel.
Source sums that value per quoted cell; shared-entity deduplication is not an
implicit economic correction. Mountains/forests/terraces must use their actual
P3 reclaimability and footprint; an empty buried voxel and a Mountain occupant
are different cases. Existing rail in a middle cell cannot receive both edges.
Endpoints can attach one edge; an already present edge succeeds.

For a well-connected normal pair (two same-Z reciprocal neighbors), a new edge
must form angles 1, 3 or 4 with **both** old edges, then becomes a junction. For
an open pair retain the lowest-index edge if its angle to the new edge is at least
3, otherwise the highest-index qualifying edge. Junction, slope and special rail
accept only existing edges. Preserve source direction scan and replacement order.

Let `M(h) = -2*h` for `h<0`, otherwise `h+1`. Construction books
`sum(6,000,000 * M(z-ground) + reclaimedValue)` to RAIL_SERVICE. Existing endpoint
attachment is charged too. Three dry ground cells cost **18,000,000**; at Z3 over
ground1 they cost **54,000,000**; two levels buried they cost **72,000,000**.
Build reclaims, creates/attaches and elects supports only for newly created rail.

Normal removal walks inclusively. Count every TrafficVoxel with any rail and no
car, even if its subtype/edge detach will do nothing; book
`count * 2,000,000 * (abs(endZ-water)+1)` to RAIL_SERVICE. SingleRailRoad clears
the rail if either requested edge matches. Junction: two matches clear it, one
match leaves the remaining normal pair, zero matches do nothing. Slope and special
detach do nothing. Source support teardown is called for **every visited cell**,
including skipped/no-op cells; GEO-004 explicitly retains this behavior below. Generic normal
deletion is not a slope, signal or special-rail removal tool.

## Slopes, piers and stored ground flags

For cardinal climb `d`, rail level `i=0..3` is at
`base + i*d + (0,0,floor(i/2))`. Fillers occupy the upper paired cells at i0/1;
support/wall voxels occupy the lower paired cells at i2/3. All eight cells must
be empty, even of TrafficVoxels. Require baseZ+1 below the stored ceiling.
For each horizontal position, **baseZ < localGround** chooses underground art;
mixed ramps are legal. Support/wall sprite index is `d.index+i-2`. Ascending
zdiff+1 is level1; descending zdiff−1 is level2, only for the matching zangle.
This is a P5 movement interface, not proof of train movement on ramps.

Build books `40,000,000 * M(baseZ-water)` once. Removal resolves the owning base,
recognizes the second rail through zdiff+1/zangle and checks all four rail car
positions. Book `8,000,000 * (abs(baseZ+1-water)+1)` once; clear four rail
components and their paired cells. Source `SlopeEntity.remove` instead scans its
cube and excludes piers directly, but calls `TrafficVoxel.remove` for its rails,
which tears down piers below each rail height. It has no slope-tool booking and
removes whole Traffic voxels rather than preserving road/pole components. Canonical
ownership validation must reject malformed partial ramps before either operation.

Pier election checks **every** voxel from Z0 to railZ−1. Any occupant prevents
election but **does not reject the rail build**. Otherwise create piers from
local ground through railZ−1; top/body styles can differ. Ordinary/girder use
default style; slopes N/E default, S/W slope style. No separate pier booking is
called by these methods. Do not “fix” the below-ground obstruction scan to begin
at the surface without a decision.

EXAV-DEP-005/007: paired voxel flags are stored at creation. For a paired voxel
at Zz and index i, `below = !(i<2 && ground>=z)` and
`above = !(i>=2 && ground>=z+1)`; `drawGround(above)` selects that stored flag.
Later terrain edits do not recalculate them or switch the stored rail art.
Keep these flags and sprite selections in saves and deltas. The drawer passes
cutZ−z+1; Mountain subtracts one while piers do not. P4 must not globally shift
the cut parameter to make the pier alpha-at-zero branch reachable.

**GEO-004 / RAIL-D006 — owner decision, 30 October 2026: retain source teardown
regardless of owner.** At each source teardown call, delete every BridgePierVoxel
in the same XY column from Z0 through selectedZ−1, including piers owned by
another entity. Ownership must not block this operation or leave an operator in
a hard-to-understand stuck state. The proposed owner-only restriction is rejected;
there is no remaining approval gate for P4-07/10/20.

Preview must explicitly include every affected pier and its owning identity,
then revalidate and remove the complete selected set atomically. Preserve other
voxel types and terrain; this decision authorizes pier teardown, not deletion of
their owning rails or other entities. Keep the source call sites and local skips,
including normal removal's teardown on cells whose rail detach is a no-op. The
separate SlopeEntity.remove path excludes piers from its direct scan, but its
TrafficVoxel.remove callbacks also invoke owner-agnostic teardown below each rail.
This transitive call was reconciled in P4-10; the owner's decision does not
introduce an additional ownership cleanup algorithm. P4-02 fixtures must
include foreign-owner piers and no-op normal detach with actual pier removal.

## Special rails and their distinct booking

All special builds reject coincident endpoints, require fixed Z and their selected
straight axis, and accept only empty cells or compatible TrafficVoxels with both
axis edges. Extra source edges are not explicitly rejected by those predicates;
do not silently tighten them to “exactly two” except where the selected source
handler does. Source build predicates often omit cars, while setters assert their
absence; the planned occupied-cell fence rejects replacement under a car. Removal
retains per-cell train skips and reports them before one atomic confirmation.

| Operation | Legality and mutation | Actual RAIL_SERVICE booking and removal |
| --- | --- | --- |
| Bridge / P4-12 | Four cardinal directions. No water requirement despite comment. Leave existing compatible rails; create begin/middle/end art across new runs. Single new cell gets Begin. Alternate pier election only on newly created cells, starting true; an existing rail resets the run flag but not pier alternation. | Build: `sum(6m*M(z-ground))` over **all inclusive cells**, including unchanged rails. Remove: `sum(2m*M(z-ground))` over **all inclusive cells**, including wrong subtype/occupied skips; clear only matching BridgeRail without a TrainCar and tear down supports. Both cost functions return zero for the starting-cell underwater predicate, independently of canBeBuilt. A zero cost is not automatically illegal here. |
| Girder / P4-13 | Eight-way straight; every cell strictly above local ground. Existing compatible rails stay unchanged. New cells get RailImpl and default piers. Axis sprite indices 0/1/2/3 → 3/1/0/2. Before-car pass only. | Same inclusive per-ground build/removal cost functions and first-cell water early-zero quirk as bridge. Clear only matching RailImpl; TrainCar cells skip but remain included in cost. |
| Tunnel / P4-14 | Cardinal; Z>=water; at least one actual MountainVoxel. Save NE/SE/SW/NW offsets, remove Mountain and create TunnelRail. Other empty cells become normal rail; compatible existing rails remain. Ground levels stay unchanged; no piers are elected. | Build: **12m per newly created cell**, either TunnelRail or normal rail, inclusive. Remove: **4m per actually restored matching tunnel cell**, **excluding the destination**, with TrainCar skips. Restore saved corners as unowned, undecorated Mountain; ordinary gap rails stay. GEO-006 retains this half-open endpoint quirk. Trees, overlays and ownership are deliberately not backed up; preview names their loss. |
| Garage / P4-15 | Cardinal; every cell at its actual ground level. Replace every selected empty/compatible rail with GarageRail, inclusive; foreground/background art by axis. No maintenance/fueling/repair mechanics are called. | Build: **6.5m per inclusive cell**, even replacement. Remove visits **[from,to)**. Matching axis GarageRail calls remove (car prevents conversion), then adds **200,000** to a running subtotal regardless of that conversion. **Spend the running subtotal on every loop iteration**, including nonmatching cells. Thus two matching cells in a three-cell selection spend 200,000 then 400,000 = **600,000**, leaving the endpoint. Retained source billing quirk, not generic demolition or an approved once-per-cell correction. |

The native AbstractLineController's displayed costs call **road** formulas and
can use an unsnapped hover endpoint. Its special-rail commented-out quote methods
are not active overrides. RAIL-D003 uses the actual selected handler's mutation
and booking for the authoritative preview. That UI correction is already required
by P4-12/14/15; it does not change the source booking formulas above.

Bridge has six back/front images; tunnel and garage have two back/front axis
images. Foreground coverage and tunnel S/W concealment invalidation are source
rules. Bind prepared recipes with exact definition identity; converted art alone
does not register a handler. Unknown definitions block readiness.

## Signals, poles and other accessories

**Signals / P4-16:** four cardinal orientations, two bundled railSignal types.
Point placement obtains an empty/existing TrafficVoxel and accepts no rail or a
SingleRailRoad; it does not require an already straight compatible track. It
replaces the pattern with the selected cardinal straight signal. Reject other
subtypes, non-Traffic obstacles and protected cars. Removal rejects a car and
restores SingleRailRoad with the signal pattern. Neither path books money.
Persist type, direction and future shared controller identity; heading within one
eighth-turn of the signal direction delegates stop time, other headings return
zero. It is a TrainHarbor, not a passenger platform.

GEO-007 is retained: `drawBefore` draws the signal when
`needsToDrawAfter(direction)` is **true**, then draws track; `drawAfter` draws it
when **false**. The method name does not override actual dispatch. P5 owns waits,
controller editing and moving-train evidence; optional automatic block/monitor
plugins have no bundled behavior credit.

**Poles / P4-17:** all four bundled electricPole contributions use a rail with
exactly two opposite cardinal edges, regardless of subtype name. Orientation is
inferred from the actual pattern (N present → N/S image, else E/W). Replace the
single accessory slot; existing accessory presence and car presence are not
source placement exclusions. Non-null replacement does not call the old
accessory's onRemoved; null removal does. Remove any ElectricPole irrespective
of selected contribution, leaving rail/road/car intact. No build/remove spend
caller exists, despite a price-capable base and a native cost control. These are
visual accessories, not simulated electrification. Before/after sprites and the
visibility option do not change world legality.

**Stationary railway accessories / P4-18:** the review enumerates every bundled
railStationary declaration, separately from rail-attached poles, P6 passage/stair
parts, P7 generic structures and empty road-accessory extensions. Preserve exact
IDs, including malformed-looking brace strings; do not normalize them as GUIDs.
Ground selection and the entire exclusive size footprint must pass actual
equal-ground/reclaimable-occupancy checks. Invisible upper voxels still occupy
the full height. The active StationaryStructPlacementController inherits the
FixedSizeStructController placement path (not the inverted predicate in the
separate FixedSizeStructurePlacementController).

The active path creates a ConstructionSite; completion clears the site and calls
the contribution constructor, which spends `type.price` to RAIL_SERVICE.
RailStationaryStructure is owned, nonreclaimable and has value zero. Removal
selects the actual structure without requiring the current catalog type, clears
the full footprint and inherits **500,000 × sizeX × sizeY to SUBSIDIARIES**,
independent of height. Do not relabel its demolition as rail spending. The site
is not a completed accessory: its state, cancellation (no completion charge),
completion callback and shared P1 scheduler/random consumer require a durable
P4-18/P7 integration before this workflow can be called delivered. Rendering a
completed recipe immediately would be a separate unapproved timing adaptation.

## Speed-limit authoring and crossings

**P4-19:** level selection follows the inclusive normal sign-vector route at
fixed Z, including RailPattern validation, but tests **TrafficVoxel existence**,
not necessarily a rail or a straight rail. Slope selection is four locations
`base+i*d+(0,0,i/2)` for cardinal d; its placement guard also tests only those
TrafficVoxels, not a complete slope entity. Reject any overlap with Low, Medium
or Fast lists. Hashtable additions deduplicate within the chosen list; ArrayList
additions append (including duplicates when called directly). Removals remove
one occurrence per selected location in each list. No money is booked.

Cleanup removes entries only where the TrafficVoxel is absent; a road-only or
accessory-only TrafficVoxel is still eligible. Preserve this distinction when
running cleanup after infrastructure changes; pruning every rail-less cell is
not the source rule. Initialize world-local saved lists independently of native
menu construction, as P4-19 already requires (EXAV-DEP-011). Flat coincident
selection is empty; slope removal resolves a member to its base, and its native
selected-direction assumption must be checked against the owning slope.

Hand P5 the captured prior-head-location scheduling interface. Low schedules 4
minutes; Medium uses native speed for native4, otherwise3; Fast uses native speed
for native4/3, otherwise2; no entry uses native speed. These equality tests are
not a generic min/max formula for arbitrary numeric speeds. Positive station
waits return earlier, and Outside countdown uses native speed. P4 authoring tests
cannot close P5 timing or EXAV-DEP-013/014 train defects.

**Road TODO disposition / P4-20, P7:** normal construction has no general road
conflict check; TrafficVoxel permits component coexistence. Preserve that source
fact rather than inventing a broad road exclusion. Crossing accessory appears
only with a straight two-edge cardinal rail and perpendicular road edges **when
the accessory slot is empty**. An existing pole suppresses automatic crossing
creation; losing crossing conditions removes only an RRCrossing. P4 must use
trusted road/car/platform component fixtures for these interfaces. It does not
claim playable P7 roads, P6 platforms or P5 trains. Unknown/unrepresentable
component combinations fail canonical validation; that is not a new gameplay
road-conflict rule. Crossing behavior is pinned through Traffic and RRCrossing.

## Accounting and decision status

All amounts above are **raw signed Int64 source money units**. RAIL_SERVICE is
`{AC30BB0B-044C-4784-A83D-FCB1F60B3CF2}`; SUBSIDIARIES retains its source identity.
Source default cash is **15,000,000,000**. Spending does not check affordability:
it subtracts, then while cash<0 adds the configured bankruptcy increment (default
**10,000,000,000**) and optionally displays a message. Zero cash needs no top-up.
P4-04 must keep this behavior and record continuation/top-up state; a blanket
“insufficient funds” rejection would be a semantic change. Persist authoritative
economic settings, not process-local dialog preferences. A nonpositive increment
or arithmetic overflow must reject safely rather than loop/wrap. The authoring
flag grants no economic exemption. P8 owns reports, debt and full finance.

| Decision | Disposition and implementing gate |
| --- | --- |
| RAIL-D001 / GEO-003 | Existing planned adaptation: reject coincident commits, retain empty/zero normal preview. P4-06. |
| RAIL-D002 | Carry P1 checked coordinates forward. Widen before cost multiplication, checked Int64 totals/cash, reject out-of-range/work limits before mutation. Source Int32 wrap is a hazard, not a new price. P4-02/04. |
| RAIL-D003 | Existing plan: preview actual operation booking and snapped geometry, never inherited road quote. P4-09/12–19. |
| RAIL-D004 / GEO-006 | Retain operation-specific endpoints, including tunnel/garage half-open removal; no endpoint fix approved. P4-14/15. |
| RAIL-D005 | Retain operation-specific occupied-cell skips/no-op charges; protect cars before replacement and atomic publication as already required by the plan. P4-06–20. |
| RAIL-D006 / GEO-004 | Owner-confirmed retained source behavior, 30 October: delete all piers below at source teardown call sites regardless of owner; show all losses in preview. No owner-only restriction or pending approval. P4-07/10/20. |
| RAIL-D007 / EXAV-DEP-005/007 | Retain stored ground flags, per-position art and distinct cut behavior. P4-10/11. |
| RAIL-D008 | Retain road coexistence/TODO and conditional crossing; no implicit new road restrictions. P4-20/P7. |
| RAIL-D009 | Retain actual bridge/girder inclusive charging and first-cell early-zero; tunnel newly-created-cell charging. P4-12–14. |
| RAIL-D010 | Retain garage repeated subtotal billing, endpoint exclusion and charged occupied skips; correction requires separate approval. P4-15. |
| RAIL-D011 / GEO-007 | Retain actual signal draw dispatch, zero signal/pole booking and pole replacement semantics. P4-16/17. |
| RAIL-D012 / EXAV-DEP-011 | Planned lifecycle adaptation: world-owned lists, startup initialization; retain Traffic eligibility/list rules. P4-19. |
| RAIL-D013 | Retain source initial cash/top-up; checked finite processing, persisted settings and no authoring exemption. P4-04/P8. |
| RAIL-D014 | Retain stationary site/completion timing and inherited SUBSIDIARIES demolition; bind shared construction-site consumer before delivery. P4-18/P7. |

No P4-01 decision remains awaiting approval. D006 records the owner's explicit
choice to retain source behavior for understandable operator cleanup. Other
retained quirks can be proposed for correction separately, with source and
adaptation vectors kept distinct. Planning alone approves none of those corrections.

## Requirements and translation scope

The review maps CORE-03/06 and EXAV-09/11/13 to every operation family. Reciprocal
tables in the action/manual records assign MAN-T02/03/05, MAN-M03, MAN-W01/02,
MAN-A03/04/12/14/16 and MAN-E05/11/12. Historical developer identities are retained:
DEV-P06 pole, P08 menu, P10 picture, P11 signal, P12 stationary, P13 road boundary,
P14 special rail, P24 costs, P26/28 sprite/voxel boundaries. Their old articles
and illustrations remain donors/context; pinned ExAV callers govern behavior.

The localization ledger carries every Japanese C# text candidate in the reviewed
P4 controllers/handlers, exact line/expression/text hash and P0 English-donor
provenance where present. Literal speed labels used in switches are semantic
enum keys to replace with typed dispatch, not a reason for translated strings to
control algorithms. Catalog names/authors/descriptions remain identity-bound P2
metadata; every relevant declaration is enumerated in the review. Numeric values,
paths, GUID-like IDs and internal keys are data, not missing translations.

EN/JA confirm/cancel, exact cost and no-op-charge explanations, skipped/protected
cells, endpoint exclusions, restoration losses, held-input resets, stale quotes
and no-replay errors are new browser wording assigned to each playable slice.
Source strings/donor candidates are reviewed scope, **not shipped translations**;
P4-22 must reconcile each with actual locale resources. No native dialog layout or
local modal pause is required by the text evidence.

## Verification

```sh
python3 scripts/review-railway.py --check
python3 scripts/review-railway.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
git diff --check
```

The default checker uses checked-in ledgers only; an external checkout is never
discovered implicitly. The explicit-source mode verifies raw pinned Git hashes,
symbols, excerpts and localization line bindings, without executing desktop code.
Integrity checks establish source/review coverage, not runtime geometry, native
render fidelity, browser interaction, performance or save continuation. P4-02
delivers [independent literal vectors](parity/railway-fixtures.json), followed by
mixed state/booking/persistence in P4-03–05 and the first playable normal-rail slice
in P4-06–09.

Fresh verification on 30 October 2026: default and explicit pinned-Git railway
checks pass; **150 Python cases complete, with one existing skip** (including
eight new evidence-corruption checks). The existing terrain review, historical
rail/support table check and `git diff --check` pass. Production .NET/client,
browser and native suites were not rerun for this source/documentation-only step.
