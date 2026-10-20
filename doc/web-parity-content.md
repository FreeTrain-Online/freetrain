# Base content reconciliation

**ExAV reassessment pending — 20 October 2026:** this P0-02 bundled-content review records the earlier original-FreeTrain source/documentation and its stated revision. It is historical input, not current ExAV completion or authority. [The current source policy](web-feature-parity.md) selects stable ExAV archive `trunk/` (2.1.1.12), including bundled plugins; P0 must validate retained findings and replace changed ones against that source. Existing original-source JSON, counts and fixtures retain their old provenance until regenerated. Carry English translations onto ExAV semantics as features are ported.

P0-01 now supplies a separate [stable ExAV discovery ledger](web-parity-exav-inventory.md):
56 manifests / 482 direct declarations, one discovered generic child and 238 loose
media files. These replace the historical discovery denominator for the next
P0-02 review; factory/resource/alias/state reconciliation below has not transferred.

**EXAV-DIR-001 — owner-approved correction, 20 October 2026:** treat the generic
loader's `SOURTH` token as a source typo. Use **`SOUTH`** for the south direction
in ported content and its parser. The source inventory retains the original
spelling/behavior; see [the recorded decision](parity/exav-baseline.json).
The stable ExAV checkout's generic loader has now been corrected to `SOUTH` in
the working tree; the pinned archive commit and its evidence remain unchanged.

**EXAV-ENC-001 — owner-approved conversion, 20 October 2026:** all 56 stable
ExAV checkout manifests now contain UTF-8 bytes and XML declarations, without BOM.
Decoded Japanese text, entities, all 482 direct declarations and line endings are
preserved. `Plugin.loadFailSafe` now reads UTF-8 metadata. Use UTF-8 for ported
manifests; the pinned archive evidence retains its original CP932 encoding.
The working-tree conversion is uncommitted; [per-file hashes and verification](parity/exav-manifest-encoding-review.json)
are separate from the pinned source ledger.

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
  discovery and must be mapped during P0-03. Do not bundle user music or equate the
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
