# P5 railway interfaces — P4-24 handoff, 2 November 2026

P4 infrastructure is accepted at browser/portable scope and available for P5.
The owner deferred the two [capacity findings](web-parity-p4-acceptance.md#performance-disposition)
to P11 integrated performance work.
This handoff specifies source behavior and legal input witnesses; it does not
implement or accept moving trains. Save8/protocol6 remain current.

Authority is stable ExAV 2.1.1.12/V2, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
[Machine-readable witnesses](parity/railway-p5-handoff.json) bind twelve files,
raw hashes, symbols and exact line excerpts to the P0 inventory. Expectations
come from pinned source and independent P4 tables, never production movement
output. Desktop assemblies were not executed.

## Topology and guidance

Canonical headings are N/NE/E/SE/S/SW/W/NW = 0…7; left/right change the index by
−1/+1 modulo eight. `RailwayTopology.Ports` supplies all 60 serialized patterns:
12 normal, 16 junction, 32 surface/underground slope. Exact definition/handler
identity remains necessary alongside the pattern. Bridge, girder, tunnel,
garage and signal are distinct subtypes, not ordinary rail with a sprite flag.

Construction's `isWellConnected` checks two reciprocal **same-Z** neighbors.
Train traversal has a different contract: choose `go` from the current rail,
compute `newLoc = current + go`, add `currentRail.zdiff(incomingHeading)`, then
require a destination rail with port `go.opposite`. World-border handling happens
before that destination check. Do not reuse same-Z construction connectivity as
the train pathfinder, or derive movement from drawn pixels.

`SingleRailRoad.guide` tries the incoming heading, then left, then right. A legal
arrival has the reciprocal incoming port and a valid continuation. On a junction,
if straight exists with a left/right option, the head asks `onJunction`; followers
go straight only when the preceding car is at `current + incomingHeading`.
Otherwise they take the available left/right branch. Straight alone needs no
controller call. When straight is absent, left precedes right. Slope and special
rail guides retain the incoming heading, which must be an existing port.

The 17 guide witnesses include every straight heading, both curve traversal
orders, head Straight/Curve selection, follower continuation independent of the
head controller and a forced junction turn. All arrivals have legal reciprocal
ports. They are interface witnesses, not a complete P5 state-machine suite.

## Slopes and elevation

P4 ramps own four Traffic rail cells plus four paired filler/support/wall cells.
Only the rail cells carry cars. For climb `d`, rail cells are
`base + i*d + (0,0,floor(i/2))`, i=0…3. Invisible art still occupies space.
Stored ground flags and underground/surface art survive later terrain edits.

`zdiff(heading)` returns the stored difference only when heading equals zangle.
Ascending level1 adds one; descending level2 subtracts one; all other steps are
zero. The argument is the **incoming heading**, not the guide output. Sixteen
literal ramp witnesses cover ascent/descent in all four cardinal directions for
surface and underground patterns, including exact successive locations. Endpoint
track must connect at the appropriate level; a destination at the wrong Z fails.
P4's protected eight-cell ownership/removal rules stay authoritative.

## Car occupancy and publication

The P4 boundary is `TrafficComponents.Car` with exact definition, TrainId and
CarId, separate from rail, road, accessory and platform components. The bundled
P4 profile does not grant arbitrary test-car definitions gameplay registration.
P5 must register actual rolling stock, validate train/car identities and retain
canonical graph checks before enabling placement or movement.

Source `Car.setState` clears the old Inside slot and occupies the new Inside
slot. Outside/Unplaced states occupy no Traffic slot. A candidate Inside state
is blocked by **any** car, including the same train's tail; there is no source
self-train exemption. Source movement passes each previous state down the consist
in head-to-tail order. P5 must publish the complete resulting consist/occupancy,
clock callback and booking atomically, so no partial move is visible to peers.
The three occupancy witnesses distinguish empty, another train and the own tail.
P4 construction protection and operation-specific demolition skips remain in
force when real cars replace trusted component fixtures.

## Signals, controllers and scheduling

**P5-01 clarification, 2 November:** the
[pinned train/controller review](web-parity-train-contract.md#controllers-harbors-groups-and-files)
confirms native dispatch uses the **passing train's effective controller**.
The nullable web ControllerId mentioned below is a reserved P4 field, not native
signal ownership or a per-signal override. Baseline P5 keeps it null and validates
that boundary explicitly; signal properties edit location-keyed settings on a
chosen shared train controller. The source simple controller ignores non-Station
harbors, whereas manual/TAT can wait at signals. This clarifies the P5 interface
without changing P4 infrastructure acceptance or the immutable source witnesses.

P4 saves signal type, cardinal direction and nullable shared ControllerId.
Unassigned is explicit; a decorative signal does not prove a functioning block
system. A signal is a TrainHarbor, not a passenger platform. It delegates
`getStopTimeSpan(train, harbor, callCount)` only when the head heading is within
one eighth-turn of its direction; otherwise zero. Eight heading witnesses record
that predicate. Off-axis rows characterize dispatch, not legal cardinal-track
placement. P5 owns controller identity/reference validation, editing and waits.

`TrainController` supplies junction Straight/Curve and stop duration. At callback
entry the train captures the original Inside head. Positive stops increment
callCount, schedule the explicit wait and return before movement or local speed
lookup. Zero proceeds; other negative values reverse, with ExAV −999 forwarding
and −1000 reverse plus forwarding. Preserve the existing
[dependency review](web-parity-geometry-dependencies.md) for handler-side reversals,
boarding/bell order and the distinct simple/manual/TAT defaults. Source protocols
must be reviewed before replacing sentinels with typed portable commands.

[Speed-limit handoff](parity/speed-limit-handoff.json) remains the numeric and
timing oracle: Low=4; Medium=native for native4, otherwise3; Fast=native for
native4/3, otherwise2. Other values retain the exact equality branches, not a
min/max clamp. Membership uses the **captured prior head location**, including
after reversal; Outside transitions/countdowns bypass local limits. Positive
stops return earlier. Ordered world-local lists, overlap guards, duplicate/removal
semantics and Traffic-based cleanup remain the delivered P4 authoring contract.

## P5 decisions and legal scenario inputs

The following remain P5 work, with no implicit acceptance or defect correction:

- EXAV-DEP-013: both forward and reversed candidates can be null; source then
  dereferences `next.isOutside`. Specify safe retry/emergency-stop behavior before
  porting this path. A nonnull occupied candidate leaves occupancy unchanged and
  still schedules from the captured old head.
- EXAV-DEP-014: delegation can return a null ancestor controller, and group
  reparenting lacks a cycle guard. Specify null fallback and reject malformed
  reference/cycle graphs before authoritative mutation; P10 owns imported graphs.
- Handler-side `reverse2`/`rforwarding2`, central reversal, Outside return candidate
  order and occupied return points need the source-derived P5 scenarios. P4's
  current save policy does not authorize historical import.

Use `MixedContinuationTests`' gameplay-built 128×160×10 network for all delivered
subtypes and saved continuations. Use the P4-23 real-handler 36-cell octagonal
rings and connected junction/signal/ramp/bridge/girder/tunnel/garage component for
legal topology/capacity inputs. These remain **railway-only** fixtures. Their
proposed train/car counts, passenger platforms and controller schedules receive
no capacity or gameplay credit until instantiated and measured by P5/P6.

```sh
python3 scripts/check-railway-handoff.py
python3 scripts/check-railway-handoff.py --source-root /path/to/freetrain-exav-archive
```

The checker validates reconciliation, pinned sources and literal interface
arithmetic without importing production movement code. P5 must consume these
expectations in its actual movement/controller tests and add multi-train,
collision, dead-end, return, reversal, timetable-mask, stock-rendering and saved
continuation scenarios before claiming its phase exit.
