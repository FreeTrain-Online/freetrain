# Content boundary

`PreparedContentCatalog.Bundled` validates and indexes the prepared stable ExAV
bundled catalog at server startup. Its 448 contribution records and 141 prepared
picture bindings are inert metadata; executable handlers and complete recipes
remain P2 and later gameplay work. The catalog is embedded with its exact hash,
and original bundled notices ship in managed build/publish output. Default
preparation reads [checked-in canonical inputs](../../content/README.md), never
the historical `core/`/`plugins/` or an implicitly discovered archive/optional pack.
See [P2-01 delivery and limits](../../doc/web-parity-bundled-preparation.md).

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
