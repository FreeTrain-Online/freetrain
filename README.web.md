# FreeTrain Online

FreeTrain Online is a multiplayer-capable, bilingual reimagination of the rail and business simulation game FreeTrain by Kohsuke Kawaguchi.

The bilingual Svelte app connects to an authoritative C# session over HTTP and
WebSocket. Its default world is now a canonical H/V spatial reference: a small
32-cell rail loop covering all eight headings. You can inspect rails, pan/zoom,
check aligned locations and save/restore the spatial graph with **Save 2**.
Clock, economy, construction, trains and stations remain future slices.

Owner/Operator/Observer roles, independent reconnects, bounded queues and epoch
fencing remain in place. Prototype Save 1 is unsupported and cannot be converted.
See [the reference-world delivery](doc/web-parity-reference-world.md).

## Parity source policy

The full-game parity target is stable **FreeTrain ExAV 2.1.1.12**, archive
[https://github.com/rdmark/freetrain-exav-archive].
The spatial reference uses source normal rail patterns. The historical demo
assets and importer retain prototype provenance; complete ExAV content conversion
remains P2 work. P0-01 ExAV
discovery, P0-02 content and P0-03 workflow/scope reconciliation, P0-04/P0-05 source
specifications and [P0-06 scenario specification](doc/web-parity-scenarios.md) are
complete. [P0-07 coverage and phase estimates](doc/web-parity-report.md) are published;
[P0-08 optional V2 source review and launch/credits specification](doc/web-parity-optional-plugins.md)
is complete. [P0-09 authority/scope integration](doc/web-parity-exav-integration.md)
is complete at source level. [P0 coverage is closed](doc/web-parity-phase-gates.md); P1 and runtime/native acceptance remain open. See
[the source inventory](doc/web-parity-exav-inventory.md) and
[maintained source policy](doc/web-feature-parity.md).

Keep LGPL bundled content separate from the non-LGPL V2 additional set, canonical
[https://github.com/rdmark/freetrain-online-plugins-nonfree]. Planned startup support
uses explicit plugin-set directory arguments and adds a localized credits button
for each enabled set, with credits supplied by XML in that separate repository.
This loading/credits interface remains to be implemented; see the
[reserved contract and source diagnostics](doc/web-parity-optional-plugins.md#owner-directed-launch-and-credits-contract).

## Prerequisites and checks

Install the exact .NET SDK in `global.json` (10.0.402) and Deno in
`.deno-version` (2.9.7). See the official
[.NET download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and
[Deno installation guide](https://docs.deno.com/runtime/getting_started/installation/).

Frontend dependencies and tasks are defined in `web/deno.json`; `web/deno.lock`
pins transitive versions and integrity hashes. Python 3 (standard library only)
is also required to reproduce/check the curated legacy sprite conversion.

The solution to build is `FreeTrain.Online.slnx`.

When updating an existing installation, use `FreeTrainOnline__…` for server
environment variables and `FREETRAIN_ONLINE_…` for deployment/frontend tooling.
The image is now `freetrain-online:prototype`. Reuse your existing save directory
or set `FREETRAIN_ONLINE_SAVE_VOLUME` to the existing Docker volume. Prototype
saves are rejected and preserved; the default world now writes spatial Save 2. Browser login and preference keys use `freetrain-online`;
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
uses source H/V geometry and an asset-free rail schematic. Each participant has
one active socket; tabs sharing cookies share that identity. Local browsers
receive independent Owner identities. Train/clock/construction operations are
unavailable until their canonical implementations arrive.

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

Save 2 checkpoints run every 30 seconds and at clean shutdown. A valid checkpoint
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
Construction confirmation, removal, Run and Step are disabled.

Game panels preserve canvas/camera identity. **View** offers height cut and local
appearance controls. **Settings** offers English/Japanese and four persisted color
schemes. Narrow panels support Expand / Reduce and internal scrolling. Toolbars
support Tab, arrows, Home and End. Before joining or after disconnect, the session
dialog appears over a separate inert demo map. A failed Leave offers Retry leave.

Save/upload replacement requires Owner access and explicit confirmation. File
validation rejects unsupported inputs before a replacement modal is shown.
The explicit historical regression harness retains station/controller/construction
UI tests; those results do not establish canonical gameplay behavior.

Reproduce/check bundled sprites:

```sh
deno task --cwd web import:content
deno task --cwd web check:content
```

These tasks use the Python standard library. Generated PNGs, night/recolor variants,
alpha masks, offsets and source hashes are included in source; browser image decode stays
outside the paint loop. Asset notices ship in `web/public/assets/NOTICE.txt` and
`COPYING.txt`. Build checks reject generated drift.

## Saves and sound

**Game → Settings** contains language and gesture-enabled sound.
**Session** (also in Game on narrow screens) contains reconnect and Leave game.
**Credits**, available in the Game menu and the connect dialog, acknowledges the
original author and contributors, and the English version.
**License**, beside Credits in both places, displays the full LGPL 2.1 text and
links to the source code on GitHub.

**Spatial Save 2 is supported; prototype Save 1 is unsupported.** Save slot and
Download save capture the validated H/V rail graph. Load/upload validates dimensions,
unique occupied cells, source patterns, reciprocal connectivity, all eight headings
and exact ruleset/content/pack identity before replacing the world and advancing
the epoch. No prototype clock, economy, random or train state is saved.

`FreeTrainOnline__Saves__Directory` selects the save directory. An incompatible
startup checkpoint produces a visible error and a fresh world; the rejected file
is never overwritten. `/api/saves/status` reports availability and startup errors.
See [the save contract](prototype/protocol/save-v2.schema.json) and
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

The spatial integration now uses **protocol 3**, with explicit ruleset/content/pack
identity, coordinate-system tags and per-car eight-direction heading indices.
Restart/deploy matching artifacts together; protocol 2 peers receive HTTP 426.
The default profile is `reference-spatial-1` / `reference-spatial-content-1`,
with H/V bounds, `normal0`..`normal11` track patterns and spatial Save 2. Unknown
profiles/packs block reveal. See [P1-01c](doc/web-parity-reference-world.md).

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
