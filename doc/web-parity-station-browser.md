# P6-08 playable station buildings — 8 November 2026

The Stations task (**J**) delivers the first playable building slice. Its building
tab selects all 35 bundled authored contributions, including separate L/R, color
and footprint variants. EN/JA names bind the pinned XML field, contribution and
source hash through the existing content-localization review. The 63 newly
reviewed name/group entries retain original text and donor dispositions; original
author/plugin metadata remains visible. Authored sprites provide the catalog
preview and translucent map ghost; no arbitrary footprint rotation is introduced.

The task offers Place/Remove, integer X/Y/level, automatic ground detection,
mouse/touch map targeting, an authoritative placement check and Confirm/Cancel.
The quote displays exact occupied cell count, price and every reclamation loss.
Removal resolves the current station ID/revision from any footprint member,
including picked upper art; properties open the same confirmed removal task.
The ghost uses the selected recipe, season/light and world projection. Intersecting
cuts use base Gray caps; full-height ghosts retain the renderer's water gate.

The existing `renameStation` command now handles canonical station buildings
inside the session FIFO. A detached successor revises the station and its spatial
owner together, preserving accounts, allocator, listener/host state, random streams
and timers. Names are trimmed, nonempty, at most 128 UTF-16 code units and contain
no control characters, matching the canonical station-state constraint. Stale or
removed targets reject without publication. No save/protocol change is required.

Picked properties use the existing Apply name / Reset draft editor and
Apply/Discard/Keep editing navigation decision, including IME composition handling.
Incoming peer names update confirmed state while retaining dirty drafts; a changed
or missing target disables Apply until Reset/Discard. Disconnect/world replacement
interrupt drafts and decisions without replay. Owner/Operator construction/naming
and Observer inspection retain the existing authority boundary.

Client preview delivery adds focused schema/consistency validation and generation,
connection, epoch, geometry, minute, content and echoed-query fences. Changing
inputs, locale, tool or panel, cancelling, and losing access discard old quotes.
Delayed responses cannot restore confirmation. Commit still uses the P6-06
quoted-cost, ID/revision and server-side detached-transition checks.

Station buildings are playable; the task explicitly says platforms and passenger
service remain pending. Host discovery, advertising and service dispatch remain
with P6-11/16–21; this step grants no passenger, fare or departure-event credit.
Save10/protocol8, content pack hashes and admission limits remain unchanged.

## Verification

- Relevant Release .NET: **356/356**, including station construction/naming,
  persisted spatial owner revision, invalid/Observer/stale/removed rejection,
  preserved timers/accounts and exact save/reopen, plus Contract/Mixed/Spatial/
  ObjectRemoval families.
- Web units: **336/336** (334 ordinary + two retention), including all 35 authored
  station catalog identities/prices/EN/JA names, every member's removal identity,
  clock-sensitive quote fences, malformed/negative/duplicate preview rejection,
  canonical draft conflicts/reset/apply and existing renderer/transport suites.
- **28 distinct selected Chromium scenarios verified**, no skips: the broad final
  run passed **27/28**, followed by fresh focused **3/3** after synchronizing the
  narrow camera frame and selecting an opaque interior touch target. This resolves
  the broad run's single edge-pixel targeting failure without runtime changes.
  The new journeys cover
  EN desktop keyboard/mouse and JA 430×860 touch, actual map/coordinate placement,
  a decoded-ghost roof pixel assertion, properties and dirty decisions, exact HTTP
  save/reopen, Owner/Operator/Observer, peers, stale clock/name state, late cancelled
  responses, disconnect and world replacement. Existing station domain/transport/
  pixel-gallery/local-redraw, historical drafts, toolbar/catalog/removal regressions
  are included.
- Generated contracts/content, EN/JA locales (**72 source-bound content entries**),
  Svelte/TypeScript (zero errors/warnings) and production build pass. Vite retains
  its existing bundle-size advisory.
- **28/28 station Python evidence tests**, **821 literal source fixtures**, raw
  pinned source review, project boundaries and whitespace checks pass.

Commands:

```sh
dotnet test FreeTrain.Online.slnx -c Release --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~Contract|FullyQualifiedName~Mixed|FullyQualifiedName~Spatial|FullyQualifiedName~ObjectRemoval'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-building.spec.ts station-building-domain.spec.ts station-rendering.spec.ts station-transport.spec.ts station-editing.spec.ts game-toolbar.spec.ts building-catalog.spec.ts object-removal.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check
python3 scripts/review-stations.py --check
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs: `tmp/p6-08-dotnet.log`, `tmp/p6-08-web-unit.log`,
`tmp/p6-08-web-build.log`, `tmp/p6-08-browser-verified.log`, `tmp/p6-08-browser-verified-final.log`,
`tmp/p6-08-{python,fixtures,review,boundaries}.log`.
Final captures inspected: `tmp/p6-08-browser-verified-final/`; full station gallery
and earlier regression captures: `tmp/p6-08-browser-verified/`.

Local IPC/server tests require execution outside the restrictive sandbox; they
were run with the authorized tool escalation. Earlier browser iterations exposed
an actual properties-to-removal timing bug, corrected by waiting for the panel/tool
render before assigning the target. Readiness waits, native-label selectors and
map targets were corrected; a run during frontend rebuild passed 26/28 and is
superseded by the isolated broad run and final focused resolution. A negative-cost preview guard was added after
its unit test failed. Native/original-runtime human fidelity, other browser engines,
optional content, full-suite/capacity/soak, Linux/container and deployment gates
were not rerun or credited here.
