# Stable ExAV bundled-content reconciliation

**P0-02 source reconciliation completed — 20 October 2026**, against FreeTrain
ExAV **2.1.1.12 / V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, SVN r115, **`trunk/`**.
The [review](parity/exav-content-review.json) and
[maintained rules](parity/exav-content-rules.json) are separate from the
[discovery ledger](parity/exav-ledger.json) and superseded original-source review.
This closes the content inventory/specification task. Revised P0-03 workflow/scope
reconciliation is also complete; P0-04–09, the overall P0
gate, content conversion, gameplay, visual fidelity and save continuation remain
open. No desktop assemblies or serialized objects were activated.

| Source accounting | ExAV result |
| --- | ---: |
| Bundled manifests, including two owner exclusions | 56 |
| Ordered direct declarations, including 33 factory definitions | 482 |
| Registered generated children | 1 |
| Direction selections / registered reverse-car wrappers | 8 / 2 |
| Explicit class/implementation bindings | 347 |
| Manifest file edges / DLL edges mapped to source and projects | 287 / 75 |
| Additional resource/project/document edges | 374 |
| Loose media, each hashed with credits/notices and a disposition | 238 |
| Referenced media / retained unused alternatives | 224 / 14 |
| Embedded image/resource values | 12 |
| Picture identities / manifest picture references | 159 / 353 |
| Excluded A4 plugin-local music objects | 9 |
| XML presentation candidates / English donor candidates | 977 / 641 |

Every explicit binding and every effective recipe implementation resolves to its
qualified source type and compile membership. Each explicit binding has a project
in the selected 2008 solution; older project memberships are retained separately.
The 75 missing DLL paths remain missing in discovery. Their review disposition is
**legacy build output**, established from type and project evidence, not filename
similarity or successful assembly loading. ExAV `PluginUtil.loadType` additionally
searches plugin subdirectories for a DLL basename when its requested path is absent;
the port must resolve reviewed dependencies deterministically.

Complete expanded XML trees retain declaration order, nested overrides, prices,
populations, capacities, categories, configuration and data files. Factories are
not ordinary catalog objects. The only generated child is **`null-contrib-0:0`**;
height, half-voxel side/highlight/color and direction-table selections do not create
additional registered IDs. Code registration-site accounting finds the generic
child loop, manifest registration, contribution-registry implementation and picture
registry. A4's nine external objects stay plugin-local and excluded. Debug-window
content is excluded; the historical recorder manifest is absent from stable.

All 353 manifest picture references and reviewed literal picture/contribution
references resolve in their respective registries. BGM `<href>` text, bell
`sound/@href`, stock data, category XML/icons, code-loaded bridge/garage/terrace
pictures, embedded resources and project items have separate edges. A project item
establishes packaging membership, not runtime reachability. The 213 BMPs comprise
169 24-bit, 34 8-bit and ten 4-bit images, all uncompressed. Crop origins, anchor
offsets, transparency margins and invisible occupied cells remain distinct.

Stable `doc/readme.txt`, `doc/readme_ftexav.txt` and `doc/COPYING.txt` are hashed and
attached to every media record alongside manifest author declarations. These
notices describe bundled licensing and credits; the review does not infer
individual image authorship or extend terms to optional packs. No optional
repository is read or blended into these outputs.

## Changes and handoffs

The maintained differences **EXAV-CONTENT-001–008** bind the following findings
to pinned source hashes:

- **Building identity and reachability:** 66 commercial and 55 variable-height
  declarations replace historical generic families. ExAV's development algorithm
  consumes these direct declarations subject to `computerCannotBuild`. The old
  MainWindow construction bindings are commented out; the generic selector lists
  `GenericStructureContribution`. Retain the direct entries in scope. **P0-03**
  now records their absent direct menu route in
  [EXAV-ACTION-009](web-parity-actions.md). The planned direct-placement adaptation
  remains open; a surviving builder class is not proof of a reachable menu. Direct variable-height builder/remover methods remain stubs.
- **Native effects:** ExAV routes night, alpha, shape, color and hue work through
  `lib/DirectXWraper/{DirectDraw.cpp,AlphaBlender.cpp,DirectXWrapper.h}`. Historical
  `DirectDraw.AlphaBlend` source paths are absent. Retain upper-left color keys,
  seasonal/night override ordering, quarter-darkening and special light colors,
  native quantization and exact sprite/overlay slicing. `DirectSprite.offset` is
  still recursive; native 16-bit detection repeats red-mask tests, and palette
  collision limitations remain documented. **P2/P0-04** must select and verify
  pixel behavior and record defect adaptations; these are source findings.
- **Numeric semantics:** structure prices/entity values use **Int64**;
  variable-height `pricePerArea` returns footprint `_ppa`, changing the historical
  result. Train **`seatedcapacity`** is distinct from total **`capacity`**, including
  reversed cars. **P0-05/P3/P5/P7** own migration, transaction and acceptance cases.
- **Placed state:** inherited contribution serialization still substitutes a
  catalog ID; pictures have a separate resolver. ExAV half-voxel ownership and
  charges, bank/economy returned timer handlers, stadium capacity/population,
  match/win/draw counters and audience-return callbacks add or change requirements.
  TAT now passes `Train` to stop-time handling. The lexical type/field index and
  state rules retain those sources; complete nested save graphs and continuation
  belong to **P0-05/P10**. Mutable ColorTest/catalog selections are not automatically
  durable merely because fields exist. Temporary BGM `fileName` serialization is
  separate from global catalog identity; **P0-03** retains its external-music exclusion; detailed state handling follows the owning slice.
- **Project gaps:** five embedded/content edges remain missing: three in the old
  core project and `res/Straight.bmp` in both TAT projects. The discovery ledger
  also retains two missing old-core Compile entries, making seven project gaps
  overall. No ExAV project source was edited here.

For TAT, the ExAV `.resx` file hash **differs** from the historical file, but its
normalized `imageList.ImageStream` base64 payload and both loose GIF byte streams
are identical. The review verifies this before reusing historical `CONTENT-001`'s
narrow pixel result: index 0 matches Straight.gif, index 1 matches Curve.gif,
including visible color/alpha. This establishes artwork continuity, not project
buildability or a fresh desktop rendering capture. The original gap remains.

The approved **EXAV-DIR-001** (`SOURTH` → `SOUTH`) and **EXAV-ENC-001** (CP932 →
UTF-8 without BOM) decisions continue to apply to ported content. Generation reads
pinned Git objects, preserving original spellings/encoding; it does not read the
archive's corrected, uncommitted working files. See
[baseline decisions](parity/exav-baseline.json) and
[encoding evidence](parity/exav-manifest-encoding-review.json).

## Translation and implementation status

The review inventories manifest and auxiliary XML presentation candidates with
source path/hash, contribution ID or field key, original text/hash, matching
historical English donor where found, text-comparison classification, proposed
wording and review status. A same-ID donor is not an approved translation.
Proposals remain explicitly unassigned until the implementing feature reviews
meaning; source code, form/resource strings, validation, reports and help now have a
separate [P0-03 inventory](web-parity-actions.md), with classification and
donor/wording review required within each implementing slice. The rule file begins a glossary for total/seated capacity, price,
footprint price and hundred-share units. Japanese text, numeric data and stable IDs
remain unchanged. Each feature slice must supply English/Japanese wording and
layout evidence; P0-06 supplies scenarios and P11 reconciles remaining translations.

Recipe status is **source-reconciled; runtime parity unverified**. Discovery and
generated-child implementation status stay `inventoried`. This review does not
migrate the prototype importer, Docker inputs, runtime catalog, protocol or saves;
those still require the explicit ExAV build/import transition. Modern destinations,
fixtures and independent expectations remain assigned with P0-03–06 and feature
slices, not manufactured from passing prototype tests.

## Reproduction

Run from the repository root with an explicit archive containing the pinned commit:

```sh
python3 scripts/reconcile-exav-content.py --source-root /path/to/freetrain-exav-archive
python3 scripts/reconcile-exav-content.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/inventory-parity.py --source exav --source-root /path/to/freetrain-exav-archive --check
python3 scripts/reconcile-parity-content.py --check
python3 -m unittest discover -s scripts/tests -v
```

The ExAV command writes/checks only `doc/parity/exav-content-review.json`; edit its
rules and regenerate. It shares inert parsing/reconciliation mechanics with the
historical command, with explicit CP932, pinned direction-token and project-model
inputs. Unknown asset dispositions, changed diagnostics, unresolved type/reference
bindings and changed TAT reuse inputs fail generation. Default tests inspect the
checked-in evidence and synthetic cases without discovering a sibling checkout.
Fresh archive generation/checks and the full Python suite passed on 20 October
2026. Application/browser, native-device, visual and save-continuation acceptance
were not run for this source/documentation change.

---

## Historical original-FreeTrain reconciliation

The following dated review retains original `1f755ca` counts and derivations as
comparison evidence. Its commands reproduce historical outputs only.

Historical P0-02 completed a **source inventory and specification**, against original FreeTrain
`1f755ca6f291253b8cd5024ef53402b85d467170`. This does not establish playable
content, rendered fidelity, historical-save compatibility or original-runtime
comparison. Those acceptance gates remain with their implementation phases.

The [discovery ledger](parity/historical/base-ledger.json) retains original discovery
diagnostics and `inventoried` implementation status. The separate
[content review](parity/historical/base-content-review.json) reconciles those records using
[maintained source rules](parity/historical/content-rules.json). No legacy DLLs or serialized
objects are activated. The optional non-free suite has a separate ledger and is
outside this base review.

## Accounting and provenance

| Item | Source reconciliation |
| --- | --- |
| 54 manifests | All declaration ordinals, IDs, exclusions and source hashes retained |
| 393 declarations | Includes 33 factory definitions; complete expanded XML trees retain child ordering, attributes and text |
| 116 registered children | Generic structure color × sprite primitives, linked to their parents; heights remain instance recipes |
| 618 selection mappings | 616 direction/color selections plus two registered reverse-car wrappers; all targets exist |
| 9 A4 music objects | Excluded external-installation objects in a plugin-local list; not globally registered children or bundled assets |
| 268 type bindings | Exact qualified source type and project compile membership checked for every manifest class/implementation |
| 148 picture identities | Separate registry includes inline/contributed pictures, cached system pictures, code-loaded plugin pictures and the terrace picture ID; all 260 manifest picture references resolve |
| 271 manifest file edges | 195 image/sound references resolve exactly or by case; 76 DLL references map to retained source/build projects |
| 255 additional file edges | Source loads, BGM element text, stock data, document links, project content and embedded resources |
| 224 loose media files | 209 have graph edges; 15 retained alternatives/unused files have explicit dispositions |
| 14 embedded resource values | Resource names, declared types, value hashes and containing `.resx` hashes retained |

Project content edges establish inclusion, not runtime reachability. Likewise,
an unreferenced image stays in the denominator without becoming a new feature.
The pinned review retains the one missing project-content edge: the baseline TAT
controller project lists `res/Straight.bmp`, which is absent. A subsequent pixel comparison
confirms that the junction dialog's embedded ImageList already contains the same
art as its two GIF files. This is a stale project entry, not missing junction art;
no fallback or asset substitution is implemented.

### Junction ImageList comparison

The working `.resx` and GIFs are byte-identical to the pinned baseline. Decoding
`JunctionConfigDialog.resx` → `imageList.ImageStream` gives two 48×48 images.

| Embedded index | Loose source | Opaque pixels | Differing alpha/visible-color pixels |
| --- | --- | ---: | ---: |
| 0 | `res/Straight.gif` | 322 | 0 / 0 |
| 1 | `res/Curve.gif` | 321 | 0 / 0 |

These are exact visual matches at original resolution, with no resizing or color
tolerance. Raw RGBA differs in the fully transparent pixels only: the GIFs retain
white RGB under transparency, while the ImageList color bitmap stores black.
Normalizing fully transparent RGB to zero gives identical pixel hashes for each
pair, recorded with input hashes in `CONTENT-001` in the maintained source rules.
Cross-comparing the opposite entries gives 29 differing alpha pixels, confirming
that the two mappings have not been swapped.

The comparison extracts the serialized byte array without constructing CLR
objects, expands the `MSFt` count/value runs using the
[WinForms stream algorithm](https://github.com/dotnet/winforms/blob/v8.0.0/src/System.Windows.Forms/src/System/Windows/Forms/ImageListStreamer.cs),
and reads the native `IL` 0x0101 header, color bitmap and one-bit transparency mask.
The [native ImageList implementation](https://github.com/wine-mirror/wine/blob/master/dlls/comctl32/imagelist.c)
documents that layout. The stored image count is two; unused bitmap capacity is
not treated as additional icons. This compares source image data, not a running
Windows rendering capture.

The current legacy project replaces the stale `res/Straight.bmp` content item
with `res/Straight.gif` and `res/Curve.gif`. This corrects its file inventory;
the embedded ImageList and source artwork are unchanged. `CONTENT-001` records
the correction separately from the pinned baseline, whose original missing-file
diagnostic remains reproducible.

Each loose resource retains its SHA-256, byte length, canonical path, incoming
owners, manifest/contribution credit evidence, notice paths/hashes and decoding
rule. BMP metadata distinguishes 4-bit/8-bit indexed and 24-bit RGB sheets; all
204 baseline BMPs use uncompressed storage. Manifest credits are declarations,
not invented per-image authorship. The baseline contains no individual asset
license files in these plugin directories; repository notices and manifest
authors remain attached without inventing additional permission grants.

Referenced auxiliary files, including `CategoryTree.xml`, `mountainPalette.xml`,
stock business/company/event data, HTML and form resources, also have path/hash
records. Historical external About hyperlinks are document links, not bundled
resources. A4 MIDI references remain explicitly external and excluded. Debug
window and video recorder manifests and their resource metadata remain excluded
entries rather than disappearing from the 54-manifest denominator.

## Recipes and drawing

Every declaration points to its effective implementation and reviewed rules.
The complete XML tree preserves fields the earlier flattened recipe scan missed,
including nested overrides, population, category, price, configuration and stock
data filenames. Rule sources are hashed from pinned Git objects.

- Picture selection follows the original four-season/day-night loader table.
  An unspecified season inherits spring; a season-specific day image without a
  night override derives its own night. The source checks the first override
  child for a night override; reordering siblings changes behavior.
- Bitmap sprites use the upper-left pixel as a color key. Source rectangles and
  destination anchor offsets stay distinct. Preserve transparent margins and
  invisible `NullSprite` cells. The rules retain default 0D/2D formulas and the
  complete pinned 3D roof/front/edge slicing implementation.
- Exact palette maps, hue masks, automatic night lighting and authored overrides
  retain their source algorithms. Native RGB555/565/888 rounding and the documented
  night-palette collision limitation remain explicit P2 fidelity decisions.
- Generic structures register `{parent}-{color}:{direction}` primitives. Direction
  fallback preserves the source sentinel when no default exists; unknown/empty
  direction tokens do not create a default. All baseline selections resolve.
  Color, height and half-voxel highlight phases are not extra registered IDs.
- Train recipes retain body/head/tail order, symmetric/asymmetric level and slope
  frames, reversal and color/shadow maps. Roads, forests, rice fields, vinyl houses,
  fences, poles, signals and half-voxel structures retain their own crop/anchor
  rules and defining source. Code-loaded bridge, garage and terrace pictures are
  resolved relative to their contribution plugin, not the core resource folder.
- Bundled BGM uses `<href>` text; bells use `sound/@href`. Both retain their source
  bytes and identities. Browser MIDI/audio conversion and playback remain P2/P9.

These are conversion specifications. No generated atlas, converted artwork or
pixel-comparison acceptance is delivered by this inventory.

## Contribution identity and saved instance state

Ordinary `Contribution.GetObjectData` substitutes `Contribution.ReferenceImpl`
with the string `id`. Recipe fields reload from the catalog rather than becoming
independent saved fields. Generated primitive IDs must resolve before loading
their placed entities. `Picture.ReferenceImpl` resolves a separate picture
registry. Persistent population uses a resolver to restore shared identity.

Placed instances still carry state: building height/location/ownership; half-voxel
fore/back contribution references with color/highlight/face/side; scattered forest
patterns; rice/vinyl/creek/pole/accessory/signal/garage selections; stadium teams,
games and results; and TAT station/junction timetable rules. Bank and stock modules
also store world `otherObjects` entries, financial/economy state and scheduled
work. The state rules name these roots, rebuild hooks and source files; the type
index retains nested names, base declarations, direct fields, storage markers and
method signatures with source hashes.

The type index is lexical declaration evidence, **not a complete CLR save graph**.
An `instance` field marker does not by itself prove serialization or reachability;
inherited custom serializers take precedence. Full nested graph mappings,
allowlisted historical import, representative saves and continuation remain
P0-05/P10 work. No schema or reader is changed here.

Two source distinctions require explicit treatment:

- ColorTest edits picture/colors on a catalog singleton, whose inherited serializer
  still writes only its ID. Generic selector selections behave similarly. Do not
  infer durable customizations from these mutable fields; a port improvement needs
  a recorded behavior difference.
- `BGMManager.TempBGMContribution` overrides serialization and stores `fileName`
  through its own reference helper. This user-file action is separate from A4
  discovery and has an explicit exclusion in current P0-03; feature detail follows its owning slice. Do not bundle user music or equate the
  temporary BGM helper with a global catalog alias.

## Reproduction

Run from the repository root, without the optional-suite checkout:

```sh
python3 scripts/inventory-parity.py --check
python3 scripts/reconcile-parity-content.py
python3 scripts/reconcile-parity-content.py --check
python3 -m unittest discover -s scripts/tests -v
```

`--report` accepts a path under the checkout's temporary directory and emits a
human-readable review. Maintained output is deterministic JSON; edit source rules
and regenerate rather than editing the generated review. The repository gate
runs both discovery and reconciliation checks. Unexpected type bindings,
contribution references, aliases, missing resources or stale asset dispositions
fail generation instead of silently dropping records.
