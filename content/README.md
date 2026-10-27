# Canonical bundled build inputs

`bundled-v2/` is the checked-in preparation input for stable FreeTrain ExAV
2.1.1.12 / V2, archive commit
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk/`.
It contains bundled content only. Optional non-free sets belong in separate
input/output roots and are never discovered by the default preparation command.

`source-index.json` records every original Git-blob SHA-256 and prepared-byte
SHA-256. The 468 selected files include all 56 manifests, 238 loose media files,
reviewed auxiliary resources and four notices. Manifest encoding is normalized
to UTF-8 under EXAV-ENC-001; all expanded declarations compare exactly with P0's
review. `expanded/` holds inert entity-expanded UTF-8 XML for normal builds.
`source/` retains manifest entities and other source bytes, including inert
desktop `.resx` resources for future review. Selected C# and native source files are inert hashed recipe evidence; none is compiled or activated by preparation. No legacy DLL is loaded.

`review.json` selects the recipes, qualified implementation bindings, source
exclusions, picture registrations, dependencies, translations and diagnostics
from `doc/parity/exav-content-review.json` and `exav-ledger.json`. Their hashes
remain in that snapshot. It preserves Japanese source text and English donor
candidates without substituting unreviewed translations. Neither the archive's
dirty working files nor this repository's historical `core/` and `plugins/`
trees supply canonical build data.
Scoped Git attributes preserve exact source/notice bytes across checkouts.

Normal generation and validation require Python's standard library only:

```sh
python3 scripts/prepare-bundled-content.py
python3 scripts/prepare-bundled-content.py --check
```

Explicit refresh and source verification additionally require Git and an archive
containing the pinned commit. There is no checkout discovery or download:

```sh
python3 scripts/prepare-bundled-content.py --source-root /path/to/freetrain-exav-archive
python3 scripts/prepare-bundled-content.py --source-root /path/to/freetrain-exav-archive --check
```

Edit reviewed inputs deliberately, then refresh and regenerate. Do not hand-edit
generated catalogs/images or relabel P0 source records as implemented gameplay.
Unknown, missing, conflicting or changed inputs fail preparation. Stale generated
files are rejected by `--check` and removed only within the dedicated bundled
output root during generation.

Outputs are the byte-identical server/browser catalogs at
`src/FreeTrain.Online.Content/bundled-v2.json` and
`web/public/content/bundled-v2/catalog.json`, their hashes, converted media and
original distribution notices. `PreparedContentCatalog` validates and indexes
this data at server startup. This prepared registry is separate from the world
profile registry: source declarations do not authorize executable game behavior.

The default world remains `reference-spatial-4` / Save 5. Its synthetic pack is
distinct from `exav-stable-bundled` v2. See
[the P2-01 delivery and remaining work](../doc/web-parity-bundled-preparation.md).

P2-02 adds executable sprite recipes, variant masks, hash-checked browser rendering
and `/content-gallery.html`. Generated media retain `plugins/<original-id>/` or
`core/res/` directories and ship `provenance.json` plus per-plugin `metadata.json`.
See [the attribution recommendation, implementation and open acceptance](../doc/web-parity-recipes.md).
