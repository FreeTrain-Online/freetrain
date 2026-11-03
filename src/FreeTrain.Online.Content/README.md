# Content boundary

`PreparedContentCatalog.Bundled` validates and indexes the prepared stable ExAV
bundled catalog at server startup. Its 448 contribution records remain inert
for gameplay. All 159 picture bindings and 813 browser render recipes are
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

`OptionalPluginRegistry` validates explicitly selected external prepared directories,
composes exact plugin dependencies/IDs, publishes authoritative pack descriptors
and serves only hashed manifest-listed content. `PluginStartup` supplies automatic
source-set preparation and repeatable `--plugin-dir` arguments. Optional originals,
converted output and localized credits stay in their separate repository/cache.
See [P2-03 and the full-suite conversion boundary](../../doc/web-parity-plugin-startup.md).

`TerrainProfiles.Bundled` registers the exact prepared ground/forest/Terrace/Creek
bindings for the separate terrain state/save/transport profile. `TerrainTransportScenario`
is a deterministic persistence/transport fixture, never a startup world or generator.
See [P3-04](../../doc/web-parity-terrain-transport.md); terrain browser rendering and
creation remain P3-05/06 work.

`RailwayProfiles.Bundled` registers P4-03's 87 exact railway **state** bindings
against the prepared pack. It does not enable live railway operations, rendering
or save/wire dispatch. See [mixed-state handoff](../../doc/web-parity-railway-state.md).

`RollingStockCatalog.Bundled` registers exact typed factories alongside prepared
art: six trains, eight cars and seven color templates. Server startup registers
`OptionalPluginRegistry.RollingStock` for its explicitly selected pack set;
bundled registration remains separate. Immutable formation creation and quotes
enforce source compositions, checked numeric fields and admission limits. This
supplies P5-04 fleet work; it enables no occupancy or purchase command by itself.
See [P5-03 bindings, limits and checks](../../doc/web-parity-rolling-stock.md).
