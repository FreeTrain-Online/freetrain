# Content boundary

`PreparedContentCatalog.Bundled` validates and indexes the prepared stable ExAV
bundled catalog at server startup. Its 448 contribution records remain inert
for gameplay. All 159 picture bindings and 812 browser render recipes are
available as immutable, pack-qualified metadata with exact hashes. Original
notices ship in managed output; generated media preserve original plugin
namespaces and per-image provenance. Default preparation reads
[checked-in canonical inputs](../../content/README.md), never historical roots
or an implicitly discovered archive/optional pack. See
[P2-02 rendering, attribution and acceptance limits](../../doc/web-parity-recipes.md).

`SpatialReferenceScenario.Create()` builds the default canonical reference world:
32 source-valid normal rail cells forming an eight-heading loop, on a 32×64×8
H/V map. `reference-spatial.json` contains the twelve source normal-pattern port
pairs, pack-qualified recipe IDs and occupied footprints, and defines the asset-free spatial pack hash. This is synthetic reference
content, not the complete bundled ExAV pack. It ships no train, station or building
entities; its clock is supplied by the canonical simulation. Random streams are world-owned; economy and gameplay remain later slices.

`ReferenceScenario.Create()` retains the obsolete original-FreeTrain 64×64
scenario for explicitly selected historical regression tests and headless traces.
Its frozen assets retain prototype provenance. Default builds validate their
artifact hashes; the old importer is an explicit historical research tool.
It is never chosen by default
service startup and has no save writer or migration path.

See [P1-01c](../../doc/web-parity-reference-world.md) and [P1-04](../../doc/web-parity-spatial-contracts.md) source and verification evidence.
