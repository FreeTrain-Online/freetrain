# Parity contract, content and persistence transition

**P0-05 ExAV source specification complete — 20 October 2026.** Authority is
archive `trunk/`, ExAV **2.1.1.12**, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`. The separate
[serializer inventory](parity/exav-serialization-review.json) expands all **195**
serialization candidates. [Reviewed source dependencies](parity/exav-persistence-rules.json)
and [transition decisions](parity/exav-domain-transition.json) replace the
original-FreeTrain authority for this specification. Historical outputs remain
unchanged under [historical/](parity/historical/README.md).

The current implementation is reviewed independently at
`65cd2fb43d1e6121dce338fe22dcaba20ef0d0ec`: protocol 2,
`prototype-content-1`, Save 1 and `reference-v1` remain in force, including the
1 MiB save limit. This work implements no migration, importer or compatibility
mode. P1/P2 implement contracts/catalogs; P3–P9 supply subsystem state; P10
owns conversions, stable-runtime fixtures and continuation. Overall P0 and native/
device acceptance remain open. [P0-02](web-parity-content.md) and
[P0-04](web-parity-geometry.md) supply the content and geometry dependencies.
[P0-06 scenarios](web-parity-scenarios.md) now specify timer/removal, accounting,
resale and before/after-due continuation cases; executing them remains with each slice/P10.

## Version and ruleset dispatch

| Boundary | Delivered value, unchanged by P0 | Reserved next design |
| --- | --- | --- |
| Wire protocol | `2` | `3`; deploy matching static client/service/schema/codegen |
| Content | `prototype-content-1` | `exav-bundled-v2-1`, plus versioned required-pack descriptors |
| JSON save | `1` | `2` for parity worlds |
| Scenario | `reference-v1` | Keep this identity for the original fixture; assign separate parity IDs |
| Ruleset | Implicit prototype | Explicit `prototype-reference-1` or `exav-2.1.1.12-v1` |

These are independent version axes. A save-version increase does not make old
content compatible. The proposed names reserve a design boundary; no emitted
protocol/content/save claims these values yet. P1 must freeze random algorithms,
integer overflow policy, command payload schemas and tested compatibility dispatch
before emitting the replacement formats.

Protocol 3 join and full snapshot identify `ruleset`, `contentVersion`, and
`requiredPacks: [{id, version, catalogHash}]` alongside the existing server
incarnation, participant/session ID, server-assigned role and epoch. The
server chooses one ruleset/pack set for a shared world. Clients cannot select
authoritative packs independently. Protocol 2 clients receive the existing
protocol-mismatch HTTP 426 response when deployed against protocol 3; never
reinterpret their direction strings or DTOs in place.

Keep the existing schema as the canonical runtime schema until P1 implements
the change. Update JSON schema, generated TypeScript, C# records, runtime
validators, fixtures, read stores, deltas/removals, renderer and command handlers
together. Every changing entity collection needs explicit ID removals and
base/new-revision validation. Pack/schema mismatch blocks join/load/reveal
before entities are applied. A coherent read belongs to one incarnation,
epoch, revision, ruleset and pack set; never combine deltas across those boundaries.

## Spatial and content contract

Parity headings are JSON integer indices `0..7`, exactly N, NE, E, SE, S, SW,
W, NW. The [design table](parity/exav-domain-transition.json) retains all vectors.
Left/right change by -1/+1, left90/right90 by -2/+2 and opposite by +4 modulo 8.
Reject missing, fractional and out-of-range headings. Existing C# ordinals
N=0/E=1/S=2/W=3 and wire/save strings `north/east/south/west` require explicit
adapter mappings to 0/2/4/6. Neither an ordinal cast nor adding more enum labels
preserves the existing meaning. Screen projection never renumbers headings.

| Existing field/model | Parity replacement and validation |
| --- | --- |
| Rectangular `MapDefinition.Width/Height/Levels`; XY-bound `Contains` | Source size H,V,Z, ground/water grid H,V, voxel storage H,V,Z; source entity locations remain X,Y,Z, with source signed arithmetic and conversions. Do not equate dimensions or add an invented coordinate-normalization rule. P1/P3 boundary fixtures establish valid cells and projections. |
| `TrackShape` string at a position | Ruleset-tagged rail DTO with stable entity/voxel identity, source `patternId`, topology, slope entity/base/climb/level, bridge/tunnel/support relationships, contribution/recipe identity and revision. IDs 0–59 are source patterns; support slices are separate art metadata. Invisible slices still carry state. |
| Head direction plus car-position array | Stable train/group/controller and individual car IDs; each car has its own contribution/recipe and tagged state. Retain train status separately. Reversal changes ordering/headings, keeping IDs. |
| `Inside` / `Outside` / `Unplaced` | `inside:{location,heading}`, `outside:{lastLocation,heading,timeLeft}` or `unplaced` with mutually exclusive fields; outside is not an in-world occupancy. Passenger-source state is independently nullable. |
| Selected contribution/flat atlas | Pack-qualified `{packId, contributionId, recipeId}`; preserve original IDs, generated children, case and explicit reviewed aliases. Include 16-angle car frames, reversal, slopes, crop/anchor, day/night/season/color/effects and drawing pass in catalog recipes. Geometry/direction and art-frame indices are distinct. |
| Single prototype clock/random | Absolute source-minute Int64 clock, explicit semantic event order and separate versioned random streams. Prototype DateTime/seed/random remain in its ruleset. |

Source train persistence also requires stop-call/move counters, passenger count
and passenger source state, group ownership, controller bindings and pending
movement/stop timers. Slope filler/support voxels share a slope entity; platforms,
stations, listeners and train occupancy have reciprocal relationships. Validate
those relationships in the candidate before exposing a snapshot. A generic
global sprite sort or an inferred `sprite size == occupancy` rule is insufficient.

All Int64 quantities remain canonical decimal strings, including time, money,
epoch, revision and event order. Geometry indices/bounded coordinates/counts are
JSON integers, validated against explicit bounds. Do not send Int64 as JS numbers.
Fractional quantities need named encodings and rounding rules before their
subsystem is added. Do not widen source calculations silently.

Pack metadata includes ID/version, source revision, canonical catalog and asset
hashes, dependencies, contribution/recipe registrations and separate notices.
P2 freezes canonical hashing/pack-schema details and checks default/opt-in outputs.
Save requirements use exact pack identity/version/hash; a reviewed compatibility
table may permit an upgrade explicitly. No semver-range guessing, case-insensitive
ID guessing, fallback substitution or silent override. Original IDs in historical
files first resolve through an explicit source-profile/pack mapping. Missing or
incompatible definitions report pack/contribution requirements and preserve both
the candidate input and current world so the operator can install/restart/retry.
The server and all clients must use the selected catalogs/assets. Machine paths,
assemblies and content binaries are outside portable saves.

## Save v1 and reference-scenario disposition

Choose **prototype compatibility mode**, not automatic conversion. The exact
`saveVersion=1`, `contentVersion=prototype-content-1`, `scenarioId=reference-v1`
triple selects the existing strict decoder/validator and prototype rules.
Reject every other unsupported triple before world replacement. Preserve the
original input byte-for-byte, report its hash, show the compatibility label in
join/read/UI, and keep the Save 1 writer while that world runs. A protocol 3
compatibility adapter may translate wire representation, but must retain
ruleset-tagged prototype tracks and simulation behavior. It must not create
parity rails merely by translating direction values.

| Prototype shape | Source-pattern correspondence |
| --- | --- |
| `eastWest` | Ports E/W correspond to source pattern 4 |
| `northSouth` | Ports N/S correspond to source pattern 10 |
| `curveSE`, `curveSW`, `curveNE`, `curveNW` | No source normal pattern permits these cardinal right-angle bends |
| `junctionE` | No source junction has N/S/E ports |
| `buffer` | No single-port source RailPattern |

Even the two straight correspondences are not a world migration: world geometry,
terrain, station relationships, car states, clocks, money/costs and random/event
semantics differ. Save 1 permits player-edited rails, so recognizing the initial
loop alone cannot cover existing saves. Never scale/rewrite a world, invent a
source station, drop cars or round time to make conversion succeed.

Retain `ReferenceScenario.Create`, current demo data, imported prototype assets,
original traces, Save 1 fixtures and regression journeys under their original
identities. Its fixed 64×64×16 loop, one three-car train, station, buildings,
constant costs and prototype time/random are a compatibility/reference fixture.
They do not become a source-parity new-game template. Add a separately named,
source-valid scenario and later demo as P1–P6 deliver their systems; preserve
demo/live isolation throughout. Existing reference UI operations remain regression
obligations in both shared-world modes where applicable.

P1 must implement and test the compatibility dispatch/retained rules before
replacing the current save reader or four-direction model. Future removal of
compatibility mode requires a separately reviewed converter: enough map space,
all topology/car/controller/event relationships, independent continuation cases,
archived input, per-entity change report and explicit Owner world-replacement
confirmation. Compatibility play does not satisfy parity scenarios.

## Save 2 domain envelope and candidate replacement

This is a field-family design, not an implemented JSON schema. Save 2 identifies
`ruleset`, `contentVersion`, `scenarioId` (provenance, not fixed entity counts),
`requiredPacks`, and complete `world` domain state. Persist stable world/entity IDs
and domain revisions; a new live epoch is assigned on replacement. Never reuse
an epoch from an imported file as session authority.

| Durable family | Required information / owner phase |
| --- | --- |
| World and space | Name, H/V/Z bounds, terrain/water/ownership/mode, stable entities and voxel relationships; P1/P3/P7 |
| Time and scheduler | Absolute minute; future actions `{dueMinute,order,kind,subjectId,payloadVersion,payload}`, next order, repeat/cancellation semantics; P1 plus each subsystem |
| Randomness | Algorithm/version, named independent stream states and defined consumption order; P1. Legacy statics may not have been saved at all; do not claim original stochastic continuation from such an input. |
| Railway and fleet | Patterns, slope/support/bridge/tunnel/signal/garage state; ordered car identities/states, train/group/controller/timetable state and counters; P4/P5 |
| Stations and passengers | Platforms/passages/grouping/listener identities, demand, waiting/boarding/unloading state, fares and source state; P6 |
| Development and economy | Land values/plans/sites/structures, populations, accounts/history, subsidiaries, debt/deposits/interest, stocks/events and stadium state; P7/P8 |
| Music selection | Nullable catalog BGM selection preserving the historical first root's meaning; no native playback object, local volume/autoplay permission or external file path; P9/P10 |

Save/load must compare independently expected results and uninterrupted
continuation immediately before/after scheduled behavior, including equal-time
events. Event payloads use an allowlisted versioned discriminated union, never
method names supplied by the file. Rebuild derived caches/listener indexes only
after all durable identities are validated; do not re-register timers twice.
Save 1 and Save 2 cannot silently read one another.

The existing candidate-then-swap and one-writer/atomic-write boundaries remain.
Capture the Owner's confirmed file/slot, epoch and input hash. Parse/resolve/
validate into an isolated candidate, report unsupported requirements, then at the
authoritative FIFO boundary recheck role, captured epoch and busy exclusion.
Swap only a complete valid candidate. On success advance epoch, clear held input,
leases, stale targets and old-world effects, reset publication to a full coherent
snapshot and await matching assets/first draw. On failure/cancel preserve the
current world and input. Keep participant receipt scoping and uncertain-outcome
no-replay behavior. New mode selection must reuse the delivered Owner replacement
confirmation; it is not a per-client UI toggle.

The current 1 MiB save limit, three manual slots and message/transfer bounds stay
in force until coordinated implementation. The supplied 23.6 MB historical sample
already exceeds that limit; an offline inspection limit does not raise service
limits. P2/P4/P10 must implement the P0-06 scene specifications, measure legal worlds and update upload/snapshot/read
store/storage/checkpoint bounds together. No full-city supported limit is claimed.

## Authority, drafts and readiness that must survive

Every new command/query declares role, target identity and conflict policy.
Read-only previews can be available to Observers, never mutate/reserve/spend, and
cannot authorize a later commit. Operators mutate only through server checks;
Owner-only save/restore/replacement stays Owner-only. Construction commit
revalidates epoch, observed revisions, terrain/occupancy, cost and funds. Controller
changes/manual input keep owned timed train leases and wall-time expiry. Simulation
minutes, network publication and render frames do not replace lease wall time.

Picked IDs, epoch/revision and draft/request generations must remain explicit.
Validation rejection preserves correctable drafts. Stale conflicts use the
existing Reset/Discard decisions. Lost target/epoch/role/lease cannot retarget
or submit an old draft; late acknowledgements cannot clear newer edits. Menu,
panel and modal entry clears held input and active targeting geometry; local
tools/overlays never pause other participants' shared time. This remains the
explicit source-to-multiplayer adaptation from the geometry review.

Preserve one canvas, decoded assets and camera across normal panel/scheme
changes. Restore camera on same-epoch reconnect. A new world/ruleset/content set
requires matching coherent read, decoded required assets and first live draw,
guarded by current connection/epoch/asset generations. Old fetches/draw callbacks
cannot reveal a newer world. Demo IDs never enter live commands or saves.
Keep the failed-Leave latch. Credentials, pending commands, uncertain receipts,
leases, held input, camera/panels/drafts and browser preferences are outside durable
domain state. Current local preference persistence remains locale and allowlisted
scheme ID; no new preference persistence is implied.

## Historical envelope and serializer inventory

`MainWindow.saveGame` writes `UC`, then `World.save` writes two independent
formatter streams: one-element `BGMContribution[]` (element may be null), then
World. `.ftgd` chooses BinaryFormatter; other extensions use SoapFormatter.
Stable ExAV warns that `.ftgt` may become unreadable after upgrades. Its writer
compression lines and BZ reader decompression call are commented out; the BZ
branch consumes the header without decompression. Older original-FreeTrain did
decompress. Bounded BZ support is an explicit older-profile compatibility addition
(`EXAV-SAVE-007`), not stable loader parity. The legacy loader consumes
two bytes even for an unrecognized header. The portable design requires an
explicit recognized wrapper and rejects ambiguity instead of reproducing that
permissive header behavior (`SAVE-001`).

SOAP uses two sequential formatter documents, not one XML World document.
P10 needs bounded document/envelope handling, encoded array/type metadata,
`id`/`href` relationships and per-root identity scopes; DTD/external resolution
must be disabled. BZ support needs both compressed and expanded byte limits.
No actual `.ftgt` example has been supplied or found in the pinned base tree or
local sample directory. Source-defined/hand-authored fixtures must be labeled
controlled evidence; they do not close representative-runtime compatibility.

The source inventory records **377declarations**, **331 serializable/enum
declarations** (31 enums), 46 helpers and **1,048 declared fields** across all
195 candidate files; 229 fields are static/constant and 36 are `[NonSerialized]`.
Compared with the historical inventory, 185 candidate paths changed bytes and ten
are new paths; one historical path disappeared (a moved listener source).
Counts are lexical source evidence, not saved-object or functioning-feature counts.
Each declaration retains path-qualified nested identity, fields/ordinals, source
inheritance and inherited slots, custom writer/keys/substitution, callbacks,
phase destination and scope. Source reviews retain project membership and source
hashes; source-only/build questions are not silently promoted to runtime features.

The scanner is not a compiler or CLR layout generator. Conditional branches are
retained together; framework bases remain explicit. Assembly/path identity,
qualified inherited copies and duplicate field ordinals cannot be collapsed into
an unqualified name dictionary. ExAV's supplementary `serializationAttributes`
records also retain the two `[OnDeserialized]` accounting methods and two
`[OptionalField(VersionAdded=5)]` passageway fields, which the historical scanner's
named-callback list did not capture. Declared initializers are source evidence,
not automatic missing-field import defaults.

| Source serializer / family | Explicit portable mapping rule |
| --- | --- |
| `Contribution.GetObjectData` → `Contribution+ReferenceImpl(id)` | Resolve exact base/generated/pack contribution ID. Inherited custom serialization wins over ordinary contribution fields; mutable catalog fields are not automatically saved. |
| `Picture` → `Picture+ReferenceImpl(id)` | Resolve picture registry/recipe identity; never embed surface/loaders/native handles. |
| `Direction` → `Direction+ReferenceImpl(index)` | Source integer heading, range checked; no reflective singleton call. |
| `RailPattern` → `RailPattern+ReferenceImpl(id)` | Source IDs 0–59; no art-based guessing. |
| `PersistentPopulation` → `SerializationHelper(reference)`, `StructureContribution+PopulationReferenceImpl(id)` | Resolve shared contribution population identity explicitly; preserve demand/listener relationships without creating duplicate population definitions. |
| `SimpleTrainControllerImpl`, `DelegationTrainControllerImpl`, `ThinPlatform+PlainPlatform` → empty reference helpers | Named source singleton tags, separate from mutable controller instances. Preserve source behavior/differences; singleton existence does not prove inheritance works. |
| `CarState+Unplaced`, `NullSprite` implement `IObjectReference` | Explicit unplaced/no-draw tags; never invoke `GetRealObject`. |
| `BGMManager+TempBGMContribution` overrides serializer with `fileName` | Recognize external-file metadata and issue a precise excluded/unsupported media diagnostic; never open paths from input or silently replace it with a catalog track. P10 must specify an explicit conversion report for any accepted omission. |
| `SparseVoxelArray` | Active default fields H/V/Z/index; commented custom serializer is inactive. Preserve multidimensional block/voxel identities, bounds and coordinate order before flattening. |
| Trains/groups, rail/voxel/entity graph | Preserve shared/cyclic IDs and reciprocal references. Keep per-car state, timers/counters, controller/group ownership, platform/station and slope/support ownership. |
| `World.otherObjects` | Typed world extensions: account genres/history, subsidiaries, bank/stock roots and other recognized registrations. Unknown reachable entries block conversion rather than disappearing. |
| Population/development/structures | Distinguish contribution definitions from placed state, plans, construction phase and future growth. Half-voxel choices/variants must survive by recipe plus instance state. |
| Banking/stock/stadium | Preserve mutable module/debt/deposit/company/trend/event/game state; static configuration is separate evidence, not assumed serialized. |
| `ArrayList`, `Hashtable`, `SortedList`, `CollectionBase`, `Set` | Inert ordered entries/references with explicit comparer/key rules; do not deserialize CLR collections or lose insertion/event ordering. |
| Drawing Point/Size, source enums and scalar structs | Versioned value mappings, exact widths/ranges; enum wire `value__` is not an entity. |
| Delegate/MemberInfo/Comparer/CompareInfo framework records | Read inert data only. Known domain callbacks become versioned semantic events or rebuilt listeners; arbitrary target/method/comparer/reflection records are unsupported. |

Custom serialization and singleton rules above cover all nine `GetObjectData`
writers and all reference-resolver families found by the inventory. The seven named
deserialization callbacks have explicit outcomes: World recreates transient
outlook listeners; Train recreates the next-car-state calculator; RRCrossing
reattaches railway-change observation; PThreeDimStructure.VoxelImpl and
FatPlatform.FatPlatformVoxel reconstruct sprites from owner/recipe; SubsidiaryMarket
recreates volatile events; Station recreates its nonserialized station-change
listeners. Rebuild these outcomes as data/derived state. Do not
execute callbacks. Bank/stock world hooks reattach module roots through
`otherObjects`; genre history invalidates catalog-side caches on world change.

`Clock.queue`/`PriorityQueue.Entry` persist due priority, insertion ID and delegate
targets. `Clock.RepeatedTimer` holds clock/handler/interval. Preserve equal-time
ordering, repeat interval and cancellation identity explicitly; do not serialize
delegates in Save 2 or re-register an existing scheduled callback on load.
Nonserialized end-of-turn handlers and static System.Random streams are not
recoverable merely by enumerating queue entries (`SAVE-004`). P1/P10 must decide
and test each reconstructible boundary and report unsupported continuation precisely.

The old `PluginSerializationBinder` ignores its `assemblyName` argument, first
tries `Type.GetType`, then searches public contribution assemblies. That is
historical evidence, not a compatibility policy. New profiles allowlist exact
source-format assembly/type/field signatures and explicit reviewed aliases;
no assembly load, reflection activation, object replacement or callback execution
is allowed in the service (`SAVE-002`).

## Stable ExAV state and reconstruction differences

The [source review](parity/exav-persistence-rules.json) hashes 26 semantic
dependencies and 24 current implementation files. Retained authority, draft,
readiness, pack isolation and prototype compatibility decisions were rechecked
against these boundaries. Geometry mappings reuse P0-04's separate ExAV evidence.

| Family | Stable source finding and implementation obligation |
| --- | --- |
| Time / numeric widths | `Time.currentTime` and `TimeLength.totalMinutes` are signed Int64 minutes. `Time.totalMinutes` subtracts `START_TIME`; absolute and elapsed values must remain distinct. Money/debt/history totals are Int64, while queue insertion IDs and many population/counter calculations remain Int32 and some reports use double. Decimal-string encoding does not authorize widening source arithmetic. P1/P6/P8 freeze encodings, overflow and rounding. |
| Timer cancellation | `Clock.registerRepeated` returns a delegate targeting `RepeatedTimer.onClock`, distinct from the user's handler. Station/platform, bank assets, Economy and stadium retain returned handles. Map due time, equal-time insertion order, interval and cancellation identity; restoring the queue and registering the same timer again is invalid. Queue comparison uses signed Int32 subtraction. P1/P10. |
| Station migration | Four old/aligned handles; `clockHandlerHour` replaces old handles when `repeatHandler1 != null`, registers hour/day boundary callbacks and clears the old pair. `speciallisteners` can be lazily rebuilt; regular/special listeners retain station and dimension relationships. These are version-specific transitions, not blanket post-load registration. P6/P10. |
| Train resale / forwarding | `_forwarding` is durable; `boughtThisTrain` is nullable `Time`, consulted by age-based resale. A null purchase time yields no resale payment on that source path. A source comment mentions OptionalField for forwarding, but the live field has no attribute. No purchase timestamp may be invented for old files. P5/P8/P10. |
| Accounting repair | `Recorder.OnDeserializedMethod` repairs null 32-day/13-month arrays; the outer history callback repairs five missing summary wrappers. These are two additional attribute-driven callbacks beyond the seven named interface callbacks. Convert outcomes as explicit data; never activate methods supplied by a save. P8/P10. |
| Passageways | Two `passagewayRailType` fields carry OptionalField VersionAdded=5 with a concrete initializer; version-specific missing-field semantics still need fixtures. P4/P10. |
| Module roots | `otherObjects` includes speed limits (three location lists), land value, account-genre histories, subsidiaries, bank, stocks, Economy and its random state. Preserve typed roots and cross-references, not arbitrary CLR dictionary activation. P4/P7/P8/P10. |
| Randomness | Stock `Economy._random` is static **and explicitly saved/restored** through `otherObjects[key+"rand"]`. Development's static `System.Random rnd` is not world-saved. Review each reachable stream; neither all-saved nor all-unsaved is a valid generalization. P1/P7/P8/P10. |
| Stadium / population | Capacity, mutable population counters, special station listener, matches and returned timer handle are durable instance state. New population families resolve against ExAV catalog rules. Generic-building reconstruction is insufficient. P6/P7/P8/P10. |

## Supplied ExAV samples and evidence limits

The [maintained structural summaries](parity/exav-save-samples.json) retain both
samples' hashes, assembly identities, root extents, field differences, contribution
references and delegate targets. Full local record-layout reports remain under
`tmp/parity-p0/`. Original inputs were read without modification or CLR activation.
The new file's **0_6_2 provenance is owner-reported from its download page**; no URL
was supplied for independent verification. Observed assembly versions are separate
evidence and are not a product-release equivalence claim.

| Observation | Earlier sample (`データ０１.ftgd`) | Additional `チョココロネB400x600x16.ftgd` |
| --- | --- | --- |
| Profile evidence | Readme requires ExAV 0.4; core `0.4.3255.7465` | Owner reports Ex-AV 0_6_2; core `0.6.3326.40211` |
| SHA-256 | `35b24fae1b40e5004ca46e9968dd34f3da61d5e6009f8ea1b4f60d256a7b9313` | `8991dbba1b2f73c14eb921e3dcb4a69d2bce26e45d81c5e318bc19c20e061263` |
| Bytes / UC root extents | 23,584,925; `[2,167)`, `[167,23584925)` | 10,119,231; `[2,168)`, `[168,10119231)` |
| Dimensions / water | 1500×1090×45 / 11 | 400×600×16 / 4 |
| Ground bytes / absolute clock minute | 1,635,000 / 532970 | 240,000 / 130722 |
| Objects | BGM array + 33,366 World-stream nodes | BGM array + 195,078 World-stream nodes |
| Layouts vs stable source | 150: 115 field-name matches, 23 framework, 12 differences | 68: 52 field-name matches, 14 framework, two differences |
| Contribution references vs stable bundled IDs | 150: 11 exact, 139 unresolved pack/alias requirements | Seven, all exact bundled IDs |
| Delegates | 309 entries / 18 target-method pairs | 22 entries / nine target-method pairs |

Both streams end exactly at EOF and references resolve within each independent
stream. The new save also names StockMarket `0.5.3326.40213` and Bank
`1.0.3326.40213`; a core version alone cannot identify the complete profile.
All new-sample non-framework layout names have a source candidate. The two
field-name differences are missing `Train.boughtThisTrain` and missing
`Economy.repeatHandlerMajorUpdateEvent` / `repeatHandlerMinorUpdateEvent`.
No purchase-time default, cancellation-handle reconstruction or runtime compatibility
is inferred. Seven matching contribution IDs do not validate the complete asset/
recipe/pack version or exercise all stable subsystems.

Against **stable ExAV**, the older sample has 12 differing layouts, superseding the
historical comparison's three differences against original-FreeTrain. They cover
StationCollection, history/Recorder, Economy, Station, TransportLog,
StationListenerImpl, Train, ThinPlatform, its stair/passageway outlooks and FatPlatform.
The complete missing/extra names remain in the maintained summaries. The original
historical report remains accurate for its original comparison revision.

Matching normalized field-name sets does not verify field types, enum values,
order, duplicates, defaults, graph semantics or algorithms. There is **no supplied
stable 2.1.1.12 runtime save, SOAP/BZ sample or original-runtime continuation**.
Older profiles remain explicit P10 mapping work (`SAVE-003`, `EXAV-SAVE-008`).

`scripts/parity_nrbf.py` is an offline structural reader. Its implemented layout
uses Microsoft's [MS-NRBF record enumeration](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/954a0657-b901-4813-9398-4ec732fe8b32),
[typed class records](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/847b0b6a-86af-4203-8ed0-f84345f845b9),
[binary type metadata](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/054e5c58-be21-4c86-b1c3-f6d3ce17ec72),
[array kinds](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/4dbbf3a8-6bc4-4dfc-aa7e-36a35be6ff58),
[primitive types](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/4e77849f-89e3-49db-8fb9-e77ee4bc7214)
and [UTF-8 strings](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-nrbf/10b218f5-9b2b-4947-b4b7-07725a2c8127).
It supports UC/two streams; typed class metadata/reuse, inert libraries, strings,
references, primitive values, arrays/rank/lower bounds and null runs. It retains
negative value-type IDs, forward/shared/cyclic references and duplicate field
slots, with no CLR activation. Large primitive arrays retain a byte hash instead
of allocating conversion values. It rejects BZ/SOAP, untyped classes, method-call/
return records and other unsupported records with a byte offset. It is not a
complete NRBF/domain validator or production importer.

Limits are 64 MiB input, 500,000 objects, 2,000,000 records, 1 MiB per UTF-8
string/16 MiB total string bytes, 256 members per class, rank at most 8,
20,000,000 elements per array/40,000,000 total, depth 100, exactly two roots
and no trailing bytes. These are explicit offline inspection budgets, not
supported gameplay limits or a hardened service memory budget. P10 still needs
typed graph/domain validation, profile mappings, bounded BZ/SOAP readers and
reviewed original-runtime continuation. Default generation/tests require no
owner sample or extra suite; the sample assertion skips when absent.

## Difference register and remaining gates

| ID | Disposition and owner |
| --- | --- |
| `SAVE-001` | Portable reader recognizes wrappers explicitly rather than consuming arbitrary leading bytes. P10 tests recognized UC/BZ and rejects ambiguity. |
| `SAVE-002` | Replace assembly-name-ignoring binder/object activation with explicit inert mappings. P10 rejects unknown signatures and reachable types. |
| `SAVE-003` | Older ExAV profiles differ from stable layouts; two new-sample and 12 older-sample layout differences require explicit mappings, not inferred defaults or silent drops. P10. |
| `SAVE-004` | Static randomness, nonserialized handlers and derived state can leave legacy continuation underdetermined. P1/P10 must declare reconstruction/mismatch limits and obtain runtime evidence. |
| `SAVE-005` | Prototype topology/time/economy are retained as labeled compatibility rules; automatic conversion is deferred until explicitly reviewed. Implement dispatch before model replacement in P1. |
| `SAVE-006` | Historical world view options/native playback objects become portable local presentation/derived playback state; retain catalog BGM selection and source art behavior, with no silently added local persistence. P9/P10 review. |

P0-05 is closed at **ExAV source-specification level**. Serializer and transition
reviews cover the stable source and both supplied older samples. P1 implements contract/model/compatibility changes; P2
implements catalog/pack changes; each gameplay phase adds durable subsystem state;
P10 closes actual conversions/continuation/recovery. `MIG-*` are future acceptance
obligations, not tests claimed to have run. P0-06 [scenario specification](web-parity-scenarios.md) is complete; fixture
implementation/capacity measurements, P0-07/P0-08/P0-09 remain open. No gameplay, original-runtime comparison, browser/native
acceptance or optional-suite compatibility is established here.

Validation on 20 October: **93 Python tests**, fresh ExAV serializer/transition
and both local sample checks, historical persistence reproducibility and
`git diff --check` pass. These are source/tooling checks, not runtime acceptance.

Reproduce from the repository root:

```sh
python3 scripts/review-exav-persistence.py --source-root ../freetrain-exav-archive
python3 scripts/review-exav-persistence.py --source-root ../freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests -v
# Explicit local-only sample inspection; no search/download or object activation.
python3 scripts/review-exav-persistence.py --source-root ../freetrain-exav-archive \
  --sample 'tmp/チョココロネB400x600x16.ftgd' \
  --sample-report tmp/parity-p0/exav-062-sample-review.json --check
python3 scripts/review-exav-persistence.py --source-root ../freetrain-exav-archive \
  --sample 'tmp/000-001-sample-save/recovered/データ０１.ftgd' \
  --sample-report tmp/parity-p0/exav-04-sample-review.json --check
```
