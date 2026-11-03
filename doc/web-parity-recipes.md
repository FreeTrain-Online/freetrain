# P2-02: bundled sprite recipes and attribution — 26 October 2026

**27 October follow-up:** [P2 remaining acceptance](web-parity-p2-acceptance.md) records the historical MIDI/bell feasibility experiment and shared gallery localization. [The owner subsequently deferred BGM](web-audio-policy.md) and removed TinySynth; bell previews and source MIDI retention remain. Original crop/fidelity dispositions and owner deferrals below retain their scope.

The prepared stable ExAV bundle now has executable browser sprite recipes,
source crops/anchors, baked effects, exact variant masks and a read-only review
gallery. It preserves the original plugin directory names in generated media
and distributes explicit attribution metadata. This is a rendering implementation;
P2-02's full visual/fidelity acceptance and the P2 phase gate remain open.

## Attribution recommendation and implemented layout

Retain the original reverse-domain plugin directories as durable source
namespaces. Keep the authoritative source mirror, generated distribution and
pack identity separate, with machine-readable links between them. A hash proves
which bytes were used; it does not replace the plugin's source or credits.

This change implements that recommendation:

```text
content/bundled-v2/
  source/trunk/plugins/org.kohsuke.freetrain.structure.c477/plugin.xml
  source/trunk/plugins/org.kohsuke.freetrain.structure.c477/bldgVH01.bmp
  expanded/trunk/plugins/org.kohsuke.freetrain.structure.c477/plugin.xml
  source-index.json
  review.json
web/public/content/bundled-v2/
  plugins/org.kohsuke.freetrain.structure.c477/metadata.json
  plugins/org.kohsuke.freetrain.structure.c477/media/
    bldgVH01.bmp.day.<sha256>.png
    bldgVH01.bmp.night.<sha256>.png
    bldgVH01.bmp.effect-<recipe-hash>.<sha256>.png
  core/res/media/...
  provenance.json
  catalog.json
  catalog.sha256
  notices/...
```

The example effect name illustrates the naming rule; derivatives are emitted
only where a recipe requests an effect. Nested source directories are preserved.
Non-domain plugin names such as `SpeedLimit` also remain literal. Core images
retain `core/res`; they are not attributed to a guessed plugin. Hashes remain in
filenames for integrity and cache identity. Byte-identical files belonging to
different source paths retain separate names and provenance.

For every source image, `provenance.json` records its canonical archive path,
original SHA-256, plugin ID/namespace where applicable, declared plugin author,
direct contribution credits and original notice paths/hashes. Day/night/native
outputs and effect derivatives have their own URL/hash/size and conversion
parameters. The index also links the pack pin, source-index hash, contribution
IDs/coverage IDs and 54 hashed per-plugin metadata files. Each plugin metadata
file contains its manifest identity, dependencies, source-file hashes,
declarations and associated translation candidates. English presentation remains
associated with original IDs; it does not rename an author's namespace.

**Preserve evidence without inventing image authorship.** Manifest authors and
contribution credits are distinct from verified per-image authors. The index
explicitly labels individual image authorship as not independently verified.
The gallery displays the literal declared evidence and source path. Future
specific author/permission findings should be added as reviewed, source-backed
asset metadata, without discarding the original declarations. Namespace names
are source/authorship context, not an automatic license grant or proof of who
drew every image. Separately distributed optional sets still need separate
roots, notices and identities in P2-03. If atlases are introduced later, preserve
this same per-image record and add a region-to-source mapping for each atlas
entry; a combined atlas must not collapse its component credits.

The old flat `media/<hash>` outputs are removed by generation inside the dedicated
bundle root. Consumers use catalog URLs. The prepared output identity advances
to **`exav-stable-bundled` v2, catalog schema 2**. The source-input index format
remains schema/version 1. The live reference world's ruleset, asset-free content
pack and Save 5 identity stay unchanged.

## Planned final asset-format pass

Owner direction on 26 October 2026 adds **POST-ASSETS**, to run after P0–P11
parity acceptance. Canonical PNG images and lossless FLAC audio masters, with generated Ogg Opus
(`.opus`) playback files, will become the maintained image/audio inputs and
outputs. Reviewed modern equivalents cover other superseded assets. The
final pass removes historical BMPs, WAVs and other replaced asset binaries from the
current repository tree after comparing accepted pixel/variant/behavior evidence.
The legacy source mirror described above remains an interim parity input.

Canonical masters must preserve the color-key, palette/RGB, reserved night-light
and authored variant information needed by recipes; transformed runtime PNGs are
not automatically suitable masters. Preparation, manifests, indexes, fixtures,
pack identities and packaging will move together to canonical-only inputs, with
clean builds independent of the archive. Original plugin namespaces, source
revision/path/hash, declared credits, conversion mappings and notices survive
binary retirement. External archives and Git history retain the historical bytes;
they are not modified by this pass. Optional-set attribution/distribution stays
separate. The plan/checklist track conversion, retirement, exceptions and final
regression evidence; this addition removes no assets today.

The audio recommendation is [Opus](https://www.opus-codec.org/) for compact
playback, with [FLAC](https://www.xiph.org/flac/features.html) preserving lossless
masters for future editing/encoding. Verify decoded PCM for masters and audible
quality, onset/duration, pre-skip/end trimming, loops and overlapping playback for
Opus renditions. Preserve relevant WAV cue/loop metadata and original credits.
Ogg Opus support on Apple platforms starts with the OS versions listed in
[WebKit's Safari 18.4 announcement](https://webkit.org/blog/16574/webkit-features-in-safari-18-4/);
older required targets may need an AAC-LC/MP4 rendition. The final pass will
validate its supported playback matrix; no deferred platform testing is resumed
by this documentation change. MIDI remains a separate synthesis/playback concern.

### Crop corrections to complete during POST-ASSETS

**Owner review and direction — 26 October 2026:** a visual sanity check found
that the affected images look fine. Record this as owner-reported image review;
it does not establish that the crop coordinates, assembled variants or every
rendering case are correct. The final asset-conversion pass must correct or
explicitly accommodate the **21 invalid crop requests** below, rather than
translating their invalid parameters into the canonical recipes/new runtime.
Preserve the artwork where possible and keep the original requests as historical
evidence. This direction authorizes the correction/accommodation work; exact
replacement coordinates and strategies remain to be derived and verified.

**POST-ASSETS-07** executes with canonical PNG/recipe conversion, before switching
maintained inputs and removing the legacy files. For each request, record the
original recipe/path/hash and rectangle, the intended visible result, the
corrected canonical recipe/rectangle or explicit accommodation, anchor/occupancy
impact, rationale and before/after evidence. Accommodation must be deliberate
and bounded, such as a reviewed padding/rebasing or invisible-cell recipe where
appropriate; do not blindly clamp, resize, fabricate artwork, issue negative-size
blits or silently discard visible content. Update tests that currently expect
these diagnostics to verify the repaired behavior while retaining independent
checks of the historical evidence. Final canonical executable requests must have
valid positive dimensions and fit their declared source/prepared surfaces.

Source images are relative to `trunk/plugins/` in the pinned archive and
`content/bundled-v2/source/` in this checkout:

| Recipe group | Plugin namespace | Source images | Dimensions | Requests |
| --- | --- | --- | --- | ---: |
| Building A / Building B — 新宿三井ビル | `org.kohsuke.freetrain.structure.c477` | `bldgVH03.bmp`, `bldgVH03_n.bmp` | 353×352 | 14 |
| Farmland 0–3 | `org.kohsuke.freetrain.land.farmland` | `hatake.bmp`, `hatake.AUT.bmp`, `hatake.WIN.bmp` | 128×80 | 4 |
| Concrete stairs | `system` | `renraku02.bmp`, `renraku02.n.bmp` | 336×153 | 1 |
| Brown stairs | `system` | `renraku02brown.bmp`, `renraku02brown_n.bmp` | 336×153 | 1 |
| Green stairs | `system` | `renraku02green.bmp`, `renraku02green_n.bmp` | 336×153 | 1 |

Recipe identities used in the table:

- Building A: `{D57200E0-8E73-4899-8EEC-0E9AC95C3D40}`.
- Building B: `{488D9D06-46B1-45EA-8DCE-61B1041254A2}`.
- Farmland 0: `{168C94A9-1D5B-4160-BBD8-21C830DCB752}-0,4`.
- Farmland 1: `{168C94A9-1D5B-4160-BBD8-21C830DCB752}-1,4`.
- Farmland 2: `{168C94A9-1D5B-4160-BBD8-21C830DCB752}-2,4`.
- Farmland 3: `{168C94A9-1D5B-4160-BBD8-21C830DCB752}-3,4`.
- Stairs: `system:concrete-stair-23`, `system:brown-stair-23`, `system:green-stair-23`.

Rectangles below are **historical `(x, y, width, height)` requests, not proposed
fixes**. All 21 are diagnosed in all four seasons and both lighting modes.
All entries remain pending correction/accommodation.

| Request | Recipe group | Frame key | Historical rectangle |
| --- | --- | --- | --- |
| ASSET-CROP-01 | Building A | `top/0/0` | `(-32, 0, 32, 40)` |
| ASSET-CROP-02 | Building A | `top/0/1` | `(-16, 0, 32, 32)` |
| ASSET-CROP-03 | Building A | `top/0/5` | `(-16, 0, 32, 48)` |
| ASSET-CROP-04 | Building A | `middle/0` | `(-32, 88, 32, 24)` |
| ASSET-CROP-05 | Building A | `middle/1` | `(-16, 88, 32, 16)` |
| ASSET-CROP-06 | Building A | `middle/4` | `(32, 88, 32, -8)` |
| ASSET-CROP-07 | Building A | `middle/5` | `(-16, 88, 32, 32)` |
| ASSET-CROP-08 | Building B | `top/0/23` | `(336, 0, 32, 32)` |
| ASSET-CROP-09 | Building B | `top/0/28` | `(336, 0, 32, 48)` |
| ASSET-CROP-10 | Building B | `top/0/29` | `(352, 0, 32, 40)` |
| ASSET-CROP-11 | Building B | `middle/5` | `(288, 88, 32, -8)` |
| ASSET-CROP-12 | Building B | `middle/23` | `(336, 88, 32, 16)` |
| ASSET-CROP-13 | Building B | `middle/28` | `(336, 88, 32, 32)` |
| ASSET-CROP-14 | Building B | `middle/29` | `(352, 88, 32, 24)` |
| ASSET-CROP-15 | Farmland 0 | `land/0` | `(0, 64, 32, 32)` |
| ASSET-CROP-16 | Farmland 1 | `land/0` | `(32, 64, 32, 32)` |
| ASSET-CROP-17 | Farmland 2 | `land/0` | `(64, 64, 32, 32)` |
| ASSET-CROP-18 | Farmland 3 | `land/0` | `(96, 64, 32, 32)` |
| ASSET-CROP-19 | Concrete stairs | `concrete-stair-23` | `(388, 4, 32, 36)` |
| ASSET-CROP-20 | Brown stairs | `brown-stair-23` | `(388, 4, 32, 36)` |
| ASSET-CROP-21 | Green stairs | `green-stair-23` | `(388, 4, 32, 36)` |

Building requests include negative X coordinates, negative heights and crops
extending past the right edge. Farmland requests extend to row 95 on 80-pixel-high
sheets. Stair requests begin at X=388 beyond a 336-pixel-wide sheet; these three
entries retain the EXAV-GAL-001 link. Matching artwork alone does not resolve
these invalid requests.

**Completion evidence:** every ASSET-CROP entry has a reviewed correction or
explicit accommodation; validate canonical bounds/anchors, all affected
season/light variants, variable-height assemblies, directional context,
transparency/effects and picking. The original malformed values survive only in
source/provenance evidence, not as executable modern crop parameters. Retaining
a diagnostic alone does not complete POST-ASSETS-07. No image, recipe or runtime
parameter is changed by recording this work.

## Implementation

[The trusted compiler](../scripts/content_recipes.py) handles the source default
0D/2D/3D loaders, explicit invisible voxel cells, 3D top hats and bottom edges,
fixed structures/stations/rail accessories, variable-height top/middle/bottom
selection, half-voxel direction/side anchors, static/random land references,
forests and ground, fences, ricefields, vinyl houses and creek connectivity.
It also handles both road layouts, poles, directional signal passes,
bridge/tunnel/girder/garage layers, symmetric/asymmetric/reversed car frames and
train compositions. Source-derived platform, stair and rail tables are promoted
from the reviewed P0 geometry inputs. Construction fence/ground/bones/walls,
cliffs, ground chips and cursor crops have explicit source bindings as well.

There are **812 recipe records / 3,176 frames**, including the 448 contribution
identities and 364 system geometry recipes. All **159 picture identities** have
resolved bindings, including the 18 formerly pending code bindings. The 468
checked-in source/evidence files come from the pinned archive Git blobs, not
its dirty checkout. Normal generation still needs no archive, historical roots,
optional repository or network access.

Numeric fields use source Int32/Int64 bounds. Int64 values are emitted as decimal
strings for exact JavaScript transport. V2 fare, amenity, trip range and total
versus seated capacity remain distinct. Train compositions validate car
references; source tail precedence for one-car trains is preserved. These are
content validation/composition rules, not gameplay or economy implementations.
No CLR type/codebase from a manifest is executed.

RGB888 night surfaces retain the P2-01 light/key behavior. Color maps retain
reverse search and duplicated night mapping/collision precedence. Hue effects
retain native RGB888 channel arithmetic, including asymmetric rounding.
Color/hue affect normal drawing; inherited alpha/shape operations use the
original picture. Alpha halves each source/destination channel separately before
adding; shape uses the source silhouette. Native 555/565 behavior is not claimed.

[The browser renderer](../web/src/lib/content-recipes.ts) loads the exact catalog
hash, validates descriptors/masks, verifies media bytes and dimensions, shares
concurrent decodes and evicts failed requests for retry. A scene becomes ready
after its required images decode. The gallery guards asynchronous completion by
generation. Traversal uses source V/H ordering, elevation and draw stage; cuts
omit higher voxel layers. Drawing and reverse-order picking use the same crop,
anchor and active season/light mask. Recipe expansion supports requested
variable heights and explicit frame selectors; representative gallery choices
are deterministic and consume no game random streams.

The embedded [server registry](../src/FreeTrain.Online.Content/PreparedContentCatalog.cs)
validates and exposes immutable, exact pack-qualified recipe records alongside
prepared definitions. Missing/duplicate recipes and broken picture/surface
bindings fail startup. Rendering availability does not authorize a gameplay
handler or select this pack for a world.

The production build includes `/content-gallery.html`. It offers plugin/recipe,
season, night, height and normal/alpha/shape controls, a composition canvas and
individual source frames with paths/credits. Pointer inspection uses actual
alpha picking. It is a separate read-only developer review page, outside the
shared game's panels and simulation.

## Source dispositions and acceptance limits

- **P2-RENDER-001:** currently retain and diagnose 21 invalid source crop requests: 14 frames
  across two variable-height definitions, four farmland frames and the three
  concrete/brown/green stair-23 crops from EXAV-GAL-001. Invalid variants have
  no drawable/pickable mask; valid variants remain usable. Do not clamp, resize,
  guess replacement art or silently declare these source defects fixed.
  **26 October owner direction:** this is temporary diagnostic behavior.
  [POST-ASSETS-07](#crop-corrections-to-complete-during-post-assets) must correct
  or explicitly accommodate all 21 requests in canonical recipes; copying the
  malformed parameters into the modern runtime is not the final disposition.
- **P2-RENDER-002:** use crop-relative alpha picking and the stored source anchor.
  This deliberately avoids `SimpleSprite.HitTest`'s whole-sheet coordinates and
  `DirectSprite.offset`'s recursive getter. Source evidence remains unchanged.
- **P2-RENDER-003:** explicitly select portable RGB888 effects; native display
  format and original-runtime comparisons remain acceptance work. Preserve the
  source's color-map collision behavior rather than silently correcting it.
- **P2-RENDER-004:** color-test car previews choose the first prepared
  `colorMapTrainPicture` deterministically, with the source red/blue/green/yellow
  defaults. The source's `list()[0]` enumeration is not a portable ordering
  guarantee. Interactive customization and persistence stay with P5/P9/P10.
- Procedural Mountain2 terrain and stadium `PThreeDimStructure` geometry have
  explicit `procedural-world` records. Their world-dependent geometry belongs
  to P3/P7, not a fabricated bitmap. Random-land selection and forest scattering
  also require their world systems; the gallery reviews authored alternatives.
- UI resources, MIDI/audio playback, optional packs and reviewed player-facing
  English/Japanese workflows remain in their assigned P2/P3–P11 work. Source
  project gaps and inert embedded resources remain diagnosed.

Full visual review of the assembled catalog, source-defect fidelity decisions,
world-consumer integration, native/device acceptance and full-content capacity
remain open. Exhaustive automated variant rendering is not original-runtime
fidelity evidence. P2-02 and overall P2 are not closed by this implementation.

## Verification

For current test commands and when to run each suite, see
[browser validation in README.web.md](../README.web.md#prerequisites-and-checks).
Routine `test:browser` runs representative recipe/status choices across all
season/light variants, plus the 54-variant live-world stationary-object test.
`test:browser:extended` adds the exhaustive bundled recipe sweep and full
lifecycle/performance matrices; `test:browser:all` also runs generated
mixed-network continuation. Run extended coverage after asset, importer, recipe
or shared renderer changes, and all coverage for comprehensive/release validation.
These automated checks remain useful regression coverage after initial review,
but do not establish per-object visual fidelity against approved reference images.

The following counts and screenshots record the original P2-02 validation run,
rather than the current suite size:

Validation: **276 .NET, 90 client and 123 Python tests pass**, along with the
production build, pinned source regeneration, generated checks and project
boundaries. Chromium covers **15 canonical cases** including all **6,496 recipe
season/light combinations**; the unchanged application's **54 historical
regressions** passed in the full run. The final canonical rerun uses the final
catalog/metadata; historical evidence is reused from that full run. The gallery
screenshot's variable-height winter/night assembly and source-frame layout were
visually inspected. This is representative visual inspection, not a claim that
all source frames have received human fidelity review.

The final local amd64 Docker image builds and its container check verifies 238
source media / 505 unique output media URLs, 54 plugin metadata files,
provenance, gallery and four static/managed notices. Source-index/catalog hash
checks match; health is `ok` and the bootstrap selects the reference profile.
The temporary validation container is removed. Catalog SHA-256:
`754fa756b35a01b1e6ab0f33013f2b119c6ff3855502cd480b6de2fade85b0e5`.

An initial .NET run reproduced the existing oversized-save HTTP connection-reset
failure; final full reruns pass without modifying that test or save code. The
first browser attempt had no pinned Chromium installed and ran no browser
checks; the installed pinned browser supplies the results above.

Final results are recorded in `tmp/p2-02-{prepare,source-check,python-final,client-final,dotnet-final,build,browser-final,browser-canonical,docker,container}.log`.
Independent source vectors cover loader anchors/crops, invisible occupancy,
hue/channel arithmetic, night color-map collisions, source integer bounds,
variant masks and complete per-image attribution. Browser coverage enumerates
every recipe in four seasons and two lighting modes, checks actual alpha/shape
pixels and static metadata hashes, and preserves the authoritative world.

Reproduce:

```sh
python3 scripts/prepare-bundled-content.py --check
python3 scripts/prepare-bundled-content.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
deno task --cwd web test
deno task --cwd web build
dotnet test FreeTrain.Online.slnx -c Release --no-restore --disable-build-servers -m:1
cd web
deno run -A npm:@playwright/test@1.64.1 test
```

The minimal-build fixture includes the new compiler and runs without Git,
`doc/`, historical roots or sibling checkouts. Docker's explicit copy list
includes the compiler. The container checker verifies base media, effect
surfaces, plugin metadata, provenance, gallery and static/managed notices over
an ephemeral loopback port with temporary saves, then removes the container.
