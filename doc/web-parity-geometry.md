# P0-04 geometry, clock, train state and drawing expectations

**ExAV reassessment pending — 20 October 2026:** this P0-04 geometry/clock/train review records the earlier original-FreeTrain source/documentation and its stated revision. It is historical input, not current ExAV completion or authority. [The current source policy](web-feature-parity.md) selects stable ExAV archive `trunk/` (2.1.1.12), including bundled plugins; P0 must validate retained findings and replace changed ones against that source. Existing original-source JSON, counts and fixtures retain their old provenance until regenerated. Carry English translations onto ExAV semantics as features are ported.

P0-01 now supplies [stable ExAV source/dependency hashes](web-parity-exav-inventory.md)
and project membership, including the generic loader's `SOURTH` token difference.
These are inputs to P0-04 source/dependency reconciliation; the historical numeric
tables and galleries below have not been rederived or accepted against ExAV.

**EXAV-DIR-001 — owner-approved correction, 20 October 2026:** `SOURTH` in
ExAV's `GenericStructureContribution.parseDirection` is a typo. Use **`SOUTH`**
for the south direction when porting the parser and content. Preserve `SOURTH`
in pinned source evidence and source-derived inventory; the port intentionally
corrects this behavior. The decision is recorded in
[the ExAV baseline](parity/exav-baseline.json).

The stable ExAV checkout's `trunk/plugins/jp.co.tripod.chiname.lib.generic/GenericStructureContribution.cs`
now uses `SOUTH` (working-tree correction, 20 October 2026). The archive commit
pin remains unchanged, so its generated source evidence still records `SOURTH`.

Historical status: original-FreeTrain source extraction complete against `1f755ca6f291253b8cd5024ef53402b85d467170`.
This is a specification for P1–P5, not delivered gameplay or an original-runtime
comparison. [Geometry metadata](parity/historical/geometry.json) pins all eight directions,
60 serialized RailPattern IDs and 16 support slices.
[Behavior tables](parity/historical/geometry-behavior.json) pin the reviewed dependency files,
48 level poses, 64 slope poses, 192 attachment decisions and `GEO-*` differences.
The source review descriptions name the symbols owning each expectation below.

Generate or verify with:

```sh
python3 scripts/parity-rail-gallery.py --check
python3 scripts/parity-rail-gallery.py --gallery tmp/parity-p0/rail-gallery
```

The generated [raw gallery](../tmp/parity-p0/rail-gallery/index.html) includes all
patterns/supports and authored day/night sheets. The
[composition gallery](../tmp/parity-p0/rail-gallery/compositions.html) adds 152
isolated snapshots: level/slope train poses, each bridge/tunnel draw stage and
surface/underground ramp consists at two cuts. These use pinned artwork and exact
source frame/anchor formulas. They do not execute the legacy game, exercise the
browser renderer, simulate a running train, or reproduce full terrain/water scenes.

## Coordinates and direction contract

Direction indices are N, NE, E, SE, S, SW, W, NW = 0…7, with vectors
`(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)`.
`left/right` subtract/add one modulo eight; `left90/right90` subtract/add two;
opposite adds four. `angle(a,b)=min(abs(a-b),8-abs(a-b))`. Cardinal (`isSharp`)
directions are even; X-parallel indices are 2/6 and Y-parallel 0/4. Never derive
world directions from screen labels or four-direction prototype indices.

Let `c=(world.size.y-1)/2`, using source integer division. `World.toHV` computes
`xx=x-c; h=(xx+y)>>1; v=y-xx`. For nonnegative view rows the inverse is
`x=h-v/2+c; y=h+(v+1)/2; z=d`. World storage is rectangular in H/V, not X/Y.
Negative signed right-shifts must retain floor behavior; C# `/` truncates toward
zero. The source's deprecated removal path uses `/2` rather than `>>1`; preserve
the boundary distinction during implementation review, rather than conflating it
with the storage getter/setter.

Projection returns the voxel's top-left at
`A=16*(x+y-c); B=8*(-x+y+c)-16*z`. Its bounding box starts 16 pixels above B and
is 32×32. Thus a unit in X projects `(16,-8)`, Y `(16,8)`, Z `(0,-16)`.
Client coordinates subtract the camera's top-left A/B. H/V projection is
`v'=v-2*z; A=16*(2*h+(v'&1)); B=8*v'`.

Picking first computes `t=2*B-16; x=(A-t)>>5+c; y=(A+t)>>5`. With a controller,
scan z from height cut down to zero and test `(x-z,y+z,z)` with its disambiguator.
Without a selectable candidate return `(x,y,0)`. This is controller-dependent
voxel selection, not an alpha-mask/global-depth-sort rule. Half-voxel content
selection remains subject to its own disambiguator/content review in P2/P7.

`Location.toward` takes one sign-vector step at the same z; ordinary route length
is `max(abs(dx),abs(dy))+1` inclusive. Distance truncates the 3D Euclidean square
root to int, with source int products before the square root. Four-way snapping
chooses X on `abs(dx)>=abs(dy)`. Eight-way snapping tests integer `Y/2>X`, then
`X/2>Y`, otherwise preserves X and sets `Y=anchorY+abs(dx)*sign(dy)`. Normal rail
uses its own same-level selection, while special-rail controllers apply snapping.

`isInsideWorld` tests H/V only. The voxel indexer also enforces z via storage and
returns an occupied OutOfWorldVoxel on index failure. Train exit uses the distinct
visible-border predicate: `h<0 || h>=size.x || v<2*z ||
v>=size.y-2*(size.z-z)`. A rectangular X/Y boundary is not equivalent.

## Track topology, construction and supports

The 12 normal patterns are exactly all unordered pairs whose angle is at least
three eighth-turns: eight 45-degree curves and four straight axes. The 16 junction
patterns each add one adjacent branch to a straight pair. IDs 0–11 are normal,
12–27 junction, 28–43 surface slopes and 44–59 underground slopes. These are
serialized identities; support sprites have no RailPattern ID. The table includes
all connections, rectangles, anchors, visibility, level, climb, zdiff and zangle.

`SingleRailRoad.guide` tries current heading, left, then right. `dir1` scans edges
from index zero; `dir2` scans from seven. `isWellConnected` requires two adjacent
same-z TrafficVoxels with reciprocal edges. Reattaching an existing edge succeeds.
For a well-connected normal pair, a new edge must form angle 1,3 or 4 with **both**
old edges, then replace the rail by the matching junction. For an open pair,
retain dir1 when its angle to the new edge is at least three, otherwise dir2 when
its angle qualifies; replace the other edge. The generated table covers every
normal pattern × new direction × connectivity state. Example: pattern 10 (N/S)
accepts NE as junction 19 when well connected, and becomes pattern 0 (NE/S) when
open. A well-connected N/S pair rejects E.

A junction head with a straight/turn choice asks its controller; the default
selects straight. Following cars select straight when their previous car is at
`currentLocation+heading`, otherwise turn, preferring left over right. Incoming
branch travel merges by the same heading/left/right rules. Junctions, slopes and
special-purpose rails accept only edges already present. A normal detach removes
the whole rail if either requested edge exists. Junction detach removes the whole
rail if both requested edges exist; one match converts the remaining pair to a
normal rail; no match does nothing. Slope and special-purpose detach do nothing.

`comupteRoute` walks the sign-vector route, quoting every endpoint and intermediate
cell, returning null on the first zero quote. At start/end only one attachment is
allowed; a middle cell containing a rail cannot accept both route edges at once.
Normal construction rejects cars, nonreclaimable obstacles, invalid attachment,
and `ground<=z<=water && ground<water`. Other empty/Traffic cells cost
`12000*(abs(z-ground)+1)`; reclaimable obstacles add entity value. It does not
implement a general road-conflict check (the TODO remains). A same-point route
preview is empty at cost zero; the UI suppresses same-point build/remove. Direct
`build(from==to)` is not safe (`GEO-003`).

Construction rechecks the quote, reclaims eligible entities, attaches endpoints or
creates normal rails, elects bridge supports and charges the rail account. Example:
three empty, flat, dry ground-level cells cost 36,000; three cells at z=3 over
ground 1 cost 108,000. Deletion quotes all unoccupied rail cells along the route,
even when actual detach will do nothing, at `4000*count*max(1,endZ-water)`; removal
calls subtype detach and support teardown. These are source behaviors, not a
promise of atomic multiplayer validation. P4 must supply authoritative preview,
commit revalidation and explicit correction/rejection with the existing UX.

A ramp is four consecutive cardinal steps, rising one z in total. For level
`i=0…3`, track is at `base+i*climb+(0,0,i/2)`. Levels 0/1 have filler voxels above
them; levels 2/3 have visible support/wall voxels below. All eight cells participate
in occupancy even when the rail is an invisible 1×1 crop. At each horizontal
position, **baseZ<localGround** chooses underground versus surface art, so mixed
ramps are possible. Ascending zdiff is +1 at level 1; descending zdiff is -1 at
level 2. `RailRoad.zdiff` checks the car's incoming heading against zangle; do not
apply a generic z increment on every slope voxel.

Creation requires an even direction, room below the ceiling, and all eight cells
empty, including absence of TrafficVoxels. Out-of-bounds sentinels block it. Cost
is `400000*max(1,baseZ-water)`. Supports are selected by `climb.index+i-2` into
the eight-entry support arrays for levels 2/3. Fillers and supports expose
HoleVoxel.drawGround flags determined at creation. Slopes invalidate their own
voxel and the upper/lower paired voxel. Teardown checks cars at the four rail
positions, recognizes the second rail via zdiff=1/zangle, charges
`80000*max(1,baseZ+1-water)`, clears each of the four rail components, removes
its paired filler/support cell and tears down the bridge piers below baseZ.
The separate SlopeEntity.remove path scans its enclosing cube instead. Generic
track deletion cannot tear down the slope. The removal UI resolves a clicked
rail to its base.

Bridge support election first refuses any occupied cell from z=0 up to the rail,
then builds piers from local ground to railZ-1. Ordinary rail uses the default
style; slopes use default for N/E and the slope style for S/W. Support teardown
currently ignores owner, while slope entity removal excludes BridgePierVoxel
(`GEO-004`). Bridge special rail uses six begin/middle/end images, alternates piers
on newly created spans, and requires compatible straight track or empty cells;
its predicate never checks water despite its comment. Tunnel construction requires
at least one MountainVoxel, is not below water, saves mountain corner heights,
and fills other empty cells with normal rail. Removal restores mountain voxels
but excludes the endpoint (`GEO-006`). The eight-way girder contribution requires
every point above local ground and maps straight indices 0/1/2/3 to sprite
3/1/0/2 respectively; it has a before-car pass only.

## Clock and callback expectations

Time is signed Int64 integer minutes: minute=1, hour=60, day=1440, year=525600,
365 days, no leap years. Start is internal minute **130080**, year 1, April 1,
08:00, totalMinutes=0. Day is `[06:00,18:00)`; seasons are March–May spring,
June–August summer, September–November autumn, December–February winter.
Weekday is `(internalMinutes/1440)%7` (not a civil-calendar mapping); the start
has weekday 6. Display floors minutes to tens without changing simulation time.
`untilTomorrow` at midnight returns 1440, never zero. Random durations use a
static System.Random and truncate the sampled duration; no seed/state contract
has yet been ported.

An accepted Clock.tick increments one minute, resets pictures/invalidates the
world exactly at 06:00 and 18:00, invokes due callbacks, then captures and clears
endOfTurnHandlers before invoking them. Re-registration from an end handler is
for the next tick. One-shots add duration to current time; repeated callbacks
reschedule **after** their handler returns. Equal-time queue entries sort by
insertion ID, so ordinary callbacks A then B due at the same minute run A,B before
end-of-turn. `unregister` removes all equal delegate values. Cancellation during a
callback can invalidate the current queue entry before the unconditional
removeMin (`GEO-002`). Scheduling in the past, zero intervals, ID overflow,
exceptions and debug-only assertions need explicit P1 decisions; they are not
normal positive-duration scheduling semantics. In particular, the debug assertion
reads minPriority before checking whether the queue is empty.

MainWindow's timed speeds use 200/100/50 ms and call tick **twice** per timer event;
the 1 ms setting runs a fast loop with DoEvents and two ticks per iteration.
An active local controller makes tick return; native modal/focus state also gates
timer progression. Preserve the simulation minute and ordering contract; replacing
local tool/modal pauses with uninterrupted shared-world time is an existing,
explicit multiplayer UX adaptation. Tick speed, network publication, frame rate
and lease wall-time are separate quantities.

## Train state and transition expectations

CarState is Unplaced (singleton), Inside(location,direction), or
Outside(last outside location,direction,timeLeft). These states are immutable;
Car.setState clears old Inside occupancy, updates its voxel, installs new Inside
occupancy and updates that voxel. Outside/Unplaced occupy no world voxel. Train
state independently has Unplaced, Moving, StoppingAtStation, StoppingAtSignal and
EmergencyStopping. The signal label is not assigned by the central clock handler;
all positive stops set StoppingAtStation (`GEO-005`).

Placement walks rails using temporary head placement and guide calls, records
directions/locations from tail toward head, rejects missing/occupied rail and
duplicate locations, then installs all cars, resets stopCallCount, schedules the
first movement after `type.minutesPerVoxel` and sets Moving. Placement does not
apply slope z transitions (`GEO-008`). Removal clears cars, cancels the clock
delegate, and sets Unplaced. Do not replace car identity with array position:
reversal changes array order while original car indices/types persist.

At each movement callback an Inside head asks its current rail for stop duration:
positive increments stopCallCount, schedules that duration and returns; negative
reverses; zero proceeds. Then reset the stop counter. Compute new location from
guide plus heading-dependent zdiff. A border exit produces Outside counter 100
before testing reciprocal track; otherwise the destination must have the opposite
edge. An occupied destination yields EmergencyStopping with no movement. Missing
track reverses and tries once from the new head; failure/blocking yields an
emergency stop. Retry is after minutesPerVoxel. A blocked car does not trigger the
missing-track reversal, and occupancy includes cars of the same train.

Moving passes each car's old state to the next car from head to tail. The first
successful movement and every sixteenth thereafter charge
`length*20+passenger/20`, with integer division. Countdown movements count too.
For a three-car train with 39 passengers, successful movements 1 and 17 each
charge 61, with no charge on 2–16. Reversal derives each Inside car's new heading
from **guide().opposite**, then reverses the car array and toggles isReversed.
It is not simply opposite of the stored heading on a curve. Previous-car lookup
uses the original index plus the reversal flag.

Outside heads decrement nonzero counters on each scheduled movement, retaining
outside location and direction. After 100 decrements, counter zero attempts return
on the following callback. Failed track lookup resets to ten; occupancy is checked
after finding the first connected candidate, so an occupied first candidate does
not cause a search of later candidates. At resulting counter 50, unload passengers
and load `min(100,capacity)`; fare uses the stored boarding location and truncated
3D distance. These events occur before car-chain shifting.

Return candidates use Matrix integer/half-integer transforms, in this order:

| Exit direction | Offsets from last outside location |
| --- | --- |
| Cardinal vector d | L90*d, R90*d, (L90-2I)*d, (R90-2I)*d, -d |
| Diagonal vector d | (L45-I)*d, (R45-I)*d, (L45-2I)*d, (R45-2I)*d, -d |

L45 is `[[.5,.5],[-.5,.5]]`, R45 is `[[.5,-.5],[.5,.5]]`;
matrix components are doubled internally and division truncates toward zero.
Candidates require rail toward the original exit direction and return heading is
its opposite. Reversing an Outside car uses a resolved return location to update
its external reference and complements the counter to `100-timeLeft`; if no
return track exists its state is left unchanged.

YardRailRoad's stop index is `(platformLength+trainLength)/2-1` in the platform
direction, `(platformLength-trainLength)/2` in the opposite direction. Out-of-range
index or no station means no stop. A positive first stop unloads passengers;
departure after at least one wait loads passengers and rings the bell. Default
controller: junction straight, 30-minute first station wait, then zero; nonstation
harbors ignored. Delegating controllers search ancestors and fall back to default.
Detailed manual/TAT timetable, station population and signal/garage semantics
remain P5/P6 source-derived acceptance work; these are the call boundaries used
by the central state machine, not substitutes for those subsystem specifications.

## Frame selection, passes, cuts and acceptance

TrainCar.draw starts at `(0,-9)` from the voxel point. Straight level angle is
`2*heading`. For a turn, normalize `diff=(outgoing-heading)&7` to ±1, compute
`dd=(2*outgoing+3*diff)&15`, add X=+3 when `2<dd<10` else -3 and Y=+2 when
`6<dd<=14` else -2; angle=`(2*heading+diff)&15`. Reverse angle XORs eight.
For N→NE the result is frame 1 at `(3,-11)`; reversed is frame 9 at that offset.
These intermediate frames are required even though movement occupies whole voxels.
The train catalog preview has separately authored offsets, not a movement oracle.

Slopes add Y offsets `[0,-4,+8,+4]` to the -9 baseline, giving
`[-9,-13,-1,-5]`. A normal consist passes stored heading and `heading==climb`;
a reversed consist passes opposite heading and `heading!=climb`. Asymmetric
level frames are a 2×8 grid; slope frame is `direction.index+(isClimbing?0:1)`
on row 64. Symmetric level frames mask angle by seven; slope frame chooses the
climbing direction divided by two. Reverse-tail wrappers XOR eight on level
frames or flip direction **and** isClimbing on slopes, independently of train
reversal. These are frame selection rules, not renamed direction enums.

QuarterViewDrawer traverses V ascending, H ascending, then Z ascending. Full
height starts at water level; cut mode starts at zero. If HoleVoxel suppresses
the initial surface, start one z lower, clamped at zero. Transparent/empty voxels
draw local ground or underwater chip at ground level, water shape only at water
level in full-height mode, or underground chip at a cut below ground, respecting
HoleVoxel flags above/below. An opaque voxel suppresses that surface branch.
Then draw voxel and per-voxel overlay. Do not replace this with global z sorting.

Voxel.drawVoxel wraps behind N/E fences around voxel drawing and front S/W fences
after it. TrafficVoxel order is **road.before → accessory.before → rail.before →
car → rail.after → accessory.after**; no road.after call exists. Normal rail has
no foreground. Bridge and tunnel foregrounds cover the car after their background
passes. PassagewayRail draws its floating walkway after the car; fat-platform
rail draws YardChip before inherited rail. Signal predicate naming is inverted
relative to actual pass selection (`GEO-007`). These must remain separate passes
in a portable renderer.

The drawer passes `heightCutDiff=-1` at full height, otherwise `cutZ-z+1`; the top
included voxel gets **one**, not the zero claimed by Voxel's comment. This leaves
BridgePierVoxel's alpha-at-zero case unreachable here (`GEO-001`). Station names
are drawn after overlay wrap-up, above their base location. Picture resets occur
at day/night transitions, forced view changes, surface loss and explicit clock
changes. Raw gallery night sheets are authored art; the train composition gallery
is a day-art study and does not validate night color transformations.

`GEO-001`–`GEO-008` retain observed source defects/quirks as explicit implementation
decisions, not silent fixes. P1–P5 acceptance must cover all source tables, both
turn directions, reversed head/body/tail identities, junction followers, each
cardinal slope level/direction, mixed surface/underground ramps, paired support
occupancy, boundaries/returns, simultaneous timers and cancellation, blocked trains,
all six bridge/two tunnel sprite indices, and water/terrain/cut/picking scenes.
Use the existing roles/leases, preview/revalidate, epoch/revision identity and
demo/live readiness contracts. Native/platform acceptance, full-world runtime
captures and historical-save continuation remain open at their named gates.
