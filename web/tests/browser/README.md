# Browser regression tests

Tests are grouped by the behavior they protect.

| Suite | Coverage |
| --- | --- |
| `station-yard.spec.ts` | P6-19 constructed host/platform, real purchased stock, Simple wait and packed boarding/cost/occupancy publication, EN/JA Observer peer, duplicate receipt, exact reopen and process checkpoint restart. Generate `FREETRAIN_YARD_FLEET_OUTPUT` with `StationYardStopTests` first. |
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
| `fleet-continuation.spec.ts` | EN/JA slot/download/rejected fleet uploads, reconnect, actual process checkpoint restart and exact uninterrupted-run oracle; set `FREETRAIN_FLEET_CONTINUATION_OUTPUT` when running the P5_27 unit matrix first. Optional `FREETRAIN_ONLINE_TEST_CONTAINER_IMAGE` repeats against a Linux runtime. |
| `train-information.spec.ts` | Nested train information, local labels/framing/follow, Outside/reversal/withdrawal/sale lifecycle, seven color templates/pixel oracles, XML, retained drafts, shared apply, Observer peers and checkpoint reopen |
| `fleet-controller-manager.spec.ts`, `fleet-groups.spec.ts` | Shared timetable controller/group editing, assignment, peer conflicts, durable reopen and absence of manual driving controls/packets |
| `fleet-deployment.spec.ts` | Real EN/JA ramp purchase/deploy/run/inspect/withdraw/redeploy, map/keyboard footprint review, stale/cancel/replay, two peers, exact reopen and backend checkpoint restart; JA 390×844/200% text, late previews and Observer/draft guards |
| `fleet-trading.spec.ts` | Real EN/JA purchase/rename/resale, exact quotes, stale/cancel/replay, two peers, Observer/unavailable states, draft/focus safety and 390×844/200% text |
| `fleet-persistence.spec.ts` | Save10 inventory/revenue, two-peer moving-consist art/picking/occupancy/booking publication, retained canvas/assets, exact HTTP reopen and reconnect; the moving case uses `FREETRAIN_MOVING_FLEET_OUTPUT` emitted by focused `FleetMovementTests` |
| `train-rendering.spec.ts` | All eight bundled car recipes against independent poses and actual PNG pixels/alpha picks in eight seasonal/light variants; infrastructure overlap contacts, hidden tunnel movement and local/full redraw identity |

Construction and terrain suites retain their existing functional names.
`browser-helpers.ts` holds shared interaction and world fixtures;
`layout-helpers.ts` holds viewport checks. Helpers do not register tests or hooks.
Tests requiring the prototype regression world remain in the
`prototype-regression` project; file organization does not change their backend.
The moving-art test bundles its production-renderer harness through the pinned
Vite API into a Playwright route; the harness is absent from the shipped build.

See [browser validation commands](../../../README.web.md#prerequisites-and-checks)
for default, extended, performance and continuation coverage. From `web/`, run
a category against a current build with, for example:

```sh
deno task test:browser:run station-editing.spec.ts
```

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
