# Browser regression tests

Tests are grouped by the behavior they protect.

| Suite | Coverage |
| --- | --- |
| `forest.spec.ts` | P7-17 EN keyboard/JA phone touch paid planting, authored catalog treatments, cancellation, owned replanting, actual forest picks, peers, free removal and exact reopen/checkpoint restart. Uses focused `ForestConstructionTests` fixtures. |
| `city-heat.spec.ts` | P7-06 EN/JA peers receive production Single heat diffusion, enforce Observer authority, replay receipts without duplicate effects, and preserve buffers/relative timer through save/reopen and checkpoint restart. |
| `station-cross-tool.spec.ts` | P6-24 EN desktop/DPR1 and JA narrow touch/DPR2 station terrain protection, source-legal poles on occupied platform rail, passage quotes retained across pole edits and clock steps, cosmetic passage edits, lost acknowledgements, idempotent receipt retries, stale/Observer rejection, demo/live gating and retained canvas. Cold join holds station art and live state independently. |
| `station-information.spec.ts` | P6-23 EN/JA keyboard/narrow touch sorted stable-ID list, picked information, actual demand/waiting and source averages, Pass/forwarding/current stop indices, peer rename drafts, local labels, midnight exact reopen, removal/disconnection and authenticated Observer query fences and timestamped live reads with delayed network responses |
| `station-controller.spec.ts` | P6-22 EN/JA desktop/narrow touch picked station and multiple platform TAT keys, independent station settings, legacy companion preview/append/export, live inherited controller reassignment, real waits/pass-through, exact peers/save/checkpoint replay and peer host removal/rebuild/chained reconnection. Uses committed domain-generated `station-controller-v11.json`. |
| `station-yard.spec.ts` | P6-19 constructed host/platform, real purchased stock, Simple wait and packed boarding/cost/occupancy publication, EN/JA Observer peer, duplicate receipt, exact reopen and process checkpoint restart. The selected run automatically generates and validates all three yard inputs through `StationYardStopTests`. |
| `station-transport.spec.ts` | EN/JA peers, controlled providers, aligned hourly decay, midnight import/export/train-log rotation, relative platform upkeep, duplicate receipts, save/reopen and actual checkpoint process restart |
| `thin-platform.spec.ts` | EN/JA slim roofed/unroofed ground/elevated/underground construction, exact roof-run ghosts and cuts, touch/map rail attachment, removal/restoration, peers, roles, stale/cancel/replacement fences and checkpoint restart |
| `session-lifecycle.spec.ts` | Connection gate, sign-in, asset readiness, disconnect/rejoin, leave and revoked access |
| `station-editing.spec.ts` | Unsaved drafts, focus traps, retargeting, stale revisions and interrupted edits |
| `ui-preferences.spec.ts` | Color scheme persistence, fallback, contrast and canvas preservation |
| `responsive-layout.spec.ts` | Viewport sizes, Japanese panels, scrolling, touch targets, enlarged text and viewport emulation |
| `keyboard-controls.spec.ts` | Toolbar/menu navigation, native editing, focus, Escape layers and command feedback |
| `map.spec.ts`, `map-panning.spec.ts` | Map shortcuts, panning, picking, camera, DPR, world replacement and absence of driving input |
| `shell.spec.ts` | Integrated inspector, clock and track confirmation/cancellation workflows |
| `multiplayer.spec.ts` | Shared state, authority, sign-out and independent participant UI state |
| `persistence.spec.ts` | Save availability, replacement confirmation and recovery |
| `fleet-continuation.spec.ts` | EN/JA slot/download/rejected fleet uploads, reconnect, actual process checkpoint restart and exact uninterrupted-run oracle; the selected run automatically generates the P5-27 input and uninterrupted-run oracle. Optional `FREETRAIN_ONLINE_TEST_CONTAINER_IMAGE` repeats against a Linux runtime. |
| `train-information.spec.ts` | Nested train information, local labels/framing/follow, Outside/reversal/withdrawal/sale lifecycle, seven color templates/pixel oracles, XML, retained drafts, shared apply, Observer peers and checkpoint reopen |
| `fleet-controller-manager.spec.ts`, `fleet-groups.spec.ts` | Shared timetable controller/group editing, assignment, peer conflicts, durable reopen and absence of manual driving controls/packets |
| `fleet-deployment.spec.ts` | Real EN/JA ramp purchase/deploy/run/inspect/withdraw/redeploy, map/keyboard footprint review, stale/cancel/replay, two peers, exact reopen and backend checkpoint restart; JA 390×844/200% text, late previews and Observer/draft guards |
| `fleet-trading.spec.ts` | Real EN/JA purchase/rename/resale, exact quotes, stale/cancel/replay, two peers, Observer/unavailable states, draft/focus safety and 390×844/200% text |
| `fleet-persistence.spec.ts` | Save10 inventory/revenue, two-peer moving-consist art/picking/occupancy/booking publication, retained canvas/assets, exact HTTP reopen and reconnect; the selected run generates Outside, timing and moving inputs through focused `FleetMovementTests` |
| `train-rendering.spec.ts` | All eight bundled car recipes against independent poses and actual PNG pixels/alpha picks in eight seasonal/light variants; infrastructure overlap contacts, hidden tunnel movement and local/full redraw identity |

Routine fleet controller-manager, group, rule-file and reconciliation matrices run
EN desktop and JA narrow/touch with 200% text. The EN narrow and JA desktop
cross-pairs carry `@extended` and run with `playwright.extended.config.ts`.
Both locale-switch directions remain in routine reconciliation coverage.
Station-family construction/reopen covers all 35 definitions in EN, one
representative in JA narrow/touch, and all 35 JA definitions under `@extended`.
The catalog unit test retains all 35 footprints and both locale name variants.

Failed-leave retry feedback belongs to `session-lifecycle.spec.ts`; Japanese
station-name Tab focus belongs to `keyboard-controls.spec.ts`. Hidden-tab shared
time remains in `multiplayer.spec.ts`. Prototype train selection checks for
absent driving controls; `fleet-controller-manager.spec.ts` owns the browser
forbidden driving-key/packet matrix.

Construction and terrain suites retain their existing functional names.
`browser-helpers.ts` holds shared interaction and world fixtures;
`layout-helpers.ts` holds viewport checks. Helpers do not register tests or hooks.
Tests requiring the prototype regression world remain in the
`prototype-regression` project; file organization does not change their backend.
`rendering-harnesses.ts` prebuilds each selected production-renderer harness
once through the pinned Vite API before workers start. Run-owned bundles are
read into isolated page routes and removed at teardown; they are absent from
`web/dist`. Editor runs without case selection prepare all five harness entries.
`transport-helpers.ts` shares compatibility URLs, authentication, preview capture,
a paused canonical scene and deliberate response holds. `sessions` owns receipt
correlation, save replacement and checkpoint restart. Feature actions and assertions stay in their specs.

The station construction peer witness retains real EN/JA decoder publication;
`StationBuildingSessionTests` and `StationBuildingConstructionTests` own its
arithmetic, timers, authorization, stale-command and receipt replay matrices.
`WorldCreationHttpTests` owns empty/generated endpoint rejection fences.
`RandomTests` and `SpatialEntityTests` own malformed random/graph matrices;
`reference-world.spec.ts` keeps file-picker feedback, confirmation gating and
state-preservation representatives.

See [browser validation commands](../../../README.web.md#prerequisites-and-checks)
for default, extended, performance and continuation coverage. From `web/`, run
a category against a current build with, for example:

```sh
deno task test:browser:run station-editing.spec.ts
```

`generated-fixtures.ts` records producer filters, consumers and every required
companion file. Global setup discovers the selected cases with Playwright's own
`--list` filters, generates only their fixture families in a fresh temporary
directory, validates the outputs, and removes them after the run.
Editor/test-server runs without CLI case filters prepare the families allowed
by their configured projects instead. Generated
`FREETRAIN_*_OUTPUT` paths are managed by setup; manual exports are unnecessary.
Departure bell inputs come from `Generate_real_selected_bell_departure_fixtures`
in `StationDepartureBellTests`. Missing outputs fail setup rather than skipping.
The default required-tests reporter also fails otherwise-green skipped runs.
Optional-content and capacity suites retain their explicit configurations.

Layout, preference and map measurements are written beneath the current
`FREETRAIN_ONLINE_BROWSER_EVIDENCE` directory in functional subdirectories.
Historical UX acceptance documents retain their original phase labels.

### Fleet capacity (P5-28)

`playwright.fleet-capacity.config.ts` is an opt-in, sequential measurement suite.
Generate the five accepted scenes with the focused `FleetCapacityTests` first,
then build the client and run that configuration. It measures legal purchased /
deployed stock, local and injected 100ms RTT feedback, running/paused resources,
peer join, named save and process restart. It does not run in the default browser
projects. See [method, reproduction and qualifications](../../../doc/web-parity-fleet-capacity.md).

### Failure evidence and bounded comparisons

The default required configurations write `summary.json`, the complete Playwright
`report.json`, and per-test evidence below the printed `Browser artifacts` root.
Each failure gets at most 50 console lines, including source and attachment
references. `errors/*` retains complete original errors and an attachment manifest;
the JSON report retains the original errors too. Skipped required cases and passes
that depend on retries still fail the run.
The final stdout summary shows passed, failed and skipped test counts and lists
failed test titles with their project and file. Timeouts and interruptions count
as failures; retries update the same test's final outcome instead of inflating
the counts. Runner errors are counted separately.

The summary and failure attachment manifests reference files instead of embedding
binary Buffer arrays. Attachments supplied as in-memory bodies are saved under
`attachments/` with their byte count in the index; their complete bytes remain
available, and the standard Playwright JSON report is unchanged.

`assertions.ts` exports `assertCanonicalState(actual, expected, context)`,
`assertJSONSave(actualData, expectedData, context)`, and
`assertSaveBytes(actualBytes, expectedBytes, context)`. Supply `{ phase, peer }`;
state envelopes provide epoch/revision/tick, or pass those fields in the context
when comparing a subtree. Compare after explicit convergence. These assertions
throw immediately; repeatedly calling them inside a poll would create evidence
for each unsuccessful poll. They inspect all values and retain array ordering.
Only diagnostic samples are bounded. Full state values are attached as text,
JSON saves as JSON, and exact save bytes as binary. Byte failures include SHA-256
hashes and their first differing offset; structural JSON diagnosis never permits
different bytes to pass. `evidencePath(test.info(), label, extension)` gives
repeated screenshots/details distinct per-test names.

Managed primary and peer pages record console/page errors, failed requests,
HTTP error responses, socket closes, and protocol metadata. On failure,
`browser-timeline.ndjson` holds the timeline, `recent-browser-events.json` keeps the
last 200 events, and `protocol-payloads.ndjson` holds full intercepted frames (binary frames use base64).
This is diagnostic evidence, not a canonical-state observer or proof of client
application. Browser-side socket-close listeners also cover routed connections
without changing their close forwarding. Successful cases discard these browser
payload files. Playwright retains failed screenshots and traces for all contexts,
including peer contexts closed in `finally` before the error reaches the runner.
The recorder also closes peer contexts a spec leaves open before finishing its
evidence, preventing their events from leaking into a subsequent test.

Before creating contexts for the next case, the harness removes the previous
case's raw trace scratch from that worker's `.playwright-artifacts-N/traces/`
directory. Playwright 1.64.1 otherwise keeps these raw recordings and resources
until worker exit, even when a passing trace archive is discarded. This bounds
scratch usage to the current cases across active workers rather than the whole
suite. Retained failure `trace.zip` files are separate and preserved. Cleanup is
skipped if a browser context is still open or live tracing is enabled. The storage
acceptance cases verify the pinned Playwright layout using two passing cases in
the same worker; keep that check when upgrading Playwright. A single long case
can still produce a large trace and full protocol payload file.

`processes/worker-N/` retains backend and preview stdout/stderr in separate files
for every process start/restart, plus a lifecycle timeline. Per-test `processes/`
attachments contain log slices and `association.json` links to full worker logs.
The reporter also associates logs when worker setup fails before test fixtures
start. Run-owned originals retain worker teardown output. Optional/capacity configurations keep their own server ownership and time budgets
while sharing the diagnostic recorder and artifact policy.

To validate the diagnostics themselves, with Chromium installed and a current
client/server build, run from `web/`:

```sh
deno task test:browser:diagnostics
```

This opt-in harness uses `unzip` to inspect traces. It lives outside required
browser spec discovery. It checks twelve deliberate assertion failures across
two independent runs with two workers, plus managed setup, failed restart,
successful restart and teardown failures. It requires those child runs to fail
with the intended errors and verifies bounded output, full errors, two-page
screenshots/traces, process logs and distinct artifact paths. The parent command
passes only when those checks pass and prints its `acceptance.json` path.
Normal assertion/reporter regression checks run with `deno task test`.
The harness also verifies that passing cases reclaim raw trace scratch between
cases and that inline attachment bytes remain accessible through the summary.

### Session checkpoints, focused phases and local validation

Use the `sessions` fixture before navigating each primary/peer page. `ready()`
means the connection and presentation are available. After replacement, command,
step or restart, use `replaceSave`, `minute`, `checkpoint`, `converge` or
`restartCheckpoint`. These select one fixed paused publication and compare every
peer's reconstructed traffic, browser-applied state and rendered revision.
`assertCheckpoint(owner, select, expected, phase)` additionally checks an
independent fixture subtree. Keep fixture/save oracles separate from decoding.
`command()` waits for a fresh receipt even on request-ID replay; `actionReceipt()`
correlates a real UI command, including expected rejections. Session effects use
bounded event history and counters that survive reconnects. Mixed worlds may
contain independent fleet, station or geometry sections. Use
`requireMixedState(snapshot, 'fleet', 'queue')` when dereferencing those
sections; the helper preserves the entire graph for exact comparisons.

`waitForMapDraw(pageOrCanvas)` waits for reactive invalidation and the matching
ready draw, including resize/refinement, without forcing a draw. Use
`renderedTool`, `panMapBy` and `projectMapPoint` for exact picking targets. The
shared station picker retains per-test screenshots and coordinate evidence.
The ready-draw observation also tracks the rendered reduced-motion preference.
For a deliberately hidden map, use an explicit absence-observation window:
visibility suspends painting. Fixed absence/measurement windows remain explicit
in the specs.

The [migration inventory and coverage mapping](migration-inventory.md) lists
before/after observers, assertions, restarts and waits for every spec and records
the preserved locale/input/peer/persistence matrix. Each station/fleet continuation
combination has independently prepared `rejected uploads`, `slot and download`,
`reconnect` and `restart continuation` cases plus its `complete journey`.
Controller editing remains in the focused EN/JA mouse/touch P6-22 journeys.

From `web/`, build once and run the complete required suite locally:

```sh
deno task build
set -o pipefail
deno task test:browser:run --workers=1 --retries=0 2>&1 | tee ../tmp/browser-increment-3-local.log
```

One worker keeps resource use lower; use `--workers=2` for a separate isolation
check. Avoid running another browser suite concurrently. The complete suite may
take about 30 minutes. Focus a single boundary or repeat a case with:

```sh
deno task test:browser:run station-continuation.spec.ts --grep 'service ja reconnect' --workers=1 --retries=0 --repeat-each=3
deno task test:browser:run fleet-continuation.spec.ts --grep 'ja restart continuation' --workers=1 --retries=0
deno task test:browser:run station-controller.spec.ts --grep 'P6-22 ja' --workers=1 --retries=0
```

Every invocation prints a fresh `tmp/browser-run-*/` root. Inspect `summary.json`
first for status, individual cases, retries and attachments, then `report.json`
for full machine-readable results. Read `errors/*` and per-test
`error-context.md`, `browser-timeline.ndjson`, `recent-browser-events.json`,
`protocol-payloads.ndjson` and process association attachments for failures.
Run-owned `processes/worker-N/` and `processes/configuration/` logs include setup,
restart and teardown. Inspect a failed trace with the pinned Playwright CLI:

```sh
deno run -A npm:@playwright/test@1.64.1 show-trace /absolute/path/to/trace.zip
```

Optional/capacity configurations now use the same diagnostics and retain their
own server ownership, selection and budgets. Select them explicitly with
`--config playwright.optional.config.ts`, `playwright.capacity.config.ts`,
`playwright.railway-capacity.config.ts`, `playwright.fleet-capacity.config.ts` or
`playwright.station-capacity.config.ts`. Generate the documented capacity fixtures
first and retain the printed artifact root alongside measurement evidence.
`playwright.extended.config.ts` and the continuation tasks remain separate;
none of these opt-in selections is implied by a default-suite pass.

`deno task check:browser-tests` guards source authoring; it rejects local raw
latest decoders, old history adapters, heat reconstruction and unbounded
state/save comparisons. Raw routes require a nearby `transport-test:` rationale.
Dedicated fault injection and raw representation assertions remain supported.
