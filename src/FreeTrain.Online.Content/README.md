# Content boundary

`SpatialReferenceScenario.Create()` builds the default canonical reference world:
32 source-valid normal rail cells forming an eight-heading loop, on a 32×64×8
H/V map. `reference-spatial.json` contains the twelve source normal-pattern port
pairs and defines the asset-free spatial pack hash. This is synthetic reference
content, not the complete bundled ExAV pack. It ships no train, station or building
entities; its clock is supplied by the canonical simulation. Economy, random
streams and gameplay remain later slices.

`ReferenceScenario.Create()` retains the obsolete original-FreeTrain 64×64
scenario for explicitly selected historical regression tests and headless traces.
Its assets/importer retain prototype provenance. It is never chosen by default
service startup and has no save writer or migration path.

See [P1-01c source and verification evidence](../../doc/web-parity-reference-world.md).
