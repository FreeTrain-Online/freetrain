**P3-11 forest consumer extension — 29 October 2026:** [Dense six-species and combined coastal-water forests](web-parity-forests.md#current-consumer-capacity-evidence) verify two1500×1090×45 exact generation/publication/reopen/checkpoint workflows. Repeated immutable terrain-read values are shared after an observed6GiB budget failure; both consecutive cases now pass unchanged budgets. Final P3 capacity acceptance remains before P3-17.

**P3-09 consumer extension — 29 October 2026:** [seven full-size coast fixtures, independent wet-edge topology and exact browser/Linux workflows](web-parity-coasts.md) extend the current capacity envelope to island, four directional, adjacent and opposite coasts. Final acceptance remains before P3-17; later consumers must extend this evidence.

# P3-16a — map-size capacity parity

29 October 2026. Implementation and terrain-capacity acceptance are complete; final verification is recorded below. The owner advanced this work before P3-09 so landscape heuristics can be tested on representative extents. Overall P3, later full-city gameplay/import acceptance, and recorded native/browser/P2 deferrals remain open.

The minimum concrete target is **1500×1090×45 stored**, corresponding to the supplied historical sample's 1500×1000×45 displayed extent. The source evidence and its limitations remain in [the capacity specification](web-parity-world-generation.md#p3-16a--map-size-capacity-parity). The historical save is an independent extent reference; these checks use canonical scenes and do not execute or import it. This implements a bounded portable capacity policy, not an inferred maximum of the desktop engine.

## Representation and persistence

**Save 7 is the only supported save format; protocol 5 is required.** Save 6 fixtures/schema are retained under `prototype/protocol/historical/save-v6` as rejection evidence. Saves 1–6 are rejected without conversion under the existing owner policy. Rejected uploads/checkpoints preserve files and the current world. Terrain rules/content/pack identities and generation seeds/algorithms are unchanged.

`PackedTerrain` encodes actual current state, rather than regenerating from provenance. Definitions are interned in stable occupant traversal order. Cells are row-major H/V bytes: bits 0–5 hold ground height; bits 6–7 select Natural=0, Cut=1, Terrace=2; value 3 is invalid. Occupant IDs, revisions, ownership, transparency, reclaimability, value, corners, Creek masks, empty-versus-absent forests, and ordered realized trees survive exactly. No H×V×Z volume is allocated.

Each occupant has a 43-byte little-endian header, followed by three bytes per realized tree:

| Byte offset | Payload |
| --- | --- |
| 0 / 8 | Signed Int64 entity ID / revision |
| 16 | UInt16 entity-definition index |
| 18 / 22 / 26 | Int32 X / Int32 Y / byte Z |
| 27 | Owned bit0, silently reclaimable bit1, transparent bit2; other bits rejected |
| 28 | Signed Int64 value |
| 36 | Mountain=0, Terrace=1, Creek=2 |
| 37 | UInt16 quarter nibbles: NE12, SE8, SW4, NW0 |
| 39 | Creek connection mask |
| 40 | UInt16 forest-definition index; 65535 means absent |
| 42 | Tree count, at most six in the current exact profile |
| 43 onward | Tree X, Y, sprite as bytes, in saved order |

The encoding identity is `terrain-packed-1`. Counts, lengths, definition identities, reserved values, ID ordering, coordinates, tree bounds, footprints, source surface/continuity rules, allocator and provenance are checked before publication. Binary expansion has a separate 2 GiB conservative estimate (`24×cells + 1024×occupants + 2×occupantBytes`). Oversized packed graphs fail before expanded graph allocation. The same estimate is enforced on realized domain state (`24×cells + 1110×occupants + 6×trees`) before building the occupancy index, so accepted edited worlds remain reopenable. Save envelopes also bound reference arrays, clock actions, random streams/values and JSON containers before DTO expansion; provenance JSON is limited to 65,536 characters. The literal Python `struct.pack` study in `PackedTerrainTests` is independent of the production encoder. Int64 boundary tests preserve IDs above 2^53 and maximum entity revisions.

Runtime cells/footprints/definitions are shared immutable values; occupancy uses a frozen sparse index. Corner validation avoids per-corner heap allocation. Immutable wire geometry is reused across clock updates. Server differences and client scalar deltas preserve that geometry without traversing, copying, freezing or revalidating the whole grid each tick. Exact edited state, clock, random streams and allocator remain authoritative.

## Transfer and session boundary

A normal browser bootstrap requests a `sessionOffer` containing identity/role/epoch metadata. Worlds above 4096 cells always return this bounded offer. Large joins/snapshots transmit a packed `StateTransferPayload` in chunks of at most **48 KiB payload** (JSON frames below 66 KiB). Each chunk binds transfer ID, epoch, revision, index/count, total bytes and SHA-256. One assembly per socket validates lengths, complete coverage, integrity, supported identity and complete terrain before atomic reveal. It accepts out-of-order chunks; duplicates, conflicting metadata, stale/corrupt data and malformed structure fail closed. Missing chunks expire after 30 seconds. A pre-parse scan bounds payload JSON containers/depth; base64 bytes and expanded graph estimates are separately bounded.

Two server snapshot writers, bounded peer queues, one client assembly and a bounded client callback queue limit transient transfer work. Changes coalesce while a snapshot writer is active; publication then resumes from the last queued revision. Slow/full queues disconnect and re-bootstrap rather than dropping dependent deltas. A later epoch/revision cannot be replaced by a stale assembled snapshot. Existing role enforcement, uncertain-outcome/no-replay behavior, content readiness and canvas lifecycle remain in place. Restart invalidates incarnation-local credentials; tests explicitly sign in again.

Candidate generation, canonical encoding, restore validation, wire preparation and size measurement run on detached workers. The FIFO rechecks roles/connection/epoch/revision and exact candidate digests before retention/commit. Confirmation publishes the already validated retained state. Saving freezes the small clock/random header at the FIFO boundary and encodes the immutable geometry off that queue. HTTP save validation/load preparation is similarly detached, with an atomic rechecked replacement. Two save-input slots and two save workers bound concurrent uploads/validation/encoding. Ordinary input commands/login/creation requests keep their **16 KiB** limit, including chunked login bodies.

## Admission and resource policy

| Bound | Current value |
| --- | --- |
| H / V | 1–2048; generated minimum16 |
| Stored cells | At most1,700,000 |
| Z / addressable voxels | 1–64; generated minimum8; at most108,800,000 |
| Generated water | 1…Z−4; existing empty/heightmap rules retained |
| Save / upload / assembled state | 256 MiB; ordinary commands remain16 KiB |
| Creation work | At most35,000,000,000 debited units; existing sequential attempt bound retained |
| Worker deadline / candidate lifetime | 30 /120 seconds |
| Creation working memory | 4 GiB global estimated reservation, at most four jobs, one per session |
| Input reservation | 96 MiB before image input/decoding; upgraded atomically to the request estimate |
| Natural terrain working estimate | 2,228,224 +2048×cells; heightmaps additionally account for image phases/input |
| Empty working estimate | 2,228,224 +256×cells |
| Packed transfer reservation | 65,536 +96×cells for natural terrain; +4×cells for empty terrain |
| Image input | Unchanged8 MiB /4096 per side /4,194,304 pixels; bounded80 MiB decoder arena |

Host cancellation/invalidation releases only host ownership of a reservation. A still-running worker retains its reservation until its lease actually finishes. A separate test reserves a full-size generated job without allocating its terrain, cancels host retention, verifies another full-size job remains denied, then disposes the worker lease and verifies admission recovers. Four small retained candidates and the existing stale/cancel/disconnect fences remain covered.

These are coordinated request envelopes, with actual bytes, expansion estimates, work meters and deadlines still authoritative. Not every dimension/control/density combination is a separately measured performance profile. Extremely dense state may be rejected even when its dimensions fit. The minimum parity extent is tested with empty, imported and generated terrain and with resource-heavy canonical scenes; P4–P11 must extend those measurements as gameplay and content consumers arrive.

## Browser rendering and picking

Large scenes build source-ordered operations only for the viewport, including the complete elevation range and sprite overhang. A bounded65,536-cell operation cache avoids rebuilding unchanged instances. Picking checks operation bounds before exact polygons/alpha masks. Only needed prepared surfaces are decoded; variants/cuts/epoch/geometry changes invalidate scenes through the existing lifecycle.

Large live scenes use two viewport rasters, each capped at16,777,216 pixels: the completed image and a replacement under construction. Camera movement immediately transforms the completed image. Replacement work builds source-ordered batches of at most128 cells and paints batches of64 operations, yielding after an approximately5 ms CPU budget per animation frame. Only a complete replacement and its matching picking operations are published. New camera requests replace unfinished work; returning to the completed camera cancels it. Geometry/epoch/variant/cut changes discard the viewport through the existing scene lifecycle. Hidden or unmounted views schedule no independent refinement work.

Rendering uses a bounded set of physical-pixel scales, preserving reusable sprite/polygon phases at fractional device-pixel ratios and on large windows with capped backing resolution. The displayed camera retains continuous50–400% zoom and cursor anchoring. The completed raster is sampled with nearest-neighbor scaling at intermediate camera zooms; those pixels can differ from the earlier arbitrary-scale, per-sprite paint. This is a display sampling choice; terrain, realized trees, entity IDs, saves and command geometry stay exact. Picking uses the published operations with the displayed camera and ignores newly exposed background outside the completed image. The measured1.5× DPR1/2 cases compare byte-for-byte with the synchronous reference and a fresh redraw after resize. Separate64 MiB sprite-crop and polygon raster caches preserve stable sampling within each render scale. Small scenes, galleries and the decorative landscape keep their synchronous path. These are portable browser rendering decisions, not new claims of native GDI pixel identity.

Candidate overviews are bounded256×256 PNG images rather than one SVG element per cell. The exact full candidate grid remains authoritative for confirmation. The mounted primary canvas remains stable.

### Zoom responsiveness follow-up — 29 October 2026

The initial capacity acceptance measured warm panning and missed arbitrary-scale zoom stalls. A local renderer diagnostic reproduced12.8-second frames in both zoom directions on the full-size forest/terrace fixture, with no resource requests during zoom. Two wheel sweeps created642,525 offscreen canvases; context creation and garbage collection were major sampled CPU costs. Fixed scales alone reduced the maximum to354 ms, which still required yielding work to protect interaction.

Five final real-UI checks cover50% and400%, rapid reversal, cancellation back to Home, fractional-zoom picking and byte-exact cached/fresh images at150%. They record no tasks over50 ms during zoom/refinement after the world is ready. Wheel-to-canvas feedback and the last-view catch-up after the rapid gesture are measured separately:

| Scene | DPR | Wheel feedback p95 / maximum (ms) | Last view settled after input (ms) |
| --- | ---: | ---: | ---: |
| Forest/terrace |1 |17.1 /17.6 |107.2 |
| Forest/terrace |1.25 |17.4 /17.7 |98.2 |
| Forest/terrace |2 |18.8 /21.3 |130.1 |
| Mountains |1 |16.8 /16.9 |57.7 |
| Mountains |2 |18.0 /21.0 |99.6 |

The isolated renderer additionally compares completed150% pixels against the synchronous reference. Cold wide forest views can take about0.9 seconds to finish refinement while the camera remains responsive; newly exposed edges show the scene background until completion. The owner reviewed the local full-size forest/terrace preview for rapid zoom, direction reversal and panning after zoom, and reported **“Responsive and visually smooth.”** This accepts the prototype’s feel in that tested setup; broader native/device coverage retains its existing gates. The original capacity results below retain their historical measurements; the opt-in capacity suite now also exercises zoom responsiveness, including fractional DPR.

Local follow-up evidence and reproduction: `tmp/zoom-analysis/README.md`, `summary.json`, per-case renderer/CPU captures, and `tmp/p3-capacity/browser-capacity/zoom-*.json`. Run the current capacity suite with `--grep 'responsive zoom refinement'` for the UI zoom checks, or `--grep-invert 'responsive zoom refinement'` for the original capacity cases.

Frontend follow-up verification: **129 client tests,84 ordinary Chromium cases,5 zoom cases and4 original capacity cases pass**. Build/generated/content/locale checks pass with zero Svelte errors/warnings. Unit coverage also checks atomic picking, cancellation back to the completed camera, fractional-DPI camera geometry and the backing-pixel bound. Owner smoothness review passed on the local forest/terrace preview.

## Measurements and acceptance

Named native environment: **MacBookPro18,2, Apple M1 Max,64 GiB RAM,10 physical CPUs**, macOS26.7.1, .NET SDK10.0.402/runtime10.0.13, Arm64. Native browser runs use the pinned Playwright1.64.1 Chromium and1880×1040 viewport; native Deno2.9.8 runs the toolchain, with pinned Deno2.9.7 independently checked in the Linux build. Browser memory uses a250 ms sampled aggregate process RSS (shared pages can be counted more than once) and separately records current JS heap and post-GC retained JS heap for the owner page. These samples are observed maxima, not a proof of an absolute process upper bound. Allocation churn is not reported as retained/peak memory. macOS `Process.PeakWorkingSet64` is unavailable; CLI peak RSS comes from `/usr/bin/time -l`.

Canonical fixtures include dense terrain (two occupants and six realized trees per cell), full-size resource-heavy terrain (terraces plus six-tree forests in one eighth of cells), worst-detail full-size mountains (amount100/scale4/smoothness0, seed Int32 minimum), empty terrain and PNG-derived terrain. The CLI uses a fresh process per measurement and verifies byte-identical save/reopen. The browser checks complete transfer, stable canvas, picking, exact save, observer roles, rejoin while shared time advances, checkpoint restart and cached-versus-fresh pixels. Command feedback is measured from an in-page button click to confirmed DOM feedback over ten steps, with 50 ms outbound and 50 ms inbound control delays. State-transfer throughput uses loopback transport; these figures are not a bandwidth-limited WAN profile. Large buffer comparisons use SHA-256 rather than expanding every byte into matcher object keys.

| Scene | Entities | Save bytes | Packed snapshot bytes | Chunks |
| --- | ---: | ---: | ---: | ---: |
| Dense 128×128×32 | 32,768 | 2,301,598 | 2,295,257 | 47 |
| Dense 256×256×64 | 131,072 | 9,182,881 | 9,176,539 | 187 |
| Heavy 1500×1090×45 | 408,750 | 30,527,841 | 30,521,499 | 621 |
| Mountains 1500×1090×45 | 1,488,288 | 87,516,611 | 87,510,268 | 1,781 |

Isolated CLI peaks were1,097,154,560 bytes for the full-size heavy scene and2,862,546,944 bytes for full-size mountains; build/save/restore totals were about3.0 /10.7 seconds. After explicit GC, retained original-world managed samples were about221 /648 MB; retaining a restored second world plus transport used about882 MB /1.83 GB. These managed figures describe the CLI's deliberate retained graph, separately from its process peak.

The final browser run enforced the budgets below. Upload is the HTTP replacement response; rejoin includes transfer, assembly, validation, assets and reveal. Restart includes process shutdown/start, explicit reauthentication and two clients reaching ready. Server/browser RSS are separate observed aggregate peaks; retained JS is the owner's page after explicit GC, not the combined browser RSS.

| Scene | Upload / rejoin / restart (s) | Paint p95 (ms) | Pick (ms) | Step feedback p95,100 ms injected RTT (ms) | Peak server / browser RSS (GiB) | Retained owner JS (MiB) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Dense 256² |0.523 /1.432 /2.833 |16.5 |7 |149.7 |0.64 /1.83 |98.5 |
| Heavy 1500×1090×45 |0.963 /3.660 /6.408 |9.1 |7 |150.6 |1.76 /2.47 |222.2 |
| Mountains 1500×1090×45 |3.108 /9.567 /16.567 |5.7 |33 |150.1 |4.62 /3.60 |370.6 |

Frame interval p95 was about 16.8 ms. Before viewport caching, dense256² pan paint p95 was 170.9 ms. The final three scenes pass byte-exact cached-versus-fresh PNG comparisons after resize. Full-size browser preview→confirmed-ready workflows took 2.470 s empty, 11.577 s PNG heightmap and 17.218 s generated mountains; downloaded canonical saves matched independently generated CLI bytes. The heightmap has 821,677 entities, a 49,297,351-byte save and 49,291,009-byte/1003-chunk state packet. Empty save size is 2,187,404 bytes.

Capacity acceptance budgets on this environment: 30-second generation/transfer bounds; initial/rejoin/restart within 60 seconds; warm pan paint p95 below 33.4 ms and local picking/input feedback below 100 ms (authoritative feedback below 250 ms); observed aggregate server/browser RSS below 6 GiB each. These are terrain-capacity measurements, not full-city simulation or a replacement for the P11 hardware/device matrix. The older UX3 rendering sample and native/device gates keep their recorded status.

## Reproduction and evidence

- `dotnet build tools/TerrainTransportProbe -c Release`; run its `--capacity H V Z empty|heightmap|generated|heavy|dense OUTPUT` mode. The generated save, packed packet, source PNG where applicable, and measurement JSON are local artifacts.
- `deno run -A node_modules/@playwright/test/cli.js test --config playwright.capacity.config.ts` from `web` runs the opt-in large-world suite. It owns disposable servers/save directories, records pan/heap/RSS samples and verifies explicit creation/confirmation at the minimum extent.
- Ordinary .NET/client/Python/Chromium suites, content/generated/locale/source/boundary checks and the pinned Linux container cover existing behavior and distribution.
- Local working evidence: `tmp/p3-capacity/`, with `browser-capacity/` for scale scenes and `browser-final/` for ordinary regressions. Large canonical save fixtures remain generated locally; they do not enter normal source/default assets.

## Verification record

Final checks: **512 .NET /127 client /142 Python cases** (one existing Python skip); **84/84 ordinary Chromium**, **4/4 capacity workflows**, repeated **3/3 capacity budget/retained-memory checks**, optional source **4/4** and prepared **4/4**. Eleven independent relief fixtures, generated contracts, pinned source/decoder/license, project boundaries, locale/content preparation and whitespace checks pass. The final pinned Linux build reports zero Svelte errors/warnings.

The final Linux Arm64 image is `sha256:a05fb6e34a5377e6b0080875b41ac5f4b9e578169586d404cc6b9f7b39bfc634` (`freetrain-online:p3-capacity`). A loopback-only disposable container with4 CPUs and6 GiB memory verifies the621-chunk heavy and1781-chunk mountain scenes, exact save/download/reopen, and generated checkpoint restart from a retained test volume. The generated save SHA-256 is `95de893ad978e36a60152bba243198f718468769f3f636c4d2b33d27339b5937`. Cgroup memory peak for generated upload/publication/reopen is4,395,167,744 bytes; startup restore/download peak is2,111,283,200 bytes. Distribution checks verify238 assets/505 unique media/4 notices, exact catalog hashes and canonical bootstrap; no Skia artifacts are present and the reviewed decoder's MIT notice is included. Test containers/volumes are removed afterward.

The advanced **P3-16a slice is complete for current terrain consumers**. P3-09–11 must add large topology/vegetation/starting-area fixtures without replacing small independent oracles; later editing consumers must extend scale evidence, and P3-17 reruns final capacity regression. Legacy save import and full-city/content-density acceptance remain P10/P11. Overall P3 and the recorded native/P2/browser deferrals remain open.
