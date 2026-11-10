# P1-04: spatial entities and profile dispatch — 22 October 2026

The live reference world now carries stable entity IDs, explicit voxel footprints
and pack-qualified definitions through saves, snapshots, deltas and inspection.
The current identity is **reference-spatial-4**, content
**reference-spatial-content-2**, pack **reference-spatial v2**, **Save 5**.
Protocol remains **3**. Only Save 5 is supported; Save 4 is archived and rejected
without conversion under the existing current-format-only policy.

[The source review](parity/spatial-contracts-review.json) binds eleven files to
stable ExAV 2.1.1.12, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0` / `trunk`.
Each hash was checked against `git show` at the pinned commit and the inspected
local bytes. This is a foundation contract slice. It does not register full
bundled/optional content or implement terrain, construction or train rules.

## Source semantics and portable contracts

[SpatialEntities.cs](../src/FreeTrain.Online.Simulation/SpatialEntities.cs) supplies:

- `Cube`: XYZ extents with exclusive upper bounds, inclusive and directional
  exclusive endpoint factories, source X/Y/Z enumeration (Z changes fastest),
  containment, checked endpoints and volume. Reverse-exclusive intervals include
  their first endpoint and exclude their second; equal endpoints give zero extent.
- `Footprint`: an immutable nonempty list of relative voxel members and individual
  transparency flags. Anchors place them in XYZ space. It supports full boxes,
  invisible upper cells and irregular footprints without filling their bounding
  box. Sprite height, half-voxel art and an entity's occupied volume are separate.
- `SpatialEntity`: a positive world-local Int64 ID, revision, exact
  `{packId, contributionId, recipeId}`, anchor, footprint, ownership, silent
  reclamation and Int64 value. IDs replace desktop object-reference identity;
  the other fields preserve distinct source concepts. Member voxels share one
  owner. Normal rails match `TrafficVoxel`: transparent, owned, not silently
  reclaimable, value zero.
- `SpatialIndex`: one owner per occupied voxel; separate outside, vacant and
  occupied lookup results; deduplicated intersecting entities. Invisible occupied
  cells block just like visible cells. Vacant cells and silently reclaimable
  entities are reusable; ownership does not determine reusability. Outside cells
  are never reusable and have no durable entity ID.

The graph is immutable. Add allocates the next positive ID; replacement retains
it and checks the old/new revision; removal checks revision and retains the next
ID. Validation rejects duplicate IDs, overlapping/duplicate footprint cells,
out-of-bounds members and allocator values that would reuse a live ID. Failed
operations leave both the old graph and allocator intact. Rebuilding from saved
state preserves the allocator after removals. Generic index operations are domain
building blocks; gameplay commands must still implement source permissions,
callbacks, bookkeeping and atomic financial effects in their owning phases.

**P1-ENTITY-001–004** document these adaptations. Spatial arithmetic remains checked
Int32 under P1-SPATIAL-001; IDs/revisions use checked Int64. Generic graphs are
bounded to 65,536 total occupied cells/entities. Reference worlds retain their
existing H≤64, V≤128, Z≤16 and 8–1,024-rail limits. These are current implementation
limits, not claimed ExAV full-world capacity. Unplaced vehicles are separate P5
actor state, rather than empty placed footprints.

The synthetic loop initially assigns IDs once in XYZ order. After that, location,
list order and epoch never regenerate them. `ReferenceWorld` validates one entity
per rail, exact recipe/pattern binding, owner metadata and a single transparent
occupied voxel, in addition to its existing reciprocal eight-heading-loop checks.
Larger source entities/footprints exercise the domain contracts independently;
no synthetic building or slope is presented as playable content.

## Compatibility, network and browser

[WorldProfiles](../src/FreeTrain.Online.Contracts/WorldProfiles.cs) dispatches exact
ruleset/content/pack combinations in saves and spatial queries. Matching is
ordinal and pack-order independent; missing, duplicate, extra or version/hash
mismatched packs fail. Definition lookup requires its pack, contribution and
recipe together. No fallback, case folding, implicit alias or silent override is
provided. P2 must extend the registry deliberately for prepared bundled/optional
packs, without borrowing the synthetic reference identity.

The v2 [reference catalog](../src/FreeTrain.Online.Content/reference-spatial.json)
adds `normal-rail` recipes `normal0`–`normal11` and their occupied footprints. Its
exact-byte SHA-256 appears in the C# descriptor and
[client profile](../prototype/protocol/canonical-profile.json). These are synthetic
reference IDs, not invented upstream contribution IDs. Source normal-pattern
indices/ports are unchanged.

Snapshots expose `entities`; each track carries an `entityId`. Deltas carry changed
entities and explicit `removed.entityIds`, alongside base/new revision, epoch and
whole-profile identity. The allocator stays private. All Int64 identities remain
canonical decimal strings, including values beyond JavaScript's safe integer
range. Optional common-envelope fields preserve protocol 3 for the explicit
historical regression harness; the canonical profile requires them. Prototype
reads must omit canonical entity metadata.

The client checks the complete canonical graph before reveal and after delta
application: unique IDs, complete reciprocal rails, exact definitions/footprints,
owner references and metadata. Dangling owners, mixed identities and partial
entity changes fail without publishing a corrupt read. Picking and the Inspector
use the stable rail ID; the XYZ location remains visible separately. Existing
English/Japanese labels, save errors and Owner confirmation flows are reused;
there are no new strings to translate. The source/translation review records
MAN/DEV handoffs and REG-02/07 coverage without closing full gameplay workflows.

## Save and recovery

[SaveCodec](../src/FreeTrain.Online.Persistence/SaveCodec.cs),
[the Save 5 schema](../prototype/protocol/historical/save-v5/schema.json) and
[the independent initial fixture](../prototype/protocol/historical/save-v5/reference-v5.json)
add rail owner IDs and `spatial: {nextEntityId, entities}`. Entity state is restored
directly, then cross-validated against rails and the selected definition registry.
Rails serialize in XYZ order and entities in numeric ID order. Clock, scheduler,
pause and complete random bank continuation retain their existing boundaries.

The 1 MiB/depth-32 limits, strict numbers/required fields, duplicate-member
rejection, owner authority, epoch fencing and atomic replacement remain. Unknown
profiles/packs/definitions return `incompatibleSave`; malformed graph/allocator
state returns `invalidSave`. Saves 1–4 return `incompatibleSave` before current
format deserialization. No old parser or upgrade path remains. Rejected uploads
and named slots preserve the active world/epoch and files. Under ONLINE-SAVE-003,
failed startup checkpoints are discarded and fresh checkpointing remains enabled.
Retired Save 4 artifacts live under
[`historical/save-v4`](../prototype/protocol/historical/save-v4/README.md).

## Verification and remaining gates

Working tree based on **bad1c3b**, 22 October 2026:

- **263 .NET cases pass**, including source Cube endpoints/order, irregular and
  invisible footprints, shared ownership, outside/reuse rules, immutable atomic
  transactions, allocator continuation, exact profiles, large decimal IDs, delta
  removals and seventeen malformed graph/profile cases. Existing real HTTP upload,
  WebSocket epoch replacement, checkpoint restart, clock and random continuation
  cases pass against Save 5. Retired Save 4 rejection/checkpoint protection passes.
- **83 client cases pass**, including full graph validation, corrupt profile/owner
  rejection, entity delta replacement/removal and Save 5 schema checks.
- **13 canonical and 54 historical Chromium cases pass**. Canonical cases cover
  stable-ID inspection, real Save 5 load/socket replacement, malformed entity
  saves before confirmation, corrupt ownership before reveal, source clock
  continuation, roles and narrow Japanese/DPR 2. English and Japanese screenshots
  were visually checked. The full run recorded 66 passes and one obsolete
  Inspector assertion: its expected coordinate key was replaced with the stable
  ID and separately formatted XYZ. The final complete canonical rerun passes all
  13 cases; the full run's 54 historical passes are reused unchanged.
- **105 Python cases pass**. Release backend/frontend builds, generated contracts,
  content/inventory/rail-gallery checks, all eight explicit-source ExAV reviews,
  eleven pinned spatial source hashes, exact catalog/profile/fixture identities,
  byte-identical retired Save 4 archives, current documentation links and project
  boundaries pass. `git diff --check` passes.

Reproduce with `dotnet test FreeTrain.Online.slnx -c Release --no-restore
--disable-build-servers -m:1`, `deno task --cwd web test`, `deno task --cwd web build`,
Playwright from `web` with
`PLAYWRIGHT_BROWSERS_PATH=/private/tmp/freetrain-p1-02-browsers`,
`python3 -m unittest discover -s scripts/tests` and
`python3 scripts/check-project-boundaries.py`. Test runners/browser servers need
local socket access. Logs are `tmp/p1-04-{tests,client,web-build,browser,python,boundaries}.log`;
the final canonical browser log is `tmp/p1-04-browser-canonical-final.log`.
Source and generated checks are `tmp/p1-04-source-contracts.log`,
`tmp/p1-04-generated.log` and `tmp/p1-04-exav-*.log`;
browser evidence is under `tmp/p1-04-browser-evidence/`.
An initial sandboxed .NET attempt could not bind the test runner's local socket;
the final run used local socket access. An early compile check caught a duplicate
test attribute; it was corrected before the passing suite.

P1-04's spatial/identity contracts are delivered. P1 phase-gate review, native/device
UX, full bundled/optional content, source placement/removal side effects,
complete-city persistence/capacity and historical desktop imports remain open.
