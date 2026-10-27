# P2-01: canonical bundled inputs and preparation — 25 October 2026

**26 October follow-up:** [P2-02 recipe rendering and attribution](web-parity-recipes.md)
advances prepared outputs to v2/schema 2, binds all 159 pictures and preserves
plugin namespaces in generated media. The v1 counts/hash/layout below record
the original P2-01 delivery.

P2 has started with reproducible stable ExAV inputs, resource conversion and an
inert prepared registry. Default frontend/container builds no longer read this
repository's historical `core/` or `plugins/`, Git metadata or an archive/sibling
checkout. Full P2 content/recipe/rendering acceptance remains open.

## Source and prepared identities

Stable ExAV 2.1.1.12 / V2, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`, remains the authority.
[Checked-in inputs](../content/README.md) select files from pinned Git blobs,
validate them against the P0 file ledger, normalize 56 manifests to UTF-8 under
EXAV-ENC-001 and compare their expanded ordered declarations with P0's review.
Dirty archive corrections do not replace original source hashes. SOUTH remains
the approved parser policy; this slice does not implement generic direction or
gameplay handlers. No optional content repository is read.

| Prepared accounting | Result |
| --- | ---: |
| Source files / UTF-8 expanded manifests | 386 / 56 |
| Reviewed plugins / selected plugins | 56 / 54 |
| Direct declarations / factory definitions | 482 / 33 |
| In-scope ordinary declarations / generated primitives | 447 / 1 |
| Exact contribution registry entries | 448 |
| Picture identities / prepared manifest picture bindings | 159 / 141 |
| Loose media / bitmap sheets | 238 / 213 |
| Retained native resources, including three MIDI files | 25 |
| Preserved distribution notices | 4 |

The prepared pack is **`exav-stable-bundled` v1**. Its byte-exact catalog SHA-256
is `0a4c25b580db697bcac491fc3999c476ff8e9d8100da2a8e30ce117f769f6df6`;
the source index SHA-256 is
`583c8159ebd299a2ffa5117a073d6c6daf2edff94f9ccf44608028f03cbd48e1`.
Engine, pack version, catalog hash, world ruleset, protocol and save versions
remain separate axes. The default world still selects `reference-spatial-4`,
content `reference-spatial-content-2`, synthetic `reference-spatial` v2 and Save 5.
Prepared definitions never silently replace that world's catalog.

## Conversion and registry behavior

[Preparation](../scripts/prepare-bundled-content.py) validates all input hashes,
ordered declarations, dependency closure and topological order. Exact original
contribution IDs, picture IDs and separate factory metadata are preserved.
Generated `null-contrib-0:0` and the ten direction/reversal selections stay
explicit; heights and effects are not fabricated registered children. Duplicate
IDs, missing/cyclic dependencies, ambiguous case-insensitive resource paths,
traversal and unreviewed input files fail before output generation.

[The decoder](../scripts/content_bitmap.py) supports reviewed uncompressed 4/8/24
bit BMPs, row padding, palette bounds, odd indexed widths and top-down storage.
All 213 sheets become native-size, color-keyed RGBA PNGs. The explicit RGB888
night policy uses source quarter-darkening and the three light colors. Color
keying happens after the transform, preserving transformed-key collisions.
This is a portable pixel policy, not native 555/565 or desktop-capture evidence.
Arbitrary tint/channel swaps from the historical sample are not canonical effects.

All 141 manifest picture bindings preserve source season inheritance and the
first-child-only night-override rule. A season's authored day without a night
override gets its own automatic night; an unspecified season inherits spring.
Browser media names are content hashes. Every output records its hash, byte size,
original path/hash, reachability and credit/notice evidence. Non-BMP UI images,
WAV and MIDI bytes are retained; retaining MIDI is not browser playback support.

[The server registry](../src/FreeTrain.Online.Content/PreparedContentCatalog.cs)
loads the embedded catalog/hash, validates its pin, dependencies and identities,
and provides immutable, exact pack-qualified metadata lookups. It never activates
class/codebase strings. Every definition has `gameplay-unavailable` status. The
server validates this prepared data at startup without registering it as a
playable `WorldProfile`. Server and browser catalog bytes/hashes must agree.

Normal build checks use the checked-in bundle. Docker copies only that bundled
input root; historical `core/` and `plugins/` are excluded from its build context.
The License dialog reads the byte-identical LGPL text from the canonical inputs.
Original notices also ship with managed build/publish output and static media.

Historical demo/regression assets remain under their original identity and
notices. Their [frozen artifact manifest](../prototype/content-artifacts.json)
checks bytes without opening old source trees. The old importer and historical
gallery tools remain explicit research/reproduction tools; they supply no
canonical definitions or default import/build inputs.

## Verification

Working tree based on `c274112`, 25 October 2026. **273 .NET, 85 client and 115
Python cases pass**, including malformed catalog rejection, source/input drift,
resource paths, independent pixel vectors, dependency cycles and clean builds.
Frontend production build, generated contracts/content, Svelte/TypeScript and
project-boundary checks pass. Pinned archive verification also passes.
**68 Chromium cases pass: 14 canonical / 54 historical regression.** The new
canonical case fetches the byte-hashed catalog and browser-decodes real day/night
rail sheets while preserving the selected world and canvas. The final production
build has byte-identical JS/CSS after the license import-path correction.
The final 14-case canonical rerun also passes after the checksum MIME mapping.

The Linux amd64 Docker image builds and passes the real container check on an
ephemeral loopback port with temporary save storage. All **238 media records /
447 unique media URLs**, four static notices and four managed publish notices
match their hashes. Health is `ok` and the authenticated bootstrap still selects
the synthetic reference profile. The validation container is removed afterward.

One intermediate .NET run hit a connection reset in the existing oversized-save
HTTP case; the final full rerun passes all 273 cases without changing that test
or its save/network implementation. Initial .NET test execution requires local
socket permission, as do the browser servers.

Local logs are `tmp/p2-01-{prepare,source-check,client,python-final,dotnet-final,web-build,browser,browser-final,docker,container,prepared-final}.log`.
The full browser run's 54 historical cases are reused unchanged alongside the
final canonical rerun. Browser artifacts are under `tmp/p2-01-browser-evidence/`
and `tmp/p2-01-browser-final-evidence/`; surface review artifacts are under
`tmp/p2-01-gallery/`. These are source-derived/portable and Chromium/container
checks, not native-device, original-runtime or full-city capacity evidence.

Reproduce the preparation and source checks using [the input instructions](../content/README.md).
Other checks:

```sh
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
dotnet test FreeTrain.Online.slnx -c Release --no-restore --disable-build-servers -m:1
deno task --cwd web test
deno task --cwd web build
python3 scripts/prepare-bundled-content.py --check --gallery tmp/p2-01-gallery
docker build --platform linux/amd64 -f deploy/Dockerfile -t freetrain-online:p2-01 .
python3 scripts/check-bundled-deployment.py --image freetrain-online:p2-01
```

The gallery contains all 213 day/night source surfaces. It is an offline review
aid, not the full recipe/variant gallery required by P2. Three contact sheets
covering all surfaces were visually inspected. Bitmap tests use independent
literal indexed/RGB fixtures, light/color-key vectors and decompressed PNG rows.
The minimal-build test copies only declared inputs and frozen artifacts to a
temporary root with no Git, `doc/`, historical `core/`/`plugins/`, archive or
optional checkout, then runs the real preparation check.

The initial Docker build exposed the pre-existing License dialog root-file
dependency absent from its copy list. Its import now uses the byte-identical
pinned bundled license. This correction does not change the displayed text.
The container HTTP check additionally found that ASP.NET's static-file provider
did not recognize `.sha256`; deployment now explicitly maps that extension to
`text/plain` so catalog checksums are available in production as well as preview.

## Remaining P2 work

P2-01 delivers **prepared resources and metadata**, not complete recipe execution.
Keep these concrete follow-ups open:

- Port code-loaded picture bindings (18 identities), sprite loaders, 0D/2D/3D
  slicing, variable-height/half-voxel and specialized land/road/rail/train recipes.
  Connect actual render/pick passes and anchors to the world/asset-readiness path.
- Implement source color/hue/alpha/night effects and resolve native/source
  anomalies, including EXAV-GAL-001, with explicit fidelity decisions.
- Add trusted executable contribution registrations and source field validation,
  including V2 fare/amenity/seated capacity, alongside their implementing systems.
  Prepared XML and qualified class bindings do not constitute those handlers.
- Resolve or disposition five source project-resource gaps and twelve inert
  embedded-resource records; deliver the full recipe/variant review gallery.
- Retain MIDI source/provenance without a playback requirement; [BGM is deferred](web-audio-policy.md). Maintain sound effects and bundled bell previews independently.
- Deliver separate optional-pack conversion/build/startup selection, collision and
  dependency diagnostics, localized per-set credits and default-cache separation.
- Review English/Japanese presentation with each delivered feature; the 977 P0
  text candidates remain candidates. Source asset conversion adds no player forms.

Terrain, construction, train/station/city/economy consumers, complete-world
continuation, historical imports, legal-city capacity and native/device UX stay
with P3–P11 and the existing UX handoff. P1's final evidence/coverage review also
remains distinct from this P2 source/build delivery.
