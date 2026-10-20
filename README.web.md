# FreeTrain Online

FreeTrain Online is a multiplayer-capable, bilingual reimagination of the rail and business simulation game FreeTrain by Kohsuke Kawaguchi.

The bilingual Svelte web app drives the deterministic reference game through
HTTP/WebSocket transport and an authoritative C# session. Run/pause/step,
controller changes and Japanese station rename use committed server state.
Reconnect/refresh recover the current game. The Canvas map supports pan/zoom,
height cuts, object picking, track previews and confirmed construction/removal,
with focus-scoped manual keyboard control. Versioned saves, server checkpoints, browser
download/upload and an optional station sound are available.

Multiple participants can share that world with owner/operator/observer roles,
conflict checks, independent reconnects and timed train-control leases.

## Parity source policy

The full-game parity target is stable **FreeTrain ExAV 2.1.1.12**, archive
[https://github.com/rdmark/freetrain-exav-archive].
The running reference slice and its asset importer still retain their recorded
prototype provenance; they are not yet complete ExAV implementations. P0-01 ExAV
discovery is complete; P0-02–09 reviews remain open. See
[the source inventory](doc/web-parity-exav-inventory.md) and
[maintained source policy](doc/web-feature-parity.md).

Keep LGPL bundled content separate from the non-LGPL V2 additional set, canonical
[https://github.com/rdmark/freetrain-online-plugins-nonfree].

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
or set `FREETRAIN_ONLINE_SAVE_VOLUME` to the existing Docker volume; save formats
are unchanged. Browser login and preference keys use `freetrain-online`;
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
screenshots in `web/test-results/`. They do not establish the final
cross-platform browser matrix or native IME behavior.

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
`GET /health` reports persistence-stage health and any automatic-checkpoint error. Commands and train positions now come
from the real headless game. The map renders imported pier/condo/FT train images
and the catalog contains the fixed pier and condo heights 2–5.
Independent browser sessions can control the same game. Each participant has one
active socket; tabs sharing cookies share that identity. Local browsers receive
independent owner identities. Train control requires a lease, visible in the inspector.

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

Disconnect clears that participant’s held input and releases their train leases.
The game pauses/checkpoints one second after the last owner/operator disconnects;
observers do not keep unattended time running. Hidden tabs clear only their own inputs.
Idle games retain their state. Restart restores the latest checkpoint, starts paused,
and changes the server incarnation. Automatic checkpoints run every 30 seconds;
an abrupt crash can lose changes since the most recent successful checkpoint.
Uncertain command outcomes are shown after reconnect and are never replayed across
connections. `web/dist/` contains the Svelte static artifact; production needs no
Deno/Node frontend process.

If using the workspace-local SDK installed during the first implementation,
prepend `$PWD/.tools/dotnet` to `PATH`. `.tools/` is ignored and not required by
the source. In a restricted execution environment, set `DOTNET_CLI_HOME` to a
writable temporary directory; .NET build/test and local web servers also need
permission to create local sockets.

## Playing the map

The game fills the browser viewport. Use the left tool strip to inspect, build
or remove track; secondary controls open bounded panels over the map. See the
[game UX interaction contract](doc/web-game-ux.md) for implemented behavior.
Before joining or while disconnected, the session dialog appears over a local,
noninteractive demo map. A failed Leave offers Retry leave until sign-out is confirmed.

Click an object to inspect, drag the map to pan, and use the wheel or zoom buttons.
Middle drag or Space + drag pans in every tool. A dirty station name offers
Apply / Discard / Keep editing before closing or changing targets; revised
stations require Reset before applying. Game → Settings and session dialogs offer
four locally persisted UI color schemes. Save load/upload requires a modal
confirmation that names the shared-world replacement; Cancel sends nothing.
Narrow portrait panels offer **Expand panel** / **Reduce panel**; short windows
use side sheets. Long content scrolls inside panels. **Game → Single step** keeps
Step available on narrow screens. The Run/Pause button follows confirmed state.
Tab enters each toolbar; arrows, Home and End move within it, then Enter or Space
activates. Native text editing, IME and browser shortcuts keep their usual behavior.
Meaningful selection/results are announced; success feedback expires after four
seconds and errors remain available for review.

Open **View** for height cut, appearance and camera actions. Height cut hides raised rails and replaces cut buildings with selectable footprints.
Night uses the source palette transformation; winter ground and recoloring are
local display options. The water/fence/slope sample by the pier is decorative.

Choose Build track, select a tile on level 1, then confirm. Tools keep shared time
running; use Pause when needed. Extend the
siding with a buffer at `(37,16,1)`; the host converts the old buffer and charges
200 total. Preview/cancel costs nothing. Removal has no refund and rejects occupied
or station track. Leaving a construction tool does not change shared time.

Click a train to open its inspector. Select Manual there to acquire control, run, and focus the map.
Another participant must release control, disconnect or let the five-second lease
expire before you can take over. Take control / Release control allow explicit handoff. Shift passes the
station, Up departs, Down reverses at a station, and Left/Right choose a junction.
Focus loss, hidden tabs, disconnect and server expiry clear held input. Text fields
and IME composition do not capture controller keys.

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

Open **Game → Save and restore**, then use **Save slot** / **Load slot** for three server slots, or **Download save** /
**Upload save** for an independent JSON backup. Loading asks for confirmation,
validates the complete file, replaces the game under a new epoch, clears held input,
and starts paused. Invalid/incompatible files preserve the current world. Uploads
are limited to 1 MiB. This format does not import historical FreeTrain saves.

The automatic checkpoint is separate from the three manual slots. The server
writes it periodically, after disconnect/load, and on graceful shutdown. Saves
use `data/saves` relative to the service working directory by default. Set an
absolute directory for predictable local or persistent-volume storage:

```sh
FreeTrainOnline__Saves__Directory="$PWD/data/saves" dotnet run --project src/FreeTrain.Online.Server --no-launch-profile
```

`FreeTrainOnline__Saves__Interval=00:00:30` configures the checkpoint interval
(allowed range: one second to five minutes). Only one server can hold a save
directory at a time. A corrupt checkpoint prevents startup and is left intact;
restore an independent download/backup using the
[recovery procedure](doc/web-deployment.md#restore-and-rollback).
Health reports `checkpointError: "checkpointFailed"` if an automatic write fails.

Click **Enable sound** to preview the bundled station WAV and enable sound for
new station-arrival events. Reconnects, refreshes and loaded snapshots do not replay
old events. **Disable sound** mutes it; refresh starts muted.

After a Release build, run the real crash/restart/independent-backup check:

```sh
python3 scripts/check-save-recovery.py
```

This launches isolated local servers with temporary save directories and writes
measurements into `tmp/web-prototype-save-measurements.json`. Linux x64 volume
recovery and local HTTPS/WSS are now validated under Docker emulation on macOS ARM.
Public hosting remains pending; cross-browser testing is skipped at the owner's request.

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
`deno task --cwd web test:browser`. `FREETRAIN_ONLINE_API_TARGET` changes the Vite
development/preview proxy target only; the production client uses its own origin.

## Headless scenario

Run the 10,000-tick reference scenario without starting an HTTP listener:

```sh
dotnet run --project src/FreeTrain.Online.Server -c Release --no-build --no-restore -- --headless-trace 10000
```

Append an output path to write full per-tick state/event NDJSON. The command
prints counts and a SHA-256. The .NET test suite compares two complete
10,000-tick traces and checks movement legality,
station timing, manual control, accounting atomicity and host isolation.

## Contract changes

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
