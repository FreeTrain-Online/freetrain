# Smooth gameplay train animation: feasibility

Assessment: 3 November 2026. **Feasible as a presentation enhancement while
retaining the authoritative voxel simulation.** First prove smooth rendering with
existing movement times and accounting, and provide multiple shared game-clock
rates. Continuous physical train simulation would be a separate, substantially
larger gameplay change.

**Owner direction, 3 November 2026:** preserve the relationship between train
speeds and every other game system. Offer various slow/fast clock settings that
scale the entire simulation together. Train-only timing changes and economic
coefficient rebalancing are outside this enhancement.

This is a code-based assessment and experiment specification. The shared clock
toolbar and initial live flat-track animation slice are recorded below. Economic
coefficients remain unchanged, and the remaining experiment gates stay open.
Frame rates and latency budgets below are proposed acceptance targets, not measured
results. Existing P5 gates retain their recorded status.

## What the current code establishes

| Concern | Current implementation | Consequence |
| --- | --- | --- |
| Attract train | [DemoRailway](../web/src/lib/demo-railway.ts), [demoCar](../web/src/lib/demo-railway-path.ts): continuous quadratic path, tangent quantized to 16 planar directions, each follower samples the path separately | Existing art can depict subvoxel movement without generating new images. The demo uses an entirely known decorative route. |
| Paint cadence | [MapView](../web/src/MapView.svelte): requestAnimationFrame loop, decorative repaint capped at 30 Hz; live scene repaints on invalidation | Live motion needs its own animation invalidation, with hidden-tab and reduced-motion handling. |
| Movement authority | [FleetMovement.Tick](../src/FreeTrain.Online.Simulation/FleetMovement.cs): one head step, occupancy check, shift predecessor states through the consist | Fractional rendered positions need not enter occupancy, controllers, collision checks or saves. |
| Atomicity | [MixedSessionState.AdvanceOneMinute](../src/FreeTrain.Online.Simulation/MixedSessionState.cs): detached minute, validation, then commit | Any presentation history must describe committed minutes only, including rollback after a later callback fails. |
| Time and publication | [NetworkSession](../src/FreeTrain.Online.Server/NetworkSession.cs): nominal 50 ms pump, one game minute per running pump, publication in 100 ms slots | A fast train can make two moves between publications. Publications may also coalesce during a chunked transfer. |
| Fleet wire state | [FleetWire](../src/FreeTrain.Online.Persistence/FleetWire.cs), [wire contracts](../src/FreeTrain.Online.Contracts/WireContracts.cs): current per-car poses, stable IDs, move count and future callback queue | Current snapshots do not provide every intervening pose or the time of each completed movement. A due callback is not a promise of a successful next move. |
| Rendering | [RailwayRenderPlan](../web/src/lib/railway-renderer.ts), [TerrainScene](../web/src/lib/terrain-renderer.ts), [TerrainViewport](../web/src/lib/terrain-viewport.ts): car operations participate in source traversal and retained terrain images | Smooth cars need dynamic composition and matching picks; repainting/rebuilding the complete scene every animation frame is unsuitable. |
| Car art | [train-renderer](../web/src/lib/train-renderer.ts): planar angles, source curve anchor offsets, separate slope selectors/height offsets | The demo's planar curve cannot be copied unchanged onto slopes, reversals or junctions. |

The six bundled train declarations contain two fast, two medium, one slow and one
superb formation. [RollingStockCatalog](../src/FreeTrain.Online.Content/RollingStockCatalog.cs)
maps these to 2, 3, 4 and 1 game minutes per voxel respectively. These counts were
read from the current [prepared catalog](../src/FreeTrain.Online.Content/bundled-v2.json).

At the nominal current running rate:

| Native speed | Game minutes / voxel | Wall ms / voxel | Frame intervals at 60 Hz | Frame intervals at 30 Hz |
| --- | ---: | ---: | ---: | ---: |
| Superb | 1 | 50 | 3 | 1.5 |
| Fast | 2 | 100 | 6 | 3 |
| Medium | 3 | 150 | 9 | 4.5 |
| Slow | 4 | 200 | 12 | 6 |

The attract train takes **650 ms per voxel** and uses one of the fast formations.
Its gameplay equivalent currently takes **100 ms**, a 6.5× difference. The entire
game day currently takes a nominal **72 real seconds**. These are arithmetic
consequences of configured rates, not timing measurements under load; the server
explicitly skips unbounded catch-up after stalls. Smoothing alone will retain this
fast pace and the fastest stock will still have few visible frames per voxel.

## Historical clock settings

Both the repository's original FreeTrain [MainWindow](../core/framework/MainWindow.cs)
and pinned ExAV `883cf7d:trunk/core/framework/MainWindow.cs` expose Pause plus
four running settings:

| Setting | Desktop timer interval | Game minutes per timer event | Nominal wall ms / game minute | Relative to Normal |
| --- | ---: | ---: | ---: | ---: |
| Slow / ゆっくり | 200 ms | 2 | 100 | 0.5× |
| Normal / 普通 | 100 ms | 2 | 50 | 1× |
| Fast / 高速 | 50 ms | 2 | 25 | 2× |
| Ultra / 最高速 | 1 ms selects a special loop | 2 per loop iteration | Workload-dependent | As fast as possible |

The timer handler calls `clock.tick()` **twice**, and each accepted tick advances
one game minute. Reading the timer intervals alone overstates the time per minute
by two. Ultra pumps UI events and repeats ticks until its conditions change; it
is not a fixed 1 ms/game-minute or guaranteed multiplier. Desktop focus/controller
guards can suppress advancement, and timer/workload limitations affect actual
throughput. This is verified source behavior, not a desktop timing measurement.

The current web rate therefore matches historical **Normal** nominal throughput.
Multiple clock settings are already deferred source-parity work under MAN-T01 /
MAN-A01 in [P1-02](web-parity-clock.md), with delivery assigned to P9. Smooth
animation is the separate enhancement; shared speed controls should be linked to
that existing requirement rather than treated as entirely new stretch scope.

Preserve Slow/Normal/Fast semantics and add slower settings for comfortable train
viewing, for example 0.125× and 0.25× alongside 0.5×, 1× and 2×. Treat the exact
extra presets and default as visual/playtest choices. A web equivalent of Ultra
needs bounded simulation batches, input responsiveness and measured capacity,
rather than copying the desktop's unbounded loop. Every preset advances all
systems through the same ordered game minutes; only wall-clock pacing changes.

## Recommended design

Keep the voxel simulation authoritative. Each car gets a separate ephemeral
render pose: fractional position, sprite selector and stable TrainId/CarId. Only
the renderer consumes this pose. Placement, track edits, inspector values,
occupancy, accounts and saves continue to use committed state.

Start with interpolation of **confirmed history** on a delayed timeline. The
buffer must cover the movement interval as well as publication/delivery jitter:
the future endpoint of a 200 ms segment is not confirmed until its final move.
Try 350–450 ms initially for current bundled speeds and measure actual jitter
before choosing a default. This is a proposed total presentation delay relative
to the server timeline; excessive delivery delay causes underruns. It does not
delay command processing. A local monotonic render clock tracks
authoritative simulation minutes and shared playback rate. It must freeze on
pause/stall and reset on reconnect, epoch replacement and visibility restoration.
Use differences of the existing large integer minute values before conversion to
bounded floating-point interpolation intervals.

For a first controlled replay, capture each committed minute locally. For reliable
live operation at current speed, add a bounded presentation history carrying
per-train/per-car pose transitions with game minute, ordered identity and
discontinuity markers. Stage transitions until the complete minute commits. Send
every transition since the peer's last queued publication, including multiple
moves in one publication; define sequence/gap detection and a current-pose reset
when a peer falls behind the bounded history. A chunked snapshot must either
retain the matching history or explicitly reset it. This is a new, validated
transport contract; the history does not need to enter durable saves.

Interpolating the last two ordinary snapshots is useful only as a limited
straight-track experiment. On a skipped curve it cuts across scenery; on a
junction it can choose a path that never happened. Re-running controllers in the
browser would also risk different choices or extra random draws. Preserve the
actual intermediate route instead. A higher publication rate alone does not
resolve coalescing or stalls.

Follow each stable car ID through its own recorded route. Reuse the demo's idea
of position/tangent sampling and existing angular frames, with geometry derived
from actual entry/exit ports. Its midpoint-to-midpoint Bézier convention differs
from the source car's voxel pose and curve anchor offsets: explicitly define the
new visual anchors, avoid applying both curve corrections, and verify continuous
joins. Parameterize progress by distance within each timed segment where needed
to avoid an unintended speed pulse around bends. Preserve native per-voxel timing,
including its cardinal/diagonal distinction, during the first experiment; changing
travel time by geometric distance would be a gameplay decision.

Flat motion is the first milestone. Slopes need a continuous projected rail
centerline that reconciles the four authored ramp offsets with discrete Z
changes. Quantized pitch/direction artwork may remain visible even with perfectly
smooth translation. Prove both travel directions and ramp/flat joins before
claiming slope support. New art is optional future quality work, not an initial
requirement.

Treat reversal as a change of formation orientation and order, while preserving
car IDs; never interpolate a car to another car's index. Missing-track reversal,
blocked movement, deployment/withdrawal, Outside transitions and world replacement
need explicit hold/reset rules. Never extend a route beyond confirmed history.
On history underrun, hold the last confirmed pose; on a gap, reset instead of
inventing intermediate track. Pause should settle immediately to the committed
pose and discard buffered travel; Step should show the exact resulting pose.
Those explicit controls take precedence over seamless visual continuity.

## Rendering is the main implementation risk

The demo draws all cars over a cached backdrop. Real maps need the existing
ordering of terrain faces, structures, tunnel mouths, bridge foregrounds, poles,
rail before/after layers and height cuts. A single train canvas above the complete
map would fail those cases.

Use the existing operation-based renderer as the starting point. Retain static
operations and prepared images, exclude baked car pixels, then compose fractional
car operations with every overlapping static occluder in the appropriate spatial
and before/after order. Prototype damage-region replay around the union of old
and new sprite bounds, including overhanging art, using a spatial operation index
to find overlaps. Avoid assuming that two global background/foreground planes
can represent all elevations and crossings. Cars spanning cells require explicit
ordering checks at cell boundaries.

Update alpha hit tests, selection highlights and train tracking from the same
render pose and completed paint. A click returns stable IDs and resolves actions
against the latest authoritative state/revision. Construction targeting continues
to use authoritative rail/voxel geometry. Do not mutate immutable ReadState objects
or rebuild TerrainScene for each fractional frame. Preserve readiness checks for
all required recipe variants, including offscreen cars.

There is also a latency tradeoff to prove. At 200 ms/game-minute, slow stock has
800 ms between moves, so strictly historical endpoint interpolation needs roughly
that interval plus publication/delivery margin. Waiting or blocked callbacks must
be represented as holds, rather than stretching travel across the entire dwell.
Animating an already committed step after receipt is another conservative option,
with roughly one-step visual lag and less dependence on a fixed global buffer.
Neither approach supplies both immediate visual response and advance knowledge of
the next collision/controller result. Test whether that delay is acceptable before
adopting slower pacing; predictive movement with authoritative route reservations
would expand the simulation scope and is outside the initial experiment.

## Time and economy: separate decisions

1. **Presentation smoothing:** keep callback minutes, move counts, running costs,
   waits, fares and all simulation rules unchanged. Identical commands and game
   minutes should produce byte-identical saves with smoothing enabled or disabled.
2. **Shared playback speed:** slow all simulation minutes relative to real time.
   The same sequence of game minutes retains the same movement and economic
   results. Costs, income, construction and calendar activity all take longer in
   real time; this changes player pacing without requiring economic coefficients
   to change. Check controls that may depend on wall time independently.
3. **Excluded by owner direction — train speed relative to the calendar:** increase minutes per voxel, alter
   waits or add acceleration. This changes trips per game day and eventually
   passenger demand, timetables, fleet utilization and profitability. It requires
   a separate balancing experiment and explicit rules/content compatibility policy;
   do not introduce it as part of smooth animation or clock settings.

For option 2, keep the real-time 50 ms input/network service loop and accumulate a
bounded simulation-minute allowance. Do not slow the service loop itself. Run,
Pause and Step remain authoritative; playback-rate changes belong to the shared
session and need Owner controls, transport metadata and a defined restart policy.

Suggested comparison settings, preserving existing native speeds:

| Wall ms / game minute | Superb ms / voxel | Fast | Medium | Slow | Real seconds / game day |
| --- | ---: | ---: | ---: | ---: | ---: |
| 50 (current) | 50 | 100 | 150 | 200 | 72 |
| 100 | 100 | 200 | 300 | 400 | 144 |
| 200 | 200 | 400 | 600 | 800 | 288 |
| 325 | 325 | 650 | 975 | 1,300 | 468 |

At 325 ms/minute the demo's fast formation matches its 650 ms/voxel presentation.
Start visual comparison at 200 ms/minute rather than assuming exact demo speed
is best for gameplay. Slowing the entire clock also slows city development and
cash generation, so assess a complete play session before selecting a default.

Current running expense is distance-based: successful moves 1/5/9/… charge
`(cars * 20 + floor(passengers / 20)) * 2000`. Waiting and blocked attempts do not
charge or count. For three cars and 39 passengers that is 122,000 per charged
move. Never charge on animation frames. If only trains slow down, fewer trips per
day also mean fewer distance-based expenses, while calendar-based demand/expenses
may continue; reducing every cost by the same factor would not preserve balance.
Full passenger/revenue and wider economy acceptance remain P6/P8 work.

## Experiment and decision gates

| Stage | Concrete deliverable | Acceptance |
| --- | --- | --- |
| A: Real-route replay | Capture committed poses from the existing gameplay-built P5-08 ring; replay the same formation through the production car recipes at original timing and the comparison rates | Per-car paths follow the actual junction/curve route; no alternate controller calls; matching endpoints, joins and stable identities. A replay alone grants no live-game credit. |
| B: Live flat-track slice | Feature-flagged interpolation, bounded transport history, dynamic occlusion/picking, authoritative pause/step and a baseline discrete mode | Two clients agree on state/accounts; fastest stock retains every turn despite 10 Hz publication; no ghosts or cross-map interpolation after lifecycle changes. |
| C: Geometry and operation cases | Slopes, bridge/pole/tunnel foregrounds, cuts, reversals, blocked trains, deployment and withdrawal | Continuous supported geometry, correct visible pixels and alpha picks; explicit fallback/reset for unsupported transitions. Outside behavior follows P5-14 rather than supplying an incomplete replacement. |
| D: Shared speed controls | Historical Slow/Normal/Fast equivalents plus extra slow presets and a bounded Ultra policy; compare rates on the same save and command trace, then run a real play session | Equal simulated minutes retain exact saves/accounts/RNG/queue. Record player-visible travel/day/development pacing. Train/calendar relationships and economic coefficients remain unchanged. |

Use the [existing movement](web-parity-fleet-movement.md) and
[moving-stock art](web-parity-moving-stock-art.md) witnesses as fixtures, not the
decorative island. Exercise two peers, 100 ms RTT plus jitter, deliberately missed
publications, a chunked transfer, disconnect/rejoin, save/reopen, epoch replacement,
hidden tabs, pause/step and reduced motion. Verify a failed minute emits no
animation transition. Inspect intermediate frames, since matching voxel endpoints
cannot establish smoothness or correct occlusion between them.

Proposed initial performance targets: 60 Hz on the supported desktop workload
with p95 paint/composition within 16.7 ms, a measured 30 Hz fallback, and no
unbounded growth in history or caches. Measure visible-car count, overlapped
operation count, damaged pixel area, sprite decode churn, history underruns,
visual lag, transfer bytes and total simulation/publication cost separately.
Include a large mostly static map to catch full-map work hidden inside each frame.
These targets must be qualified on actual scenes/hardware; the existing
P4-CAP-001/002 deferrals do not make them passing.

**Recommendation:** proceed with A/B as a separate enhancement alongside P5.
The rendering/occlusion work is the largest uncertainty; bounded committed history
is the main live transport requirement. Shared clock speed controls preserve the
owner-directed simulation ratios and fulfill existing source-parity scope; extra
slow presets support animation review. Fractional occupancy, acceleration/braking
physics and economic coefficient changes are outside this enhancement.
P5-12/13/14/15 retain authority over reversal, timing/costs,
Outside behavior and multi-train ordering respectively.

## Assessment verification

The bundled speed counts and timing tables were checked against the prepared
catalog and current pump/registration constants. Original and pinned ExAV source
checks confirm all four timer settings and paired minute ticks in both timer
branches. All 18 local assessment links resolve. The six existing
`web/tests/train-renderer.test.ts` tests passed in the initial assessment, including
448 source poses, recipe variants, foreground ordering, junction followers and
localized movement redraw. These verify the existing renderer baseline; they
provide no evidence yet for smooth intermediate frames or the proposed budgets.

## Initial toolbar slice — 4 November 2026

The top toolbar now exposes shared 0.125×, 0.25×, Slow (0.5×), Normal (1×) and
Fast (2×) clock rates. Normal remains the default. Only Owners can change the rate;
other participants see the confirmed shared value. The 50 ms network/input pump
and 100 ms publication slots retain their service cadence. A fractional minute
allowance scales the whole simulation, with bounded batches and discarded stall
time. Pause and Step remain authoritative at every rate.

The chosen rate survives client reconnects within the current session. Loading a
save, creating a world or restarting the server resets it to Normal; it is not
stored in Save 9. Rate commands revise the published session without advancing
simulation minutes or geometry. The toolbar also displays current authoritative
cash with a literal ¥ prefix, including mixed gameplay worlds and narrow layouts.

Focused checks cover all five rates, rejected values/roles, prompt commands at the
slowest rate, pause/step, bounded stalls, peer synchronization, reconnects, English
and Japanese narrow layouts, and live funds changes after purchase/resale. The
existing gameplay-built ring produces byte-identical saves after equal minutes
at every rate, covering fleet poses, move counts, accounts, RNG and callback queue.
This does not close stage D's Ultra policy or play-session pacing review, or grant
any smooth-rendering acceptance for stages A–C.

### Live-running regression fix — 4 November 2026

Normal/Fast exposed a fleet-successor validator that required every changed train
or car revision to increase by exactly one. Live publications can coalesce several
committed moves. A six-second moving-ring reproduction requested 24 resyncs,
repeatedly reloading the full world and disabling controls during synchronization.
The validator now accepts monotonically advancing revisions while retaining
unversioned-change, identity, ownership and queue checks.

Profiling a read-only copy of the affected 400×600 world also identified repeated
full terrain validation during movement. Mixed updates now reuse a validated
immutable predecessor only when every static terrain, footprint and railway field
is unchanged; fleet occupancy, accounts and callbacks are still checked. Static
changes retain full validation. Clock-only reads carry the renderer's validation
certificate as well as the terrain index, preventing intervening clock updates
from triggering another complete terrain scan.

The copied world ran at Normal and Fast without resyncs or disabled-control
flicker. A repeatable gameplay-built moving-ring browser regression checks both
rates, continued movements/publications, responsive menus and a 200 ms p95 ceiling
for a 50 ms event-loop probe. The 252 web unit tests pass, including coalesced
revisions, zero terrain-cell reads for certified updates, full-validation
agreement and rejection of corrupted geometry, occupancy, accounts and callbacks.
These focused checks do not close the wider rendering/performance gates above.


## Initial live flat-track animation — 4 November 2026

Gameplay trains now animate at **0.125×, 0.25× and Slow (0.5×)**. Normal and Fast
retain the discrete baseline. Add `?smoothTrains=0` to the game URL to disable the
experiment locally. The browser's reduced-motion preference also disables it.
These are presentation choices; no train timing, callback, controller, occupancy,
accounting, RNG or durable-save rules change.

This slice uses the conservative alternative described above: **animate an already
committed step after receipt**, with approximately one voxel of visual lag plus
delivery time. Each native callback's newly scheduled interval supplies its
presentation duration, scaled by the shared clock rate. The continuity refinement
below adds a short buffer and adjusts presentation pacing to the observed shared
clock. Blocked callbacks hold; there is no travel beyond confirmed endpoints. This is an initial A/B deliverable,
not completion of every A–C acceptance case.

### Committed history and reset behavior

[SessionHost.TrainMotion](../src/FreeTrain.Online.Session/SessionHost.TrainMotion.cs)
records per-train, stable-car transitions only after the detached minute commits.
The ephemeral wire extension carries a sequence, game minute, duration and endpoint
position/orientation. An empty car list explicitly marks a discontinuous or
unsupported train transition. It stays in sequence even if another move occurs in
the same publication. No controller is rerun and no random number is drawn for
presentation.

The server retains at most **128 steps / 8,192 car transitions**. Deltas carry every
step since the peer's last queued snapshot. A bounded-history overrun sends a reset
with the current authoritative state. Chunk-transfer coalescing uses this same
sequence comparison. History is outside Save 9 and is cleared on world-epoch
replacement. The browser validates history size, ordering, timestamps, identities,
adjacent endpoints and headings before use, and limits each train's local queue to
32 steps.

Pause and Step settle directly to committed poses and discard buffered motion.
Rate changes, visibility restoration, reconnect and replacement reset local
playback. Deployment, withdrawal, reversal, Outside transitions, history gaps and
queue overflows cannot interpolate between unrelated poses. A reset in a coalesced
publication suppresses subsequent travel for that train until a fresh publication
can establish continuity.

### Fractional rendering and current geometry boundary

[TrainMotionPlayer](../web/src/lib/train-motion.ts) follows each car's recorded
route independently. Cubic segments join the exact authored planar anchors and
use existing angular frames, with a bounded arc-length lookup for translation.
The source curve offset is incorporated once. Immutable read states stay intact.
All planar frames for deployed cars, including offscreen cars, are preflighted
against the active recipe variant.

[TrainCompositor](../web/src/lib/train-compositor.ts) indexes the completed scene's
operations spatially and repaints the union of authoritative and fractional sprite
bounds. Each damaged region replays overlapping terrain, rail, cars and foregrounds
in source traversal order, with the same-level roadbed exception described below.
The terrain scene and retained viewport are reused
between animation frames. Replay uses the retained raster's scale and phase;
cached polygon edges and a small paint halo keep terrain edges stable. Fractional
car frames bypass the static subpixel-image cache. Alpha picking, selection and
the existing one-time Find action use the painted pose and stable identities;
commands still resolve against authoritative state.

Flat normal track, curves, simple junction routes and flat bridge composition are
covered by the focused checks. **Slopes and tunnel transitions explicitly use
committed discrete poses for the entire affected consist.** An intermediate tunnel
probe exposed 25 pickable car pixels at the half-voxel boundary even though both
voxel endpoints were hidden. Tunnel smoothing therefore remains disabled until
continuous interior occlusion is implemented and verified. Reversal animation and
Outside presentation remain later geometry/lifecycle work; their existing P5
scope is unchanged.

### Verification and remaining acceptance

Initial verification passed the production frontend build, generated-contract
checks and all **257 web unit tests**. **79 affected backend tests** pass, including complete coalesced route
history, bounded overflow, reload, unsupported-transition markers, failed-minute
rollback after an earlier train moved, and byte-identical ring saves after equal
minutes at every shared rate. Snapshot regression expectations now distinguish
session pacing/presentation metadata from durable state.

The six toolbar/train-rendering browser regressions pass. The live two-client
journey exercises all three slow settings with 100 ms delivery delay plus bounded
jitter, multiple fractional frames, pause/Step, reduced motion, reconnect and the
discrete flag, without resyncs. Normal/Fast responsiveness and the existing
all-stock/eight-variant source pixel and alpha-pick gallery also pass. Intermediate
normal-track and bridge damage replay matches a complete source-order redraw;
tunnel fallback retains hidden pixels and picks. The new composition witness
checks 15 frames and 144,000 picks per light setting.

The full backend run passes **1,326 of 1,327 tests**. Its remaining failure is the
unrelated legacy
`SignalConstructionTests.Shared_controller_reference_survives_exact_save_and_zero_booking_needs_no_journal_slot`
expectation: it attempts to save signal controller `42`, while TRAIN-D010 requires
the reserved reference to be null. That test's expectation and production signal
policy are unchanged by this enhancement.

Full stages A–C acceptance remains open for fastest-stock live smoothing,
deliberately missed/chunked-publication journeys, broader infrastructure/cut
combinations, hidden-tab/process-restart workflows, continuous slopes/tunnels and
large moving-fleet capacity. The proposed 60 Hz / p95 16.7 ms target and 30 Hz
fallback are not established by these focused tests. Stage D's Ultra and complete
play-session pacing review also remain open.


### Continuity and car-over-track refinement — 4 November 2026

Owner playtesting found a brief pause at each voxel and track pixels covering the
leading/trailing ends of moving cars. Both came from presentation rather than
simulation timing.

The player now schedules adjacent confirmed steps on **one contiguous timeline**.
A **200 ms startup buffer** covers the normal 100 ms publication cadence plus
ordinary delivery jitter. A bounded sample of shared game-clock publications
estimates actual wall time per minute; small, bounded corrections to future
segment durations keep the buffer aligned without moving the current car or its
joins. Coalesced steps keep their order. Genuine blocked callback intervals remain
holds, and an actual history underrun still holds at the confirmed endpoint.
Pause/Step and lifecycle resets discard the buffer as before. The extra buffer is
presentation latency, and changes no movement callbacks, costs or game-clock rate.

The renderer also continues fractional painting over the **last completed scene**
while its committed successor prepares. Previously the scene-key handoff could
fall back to a discrete frame at every movement publication even while the motion
player still had buffered travel.

Moving car pixels now take precedence over **same-level flat ground and rail
beds across voxel boundaries**. When a later roadbed operation overlaps a car,
the compositor masks out the car's opaque pixels from that operation. It leaves
the surrounding terrain in its original order and retains bridge, signal, pole
and higher-level foreground occlusion. Alpha picking applies the same exception.
The reusable mask surface is bounded by the damaged region. Slopes and tunnels
retain their existing discrete fallback.

Focused unit coverage plays 60 confirmed moves at each slow rate with 0–100 ms
publication jitter and both nominal and 8% slower wall-clock pacing. It checks
every 10 ms sample for uninterrupted progress and continuous joins, plus genuine
blocked holds and underruns. The live two-client browser witness now measures
3.4 seconds at each slow rate with delayed/jittered delivery, requiring no missing
fractional frames and no unchanged-pose interval reaching 75 ms after startup.

A separate pixel oracle renders all flat track first and whole cars afterward.
It checks 80 intermediate/end frames per light setting across all eight directions
and both formation orientations, including opaque head/tail pixels and their alpha
picks. The infrastructure damage-replay test retains its foreground and tunnel
fallback checks. The current frontend build, **259 web unit tests** and all
**seven toolbar/train-rendering browser regressions** pass. The backend
simulation/persistence implementation is unchanged by this refinement.


### Tight-turn car overlap refinement — 4 November 2026

Owner playtesting found brief car-to-car overlap flickers immediately before and
after tight 90-degree bends, affecting cars with a following neighbor. A focused
pixel reproduction failed on the old renderer at 51% progress entering the bend.
The rounded voxel depths could swap neighboring cars' paint order repeatedly;
a two-car diagnostic found up to seven swaps where continuous depth required one.

Overlapping moving cars at the same level now resolve their opaque pixels using
**continuous fractional V/H depth**. Near-equal coordinates tolerate floating-point
noise, and stable car IDs settle exact ties. The compositor's existing pixel masks
protect a nearer car from a later-painted farther car; picking uses the same rule.
Terrain, roadbeds and structural foregrounds retain their existing composition.
This does not change car positions, angular art, simulation or playback timing.

A literal legal route containing two adjacent 45-degree curves supplies the tight
90-degree regression. An independent full-frame reference paints flat track and
then cars ordered by their fractional positions. The test compares pixels and
picks at opaque car-to-car intersections across **2,816 frames per light setting**:
all eight approach directions, both turn directions, both formation orientations,
and samples around every car's turn entry, boundary and exit. Both day and night
pass. Unit tests also verify continuous neighbor ordering and deterministic ties.
The frontend build, **261 web unit tests**, the new sharp-turn browser regression
and all seven existing toolbar/train-rendering browser regressions pass.
