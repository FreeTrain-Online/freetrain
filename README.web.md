**P4-05 mixed infrastructure:** [Save8/protocol6 mixed persistence and dispatch](doc/web-parity-railway-persistence.md)
now preserve railway components, booking and shared allocator alongside terrain.
Save1–7 are unsupported and preserved under the current-format-only policy.
[P4-07 construction](doc/web-parity-railway-construction.md) now validates actual terrain, reclamation and automatic piers,
and stages source-priced normal-rail build/removal atomically.
[P4-08 rendering](doc/web-parity-railway-rendering.md) displays saved rails and piers through bundled art,
terrain cuts and English/Japanese inspection.
[P4-09 Track](doc/web-parity-normal-rail-tool.md) now builds/removes normal routes:
choose two map points (or XY coordinates) with automatic start-level detection
or a manual height, preview exact cost/losses,
then confirm. Closest legal route compares two shortest eight-direction paths;
longer detours and roads remain future work. New worlds gain mixed railway state
on their first successful railway confirmation.
[P4-11 Slope](doc/web-parity-slope-tool.md) adds the distinct EN/JA ramp task:
choose a cardinal uphill direction and lower base (map or keyboard), review all
eight occupied cells, actual source cost and pier losses, then confirm. Removal
resolves any member, including invisible fillers, to its whole ramp. Surface/
underground art, stored ground holes and height cuts use the same live renderer.
Flat-to-slope-to-elevated networks save and reopen exactly.
[P4-12 Bridge](doc/web-parity-bridge-tool.md) adds the EN/JA **Bridge** task (**B**):
cardinal endpoint snapping, source begin/middle/end art, alternating top/body piers,
actual inclusive-cell booking and explicit support losses. Compatible rails stay
in place; dry land is allowed. Preview, confirm, remove and save/reopen use the
same authoritative mixed-world boundary.
[P4-13 Girder viaduct](doc/web-parity-girder-tool.md) adds the EN/JA **Girder viaduct** task (**V**):
eight-way straight snapping, every-cell above-ground validation, four exact
before-car orientations and default piers beneath every new rail. Compatible
rails retain their identities. Preview, inclusive source booking, confirmation,
removal and exact save/reopen use the shared special-rail task. Auto selects an
elevated starting height for isolated spans; changing a draft’s height refreshes
its authoritative preview and confirmation.
[P4-14 Tunnel](doc/web-parity-tunnel-tool.md) adds the EN/JA **Tunnel** task (**U**):
cardinal routes through actual Mountains, mixed tunnel/ordinary rail, saved
corner restoration and source before/after artwork. Construction charges only
new cells; removal excludes the destination and preserves ordinary gap rails.
Preview/confirm, underground cuts/picking and exact save/reopen/restoration share
the authoritative mixed-world boundary. Portal selection also accepts one cell
before or inside either mountainside; automatic height can adjust by one level.
The preview shows the resolved path and actual cost before confirmation.
Shape terrain before laying rail;
mixed-world terrain editing remains a later cross-tool step.

# FreeTrain Online

FreeTrain Online is a multiplayer-capable, bilingual reimagination of the rail and business simulation game FreeTrain by Kohsuke Kawaguchi.

The localized Svelte app connects to an authoritative C# session over HTTP and
WebSocket. Without a usable saved checkpoint, the server generates a fresh seeded
400(H)×600(V) terrain world with rolling hills, sparse trees, and a random choice of
dry land, a lake, a river, or a coast. Existing saved worlds restore exactly.
You can inspect terrain, pan/zoom,
check aligned locations, run/pause/step the source-minute calendar and save/restore
with **Save 8**. Economy, construction, trains and stations remain future slices.
Settings offers six departure-bell previews alongside the existing sound effects.
BGM is deferred to a [future improvement project](doc/web-audio-policy.md); the
TinySynth feasibility experiment was removed after audible pops and crackles.
Future music may use digital audio, including reimagined versions of the original
MIDI compositions. English/Japanese text uses a shared language registry with
separate translation files; see [adding a language](doc/web-parity-localization.md)
and [P2 acceptance evidence](doc/web-parity-p2-acceptance.md).
See [P1-02 clock and scheduler](doc/web-parity-clock.md) and
[P1-03 random streams](doc/web-parity-random.md) and
[P1-04 spatial contracts](doc/web-parity-spatial-contracts.md).

[P3-16a capacity](doc/web-parity-map-capacity.md) adds packed terrain/trees and interned definitions, coherent bounded snapshot chunks and viewport caches. The concrete parity target is 1500 × 1090 × 45 stored cells. Larger requests remain subject to measured work/memory/byte/deadline admission. Save 8 is the only supported save format; Saves 1–7 are retired without conversion under the current-format policy. Terrain content identities and source algorithms are unchanged.

[P3 integrated acceptance and P4 handoff](doc/web-parity-p3-acceptance.md) covers
source terrain workflows and the separate seeded-world topology matrix. For
continuous editing, choose **Modify terrain → Terrain selection → Paint terrain**,
select **Raise terrain** or **Lower terrain**, and hold the left mouse button while
moving over the map. Holding still repeats quarter-level changes; Shift + left drag reverses
the action. Release or Escape stops further edits. Painting changes the shared
world immediately; source slope/occupancy/height limits still apply. Existing
coordinate preview/confirmation remains available. Current dense edit latency is
accepted by the owner; the earlier250 ms target is not claimed as met. Native,
deferred-browser and later full-game/import gates keep their recorded status.

Owner/Operator/Observer roles, independent reconnects, bounded queues and epoch
fencing remain in place. Prototype Save 1 is unsupported and cannot be converted.
See [the reference-world delivery](doc/web-parity-reference-world.md).

P3 now has [source terrain semantics](doc/web-parity-terrain-contract.md) and
[world-creation requests, stage seeds and measured admission budgets](doc/web-parity-world-creation-contract.md).
[Canonical terrain state](doc/web-parity-terrain-state.md) now includes ground,
quarter corners, water, decorations and zero-rail worlds. Owners can now use
**Game → New world** for flat empty worlds with name, stored H/V/Z dimensions,
global water, candidate preview and explicit replacement confirmation. [Terrain saves/transport](doc/web-parity-terrain-transport.md)
and [rendering/picking](doc/web-parity-terrain-rendering.md) are implemented;
[P3-06 empty-world creation](doc/web-parity-empty-world.md) is implemented.
Empty H/V dimensions admit 1–2048, at most 1,700,000 stored cells, and Z1–64; Source defaults uses
30 × 42 × 6 stored cells and water1. [P3-07 heightmap creation](doc/web-parity-heightmap.md)
adds opaque single-frame PNG/JPEG/BMP/GIF imports, image/terrain preview and reviewed
installed fixed maps. Heightmaps admit H/V1–2048 (at most 1,700,000 stored cells), Z1–64; image limits are
8 MiB, 4096 pixels per side and 4,194,304 total pixels. Imported terrain reopens
without its image. The [reviewed decoder](doc/web-heightmap-decoder-review.md) is
pinned StbImageSharp 2.30.16 source under MIT, with bounded project-owned allocation
helpers and PNG preview encoding. Server/Docker notices retain its MIT grant;
SkiaSharp managed/native dependencies have been removed. FreeTrain's license is
unchanged. [P3-08 seeded relief](doc/web-parity-seeded-relief.md) adds generated
plains/hills/mountains, seed and relief/scale/smoothness controls, detached preview
and exact confirmation. Generated admission is H/V16–2048 (at most 1,700,000 stored cells), Z8–64, water1…Z−4;
actual terrain and provenance reopen without regeneration. [P3-09 coasts](doc/web-parity-coasts.md) adds islands,
independent world-compass coast directions, sea coverage targets (±5 percentage points),
actual surface-water metrics and bounded topology diagnostics, including full-size fixtures.
[P3-10 lakes and rivers](doc/web-parity-water.md) adds gently irregular lake shores and smooth fractal river bends at the global water level, lake count/area and river direction/width/meander controls (None, North → South, East → West; matching coastal seas connect at the map boundary), EN/JA feedback and exact Save8 confirmation. Forests and starting areas are delivered in [P3-11](doc/web-parity-forests.md): six species/18 ground treatments, source scatter, coverage/clustering/palette controls, connected level reservation and actual preview metrics. [P3-12 generation UX](doc/web-parity-generation-ux.md) adds retained north-up/isometric previews, compass, measured presets and generation lifecycle feedback.
[P3-13 corner tools](doc/web-parity-corner-terrain.md) add **Game → Modify terrain**:
Raise/Lower, source ground/corner picking, Ctrl reversal, Shift-drag or explicit
coordinate strokes, read-only legality/loss preview and one revalidated Confirm.
[Localized redraw](doc/web-parity-terrain-rendering.md#localized-terrain-redraw--1-november-2026)
keeps validated snapshot identity and repaints changed terrain plus overlapping scenery.
[P3-14 rectangle tools](doc/web-parity-rectangle-terrain.md) add normalized two-point
or coordinate rectangles, source Raise/Excavate profiles, explicit Repair,
Ctrl repair/Shift upper profile, per-site skips and one atomic confirmation.
Selections admit at most 64 inclusive cells; Repair includes the far edges while
Raise/Excavate operate on shared corners before those edges.
[P3-15 terraces and cliffs](doc/web-parity-terraces.md) add separate Place/Remove terrace and Make/Restore cut actions, source voxel targeting, exact corner backups and explicit forest/ownership-loss previews. Changes preserve actual Save8 continuation. Full-size editing has [P3-17 acceptance](doc/web-parity-p3-acceptance.md);
resource-heavy authoritative edits retain the documented 250 ms target miss.

## Parity source policy

The full-game parity target is stable **FreeTrain ExAV 2.1.1.12**, archive
[https://github.com/rdmark/freetrain-exav-archive].
The regression-only spatial reference uses source normal rail patterns. The connection screen
shows a separate synthetic 48×96 coastal terrain landscape, using canonical
quarter slopes, water, forests and terraces, plus a decorative coastal railway
loop and locally animated train. The lowest beach level is tree-free; the demo
has no creek. It never enters the game session or save path. Historical prototype assets retain prototype provenance. [P2-01 canonical preparation](doc/web-parity-bundled-preparation.md)
now supplies pinned bundled inputs, converted surfaces and a prepared metadata
registry. [P2-02 executable recipes and gallery](doc/web-parity-recipes.md)
and [P2-03 runtime plugin loading](doc/web-parity-plugin-startup.md) are implemented;
[remaining acceptance and deferrals](doc/web-parity-p2-acceptance.md) are tracked separately. P0-01 ExAV
discovery, P0-02 content and P0-03 workflow/scope reconciliation, P0-04/P0-05 source
specifications and [P0-06 scenario specification](doc/web-parity-scenarios.md) are
complete. [P0-07 coverage and phase estimates](doc/web-parity-report.md) are published;
[P0-08 optional V2 source review and launch/credits specification](doc/web-parity-optional-plugins.md)
is complete. [P0-09 authority/scope integration](doc/web-parity-exav-integration.md)
is complete at source level. [P0 coverage is closed](doc/web-parity-phase-gates.md); P1 and runtime/native acceptance remain open. See
[the source inventory](doc/web-parity-exav-inventory.md) and
[maintained source policy](doc/web-feature-parity.md).

Keep LGPL bundled content separate from the non-LGPL V2 additional set, canonical
[https://github.com/rdmark/freetrain-online-plugins-nonfree]. Startup support
uses explicit repeatable `--plugin-dir` arguments and adds a localized credits button
for each enabled set, with credits supplied by XML in that separate repository.
See [the implemented loading/credits interface and source diagnostics](doc/web-parity-plugin-startup.md).
Full canonical V2 adapter/resource repairs and historical-suite acceptance remain
postponed in the separate repository.

## Prerequisites and checks

Install the exact .NET SDK in `global.json` (10.0.402) and Deno in
`.deno-version` (2.9.7). See the official
[.NET download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and
[Deno installation guide](https://docs.deno.com/runtime/getting_started/installation/).

Frontend dependencies and tasks are defined in `web/deno.json`; `web/deno.lock`
pins transitive versions and integrity hashes. Python 3 (standard library only)
is also required to prepare/check the canonical bundled content.

The solution to build is `FreeTrain.Online.slnx`.

When updating an existing installation, use `FreeTrainOnline__…` for server
environment variables and `FREETRAIN_ONLINE_…` for deployment/frontend tooling.
The image is now `freetrain-online:prototype`. Reuse your existing save directory
or set `FREETRAIN_ONLINE_SAVE_VOLUME` to the existing Docker volume. Prototype
saves are rejected and preserved; the default world now writes canonical Save 8. Browser login and preference keys use `freetrain-online`;
sign in again and reselect language/color preferences after upgrading.

From the repository root:

```sh
./scripts/check-web-prototype.sh
```

This checks project boundaries, restores .NET packages in locked mode, builds
Release, runs headless tests, installs frontend packages with `deno install --frozen`, validates
shared fixtures, checks generated types and Svelte/TypeScript, and builds static
files into `web/dist/`.

For browser interaction checks after the build:

```sh
cd web
deno task install:browser
deno task test:browser
```

These tests build the Svelte shell into `web/dist/`, run
Chromium against those release static files, and capture English/Japanese
screenshots in `web/test-results/`. The `canonical` project exercises the default server, while `prototype-regression`
explicitly starts a loopback-only `--prototype-regression-harness`. These tests do
not establish the final cross-platform browser matrix or native IME behavior.

## Local development

In separate terminals:

```sh
dotnet run --project src/FreeTrain.Online.Server --no-launch-profile
```

```sh
deno task --cwd web dev
```

Open the Vite loopback URL printed in the second terminal. The server binds to
`http://127.0.0.1:5080`; Vite proxies `/api` HTTP and WebSocket traffic to it.
`GET /health` reports service health and automatic-checkpoint errors. The live map
uses source H/V terrain geometry. Each participant has
one active socket; tabs sharing cookies share that identity. Local browsers
receive independent Owner identities. Train/construction operations are
unavailable until their canonical implementations arrive.

Fresh-world defaults can be configured with `FreeTrainOnline__StartupWorld__…`:
`Width` (H: 400), `Height` (V: 600), `Hills` (35, range 0–100), `TreesPercent` (2, range
0–90), and `Water` (`Random`, `Dry`, `Lake`, `River`, or `Coast`). Set `Seed` to an
integer for reproducible terrain; omit it for a fresh random seed. These settings
only apply when there is no usable checkpoint. The chosen seed and generation
parameters are retained in Save 8 along with the actual map. The old rail-loop
map remains an explicit regression fixture; it is no longer a startup world.

For a remote binding, configure `FreeTrainOnline__OperatorSecret` on the server. The
browser exchanges the access key for an HttpOnly, SameSite cookie. Keep the static
client and service behind one HTTPS origin. The release container serves both;
Caddy provides HTTPS/WSS. Additional remote participants use separate `FreeTrainOnline__Participants__0__Id`,
`__Role` (`owner`, `operator`, `observer`) and `__Secret` settings.
Save/load mutations require an owner. **Leave game** revokes this login and returns
to sign-in, allowing you to connect with another participant key.
For a complete local setup with Owner, Operator and Observer clients, see
[multiplayer operation guide](doc/web-multiplayer-operation.md).
No secret belongs in frontend configuration. Optional explicit
origins use `FreeTrainOnline__AllowedOrigins__0`; lifecycle and queue settings use
`FreeTrainOnline__Network__<Property>`. See
[NetworkOptions](src/FreeTrain.Online.Server/NetworkSession.cs) for defaults and limits.

Save 8 checkpoints run every 30 seconds and at clean shutdown. A valid checkpoint
restores the spatial world after restart; the server incarnation changes. Rejected
startup checkpoints are reported and preserved, including through later automatic
and shutdown attempts. Named slots and downloads remain available for the fresh
canonical world. Uncertain command outcomes are never replayed across connections.
`web/dist/` contains the Svelte static artifact; production needs no frontend process.

If using the workspace-local SDK installed during the first implementation,
prepend `$PWD/.tools/dotnet` to `PATH`. `.tools/` is ignored and not required by
the source. In a restricted execution environment, set `DOTNET_CLI_HOME` to a
writable temporary directory; .NET build/test and local web servers also need
permission to create local sockets.

## Playing the map

The map fills the viewport. Click a rail to inspect its source normal-pattern
index and stable location; drag to pan and use the wheel or zoom buttons.
Middle drag or Space + drag pans in every tool. **Check a location** offers level
selection and Free / Four directions / Eight directions alignment with an optional
anchor. The server verifies H/V bounds and projection without changing the world.
Construction confirmation and removal are disabled. Run/Pause/Step advance only
the source-minute calendar; gameplay consumers remain future slices.

Game panels preserve canvas/camera identity. **View** offers height cut and local
appearance controls. **Settings** offers English/Japanese and four persisted color
schemes. Narrow panels support Expand / Reduce and internal scrolling. Toolbars
support Tab, arrows, Home and End. Before joining or after disconnect, the session
dialog appears over a separate inert demo map. A failed Leave offers Retry leave.

Save/upload replacement requires Owner access and explicit confirmation. File
validation rejects unsupported inputs before a replacement modal is shown.
The explicit historical regression harness retains station/controller/construction
UI tests; those results do not establish canonical gameplay behavior.

Prepare/check bundled content:

```sh
deno task --cwd web import:content
deno task --cwd web check:content
```

These tasks use the Python standard library and [checked-in stable ExAV inputs](content/README.md).
They validate pinned source hashes and produce identical server/browser catalogs,
213 native-size bitmap sheets with day/night PNGs, 159 seasonal/code picture bindings,
other native media and original notices under `web/public/content/bundled-v2/`.
The bundle has 812 executable art recipes, source effects/anchors and active
variant masks. Gameplay registrations remain separate. Generated media preserve
`plugins/<original-plugin-id>/` and distribute per-image provenance and plugin
metadata. Open `/content-gallery.html` for read-only recipe/variant inspection.
See [P2-02 attribution, rendering and acceptance limits](doc/web-parity-recipes.md).

Default builds and Docker need no archive checkout, historical `core/`/`plugins/`
trees or optional repository. To refresh or verify the source bundle explicitly:

```sh
python3 scripts/prepare-bundled-content.py --source-root /path/to/freetrain-exav-archive
python3 scripts/prepare-bundled-content.py --source-root /path/to/freetrain-exav-archive --check
```

The regression-only synthetic rail world retains its distinct pack and Save 8 identity.
Historical demo/regression PNGs, variants, masks and notices in `web/public/assets/`
remain frozen artifacts checked by `prototype/content-artifacts.json`; they no
longer trigger historical source import during normal builds. Browser decode
stays outside the paint loop. Build checks reject generated drift and stale files.

## Saves and sound

**Game → Settings** contains language and gesture-enabled sound.
**Session** (also in Game on narrow screens) contains reconnect and Leave game.
**Credits**, available in the Game menu and the connect dialog, acknowledges the
original author and contributors, and the English version.
**License**, beside Credits in both places, displays the full LGPL 2.1 text and
links to the source code on GitHub.

**Only canonical Save 8 is supported.** Protocol 6 also carries actual terrain,
realized vegetation, authoring mode and creation provenance under the exact bundled
terrain profile. Terrain save/load/restart, transport and browser rendering/picking
are implemented, including [empty-world creation](doc/web-parity-empty-world.md). The application starts with generated terrain. Save/upload and outgoing terrain data have a 256 MiB limit.

Save slot and Download save capture the validated H/V rail graph, stable entities/footprints and allocator, source-minute clock, paused state and all versioned random streams. Load/upload validates dimensions,
unique occupied cells, source patterns and exact ruleset/content/pack identity.
The synthetic reference profile additionally requires reciprocal loop connectivity
and all eight headings; mixed railway state allows open and disconnected networks.
All profile checks complete before replacing the world and advancing
the epoch. Earlier formats, including Saves 1–7, return `incompatibleSave` without
conversion; the current world and rejected files are preserved.

`FreeTrainOnline__Saves__Directory` selects the save directory. An incompatible
startup checkpoint produces a visible error and a fresh world; the rejected file
is never overwritten. `/api/saves/status` reports availability and startup errors.
See [the save contract](prototype/protocol/save-v8.schema.json) and
[current policy](doc/web-parity-runtime-policy.md).

Click **Enable sound** to preview the bundled station WAV and enable sound for
new station-arrival events in the historical harness. The canonical world has no trains yet. Reconnects and refreshes do not replay old events.
**Disable sound** mutes it; refresh starts muted.

## Release deployment and profiling

```sh
./scripts/build-web-release.sh
PLAYWRIGHT_BROWSERS_PATH=/tmp/freetrain-online-playwright deno task --cwd web test:deployment
PLAYWRIGHT_BROWSERS_PATH=/tmp/freetrain-online-playwright deno task --cwd web profile
```

Release image/static archives and checksums go into `tmp/release/`. Deployment
checks use a disposable Compose project, Linux x64 runtime, one HTTPS/WSS origin,
and persistent test volumes. The [runbook](doc/web-deployment.md)
covers configuration, local certificates, controlled public hosting, health,
updates, shutdown and independent backup restoration. Performance evidence lives
in `tmp/`.

If a development server already uses browser-test ports, set
`FREETRAIN_ONLINE_TEST_SERVER_PORT=5182 FREETRAIN_ONLINE_TEST_WEB_PORT=4182` when running
`deno task --cwd web test:browser`. The canonical test servers use the next port
number in each pair (defaults 5081/4174). `FREETRAIN_ONLINE_API_TARGET` changes the Vite
development/preview proxy target only; the production client uses its own origin.
Browser tests use Release server builds by default. To check development-build
latency and connection behavior, build the server with `-c Debug` and set
`FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug` for the browser run.

## Headless scenario

Run the historical prototype 10,000-tick scenario without starting an HTTP listener:

```sh
dotnet run --project src/FreeTrain.Online.Server -c Release --no-build --no-restore -- --headless-trace 10000
```

Append an output path to write full per-tick state/event NDJSON. The command
prints counts and a SHA-256. The .NET test suite compares two complete
10,000-tick traces and checks movement legality,
station timing, manual control, accounting atomicity and host isolation. These
are historical regression checks, not canonical gameplay/capacity evidence.

## Contract changes

The spatial integration now uses **protocol 6**, with explicit ruleset/content/pack
identity, coordinate-system tags and per-car eight-direction heading indices.
Restart/deploy matching artifacts together; protocol 2/3 peers receive HTTP 426.
The default profile is `reference-spatial-4` / `reference-spatial-content-2`,
with H/V bounds, `normal0`..`normal11` track patterns and canonical Save 8. Unknown
profiles/packs block reveal. See [P1-01c](doc/web-parity-reference-world.md) and [terrain save/transport scope](doc/web-parity-terrain-transport.md).

Edit `prototype/protocol/schema.json`, run
`deno task --cwd web generate:contracts`, and update the C# DTOs and shared
fixtures together. See [the protocol notes](prototype/protocol/README.md) and
[scenario behavior cases](prototype/scenarios/README.md). Keep protocol, content,
and save versions independent.

To change frontend dependency versions, edit `web/deno.json`, then run:

```sh
deno install --config web/deno.json --frozen=false
```

Commit the updated `web/deno.lock` together with the dependency changes. Ordinary
installs and tasks use the frozen lockfile and fail if it is out of date.

### Optional plugin directories

The executable accepts repeatable `--plugin-dir /path/to/plugin-set` arguments.
Each directory contains `plugin-set.xml` source metadata or a prepared
`prepared-pack.json` distribution. Optional content, credits, notices and cache
remain with the separate plugin distribution; normal builds require none of it.
See [runtime configuration, external preparation and current full-suite limits](doc/web-parity-plugin-startup.md).
