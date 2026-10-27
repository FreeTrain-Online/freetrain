# P3 world creation, generation and terrain — 27 October 2026

**P3-01 complete at source-contract level — 27 October:**
[terrain contract, decisions and evidence](web-parity-terrain-contract.md) now
specify dimensions/bounds, terrain/corners/water, source editing/forest/creek/
terrace behavior, source-defect adaptations, compass sectors and workflow/text
handoffs. Forty pinned files, 77 literal fixture vectors and 59 source text
records are checked separately from runtime verification. Next is **P3-02**.
**Owner water policy:** retain the existing simple global plane throughout this
checklist; elevated/flowing water and fluid simulation are outside its scope.

The owner has directed execution to move into P3 and requested configurable
procedural world generation. This document specifies that work and breaks it into
reviewable implementation slices. **Source research and planning only:** no new
world generator, terrain commands, save format or browser controls are implemented
by this change. P3 implementation and acceptance remain open. P2's deferred human,
native/audio, crop and optional-pack qualifications retain their existing owners;
moving to P3 does not close them.

Stable ExAV **2.1.1.12 / V2**, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk/`, supplies the terrain
semantics. [The source review](parity/world-generation-review.json) records 18
pinned raw-file hashes and symbols/review areas. Three working-tree manifests have
UTF-8 declarations in place of Shift_JIS; their decoded differences are recorded.
The C# files reviewed match the pinned bytes. No original Windows runtime was run.
The existing [action](web-parity-actions.md), [geometry](web-parity-geometry.md),
[random](web-parity-random.md), [manual](web-parity-manual.md) and
[scenario](web-parity-scenarios.md) reviews remain the broader evidence authorities.

## What ExAV actually supplies

Paths below are relative to the pinned archive's `trunk/`.

| Facility | Source and actual behavior | P3 implication |
| --- | --- | --- |
| New-world providers | `plugins/system/plugin.xml` declares empty-world provider `{9093E8B2-9215-42f3-A36A-5CAA7AF389FF}`; `plugins/org.kohsuke.freetrain.tools.terrainloader/plugin.xml` declares image provider `{9CC6E93F-06D0-4d98-8F8C-7B3CE481F62E}`. `core/framework/NewWorldDialog.cs` selects providers. | Two declared bundled routes, plus the requested procedural route as a web addition. |
| Empty world | `core/contributions/others/NewWorldDialog.cs:createWorld` accepts name and positive x/y/z entries, defaults 30/30/6, constructs `Distance(x, y + z*2, z)` with water `z/4`. `World` initializes every ground level to water level. | Flat empty ground; the source's displayed dimensions and stored H/V dimensions differ. Specify their browser mapping explicitly. No terrain noise or forest initialization here. |
| Heightmap input | `LoadDialog` accepts BMP/GIF/PNG/JPG, width, depth, height and water level; dimensions must be positive and `0 <= water < height`. `TerrainLoader.getDepth` samples `Color.GetBrightness()` with bilinear interpolation, offsets alternate H/V rows by half a sample and clamps source-image edges. `displace` truncates `min(Z-1, Z*brightness)`. | Port the actual brightness formula (HSL lightness, not luminance), sampling, truncation and coordinate mapping; expose dimensions/water and a preview. Bound decoded pixels and output work independently of file bytes. |
| Height normalization | `TerrainLoader.interpolate1` repeatedly lowers high ground until each valid four-neighbor group spans at most one whole level. | This can erode peaks and narrow land. It is a deterministic terrain compatibility pass, not hydraulic erosion. Measure its effects on generated geography. |
| Slopes and terrain texturing | `smooth` considers flat 2×2 neighborhoods and higher neighbors within 4×4, deriving quarter-height corner offsets from the image. `texture/getCornerHeight` creates `MountainVoxel` slopes. | Preserve quarter-height corners, shared-corner agreement and boundary behavior. Here “texturing” means mountain/slope voxel creation; it does not plant forests or synthesize biomes. |
| Fixed terrain maps supplied by plugins | `TerrainOnlyNewGameContribution` reads image, size, height, water, name, description and author, then calls the same loader. The manifest registers its factory type. | A reusable map-template facility; not a third declared base map and not a procedural algorithm. Resolve pack resources through prepared metadata. |
| Water | `World.waterLevel` is one immutable world-wide byte; `QuarterViewDrawer` uses that plane for water surface and underwater drawing. Ground is tracked separately. | Sea-level basins can represent lakes, seas and straits. No independently elevated lake surfaces, flow simulation, drainage network or waterfall generator is established. |
| Forests | `ForestBuilder.createRandomTrees` uses `2*density` fair draws, so tree count is binomial with mean `density`; picks random sub-tile positions and sprite variants and sorts positions by screen Y. `create` requires the ground level and reusable terrain or an existing `MountainVoxel`, then calls `setTrees`. | Useful scatter/appearance semantics and slope integration. This is a rectangle placement tool, not automatic ecological world generation. Preserve species contribution IDs and variants. |
| Bundled tree palette | `org.kohsuke.freetrain.land.forest/plugin.xml` declares six species: cedar, pine, camphor, maple, ginkgo and beech; each has base, `-m` and `-g` declarations and seasonal picture bindings where supplied. | Start with these available trees. Decode variant meaning from metadata rather than treating suffixes as new species. No new foliage art is needed for the first generator. |
| Random land selection | `core/contributions/land/RandomLandBuilder.cs` chooses uniformly among configured static-land IDs for eligible cells in a rectangle. | Provides mixed ground-cover selection, without altitude/moisture/clustering heuristics. Only offer reviewed, installed contributions as procedural foliage/cover. |
| Creeks | `CreekBuilder.computeRoute` walks toward two endpoints at a fixed z using cardinal steps, randomly choosing an axis when needed; seed is `((x1*128+y1)^(x2*128+y2))`. `apply` checks reusable ground, joins existing creek direction bits and places `CreekVoxel`. | Deterministic decorative connected creek tiles. It neither excavates a water channel nor searches downhill or around obstacles. Rejected cells can break the placed route. Do not equate it with a hydrological river generator. |
| Manual terrain editing | `MountainController` selects and adjusts quarter-height corners, Ctrl reverses raise/lower, Shift applies on mouse movement. Registered `foobarbazer.Mountain2` adds rectangular raise/excavate and Ctrl repair. Terrace plugin supplies separate terrace/cliff strategies. | Keep the distinct tools and constraints; provide explicit accessible actions alongside keyboard shortcuts and authoritative validation. |
| Map-construction flag | `Core.init` sets `isConstructionMode`; a search of stable core/plugins finds its declaration/assignment but no other consumers. | Preserve the requested map-authoring workflow independently of the retired launcher. The flag alone is no evidence for unlimited money, unrestricted placement, special pause behavior or bypassing legality. Specify web behavior explicitly. |

Inspection of these providers and a stable-source search found no built-in
configurable plains/mountain, island, coastline, lake-basin, forest-distribution or
whole-map seed generator. These requested heuristics are **new generation
behavior**, feeding the source-compatible terrain model. NeoFT generators and
Heat/RoadSimulator experiments retain their separate later-enhancement status;
this request does not make those algorithms parity authorities.

## Proposed browser controls

Use the existing New world/Save workflow and `PanelFrame`/draft/confirmation
components. Offer **Empty**, **Heightmap** and **Generated** sources. The generated
source gets a small useful set of controls, with advanced parameters disclosed
progressively. Labels and help describe geography, not noise-library internals.

| Group | First delivery | Meaning and validation |
| --- | --- | --- |
| World | Name, size preset/custom H/V dimensions, vertical capacity, seed and New seed | Show actual playable dimensions and a compass. Initial size suggestions are 64×64×16, 128×128×32 and 256×256×64 from the city proposals; these become selectable only after capacity checks, not by copying those proposals into supported limits. |
| Terrain | Plains / rolling hills / mountainous; relief, feature scale, smoothness | Presets expand to explicit values; relief and smoothness are separate from vertical capacity. Preserve sufficient headroom for future elevated construction. |
| Coast | Inland / island / selected coast sides; independent N/E/S/W selections; sea level; approximate water coverage | Inland has no edge-connected sea. Island requires sea around the boundary and a connected main landmass. Selected sides allow multi-side coasts; preserve land connections to unselected sides. Island and selected-side modes are mutually exclusive. Water fraction is a target with declared tolerance, subordinate to topology. |
| Lakes | Off / count, approximate size | Enclosed basins at the global water plane; validate a separating land rim after normalization. Report infeasible sizes instead of silently merging a required lake with the sea. |
| Rivers | Off / count, width and meander; automatic outlet or chosen coast side | First delivery: connected channels at the global water plane, with dry-land banks and an outlet into selected sea or an interior lake. Inland without a lake can use explicitly labeled edge-to-edge channels. Decorative creeks remain a separate source tool. |
| Forests | None / sparse / standard / dense; mixed species or chosen tree set; clustering | Coverage controls which eligible cells get forest; per-cell source density controls tree appearance. Reject underwater/invalid surfaces. Species are resolved from the prepared bundled catalog. |
| Other cover | Optional reviewed foliage/ground-cover mix | Enabled only for installed contributions with verified placement/render/save behavior. Surface missing/unsupported choices explicitly; optional packs retain separate identities and distribution. |
| Buildable land | Minimum connected flat area / gentle starting area | A generator constraint with measurable cell area and slope threshold. Do not promise playable railway/economy acceptance before P4–P8. |

Provide top-down height/water/forest preview with a compass and a small isometric
sample using real terrain recipes. Show realized land/water/forest coverage,
height range and connected buildable area. Changing a setting marks the preview
outdated; bounded automatic refresh or an explicit Generate preview action
produces a new candidate. Regenerate with the same seed is reproducible; **New
seed** is the explicit variation action. Editing controls or closing the panel
does not replace the live world or advance its random streams.

N/E/S/W must follow canonical `Direction` and world geometry. Bind the compass,
side selectors and preview to the same coordinate transform, with asymmetric
fixtures; H/V storage edges and screen edges must not be casually relabeled as
compass directions. P3-01 specifies the boundary-sector mapping as
[WORLDGEN-D001](web-parity-terrain-contract.md#creation-bounds-and-coordinates).

## Generation design and constraints

Keep generation in Simulation with immutable request/result contracts. The server
owns final generation, normalization, validation and candidate hashes. Browser
preview renders that result. Prepared pack resolution stays in Content; service
orchestration and image decoding stay outside the simulation rules. New types use
ordinary project names without `Exav` prefixes.

1. **Normalize and validate the request.** Expand presets, reject unsupported sizes,
   invalid water/headroom, missing recipes, contradictory topology and work budgets.
   Define exact numeric ranges/defaults in P3-01/02. Do not silently clamp geography
   settings or copy the old byte-overflow behavior.
2. **Derive generation randomness.** Version a generator profile such as
   `terrain-generation-1`, root seed and independent terrain/water/vegetation stage
   seeds. Freeze seed encoding, sampling, iteration and numeric rounding. Keep
   candidate generation separate from the ten durable P1 gameplay streams; preview,
   cancellation and changing forest settings must not alter terrain or future
   development draws. Do not change the existing bank version merely to add preview.
3. **Produce a height field.** Use deterministic layered noise with low relief for
   plains, broader variation for hills and ridge-shaped noise for mountains. Feature
   scale controls wavelength; relief controls amplitude. Pin arithmetic or quantize
   explicitly so runtime/library updates cannot silently change a saved recipe.
4. **Shape land and sea.** Apply island falloff or signed-distance masks for selected
   coast sectors, roughened by bounded noise. Select an elevation offset for target
   water coverage, keeping the requested sea plane fixed. Enforce topology after
   quantization; a picture resembling an island is insufficient.
5. **Carve water features.** Choose enclosed lake basins and seeded meandering river
   paths; account for channel width, land rim and outlet connectivity. In v1 their
   wet beds lie below the shared water plane. Routed channels may cut terrain and
   split land, so connectivity/buildability checks follow this stage. Elevated
   lakes, downhill water surfaces, waterfalls, erosion and fluid dynamics are
   outside this checklist per the owner's simple-water direction.
6. **Normalize terrain.** Convert to legal whole-ground and quarter-corner geometry,
   using the reviewed loader constraints. Recheck lake rims, river connectivity,
   coastline sides and landmass after smoothing; it may remove narrow barriers.
   Use bounded deterministic repair/retry with a fixed attempt count and tie-breaks.
   Failure reports the unsatisfied constraints and retains the current world.
7. **Reserve buildable space and apply vegetation.** Find or gently shape a connected
   starting area subject to coast/water constraints, then revalidate terrain/water.
   Cluster forest masks with separate noise; filter by water, slope and optional
   altitude/moisture suitability. Such ecological weights are new heuristics, not
   ExAV rules. Use source forest scatter and actual contribution bindings for each
   accepted cell. Extra foliage is a separate verified catalog capability.
8. **Validate and describe the candidate.** Check all heights/corners/occupancy,
   required content and feature connectivity; calculate actual metrics and a hash
   over canonical world state plus normalized generation inputs. Return warnings
   for soft target deviations and errors for failed required topology. Commit only
   the exact reviewed candidate, or a byte-identical regeneration with the same
   algorithm/content identity. Expired/stale candidates require a refreshed preview.

All work has size/pixel/voxel/recipe/iteration limits and cancellation. Long jobs
must not block the session FIFO or ordinary game ticks. Replacement still occurs
through an Owner-authorized FIFO operation with epoch fencing. Other clients see
the authoritative replacement and invalidate old picks/drafts. Preserve the current
Owner/Operator/Observer distinctions for subsequent terrain editing; no generator
request grants map-edit permissions.

Persist **actual terrain, water, vegetation instances, ownership, mode and required
pack identities**, plus generator version/seed/normalized parameters/input image
digest as provenance. Loading restores state; it does not regenerate the world.
Imported worlds need no local file path or original image to reopen. Subsequent
terrain edits remain authoritative even if they differ from the original recipe.
Use the current-format-only save policy when the schema changes: select the next
canonical save/ruleset/content/protocol identities during implementation; do not
invent a number here or revive migrations for retired development formats.

## Granular execution slices

Each slice ships its needed English/Japanese UI/help, source/adaptation decisions,
save/read-model changes and focused verification. The initial source review above
feeds the completed [P3-01 semantic contract](web-parity-terrain-contract.md),
which supplies legal transitions, constants, defect dispositions and independent
fixture expectations. Later slices must bind and execute those expectations in
the production runtime before claiming gameplay verification.

| Slice | Work and dependency | Required evidence before checking it complete |
| --- | --- | --- |
| **P3-01 — source contract and decisions** | **Source specification complete:** [contract and evidence](web-parity-terrain-contract.md) resolve dimensions, terrain/water/corners, forest variants, Mountain2/terrace rules, defects, test-train side effects, authoring mode and compass sectors; MAN/DEV/EXAV-13/text links retained. | 40 pinned source files, 12 terrain decisions plus WORLDGEN-D001, 77 literal fixtures and 59 source text records. Read-only checks pass; production/gameplay/browser acceptance remains open. |
| **P3-02 — request/profile and budgets** | Define empty/import/generated DTOs, presets, bounds, generator identity, stage seed derivation, operation/job lifecycle and content requirements. Measure candidate size/memory before selecting exposed size limits. Depends on 01. | Validation matrix for overflow, contradictory settings, tiny maps, high water, missing packs, work limits and deterministic seed/stage vectors. Document initial supported dimensions separately from future capacity targets. |
| **P3-03 — canonical terrain state** | Add ground grid, quarter corners, global water and vegetation/terrace occupancy to canonical world/read models. Generalize the reference-loop-only invariant so a valid empty world can have zero rails. Integrate stable IDs and prepared recipes with exact profile dispatch. Depends on 01/02. | Independent flat/slope/underwater/border fixtures; invalid corners/overlap rejected without partial mutation; Simulation remains free of native/UI/image dependencies. |
| **P3-04 — saves and transport foundation** | Serialize actual terrain, decorations, mode and generation provenance; extend snapshot/delta/removal and current-format schema/identity checks. Bound transfer/checkpoint work. Depends on 03; extend incrementally in later slices. | Save/load and restart equality, edited-terrain round trips, missing-pack/unsupported-version rejection preserving files/current world, multi-client resync and epoch fencing. |
| **P3-05 — terrain rendering and picking** | Draw ground, mountain slopes, water/cuts/occlusion and forest ground/tree layers with P2 recipes. Ground/corner/water targeting shares source geometry. Depends on 03/04. | Reviewed asymmetric scenes and exact hit targets at land/water/cut/edge boundaries, seasonal/day-night trees, camera/panel lifecycle and decode reuse. Invalid source crops retain P2 diagnostics/deferrals. |
| **P3-06 — empty-world vertical slice** | Add New world panel, dimensions/name, dry flat candidate, source-default option, validation and Owner replacement through existing confirmation. Depends on 04/05. | Browser create/Cancel/Confirm/save/reopen, zero rails, multiple clients/roles, stale previews and missing content, EN/JA narrow layouts. First playable P3 milestone. |
| **P3-07 — heightmap vertical slice** | Decode bounded image input outside Simulation; port brightness/bilinear/staggered sampling, displacement, lowering interpolation, quarter smoothing and texture generation. Add image/dimensions/water preview. Support reviewed `terrainOnlyNewGame` definitions through pack resources. Depends on 06. | Independent black/white/ramp/checker/colored-lightness fixtures, 1-pixel images, borders and steep gradients; unsupported/oversized input, cancellation and save/reopen without source image. No early world publication as in the desktop loader. |
| **P3-08 — seeded relief** | Implement versioned terrain stage with plains/hills/mountains, relief/scale/smoothness and seed controls; add generated preview. Depends on 02/07. | Fixed expected samples/hashes, same-input replay, distinct-seed variation, quantized legal slopes, preset-specific measured relief and no live/gameplay RNG consumption. |
| **P3-09 — islands and directional coasts** | Add inland/island/selected-side masks, sea level and approximate coverage. Specify connected land/sea predicates and hard/soft constraints. Depends on 08. | Flood-fill/connectivity oracles after normalization; island rim, four asymmetric single-coast cases and multi-side cases; reject impossible coverage/size combinations. |
| **P3-10 — lakes and rivers** | Add sea-level enclosed basins and continuous-width channels/outlets; deterministic routing, bounded repairs and conflict diagnostics. Keep decorative creek mode separate. Depends on 09. | Enclosed lake rim, connected river bed/outlet/banks, no illegal uphill water surface; tiny maps/high relief/river-lake-coast combinations; source creek vectors and skipped-cell behavior independently covered. |
| **P3-11 — forests and cover** | Bind six bundled species/variants, source binomial scatter, seasonal trees and ground cover; add coverage/clustering/palette filters and starting-area reservation. Other foliage only through reviewed compatible contributions. Depends on 05/10. | Generated vegetation uses dry eligible cells; manual source underwater eligibility stays distinct. Check count/offset vectors, save-preserved appearances, expected coverage, forest-setting changes leaving terrain/water identical, base-only and optional identity separation. |
| **P3-12 — preview and generation UX completion** | Complete compass, actual coverage/buildable metrics, seed/preset controls, draft invalidation, progress/cancel/retry and exact-candidate replacement. Reuse functionality introduced in 06–11. | Rapid control changes, stale/out-of-order results, epoch/content changes during jobs, reconnect, cancellation, Owner-only commit and EN/JA keyboard/focus/narrow-panel behavior. Preview equals committed terrain. |
| **P3-13 — corner editing** | Port raise/lower, corner targeting and source Ctrl/Shift behavior with explicit accessible alternatives. Add authoritative read-only legality preview and atomic commit, operation-specific costs/ownership where source requires them. Depends on 06 and terrain rules from 01. | Independent corner transitions at bounds/water/occupied voxels, preview has no effects, commit revalidates revision/role/occupancy and publishes one consistent delta; save continuation. |
| **P3-14 — rectangle editing and repair** | Port Mountain2 rectangle raise/excavate and explicit repair; review modifier semantics and transaction failures. Depends on 13. | Shared-corner repair vectors, reversed selection, clipped/border/occupied regions, no partial unauthorized edits and multi-client conflict cases. |
| **P3-15 — terraces and cliffs** | Port separate placement/removal strategies, geometry/recipes, ownership and source booking. Depends on 14. | Placement/removal/cut/picking matrices, water/ground/elevation interactions, occupancy protection and saved-state restoration. |
| **P3-16 — map authoring mode** | Expose Owner-controlled map-authoring state and source/new-world workflows; specify entry/exit and persistence explicitly. Reuse terrain/forest/creek tools; preserve shared-world roles, draft and timing rules. Depends on 12–15. | Mode/permissions matrix, save/reopen/restart, multiple sessions, mode changes with drafts and pending work. No legality bypass or invented economic exemption inferred from the old unused flag. |
| **P3-17 — integrated acceptance and P4 handoff** | Exercise empty/import/generated/edit/reopen workflows and the topology matrix; measure terrain/render/job/transfer/checkpoint capacity. Reconcile ledgers, translations, help and supported limits. Depends on 16 and all earlier slices. | Varied worlds and seeded fixtures with actual hashes/metrics; Chromium and existing regression gates, no auto-restart of deferred browsers/native checks. Give P4 ground/underwater/elevated/support queries and legal test scenes; do not claim P4 construction is already verified. |

Execution order is 01 → 02 → 03 → 04 → 05 → 06 → 07 → 08 → 09 → 10 → 11 →
12, with editing 13 → 14 → 15 after 06, then 16 → 17. Implement persistence and
UI with each consumer; row 04/12 is not permission to defer them until phase end.

## Acceptance matrix and completion claims

Derive the source cases before implementation and retain exact expectations apart
from generator output. Add new generator cases under `WORLDGEN-*` identities so
they cannot inflate ExAV parity totals. Extend existing scenario reviews with
terrain-specific source scenarios rather than assigning a new `EXAV-SCN` number
without reconciling their authoritative JSON.

Required generated scenes: dry plains, high-relief mountains, island, each of four
single-side coasts, two adjacent and two opposite coast sides, inland lake,
coast/river/lake combination, sparse/dense/mixed forests and no vegetation. Cross
representative small/medium sizes, water extremes and fixed seeds; include bounded
failure cases for contradictory settings and insufficient space. Verify topology
after the final normalization and starting-area stages. Golden hashes establish
reproducibility; independent geometry, connectivity and metric assertions establish
that a reproducible map meets its request.

The P3 parity gate remains browser creation/edit/save/reopen of varied worlds with
source terrain/placement/water/underground/elevated constraints exercised. The
requested procedural addition also requires its controls and geography constraints
to pass. Report the two scopes separately, and keep original-runtime comparison,
full-world gameplay, native/device observations and deferred browser certification
at their actual evidence status. This planning change runs no application tests
and closes no implementation or acceptance gate.
