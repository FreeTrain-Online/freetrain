# Spatial client/server integration — 21 October 2026

**22 October: P1-01c now replaces the default live prototype with the [canonical spatial reference world and Save 2](web-parity-reference-world.md).** The prototype-runtime statements and counts below record the earlier slice. Prototype gameplay remains only in the explicit loopback regression harness.

**Owner policy update — 22 October 2026:** [prototype Save 1 support is retired](web-parity-runtime-policy.md). Unsupported inputs report an error and preserve the current world/files; automatic and manual prototype writes are disabled. Earlier Save 1 compatibility/continuation statements below are historical and superseded. Canonical-world persistence remains open.

P1-01's domain foundation now has a client implementation, versioned spatial
reads and a usable targeting workflow. Stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0` / `trunk/`, remains the sole rule
authority. The [geometry specification](web-parity-geometry.md) and
[source-vector fixture](../prototype/scenarios/spatial-vectors.json) define the
normal-range expectations. Canonical code uses ordinary project names; obsolete
prototype adapters are isolated under `Prototype/` and `prototype/`.

## Delivered behavior

The Build track card offers Free, Four directions and Eight directions alignment.
Select a point and use it as an alignment anchor, then select another point.
The client snaps the target using the source integer thresholds, draws a guide,
and shows the eight-direction name and truncated three-dimensional distance.
Confirm changes the selected tile; this is not multi-cell rail construction.

Selecting a target issues `POST /api/spatial/preview` with protocol/content
compatibility parameters, expected world epoch, position, optional anchor and
snap mode `0`, `4` or `8`. The authenticated read is available to Observers too.
It captures one coherent world read, performs checked spatial arithmetic and
returns the snapped position, bounds result, projection, optional H/V storage,
heading/distance, epoch/revision and world identity. It does not mutate, reserve,
spend or schedule. Requests are bounded by the existing 16 KiB message limit;
unsupported arithmetic/input returns 400 and stale epoch returns 409.

The client independently computes the same result. Confirm stays disabled until
the server result matches the current epoch, identity, target, bounds, projection
and derived values. Request cancellation and generation checks discard late
results after retargeting, Cancel, tool/level/shape/panel changes, disconnect,
leave or world replacement. A failed/mismatched check preserves the target and
asks the player to select it again. Geometry approval is not placement/cost
approval: the existing mutation still revalidates legality, occupancy, funds,
role, epoch and relevant revisions at commit.

## Contract and compatibility boundary

Wire protocol is now **3**. Deploy/restart matching client and service artifacts;
protocol 2 bootstrap/socket requests receive HTTP 426. The
[schema and shared fixtures](../prototype/protocol/README.md), generated
TypeScript and C# DTOs change together. Original protocol-2 evidence is retained
under [historical/v2](../prototype/protocol/historical/v2/README.md).

Join, full snapshots and deltas carry explicit ruleset, content version and exact
required-pack ID/version/catalog hash. The browser checks identities before
revealing a world or applying a delta. Unknown packs/rulesets block at the
compatibility gate. The prototype pack hash is SHA-256 of the reviewed
`sprites.json` catalog; it is not a canonical content pack or a claim that asset
byte hashes/portable pack conversion are complete.

Train reads carry integer `heading: 0..7` and individual `carHeadings` alongside
positions. Server adapters explicitly map prototype cardinal ordinals to
`0/2/4/6`; followers receive their own heading. The renderer consumes those
headings rather than inferring a four-way direction from screen/coordinate order.
Canonical diagonal rendering currently uses a directional spatial marker;
P2/P5 still owe the source car frames/composition/movement.

Map reads explicitly select `hv` or obsolete `prototypeXY`. Width/Height mean
H/V storage dimensions for `hv`, and old X/Y bounds for `prototypeXY`. Canonical
client geometry implements signed-shift H/V conversion, source projection and
fixed-level picking, including the source tile-center offset. Terrain iteration,
camera framing, pointer conversion, rendering and previews dispatch through the
selected map adapter. Prototype rounded picking/view origin and rail shapes are
kept in explicit compatibility modules.

**The running reference game remains `prototype-reference-1`,
`prototype-content-1`, Save 1 and X/Y bounds.** Its Session panel labels it a
prototype compatibility game. Save 1 input/writer/validation and gameplay rules
are preserved; its map/direction schemas remain separate from the new wire
representation. No world is silently converted to H/V or canonical rail topology.
The canonical H/V path is verified with source fixtures on both sides and in the
renderer; a playable canonical world/pack is not yet installed or selectable.
That remains P1 compatibility/entity work plus P2–P6 subsystem delivery.

## Source, presentation and acceptance mapping

| Surface | Source and browser disposition | Remaining scope |
| --- | --- | --- |
| `rail-alignment`, `rail-anchor` | `Location.align4To/align8To`, `getDirectionTo/getDistanceTo`; explicit controls replace implicit desktop selection gestures, and apply to one target | [MAN-A03](web-parity-manual.md) route construction/cost/obstacle acceptance remains P4 |
| Pointer target, bounds and projection | `World.toHV/toXYZ/fromXYZToAB/isInsideWorld`, fixed-level application of `QuarterViewDrawer.fromABToXYZ` candidates; canonical source and prototype compatibility bounds remain distinct | Terrain heights, controller disambiguators, host footprints and final draw/pick fidelity remain P2–P7; see [DEV-P28](../tmp/web-feature-parity-developer-docs.md) |
| Per-car heading | `Direction.directions`, rotations and sign-vector direction; numeric wire representation preserves indices | Source car art/16 look angles and train state remain P2/P5; DEV-P18/P32 remain authoring evidence |
| English/Japanese | [23 reviewed presentation entries](parity/spatial-translation-review.json), including eight original Japanese direction names/hashes and historical English donors; other labels are browser adaptations | No blanket translation promotion of P0 candidates |

The shared fixture has twelve independently derived canonical/prototype cases.
Both .NET queries and TypeScript use it. Further tests cover all eight headings,
rotations/angles, negative shifts, snapping thresholds, checked Int32 failures,
projection/picking under pan/zoom/elevation, bounds and independent map origins.
HTTP tests verify authentication, Observer reads, no mutation, epoch fencing and
old-protocol rejection. Browser tests cover live alignment, cancellation/newer
targets, disagreement, world replacement, incompatible packs and narrow Japanese
at DPR 2. Existing suites cover save continuation, multiplayer leases, shared
clock, modal/draft behavior, canvas lifecycle and readiness.

P1 remains open for the source clock/scheduler, isolated random streams, complete
stable entity/footprint contracts and canonical-world/save dispatch. Physical
device/native acceptance remains open; automated browser evidence does not close
UX3-GATE.

P0 action tooling now reads the implementation revision it actually reviewed.
Historical action source hashes/pins/statuses are preserved; current ExAV action
metadata explicitly pins its previous UI snapshot at `332ac5f`. Regenerated
source outputs refresh tool/dependency fingerprints, keeping source denominators
and implementation statuses unchanged. This slice's new controls/evidence are
recorded here rather than relabeling the P0 source inventory as implemented.

Validation: **181 .NET tests**, **73 client tests**, **105 Python tests** and
**53 Playwright browser tests** pass. Production client build, generated-contract
check, content reproducibility, all eight explicit-source checks, historical
inventory reproducibility and project-boundary checks pass. English/Japanese
screenshots were visually inspected. Local evidence is under
`tmp/parity-p1/integration-*.log` and
`tmp/browser-evidence-BXXaPw/spatial/`; browser test sources reproduce the journeys.
