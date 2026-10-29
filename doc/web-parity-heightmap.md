**P3-16a capacity update — 29 October 2026:** [Current packed state, coherent transfer, admissions and evidence](web-parity-map-capacity.md) supersede the temporary size/byte/working limits and Save 6/protocol 4 statements below. Earlier measurements remain historical slice evidence. Current envelopes are H/V≤2048, at most 1,700,000 stored cells, Z≤64 (generated minimum H/V16,Z8), Save 7/protocol 5 and 256 MiB assembled state. The concrete scale target is 1500×1090×45 stored. Overall P3 and the recorded P2/native/full-game gates remain open.

# P3-07 heightmap browser slice — 29 October 2026

**P3-07 expanded scope complete — 29 October 2026.** The SkiaSharp baseline
has been replaced by a bounded source build of **StbImageSharp 2.30.16**, pinned
at `125af70cb557033f2c46aec8e82eaaf72ac49817`, selecting **MIT**, plus a
project-owned PNG thumbnail writer. No Skia packages, native binaries or shipped
Skia notices remain in clean publish/container output. FreeTrain's license is
unchanged. [Replacement decision/provenance](web-heightmap-decoder-review.md) and
[the historical Skia audit](web-license-compatibility.md) record the separate findings.
P3-08 follows; overall P3, capacity, native/device and existing P2 gates remain open.

Game → New world now offers **Empty world** and **Heightmap image**. Owners can
upload an image, choose the world name, stored H/V/Z dimensions and global water
level, inspect the decoded image and resulting ground-height preview, then confirm
the exact retained candidate. Creation stays detached until confirmation. Cancel,
invalid images, stale revisions/epochs/connections and failed jobs preserve the
current world. Actual terrain, including quarter slopes, reopens without the image.
Procedural generation, editing, larger-map capacity and overall P3 remain open.

The source is ExAV 2.1.1.12, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/plugins/org.kohsuke.freetrain.tools.terrainloader/src/{TerrainLoader,LoadDialog,TerrainOnlyNewGameContribution}.cs`
and `trunk/core/world/World.cs`, pinned by the [terrain contract](web-parity-terrain-contract.md).
This supplies the heightmap/provider portions of MAN-M01/O02/E02/A02/A19,
DEV-P09/P17 and EXAV-13. This is portable implementation and Chromium evidence;
original Windows runtime/GDI identity and native/device acceptance are unverified.

## Input, decoder and admission

| Item | Shipped behavior |
| --- | --- |
| Browser dimensions | Stored H/V1–32, Z1–64, water0…Z−1, also subject to structural/work validation. Initial heightmap form expands H32,V32,Z16,water4. Empty retains its separate H/V1–64 admission. No hidden V padding or image-derived resizing. |
| Encoded image | PNG, JPEG, BMP or GIF; 1…8,388,608 bytes. Signature must agree with a supplied concrete MIME; an omitted/opaque binary MIME is detected from bytes. No path or remote URL is accepted. |
| Decoded raster | Each side1…4096, at most4,194,304 pixels, a single opaque frame. Actual alpha is checked after decoding; transparent pixels fail without compositing. Animated GIF and APNG fail before expanded decoding. |
| Decode policy | Stored raster orientation, original dimensions, RGBA8 output. No EXIF rotation, ICC/display transform, browser scaling or luminance conversion enters generation. Unsupported/truncated encodings fail. |
| Preview | Service-generated, bounded PNG thumbnail from decoded pixels; schematic row-major ground-height/water SVG from actual candidate cells. Full quarter slopes render on the existing live canvas after confirmation; expanded preview modes remain P3-12. |
| Authoritative state | Current Save6/protocol4 and bundled terrain profile. No input image bytes, source filename, filesystem path or decode recipe is needed on load. Encoded SHA-256, actual dimensions/MIME/length and provider identity remain request provenance. |

`Server/HeightmapDecoder.cs` uses the reviewed pinned source in
`Server/Imaging/StbImageSharp`; decoding remains outside Simulation. The release
package was evaluated but its unbounded allocator/wrapper was not adopted.
The generated stb port uses project-owned runtime helpers; no Hebron.Runtime,
GPL generator, upstream test code or native image library is incorporated.
The [MIT notice](notices/StbImageSharp/LICENSE.txt) is copied into publish and Docker.
[The offline source/distribution checker](../scripts/check-heightmap-decoder.py)
verifies local source hashes, the selected pin, lock files and clean artifacts.

PNG chunk lengths/CRCs/IEND/APNG, and GIF descriptors/subblocks/frame extents,
are checked before decode. Header dimensions precede expanded allocation.
The decoder owns an **80MiB maximum arena**, including both blocks of moving
reallocations, with at most256 outstanding pointers to bound tracking/allocator metadata. PNG inflation uses the exact expected scanline length, including
Adam7, rather than a growing decompression buffer. Stream reads target spans
without another expanded array. Every decoder method and braced loop participates
in work/cancellation probes (256 million probes, 20-second inner deadline, token
checks at most 1024 probes apart). Other bounded pixel/table loops also reach
these checks; GIF prefix expansion is iterative with at most8192 entries.
All pointers are tracked and freed on success, failure, cancellation and deadline.
The arena is trimmed to the single validated output before allocating managed RGBA.
Fixed-size palette/Huffman metadata is separate from the arena; no input-sized
managed decoder scratch remains. RGBA8 output preserves stored orientation and
ignores EXIF/ICC/display transforms.

The project-owned preview encoder samples the same stored pixels to at most192×192,
writes filter0 RGB8/noninterlaced PNG using ZLibStream, and owns chunk lengths/CRCs.
It adds no general image/encoding dependency. Literal row/chunk/CRC tests and
Chromium's independent image decoder verify its pixels and format.

The service derives the request's image metadata and hash itself. Client-declared
width, frame count, alpha and digest never authorize allocation. The raw-body POST
`/api/worlds/heightmap/preview` uses bounded query options and authenticated Owner
identity, checks dimensions/name/water, and rejects both declared and streamed
encoded overflow. It captures the connected participant before reading the body.
Uploads, decoding and retained candidates share the existing **four global96MiB
reservations**; at most four workers run. Reservations are acquired before reading
input and transferred to the session job, then released on cancel/failure/expiry,
replacement or disposal. Slow uploads consume a reservation and share the30-second
request/worker deadline. Abort cancels input/work, stale connection/host checks fence
publication, and raced FIFO starts are observed/cancelled by their own job ID.
Decoder probes make cancellation cooperative inside decoding; arena cleanup completes
before its worker reservation is released. No background decoder survives a returned
worker task. Reservations remain in force through retained candidate lifetime.

## Source terrain pipeline

`Simulation/HeightmapTerrain.cs` ports the source order:

1. Float32 HSL lightness `(min(R,G,B)+max(R,G,B))/510f`, with source staggered
   bilinear sampling and clamped floor/ceil indices. Displacement truncates
   `min(Z−1,Z×depth)`. Pure red, green and blue each produce0.5.
2. Traverse source `v=-1…V−1`, `h=-1…H−1` XY quartets, repeatedly lowering members
   above the quartet's minimum+1. Never raise valleys. Bound convergence to64×Z
   scans; all stages debit one shared work meter.
3. Resample qualifying flat2×2 corners with a higher4×4 surround, retaining the
   source quarter offset calculation and border fallback.
4. Convert shared fine heights to NE/SE/SW/NW offsets and stable row-major Mountain
   entities. Validate the entire natural surface and occupancy graph before use.

Raster alpha probes, sampled pixel work, neighborhood probes and texture visits
consume the shared `GenerationWork` allowance with cancellation checks. The world
has zero rails, no forest/decorations/test trains, paused initial time and the
normal independent gameplay random bank. No preview borrows live random streams
or publishes the desktop loader's early `setWorld` side effect.

For an independent2×2 horizontal black/white ramp imported as H4,V4,Z8, the
normalized whole-ground rows are:

```text
0 1 2 3
1 2 3 4
0 1 2 3
1 2 3 4
```

This oracle uses min-plus shortest paths over shared-corner neighborhood cliques,
independent of the production lowering traversal. Separate fixtures check literal
odd-row depths0.25/0.75, border clamping, primary-color lightness, one-pixel maps,
checkerboards, literal `(0,1,0,0)` border-quarter texturing and naturally matched corners.

Completion and confirmation reuse the session FIFO, retained Save6 digest,
Owner/epoch/revision/connection fences, byte checks and checkpoint/publication
boundary from [P3-06](web-parity-empty-world.md). Heightmap completion permits
Mountain occupants and non-flat cells; empty completion retains its flat/zero-entity
checks. Confirmation consumes exactly the retained bytes once, using the existing
bounded commit/cancel endpoints. No generation runs again on confirmation.

## Reviewed fixed maps

The bundled catalog declares the `terrainOnlyNewGame` factory but supplies **no
fixed-map instances**. No map is invented or bundled to fill that empty list.
The optional preparation adapter now handles that exact factory/implementation,
retaining image bytes verbatim and name/author/trimmed description/stored H/V/Z/
water metadata in `fixedHeightmaps`. It does not apply sprite color keys or create
an executable legacy plugin. Resources use the existing safe resolver, immutable
hash-bound pack artifacts and notices; no non-free content enters this repository.

`Content/FixedHeightmaps.cs` validates the qualified handler and owning pack,
version/catalog hash/resource URL. Startup checks metadata. The browser lists
installed definitions, displays their author/description and locks supplied size/
water values. Oversized fixed dimensions remain outside the temporary32² browser
admission. Selection is an exact contribution ID; the service resolves only the
installed prepared binding and checks actual encoded hash/length and supplied
size/water before generation. Mounted image reads are length-bounded and rehashed.

The resulting geometry references only bundled terrain definitions. A fixed-map
pack is **data-only creation provenance**, not an executable dependency of the
realized world. Its exact binding remains in the saved request; reopening does
not resolve or decode its image. Separate source/prepared synthetic-pack browser
cases verify preparation, metadata, exact resource binding, confirmation and
reopening after source-directory removal. Canonical non-free-suite repairs and
historical full-suite acceptance retain their existing separate owners.

## Resource measurements

P3-02's provisional dense estimate was insufficient for actual entities.
Heightmaps reserve `65,536 + 1,536×C` transfer bytes and
`2,228,224 + max(80MiB, 16,384×C + 8×P) + encodedBytes` working bytes,
where C is stored cells and P is decoded pixels. Decoding and terrain/serialization
are sequential: the full arena is released before generation, and only its final
RGBA output overlaps the managed raster copy. The fixed allowance covers metadata,
first-use and headers. The maximum admitted image at32² reserves94,502,912 bytes,
below the hard96MiB job reservation. Maximal-image64² still exceeds that envelope;
the browser remains H/V1–32 independently of the profile formula. Other source
admissions are unchanged. Actual saves/snapshots must also satisfy16MiB and request
transfer limits. At most four global jobs/retained candidates hold reservations.

Release samples: macOS26.7 / Arm64, .NET SDK10.0.402/runtime10.0.13.
Managed numbers are thread-local allocation churn, **not resident peak**. The arena
reports actual high-water unmanaged bytes, including realloc overlaps, and returns
to zero after every success/failure/cancel test. PNG fixture construction precedes
the counters. Maximum-image rows include decode, terrain, save, restore, snapshot,
reserialization and thumbnail; small rows include session preview/commit/reopen.

| Extent / input | Mountain occupants | Save bytes | Snapshot bytes | Managed allocation bytes | Decoder native high-water bytes | Elapsed ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1×1×1 /64² BMP | 0 | 7,694 | 1,345 | 1,246,504 | not sampled separately | 8.35 |
| 16×16×16 /64² BMP | 0 | 16,115 | 9,766 | 5,561,536 | not sampled separately | 42.82 |
| 32×32×64 /64² checker BMP | 512 | 250,031 | 243,680 | 33,306,856 | not sampled separately | 106.03 |
| 32×32×64 /2048² RGBA8 PNG, exactly8MiB | 512 | 250,042 | 243,691 | 32,113,592 | 33,572,864 | 352.44 |
| 32×32×64 /2048² RGBA16 PNG, exactly8MiB | 512 | 250,042 | 243,691 | 33,067,976 | 67,143,680 | 921.90 |

Ancillary padding reaches the encoded boundary without increasing decoded pixels.
Four concurrent maximum-pixel RGBA8 decoders each measured33,572,864 native bytes;
all returned to zero. The arena limit also counts failed moving reallocations;
malformed small-header/large-deflate data cannot allocate beyond expected rows.
These samples do not claim latency percentiles, isolated process peak RSS or
full-city capacity. macOS PeakWorkingSet64 returned0 and is not used as memory evidence.

The final Linux/Arm64 image `d085e773d3d237d00436f4e34eab2ff045bb9e3f8b8e7f0b4d088ce247984a11`
passes import/preview/confirmation/reopen **and checkpoint restart** with exactly8MiB,
2048², RGBA16 input at32×32×64. Save bytes are250,022 with1024 cells, zero rails
and byte-identical recovery. Only the checkpoint and writer lock persist under/data;
no source image is mounted or stored. A clean container artifact scan finds no
Skia filenames/dependency entries and verifies the exact replacement notice.
The early startup cgroup read was18,030,592 current/18,030,592 peak bytes;
after the workflow it was173,772,800 current/203,354,112 peak. Those are whole-container
samples including startup, runtime, content and GC, not per-job RSS or comparable
steady baselines. The temporary loopback container is removed after validation.

## Decoder compatibility review

An isolated temporary comparison with the removed Skia decoder found identical
RGBA and terrain for the orientation JPEG and independent PNG RGBA8/16, Adam7,
palette and packed-gray fixtures. The source lightness/bilinear/staggered/lowering/
quarter oracles are unchanged and pass.
A synthetic16² color JPEG produced110 differing channels (maximum absolute delta2).
At H32,V32,Z64, whole-cell ground stayed identical but five Mountain quarter
patterns at matched cell locations changed. This is a reviewed **lossy JPEG decoder adaptation**, not a change
to terrain sampling or a relaxed oracle. The comparison is limited to these fixtures;
it does not establish pixel identity for arbitrary JPEGs. New imports use the pinned
replacement consistently; existing Save6 worlds retain realized geometry and do not
decode again. No native Windows/GDI comparison is claimed.

## Text, validation and handoff

`heightmapSource` and `heightmapHelp` adapt TERRAIN-TEXT-035/036 (地形ローダー /
画像から地形データのみをロードします), with the pinned Japanese hashes and historical
English donor in [the frozen text review](parity/terrain-localization-review.json).
`heightmapImage` adapts the LoadDialog file field into a browser upload control.
Shared name/dimensions/water/preview/confirmation labels retain the P3-06 review.
`worldSource`, `emptySource`, `fixedHeightmap`, `uploadHeightmap`, image/result
preview descriptions and six distinct image failure messages are new browser
wording under WORLDGEN-D002, reviewed in EN/JA locale resources. Installed provider
metadata remains author-supplied source text. Help explains limits, lowering/slope
smoothing and the single water plane. No source translation inventory is silently
promoted to shipped coverage for unrelated tools.

Verification covers the working tree based on
`257212c4663af4771dadf99d88464c735f99b201`, not a new committed revision.
Release builds, Svelte/TypeScript/generated/content/locale checks pass without
warnings/errors. All **474 .NET tests** pass, including four real retained dense candidates: each
retains250,031 save bytes (243,680-byte snapshot); a fifth is rejected until
cancellation releases a slot, and all current worlds remain unchanged. Client tests pass120; Python runs142 cases (141 pass, one existing
skip). Project boundaries, pinned/offline terrain review and whitespace pass.
The full Chromium suite passes81/81 (27 canonical,54 historical); a final decoder
build rerun passes all9 terrain cases. Optional source and prepared fixture modes
each pass4/4. This includes exact preview/commit/cancel, two-client publication,
Owner/Observer/stale/connection fences, Japanese narrow scrolling, fixed resource
bindings, save/reopen and checkpoint recovery without source images.
The Linux image/build/artifact/import/restart checks above pass.

Screenshots under local `tmp/browser-evidence-JUQNze/heightmap/` were visually
inspected: decoded/result previews, English live terrain and Japanese390px controls
remain readable with internal scrolling and no document overflow. Chromium image
pixel checks independently decode the project-owned PNG thumbnail.

Reproduction uses the [existing regression commands](web-parity-empty-world.md#verification-and-remaining-gates),
plus `python3 scripts/check-heightmap-decoder.py --publish-dir <clean-publish-or-container-app>`.
The [machine-readable replacement record](parity/heightmap-decoder-review.json) binds
final production/test input hashes, source pin, limits, measurements and check totals.
Raw replacement logs, clean publish, container extraction and comparison research:
local `tmp/p3-07-replacement/`. `SOURCE.json` pins adopted file hashes; the temporary
comparison's Skia reference is isolated research outside production/build inputs.
The old Skia 453 .NET, 120 client, 142 Python and81 Chromium counts remain historical
baseline evidence; they are superseded by these replacement checks.

P3-07 closes only this heightmap slice and its expanded replacement work.
**Next: P3-08 seeded relief.** Overall P3, larger/dense capacity, native/device and
Windows/GDI comparison, P2 crop/fidelity/optional repair and deferred browser gates
remain open; BGM remains deferred by owner policy.
