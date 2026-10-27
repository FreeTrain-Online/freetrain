# P2-03 runtime plugin directories — 27 October 2026

The service now accepts repeatable `--plugin-dir` options. Each directory is either
a source plugin set with `plugin-set.xml`, or a prepared distribution with
`prepared-pack.json`. Sources, original notices, credits, converted media and caches
stay in the separate plugin repository/distribution. Default startup never searches
siblings, reads an optional cache, downloads content or invokes Python.

This implements the loading/preparation architecture. **P2-03 remains open for
full canonical V2 conversion and historical-suite acceptance.** The full canonical
set is deliberately rejected while its required recipe adapters and missing
resources are unresolved. Metadata discovery is not gameplay support. The live
reference world and current Save 5 profile remain asset-free; pack-dependent game
systems and saves belong to their implementing phases and P10.

## Operator interface

```sh
dotnet run --project src/FreeTrain.Online.Server -- \
  --plugin-dir /path/to/freetrain-online-plugins-nonfree \
  --plugin-dir /path/to/another-compatible-set
```

Paths are explicit and repeatable, including `--plugin-dir=/path/with spaces` when
quoted by the shell. No path is hard-coded. Configuration arrays also work:
`FreeTrainOnline__PluginDirectories__0`, `__1`, etc. An empty list is base-only.
Duplicate sets, plugin directory names, factory/contribution/generated IDs,
missing or cyclic dependencies, unported implementations and missing/ambiguous
resources fail startup before the session or catalog endpoints are published.
Diagnostics name the source manifest and contribution. All legacy DLLs stay inert.

Source sets use Python 3 and the same portable bitmap/recipe code as bundled
preparation. The managed publish includes that code, the pinned bundled catalog
and bundled bitmap inputs. The release image includes Python 3; loading a prepared
set itself uses only the managed reader. Preparation is automatic at startup;
there is no required core rebuild or separate manual conversion operation.

By default, a source set's immutable cache is at
`<set>/.freetrain-online/prepared/<content-key>/`. For a read-only source mount,
configure `FreeTrainOnline__PluginCacheDirectory=/data/plugin-cache`. This is an
operator-selected external cache; core/web build trees are never optional output
targets. Offline preparation uses the identical converter:

```sh
python3 scripts/prepare-optional-content.py \
  --plugin-dir /path/to/set-one --plugin-dir /path/to/set-two \
  --output-dir /path/to/separate-distribution
# Prints the prepared directory paths to pass to --plugin-dir.
```

The converter keys output by all selected source bytes (including localized credits
and notices), bundled/reference catalog hashes and converter source hashes. It
validates every selected set before publishing any immutable cache directory,
uses staging plus atomic rename and verifies reused output bytes. Original bitmap
bytes needed for subsequent cross-pack preparation stay with the optional prepared
distribution under `sources/`; browser PNGs and effect derivatives stay separately
under their original plugin namespaces. Collection and plugin notice candidates
retain their exact bytes and UTF-8/Shift-JIS response encoding. No optional output is written to `web/public`, bundled
inputs, managed embedded resources or normal release layers.

Prepared plus source sets may be mixed. Prepared catalogs supply exact dependency
requirements and source-media hashes for a source set using their art; all referenced
sets must be enabled together. Independent directory order gives identical output.

## Set metadata and source families

`plugin-set.xml` is UTF-8, schema 1. It requires an ID matching
`[a-z0-9][a-z0-9._-]{0,95}`, version, immediate-child manifest root, English/Japanese
plain-text credits with a valid default locale, and at least one original notice.
The [metadata example](web-parity-optional-plugins.md#owner-directed-launch-and-credits-contract)
now describes the implemented shape. `profile` and `sourceRevision` preserve source
family/provenance; source-byte hashes prevent a mutable checkout masquerading as
an unchanged pack. Nested manifests are retained as source evidence and never
activated a second time. The original Japanese declarations remain in the catalog.

Canonical V2 manifests default to UTF-8. A **separate** historical set can explicitly
specify `<plugins root="PluginsExtra" discovery="immediate-children" encoding="cp932"/>`
with its own set ID/profile/version. There is no automatic V1/V2 blending or aliasing.
This accepts legacy input encoding; it does not certify all historical recipes.
Internal XML entities use the P0 scanner's bounded inert expansion semantics.
External DTDs, parameter entities, recursive/oversized entities and unconstrained
external XML access are rejected. A local XML fragment requires an explicit
`<xml-include manifest="plugins/example/plugin.xml" entity="part"
path="plugins/example/part.xml"/>` in the set metadata. The six canonical includes
are declared in the separate repository's metadata. No network/absolute XML resolver
or assembly loader is used. Relative resources may traverse to another selected
plugin within the set, or an exact pinned bundled resource; escaping paths and
ambiguous case matches are rejected.

The non-free repo now contains its own `plugin-set.xml`, localized collection
credits (including all original Japanese acknowledgments and site addresses),
original notice references, and the explicit local-include list. Original manifests,
images, binaries and historical Git pins are preserved. No non-free originals,
derivatives or localized credit text have been added to this repository.

## Server/browser authority and credits

`/api/content` publishes the selected descriptors, exact catalog/pack hashes,
source profiles, credits and notice links. Only manifest-listed files are served,
under `/api/content/<set-id>/<content-key>/...`; local paths, source metadata files
outside the allowlist and arbitrary directory listings are not exposed. Managed
startup revalidates every prepared file hash/length, registries, dependency graph,
render bindings, owned definitions, original notices and media URLs. File responses
recheck bytes to reject a mount modified after startup.

Bootstrap advertises `X-FreeTrain-Content-Set`. The browser fetches that exact set,
verifies every selected catalog's bytes/hash and render bindings, then presents the
set hash on WebSocket join and spatial queries. A missing/stale hash is rejected
when optional packs are enabled. The browser cannot add packs independently.
Base-only older callers remain compatible; the current client also negotiates the
empty optional set. These authoring packs do not change the world's gameplay profile.

Each enabled set adds one localized Game-menu credits button. Its own plain-text
dialog preserves paragraphs and links to its notices. Missing UI languages fall
back to the pack's default. Existing modal focus, keyboard close, inertness,
interaction reset and narrow-screen scrolling are reused. Base-only mode adds no
optional buttons. Rich text and arbitrary plugin HTML/code are never rendered.

## Full-suite conversion boundary

Fresh startup validation enumerates the canonical **450 immediate-child manifests**,
**11,470 declarations** and generated identity space, preserving the two nested
copies as source-only. It fails before producing a cache. Current preflight reports
**1,318 GenericStructure adapter requirements, 640 other implementation requirements,
26 missing picture bindings and two duplicate-class declarations** (1,986 diagnostic
records). These are startup diagnostics, not an additional parity denominator;
subsequent recipe compilation can expose further issues once preflight is repaired.
The earlier P0 inventory's 27 missing image edges include repeated/indirect edges
and have a different denominator.

Remaining work includes generated structure/selection execution, PatternTrain and
ColoredTrain variants, road accessories/dummy cars and signal dispatch, the binary-only
map-capture disposition, approved missing-resource repairs and complete recipe/fidelity
coverage. Full historical-suite conversion is also unverified. There is no inferred
asset replacement, blanket adapter based on an assembly filename, or silent subset
activation. These requirements remain recorded in P2-03/EXTRA-06 and later feature
phases. P2-03 must not be marked fully accepted from synthetic fixtures alone.

## Verification

Fresh checks on 27 October: **287 .NET, 133 Python and 93 client tests pass**;
**15 canonical Chromium cases (including all 6,496 bundled recipe variants),
54 historical Chromium cases, three optional source-startup and three optional
prepared-startup cases pass**. Production frontend/managed builds, generated
content/contracts, project boundaries and whitespace checks pass. The prepared
browser run removes the original source directories before starting the service.

The `freetrain-online:p2-03` image (`sha256:79ac98ec1fec11d3183ba00788490bfc49600cf624faf0ab7e3664460f35ecc8`)
passes read-only source mounts with an external writable cache, read-only prepared
mounts and base-only restart after both optional flows. Source and offline-prepared
fixtures advertise the same set hash. Original CP932 notice bytes and Shift-JIS
response headers pass. The application tree contains no optional set metadata;
base mode advertises zero optional packs. Bundled deployment validation also passes
for 238 assets, 505 unique media outputs, four notices and the reference world.
The bundled catalog hash remains `754fa756b35a01b1e6ab0f33013f2b119c6ff3855502cd480b6de2fade85b0e5`.

The original synthetic fixtures exercise source startup, immutable prepared startup
with the source directories removed, mixed prepared/source cross-pack art,
deterministic dependencies/collisions, bounded XML, explicit CP932 profiles,
cache corruption, runtime file tampering, hash-fenced WebSocket joins, English/Japanese
credits, keyboard/focus return, narrow layout and changed-catalog rejection.
They contain no non-free content and never discover a sibling repository.

```sh
python3 -m unittest discover -s scripts/tests
dotnet build FreeTrain.Online.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore -m:1
deno task --cwd web test
deno task --cwd web build
python3 scripts/check-optional-deployment.py --image freetrain-online:p2-03
cd web
deno run -A npm:@playwright/test@1.64.1 test --config playwright.optional.config.ts
FREETRAIN_ONLINE_OPTIONAL_PREPARED=1 deno run -A npm:@playwright/test@1.64.1 test --config playwright.optional.config.ts
```

Native/device acceptance, full optional gameplay/save continuation and full collection
redistribution/derivative-permission review retain their existing phase owners.
