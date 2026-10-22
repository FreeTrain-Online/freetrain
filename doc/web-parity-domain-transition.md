# Parity domain transition design

**Owner policy update — 22 October 2026:** [prototype Save 1 support is retired](web-parity-runtime-policy.md). Unsupported inputs report an error and preserve the current world/files; automatic and manual prototype writes are disabled. Earlier Save 1 compatibility/continuation statements below are historical and superseded. Canonical-world persistence remains open.

**P0-09 integration — 21 October 2026:** [the maintained authority/scope reconciliation](web-parity-exav-integration.md) now links EXAV-01–16, source gaps, branch boundaries and implementation handoffs. Read historical examples below through the current ExAV specifications; feature detail, translation and runtime acceptance remain with each slice.

**ExAV source specifications complete — 20 October 2026:** P0-04 geometry and
P0-05 contract/save reviews supersede the historical examples on this page. Use
[ExAV transition decisions](parity/exav-domain-transition.json),
[serializer inventory](parity/exav-serialization-review.json) and
[contract/persistence specification](web-parity-persistence.md) for current authority.
Original derivations below retain their historical meaning; runtime format,
compatibility and continuation remain implementation work.

Status: P0 source specification and P1 design input, with P0-04 detail in the
[geometry/clock/train review](web-parity-geometry.md) and the completed P0-05
[contract/content/persistence specification](web-parity-persistence.md).
The latter supplies the detailed version/compatibility decisions and serializer
inventory; its explicit rules supersede the initial persistence draft below.
No runtime contract or
save format changes are implemented by this document. Sources are pinned by the
[coverage inventory](web-feature-parity.md); examples are source-derived, without
an original Windows runtime comparison.

**P0-04 update — 20 October 2026:** the maintained
[ExAV geometry/clock/train specification](web-parity-geometry.md) now supersedes
this document's historical geometry/train rule examples, including train cost
cadence, forwarding, speed-limit and platform behavior. Its separate generated
ExAV tables preserve source provenance. The
[four dependency chains](web-parity-geometry-dependencies.md) are source-reviewed;
the [expanded source gallery review](web-parity-geometry-gallery.md) now closes
P0-04 at source-specification level. P0-05 now supplies the separate stable ExAV persistence review and reserved profiles.

## Geometry, clock and train state

`core/world/Direction.cs` numbers N, NE, E, SE, S, SW, W, NW from 0 through 7.
Offsets are `(0,-1)`, `(1,-1)`, `(1,0)`, `(1,1)`, `(0,1)`, `(-1,1)`, `(-1,0)`,
`(-1,-1)`. Left/right advance by -1/+1 modulo 8, left90/right90 by -2/+2,
and opposite by +4. Preserve these indices independently of screen projection.
`Location.getDirectionTo` uses signs of coordinate differences; distance truncates
the three-dimensional Euclidean square root to an integer.

`RailPattern.get` admits pairs separated by at least three eighth-turns. Its
12 normal and 16 junction patterns cannot represent cardinal right-angle bends.
The 16 surface and 16 underground slope patterns include invisible 1×1 samples
with meaningful z transitions. Supports are eight slices per sheet. The generated
[geometry table](parity/historical/geometry.json) retains IDs, connections, exact rectangles,
anchors, climb/level/zdiff/zangle and visibility. Use authored `RailRoads_n.bmp`
for explicit night art. Neither visibility nor sprite size establishes occupancy.

`SingleRailRoad.guide` tries current heading, then left, then right. Its attach
logic distinguishes already-connected track from open endpoints. The P0-04 review
now specifies removal, junction switching, route/slope legality and source quirks;
192 normal-attachment decisions are in the maintained behavior tables. Runtime
fixtures must verify these expectations before replacement.
The voxel draw order in `TrafficVoxel.draw` is road before, accessory before,
rail before, car, rail after, accessory after. Bridge/tunnel foregrounds therefore
cover cars after their backgrounds. `QuarterViewDrawer` traverses view rows,
columns and height, with terrain/water and cut-plane decisions; a global sprite
sort is not an equivalent specification. P0-04 adds source-derived offline
train/pass/ramp/cut compositions; live full-world/runtime comparison remains open.

`Time` uses integer minutes, 60 per hour, 1,440 per day and 365 days per year.
Start is April 1 at 08:00: `(31+28+31)*1440+480 = 130080` internal minutes.
`Clock.tick` increments one minute, refreshes art at 06:00/18:00, invokes due
callbacks, then takes and clears the end-of-turn handler list before invoking it.
`PriorityQueue.Entry.CompareTo` breaks equal priorities by insertion ID, so ordinary
equal-time callbacks run in insertion order. Repeated timers reschedule after the
callback. Persist due minute, ordering counter and semantic action payloads,
replacing serialized delegates. Audit callback cancellation/reentrancy and counter
overflow before generalizing this into a new scheduler.

The original clock stops while a local controller is active. The delivered shared
world requires local tools and overlays to leave other participants running. This
is an explicit authority/UX adaptation to record, not an implicit source-parity
claim. Simulation minutes, transport publication, animation frames and lease
wall-time expiry remain separate.

`CarState` distinguishes Inside(location,direction), Outside(last external
location,direction,timeLeft), and singleton Unplaced. Train state is independently
Unplaced/Moving/StoppingAtStation/StoppingAtSignal/EmergencyStopping. The enum's
existence is not proof each label is reached: positive stop spans currently set
StoppingAtStation, including a TODO about identifying the actual stop cause.
The car chain shifts old states from head toward tail. Reversal flips each car's
heading using `guide().opposite` and swaps the car array; preserve car/contribution
identity and reversed art. A border exit starts an outside countdown of 100; nonzero counters decrement
per movement callback, zero attempts return, and a failed return resets to 10.
The P0-04 review specifies central blocking/return and yard/default-controller
boundaries; full timetable/station subsystem acceptance remains P5/P6 work.

`Train.move` charges `length*20 + passenger/20` on the first move and every 16
moves thereafter; passenger division is integer division. Fares are
`passengers * fare * truncatedEuclideanDistance / 5000` in source integer
arithmetic. Width/overflow behavior needs an explicit compatibility decision
before widening calculations. Static `System.Random` instances occur in
TimeLength, development and other subsystems. The prototype Park–Miller state is
not a compatible replacement; enumerate call sites/consumption and select a
documented algorithm/state contract during P1.

## Contract and persistence boundaries

Keep current protocol **2**, content `prototype-content-1`, and JSON Save **1**
unchanged while P0 discovery proceeds. P0-05 reserves protocol 3, base content
`exav-bundled-v2-1` and parity Save 2, with explicit ruleset/pack metadata; these
versions are not implemented. The proposed next protocol must explicitly
carry eight-direction indices; sparse terrain/ownership and bounded world
dimensions; rail pattern/topology and slope/support metadata; stable per-car
identity/state; contribution/recipe IDs; and semantic scheduled actions. Add
bounded chunked transfer only after measuring legal-city snapshots. Schema/codegen,
C# DTOs, read stores, delta removals, rendering and validation change together.
Do not reinterpret old enum values under a new protocol number silently.

Retain participant-assigned roles, Owner-only world replacement, epoch/revision
preconditions, train leases and wall-time input expiry. Each new command/query
needs an authority and conflict policy. Read-only placement preview validates
legality and cost, then commit revalidates; preview/cancel never mutates state.
Stable picked identity plus epoch/revision survives validation errors but cannot
retarget after replacement. Demo readiness and coherent live read/assets/first-draw
gates continue to isolate commands and saves from demo entities.

Choose an explicit **Save v1 compatibility mode** initially. Route v1 files through
the existing validated prototype rules/content, visibly identify that mode, and
keep the original input. Do not feed its four-direction corners into the legacy
rules, alter train paths silently, or count compatibility scenarios toward parity.
A future converter must produce a reviewed expanded topology and a per-entity
change report before the user can replace a world. Implement mode selection and
its tests before replacing the current save reader; this document alone delivers
no compatibility mode UI or migration code.

The next save schema must include world/terrain/ownership, complete entities and
per-car state, controllers/timetables, passengers/population/development, accounts/
subsidiaries/bank/stocks, random stream states and ordered scheduled actions.
Use exact decimal strings for Int64 quantities, following the existing contract.
Credentials, connection state, leases, held input, browser preferences, CLR type
names and delegates stay outside durable domain state. Save immediately before
and after equal-time actions and compare against independently expected results
as well as uninterrupted continuation.

## Historical files

`MainWindow.getFormatter` selects BinaryFormatter for `.ftgd` and SOAP for `.ftgt`.
`World.save` writes **two serialized roots** in sequence: a one-element
`BGMContribution[]` (nullable current BGM), then World. `World.load` reads the
same sequence using PluginSerializationBinder. A reader assuming just one World
root would discard/misread valid files. `Contribution.GetObjectData` substitutes
ReferenceImpl with a string ID; its object reference resolves through the catalog,
including generated children. RailPattern and Direction also serialize through
reference implementations, and Train recreates its nonserialized state calculator
after loading. Preserve reference identity and required callback outcomes as
explicit data transformations, not object activation.

The historical inventory remains under [historical/](parity/historical/README.md)
with its original 186-file/359-declaration/903-field denominator. The current
[ExAV inventory](parity/exav-serialization-review.json) covers 195 files,
377 declarations and 1,048 fields, plus separate attributed-callback evidence.
The [sample review](web-parity-persistence.md#supplied-exav-samples-and-evidence-limits)
now compares both owner-supplied saves against stable ExAV: 12 differing layouts
for the 0.4 sample and two for the owner-reported 0.6.2 sample. Its product-release
provenance remains separate from observed core `0.6.3326.40211` assembly metadata.
Neither sample is a stable 2.1.1.12 fixture or runtime continuation verdict.
Historical counts and comparisons retain their original source meanings.
Build a bounded record/type reader outside the live world, with allowlisted
mappings, limits and precise unsupported-type diagnostics. Never call the old
deserializers inside the web service. Missing content, malformed input or an
unsupported mapping must preserve the file and running world.

## Optional non-free pack design

The [P0-08 source review and owner-directed launch/credits contract](web-parity-optional-plugins.md)
specifies the separate canonical V2 pack. Default builds/tests/startup need only
bundled content. The reserved startup interface is repeatable `--plugin-dir PATH`,
each naming a plugin-set root with its own `plugin-set.xml` and manifest directory.
Startup validates and prepares or reuses a separately cached portable pack; an
operator need not rebuild core or perform a mandatory manual conversion step.
Explicit offline conversion still takes separate source/output directories for
packaging. Configuration may map the same list to `FreeTrainOnline:Content:Packs`;
names remain reserved until P2 implements them. Containers mount the selected
sets. Default startup performs no download or sibling-directory discovery.

The server publishes exactly its selected catalogs/assets to every client after
validation. Preserve stable ExAV's immediate-child membership rather than
recursively activating the two nested duplicate manifests. Reject selected
factory/plugin/contribution/generated-ID conflicts and unsupported code; do not
apply overrides. Legacy DLL loading is not the portable extension contract. The
canonical set has two DLLs; rail signal has stable source, while map capture still
needs a source/adapter or scope disposition. Resource/case repairs remain P2 work.

Each enabled set adds its own localized credits button/dialog using the set's XML
English/Japanese labels and plain credit text. Base-only mode has no optional-set
button. Set identity/version, source revision, canonical catalog/asset hashes,
dependencies and credits/notice hashes belong to its metadata. Missing/malformed
credits metadata fails that set's startup validation. Both locales, fallback,
long text, keyboard/focus return and multiple sets require P9 acceptance.

Saves reference required pack ID/version/hash and contribution/recipe IDs; local
paths and content binaries are excluded. The server's enabled pack set governs
join/load compatibility. Missing/incompatible packs must be named before world
replacement, preserve the current world/input, and allow retry after installation
and restart. Base-only worlds remain usable without the extra suite. Source,
converted art, localized credits, notices, screenshots, caches and release outputs
remain separate from bundled artifacts; verify a clean base build after opt-in
use. P0-08 closes source inventory/specification, not P2/P9/P10/P11 implementation
or runtime/save compatibility. See OPT-AC-01–05 in the maintained optional rules.
