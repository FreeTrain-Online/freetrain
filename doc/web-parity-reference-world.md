# P1-01c: canonical live reference world — 22 October 2026

**Current save policy — 22 October 2026:** [only Save 4 is supported](web-parity-runtime-policy.md). Save 2/3 parsers and upgrades are removed; earlier compatibility and verification statements below record the previous slices only.

**P1-02 update — 22 October 2026:** [canonical clock/calendar/scheduler](web-parity-clock.md) now runs in the live reference world with Run/Pause/Step, ruleset `reference-spatial-2` and Save 3. Canonical Save 2 upgrades explicitly at source start time, paused. The P1-01c static-world/Save 2 descriptions below record that earlier slice.

Normal service startup now hosts a source-valid **32-cell eight-heading rail loop**
on a **32×64×8 H/V map**, with strict spatial **Save 2** persistence. The world is
static: clock/scheduler, random streams, economy, trains, stations and construction
are pending their canonical slices. It does not construct or advance a prototype
world. Wire tick/revision/cash are neutral zero fields and paused is true; the UI
shows a spatial-reference label instead of clock and money values.

## Source and implementation

Stable FreeTrain ExAV 2.1.1.12, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/`, remains authoritative. [Hashed source bindings and presentation review](parity/reference-world-review.json)
cover `RailPattern.singlePatterns`, Direction, Location and World. The twelve
normal two-port patterns retain source indices 0–11. Each curve joins headings
three eighth-turns apart; the route changes travel heading by 45 degrees. No
prototype cardinal right-angle curve, buffer or junction is converted.

[ReferenceWorld](../src/FreeTrain.Online.Simulation/ReferenceWorld.cs) validates
unique ground-level locations, H/V bounds, legal source patterns, reciprocal
neighbors, one connected loop and all eight headings. The spatial-reference limits
are H≤64, V≤128, Z≤16 and 8–1,024 rails, not claimed full-game source limits.
[SpatialReferenceScenario](../src/FreeTrain.Online.Content/SpatialReferenceScenario.cs)
authors four unit steps per heading. Independently reviewed perimeter vertices
are `(27,24) → (31,24) → (35,28) → (35,32) → (31,36) → (27,36) → (23,32) → (23,28)`.
The [shared snapshot](../prototype/protocol/fixtures/snapshot-canonical.json)
provides independently tabulated normal-pattern indices and coordinates.

The default identity is `reference-spatial-1` / `reference-spatial-content-1`.
Its required `reference-spatial` v1 pack hash covers the exact bytes of the
[asset-free pattern catalog](../src/FreeTrain.Online.Content/reference-spatial.json).
This is a synthetic spatial pack, not full ExAV bundled-content registration.
Normal rails render schematically from canonical port vectors; source sprites,
full rendering and train frames remain P2/P5 work. Track selection retains stable
XYZ keys; complete stable entity/footprint contracts remain P1-04.

The existing SessionHost FIFO, authentication, roles, receipts and epoch fencing
serve this runtime. `Pump` drains bounded work without advancing game time.
All gameplay commands return `operationUnavailable` after role/epoch checks.
Snapshots and geometry queries use canonical bounds/projection. The client checks
the exact profile and complete loop before reveal, renders/picks every source
normal pattern and clears targeting on epoch replacement. Unknown packs block at
the compatibility gate. Protocol remains **3**; supported content versions can be
advertised with repeated `contentVersion` query values.

## Save and replacement behavior

[SaveCodec](../src/FreeTrain.Online.Persistence/SaveCodec.cs) writes a new graph:
version, exact ruleset/content/pack descriptors, explicit H/V dimensions and XYZ
rail cells with source normal-pattern indices. [The schema](../prototype/protocol/historical/save-v2/schema.json)
and [independent sample](../prototype/protocol/historical/save-v2/reference-v2.json) describe it.
Rails serialize in deterministic XYZ order. There is no prototype payload, clock,
cash, RNG, train, session epoch, participant, lease or machine path in this format.

Input is bounded to 1 MiB and depth 32. The codec rejects duplicate JSON members,
unknown/missing or incorrectly cased fields, invalid numbers/null entries, dimensions, occupied-cell
duplicates, invalid patterns, broken/disconnected graphs and incompatible pack or
coordinate identities. All validation precedes replacement. Failed loads preserve
the current epoch/world and queued work. Successful loads increment the epoch and
fence subsequent requests carrying the prior epoch. Canonical saves cannot replace
an explicitly instantiated prototype regression host.

Owner-only slot/download and load/upload use the existing save panel and shared
replacement confirmation. Upload stays disabled until availability is known, preventing an initial-response
race from bypassing confirmation. Uploads first call authenticated, read-only
`/api/saves/validate`; unsupported inputs show their error without a replacement
modal. Confirm still rechecks role, epoch and data at the authority boundary.
Automatic/shutdown checkpoints use Save 2. Valid checkpoints restore on startup.
Rejected startup files remain byte-for-byte intact, with automatic/shutdown writes
suppressed for that process and explicit checkpoint writes rejected. Named slots
and downloads can still preserve the fresh canonical world.

## Workflow and presentation mapping

**Inspect** selects a source rail pattern and stable position. **Check a location**
selects a level and verifies Free/Four/Eight-direction alignment with an optional
anchor. The check is read-only; construction confirmation/removal and Run/Step are
disabled. Settings, View, Session and owner save workflows retain the existing
panel/canvas/readiness boundaries. English/Japanese labels are reviewed browser
adaptations, with source context and absence of source UI strings stated explicitly
in the presentation review. Existing source direction translations are reused.

MAN-A03 route-construction/cost/obstacle acceptance and DEV-P28/P32 full-world
geometry/controller/content authoring remain with P2–P6. This slice verifies the
spatial workflow beneath them, not those complete requirements. Optional-pack
installation, source clock/scheduler, random-state persistence, full stable
entities/footprints, historical desktop saves and native/device UX remain open.

## Verification and limits

- **167 .NET cases pass**, including 19 new reference-world cases plus one shared
  canonical contract fixture. These cover independent source-pattern expectations,
  immobility, unavailable commands, deterministic graph round-trip, malformed /
  incompatible rejection, FIFO epoch fencing, real HTTP upload/download/slot writes,
  WebSocket replacement and actual checkpoint restart. Historical prototype tests
  remain explicitly instantiated regressions.
- **77 client tests pass**, including the shared canonical snapshot, every normal
  rail's rendering/picking, rejection of invalid canonical reads, pack hashing and
  independent Save 2 structural schema validation.
- **9 canonical browser cases pass** against normal service startup: live rail
  picking, source H/V targeting/snapping, real Save 2 replacement, unsupported-input
  preservation without confirmation, delayed-preview epoch fencing, unknown packs,
  narrow Japanese at DPR 2, Observer read/save enforcement and delayed-availability /
  Cancel confirmation fencing. English/Japanese
  screenshots are visually reviewed.
- **54 historical browser regressions pass** in the separate `prototype-regression`
  project, explicitly selected with loopback-only `--prototype-regression-harness`.
  They preserve UI/multiplayer evidence without claiming canonical train, station,
  clock, economy, construction or positive prototype-save support. The headless
  trace/profile tools also select that historical workload explicitly.
- **105 Python tests**, production build, generated contracts/content checks, source
  geometry reproducibility, project boundaries and deployment/profile script type
  checks pass. Docker's frontend stage now includes the spatial catalog input.

Evidence is from the working tree based on `447f60a`, not a new committed revision.
Local logs are `tmp/p1-c-*.log`; browser sources reproduce the cases. The full
browser run recorded 62 passes and an upload-fixture synchronization failure:
Playwright set a file on the intentionally disabled input before availability was
known. The fixture now waits for enabled state. The final targeted rerun in
`tmp/p1-c-browser-save-final.log` passes all 13 cases (nine canonical and four
historical save-policy cases); the other 50 historical passes are reused unchanged.
Docker
execution, public hosting, full-game capacity and physical/native acceptance were
not rerun or closed. P1 remains open; next is **P1-02 clock/calendar/scheduler**.
