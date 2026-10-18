# Parity domain transition design

Status: initial P0 source specification and P1 design input. No runtime contract or
save format changes are implemented by this document. Sources are pinned by the
[coverage inventory](web-feature-parity.md); examples are source-derived, without
an original Windows runtime comparison.

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
[geometry table](parity/geometry.json) retains IDs, connections, exact rectangles,
anchors, climb/level/zdiff/zangle and visibility. Use authored `RailRoads_n.bmp`
for explicit night art. Neither visibility nor sprite size establishes occupancy.

`SingleRailRoad.guide` tries current heading, then left, then right. Its attach
logic distinguishes already-connected track from open endpoints. Removal,
junction switching and slope legality need additional fixtures before replacement.
The voxel draw order in `TrafficVoxel.draw` is road before, accessory before,
rail before, car, rail after, accessory after. Bridge/tunnel foregrounds therefore
cover cars after their backgrounds. `QuarterViewDrawer` traverses view rows,
columns and height, with terrain/water and cut-plane decisions; a global sprite
sort is not an equivalent specification. Live before/after train and cutaway
gallery scenes remain outstanding.

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
heading and swaps the car array; preserve car/contribution identity and reversed
art. A border exit starts an outside countdown of 100; nonzero counters decrement
per movement callback, zero attempts return, and a failed return resets to 10.
Full station/controller/blocking/return calculations remain P0/P5 review work.

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
unchanged while P0 discovery proceeds. The proposed next protocol must explicitly
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

The generated ledger lists 186 lexical serialization candidate files; nested
types, fields, assembly names/version aliases, lists and cycles still require a
complete mapping. No `.ftgd`/`.ftgt` examples were found in the pinned baseline or
current workspace search. Controlled fixtures must be labeled as such; historical
compatibility stays open until representative files and continuation are reviewed.
Build a bounded record/type reader outside the live world, with allowlisted
mappings, limits and precise unsupported-type diagnostics. Never call the old
deserializers inside the web service. Missing content, malformed input or an
unsupported mapping must preserve the file and running world.

## Optional non-free pack design

Default builds and tests use only base content. The proposed build interface is
`--extra-source <repository>` plus a required separate `--extra-output <directory>`;
names remain design proposals until the canonical importer/package tooling is
implemented. Startup configuration will accept a list of prepared pack directories
(`FreeTrain:Content:Packs`) and serve exactly the selected catalog/assets to all
clients. Container operators mount the prepared pack; no download or sibling-path
discovery occurs implicitly. Verify both explicit build/run and base build/run
with a mounted prepared pack. Reject conflicting IDs rather than applying overrides.

Each pack needs ID, version, source revision, canonical catalog hash, asset hashes,
base/pack dependencies, contribution IDs and notices. Saves reference required
pack ID/version/hash and contribution/recipe IDs; local paths and content binaries
are excluded. The server's enabled pack set governs join/load compatibility.
Missing/incompatible packs must be named before world replacement, preserve the
current world/input, and allow retry after installation/restart. Base-only worlds
must remain usable without the extra suite. Source, converted art, notices,
screenshots, caches and release outputs must remain separate from base artifacts;
test a subsequent clean base build after an opt-in build. One legacy DLL is
inventoried, with binary-only behavior still unresolved. No Windows assembly
loading is a runtime extension contract.
