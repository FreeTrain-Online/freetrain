# Browser suite migration inventory and coverage mapping

17 November 2026. Before = repository HEAD at the start of increment 3. The table inventories all spec files, including dedicated transport and measurement tests. Counts of deep assertions are a superset of whole-state checks; small literal-array assertions may remain.

| Spec | Before: socket routes / raw latest or adapters / deep equality / unchecked restart / fixed waits / frame waits | After: socket routes / bounded assertions / unchecked restart / fixed waits / frame waits |
| --- | --- | --- |
| [authoring-terrain.spec.ts](authoring-terrain.spec.ts) | 0 / 0 / 8 / 0 / 0 / 0 | 0 / 7 / 0 / 0 / 0 |
| [bridge-construction.spec.ts](bridge-construction.spec.ts) | 1 / 0 / 11 / 0 / 0 / 2 | 1 / 10 / 0 / 0 / 0 |
| [building-catalog.spec.ts](building-catalog.spec.ts) | 0 / 0 / 5 / 0 / 0 / 0 | 0 / 4 / 0 / 0 / 0 |
| [capacity.spec.ts](capacity.spec.ts) | 0 / 0 / 4 / 0 / 1 / 13 | 0 / 0 / 0 / 1 / 5 |
| [city-building.spec.ts](city-building.spec.ts) | 1 / 0 / 42 / 0 / 0 / 0 | 0 / 25 / 0 / 0 / 0 |
| [city-heat.spec.ts](city-heat.spec.ts) | 1 / 1 / 8 / 1 / 0 / 0 | 0 / 6 / 0 / 0 / 0 |
| [city-rendering.spec.ts](city-rendering.spec.ts) | 0 / 0 / 3 / 0 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [city-transport.spec.ts](city-transport.spec.ts) | 1 / 1 / 10 / 1 / 0 / 0 | 0 / 8 / 0 / 0 / 0 |
| [construction-regression.spec.ts](construction-regression.spec.ts) | 2 / 0 / 5 / 0 / 0 / 0 | 2 / 4 / 0 / 0 / 0 |
| [corner-terrain.spec.ts](corner-terrain.spec.ts) | 0 / 0 / 6 / 0 / 3 / 0 | 0 / 4 / 0 / 3 / 0 |
| [cross-tool.spec.ts](cross-tool.spec.ts) | 0 / 0 / 8 / 0 / 0 / 0 | 0 / 6 / 0 / 0 / 0 |
| [departure-bells.spec.ts](departure-bells.spec.ts) | 2 / 1 / 13 / 1 / 0 / 0 | 0 / 3 / 0 / 0 / 0 |
| [fixed-city.spec.ts](fixed-city.spec.ts) | 1 / 1 / 8 / 1 / 0 / 0 | 0 / 6 / 0 / 0 / 0 |
| [fleet-capacity.spec.ts](fleet-capacity.spec.ts) | 0 / 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 |
| [fleet-continuation.spec.ts](fleet-continuation.spec.ts) | 0 / 0 / 9 / 1 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [fleet-controller-manager.spec.ts](fleet-controller-manager.spec.ts) | 2 / 0 / 34 / 4 / 1 / 0 | 2 / 22 / 0 / 1 / 0 |
| [fleet-cross-tool.spec.ts](fleet-cross-tool.spec.ts) | 2 / 0 / 15 / 0 / 0 / 0 | 2 / 13 / 0 / 0 / 0 |
| [fleet-deployment.spec.ts](fleet-deployment.spec.ts) | 2 / 1 / 41 / 3 / 0 / 0 | 1 / 36 / 0 / 0 / 0 |
| [fleet-groups.spec.ts](fleet-groups.spec.ts) | 0 / 0 / 8 / 1 / 0 / 0 | 0 / 7 / 0 / 0 / 0 |
| [fleet-persistence.spec.ts](fleet-persistence.spec.ts) | 4 / 2 / 39 / 1 / 0 / 2 | 0 / 25 / 0 / 0 / 0 |
| [fleet-reconciliation.spec.ts](fleet-reconciliation.spec.ts) | 0 / 0 / 4 / 0 / 0 / 0 | 0 / 2 / 0 / 0 / 0 |
| [fleet-rule-file.spec.ts](fleet-rule-file.spec.ts) | 0 / 0 / 12 / 1 / 0 / 0 | 0 / 8 / 0 / 0 / 0 |
| [fleet-trading.spec.ts](fleet-trading.spec.ts) | 2 / 0 / 10 / 0 / 0 / 0 | 2 / 9 / 0 / 0 / 0 |
| [game-toolbar.spec.ts](game-toolbar.spec.ts) | 2 / 0 / 0 / 0 / 0 / 6 | 2 / 0 / 0 / 0 / 6 |
| [garage-construction.spec.ts](garage-construction.spec.ts) | 0 / 0 / 6 / 0 / 0 / 0 | 0 / 6 / 0 / 0 / 0 |
| [girder-construction.spec.ts](girder-construction.spec.ts) | 1 / 0 / 10 / 0 / 0 / 2 | 1 / 9 / 0 / 0 / 0 |
| [keyboard-controls.spec.ts](keyboard-controls.spec.ts) | 0 / 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 |
| [land-trading.spec.ts](land-trading.spec.ts) | 1 / 0 / 11 / 1 / 0 / 0 | 0 / 9 / 0 / 0 / 0 |
| [map-panning.spec.ts](map-panning.spec.ts) | 0 / 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 |
| [map.spec.ts](map.spec.ts) | 2 / 0 / 2 / 0 / 0 / 2 | 2 / 1 / 0 / 0 / 0 |
| [mixed-continuation.spec.ts](mixed-continuation.spec.ts) | 0 / 0 / 8 / 0 / 0 / 0 | 0 / 7 / 0 / 0 / 0 |
| [multiplayer.spec.ts](multiplayer.spec.ts) | 0 / 0 / 2 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 |
| [object-removal.spec.ts](object-removal.spec.ts) | 1 / 0 / 10 / 0 / 0 / 2 | 1 / 10 / 0 / 0 / 0 |
| [optional-content.spec.ts](optional-content.spec.ts) | 0 / 0 / 2 / 0 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [paint-terrain.spec.ts](paint-terrain.spec.ts) | 0 / 0 / 1 / 0 / 4 / 2 | 0 / 1 / 0 / 4 / 2 |
| [passage.spec.ts](passage.spec.ts) | 3 / 0 / 18 / 2 / 0 / 0 | 0 / 13 / 0 / 0 / 0 |
| [passenger-feedback.spec.ts](passenger-feedback.spec.ts) | 1 / 2 / 20 / 2 / 0 / 0 | 0 / 12 / 0 / 0 / 0 |
| [passenger-service.spec.ts](passenger-service.spec.ts) | 1 / 2 / 12 / 1 / 0 / 4 | 0 / 6 / 0 / 0 / 0 |
| [persistence.spec.ts](persistence.spec.ts) | 1 / 0 / 4 / 0 / 0 / 0 | 1 / 4 / 0 / 0 / 0 |
| [platform-lanes.spec.ts](platform-lanes.spec.ts) | 2 / 0 / 16 / 2 / 0 / 0 | 0 / 13 / 0 / 0 / 0 |
| [platform-properties.spec.ts](platform-properties.spec.ts) | 1 / 0 / 9 / 1 / 0 / 0 | 0 / 6 / 0 / 0 / 0 |
| [pole-construction.spec.ts](pole-construction.spec.ts) | 0 / 0 / 6 / 0 / 0 / 6 | 0 / 6 / 0 / 0 / 0 |
| [primary-action.spec.ts](primary-action.spec.ts) | 0 / 0 / 1 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 |
| [railway-capacity.spec.ts](railway-capacity.spec.ts) | 0 / 0 / 6 / 0 / 0 / 5 | 0 / 4 / 0 / 0 / 3 |
| [railway-construction.spec.ts](railway-construction.spec.ts) | 5 / 0 / 22 / 0 / 0 / 2 | 5 / 14 / 0 / 0 / 0 |
| [railway-rendering.spec.ts](railway-rendering.spec.ts) | 1 / 0 / 7 / 0 / 0 / 0 | 1 / 4 / 0 / 0 / 0 |
| [rectangle-terrain.spec.ts](rectangle-terrain.spec.ts) | 0 / 0 / 7 / 0 / 0 / 0 | 0 / 6 / 0 / 0 / 0 |
| [reference-world.spec.ts](reference-world.spec.ts) | 2 / 0 / 23 / 0 / 0 / 1 | 2 / 14 / 0 / 0 / 1 |
| [responsive-layout.spec.ts](responsive-layout.spec.ts) | 0 / 0 / 4 / 0 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [session-lifecycle.spec.ts](session-lifecycle.spec.ts) | 3 / 0 / 0 / 0 / 1 / 0 | 3 / 0 / 0 / 1 / 0 |
| [shell.spec.ts](shell.spec.ts) | 4 / 0 / 3 / 0 / 1 / 0 | 4 / 1 / 0 / 1 / 0 |
| [signal-construction.spec.ts](signal-construction.spec.ts) | 0 / 0 / 5 / 0 / 0 / 0 | 0 / 5 / 0 / 0 / 0 |
| [signal-integration.spec.ts](signal-integration.spec.ts) | 0 / 0 / 9 / 1 / 0 / 0 | 0 / 8 / 0 / 0 / 0 |
| [slope-construction.spec.ts](slope-construction.spec.ts) | 2 / 0 / 13 / 0 / 0 / 0 | 2 / 11 / 0 / 0 / 0 |
| [spatial.spec.ts](spatial.spec.ts) | 0 / 0 / 2 / 0 / 0 / 0 | 0 / 2 / 0 / 0 / 0 |
| [speed-limit.spec.ts](speed-limit.spec.ts) | 0 / 0 / 11 / 0 / 0 / 2 | 0 / 11 / 0 / 0 / 0 |
| [startup-world.spec.ts](startup-world.spec.ts) | 0 / 0 / 3 / 0 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [static-ground.spec.ts](static-ground.spec.ts) | 0 / 0 / 8 / 1 / 0 / 0 | 0 / 7 / 0 / 0 / 0 |
| [station-building-domain.spec.ts](station-building-domain.spec.ts) | 0 / 2 / 5 / 0 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [station-building.spec.ts](station-building.spec.ts) | 1 / 0 / 15 / 0 / 0 / 2 | 1 / 11 / 0 / 0 / 0 |
| [station-capacity.spec.ts](station-capacity.spec.ts) | 0 / 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 |
| [station-continuation.spec.ts](station-continuation.spec.ts) | 0 / 0 / 1 / 0 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [station-controller.spec.ts](station-controller.spec.ts) | 0 / 0 / 4 / 0 / 0 / 0 | 0 / 2 / 0 / 0 / 0 |
| [station-cross-tool.spec.ts](station-cross-tool.spec.ts) | 2 / 0 / 18 / 0 / 0 / 0 | 2 / 16 / 0 / 0 / 0 |
| [station-editing.spec.ts](station-editing.spec.ts) | 1 / 0 / 0 / 0 / 0 / 0 | 1 / 0 / 0 / 0 / 0 |
| [station-hosts.spec.ts](station-hosts.spec.ts) | 1 / 0 / 12 / 1 / 0 / 0 | 0 / 5 / 0 / 0 / 0 |
| [station-information.spec.ts](station-information.spec.ts) | 1 / 0 / 7 / 0 / 0 / 4 | 0 / 5 / 0 / 0 / 0 |
| [station-rendering.spec.ts](station-rendering.spec.ts) | 1 / 0 / 4 / 0 / 0 / 0 | 0 / 1 / 0 / 0 / 0 |
| [station-transport.spec.ts](station-transport.spec.ts) | 2 / 2 / 24 / 1 / 0 / 0 | 0 / 15 / 0 / 0 / 0 |
| [station-yard.spec.ts](station-yard.spec.ts) | 1 / 1 / 10 / 1 / 0 / 0 | 0 / 8 / 0 / 0 / 0 |
| [stationary-construction.spec.ts](stationary-construction.spec.ts) | 0 / 0 / 4 / 0 / 0 / 0 | 0 / 4 / 0 / 0 / 0 |
| [terrace-terrain.spec.ts](terrace-terrain.spec.ts) | 0 / 0 / 12 / 0 / 0 / 0 | 0 / 12 / 0 / 0 / 0 |
| [terrain.spec.ts](terrain.spec.ts) | 1 / 0 / 48 / 0 / 0 / 8 | 1 / 35 / 0 / 0 / 2 |
| [thin-platform.spec.ts](thin-platform.spec.ts) | 1 / 0 / 14 / 1 / 0 / 8 | 0 / 10 / 0 / 0 / 0 |
| [train-information.spec.ts](train-information.spec.ts) | 0 / 0 / 8 / 0 / 0 / 0 | 0 / 5 / 0 / 0 / 0 |
| [train-rendering.spec.ts](train-rendering.spec.ts) | 0 / 0 / 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 / 0 |
| [train-runtime.spec.ts](train-runtime.spec.ts) | 2 / 0 / 0 / 0 / 0 / 10 | 2 / 0 / 0 / 0 / 10 |
| [tunnel-construction.spec.ts](tunnel-construction.spec.ts) | 0 / 0 / 15 / 0 / 0 / 0 | 0 / 15 / 0 / 0 / 0 |
| [ui-preferences.spec.ts](ui-preferences.spec.ts) | 0 / 0 / 4 / 0 / 0 / 0 | 0 / 2 / 0 / 0 / 0 |
| [wide-platform.spec.ts](wide-platform.spec.ts) | 1 / 0 / 14 / 1 / 0 / 8 | 0 / 10 / 0 / 0 / 0 |

`frame waits` counts requestAnimationFrame mentions, including intentional sampling loops and polling of observable states. Production map synchronization now uses `waitForMapDraw()` for reactive changes/resize/refinement, and exact camera/tool targets for picking. Remaining fixed waits explicitly observe the absence of delayed writes/repeats (terrain and paint cancellation, controller shortcuts, access revocation) or measure update/resource intervals. Transport `setTimeout` calls delay delivery on purpose; performance loops sample frames rather than synchronize gameplay.

## Observer and transport boundaries

City heat/transport/fixed production, passenger feedback/service, station yard/providers/statistics, fleet movement/persistence and deployment operations use the incremental session observer. Command-only routes in station hosts, properties, lanes, passages, land, station art/information, city observer authority, thin/wide platform authority now use request-ID command/receipt methods. The obsolete history adapter and collectSession helper are removed.

Every retained raw route has a nearby `transport-test:` rationale. These cover fault injection (drop/duplicate/hold/revoke), exact request replay, outgoing command counts/coordinates, compact transport metadata, and movement delivery measurements. They are dedicated transport cases within feature spec files. Raw compact publications never stand in for reconstructed checkpoint state. Departure bells use the common observer with a deliberate delivery hook; raw order/event assertions remain explicit.

`source-policy.ts` runs during default discovery and `deno task check:browser-tests`. It rejects local raw latest decoders, history adapters, hand-written heat-reference repair, undocumented raw routes, and common unbounded state/save equality forms. This is a small authoring guard, not a proof of arbitrary dataflow; aliases still need review.

## Before / after coverage for independently prepared cases

The old station matrix was four modes × EN/JA complete journeys (8 cases); the old fleet matrix was EN/JA complete journeys (2 cases). Each combination now has four independently prepared feature cases plus its original complete journey: 40 station cases and 10 fleet cases. No original locale, mode, feature invariant or peer/persistence boundary is removed. Preparation always uses the same backend-generated input and independent successor bytes. Registration stays in the original spec files, preserving ownership and fixture selection.

| Before invariant | After case suffix | Locale and mode | Viewport / input | Roles and peer behavior | Persistence boundary | Complete witness |
| --- | --- | --- | --- | --- | --- | --- |
| Malformed, version, pack, graph and oversized uploads cannot mutate live state or named slot | `rejected uploads` | Station: service/reverse/turning/midnight × EN/JA; fleet: EN/JA | Original 1280×900 desktop; real file input | Owner and JA peer, exact applied checkpoint unchanged | Named slot written before rejection, loaded afterward; bytes unchanged | `complete journey` in every original combination |
| Named save, browser download and reload preserve every byte | `slot and download` | Same complete mode/locale matrix | Original desktop, native download event and file bytes | Owner writes/loads; JA peer converges on replacement epoch | Named slot → actual download → named load | Same matrix of complete journeys |
| Explicit reconnect does not replay or change shared state | `reconnect` | Same complete mode/locale matrix | Original desktop; real Session panel button | Owner gets a new connection; JA peer retains its generation; both apply fixed target | Exact save before/after reconnect | Same matrix of complete journeys |
| Checkpoint process restart continues every source callback exactly | `restart continuation` | Same complete mode/locale matrix | Original desktop; real Step button each minute | Owner and JA peer reauthenticate/reload; receipt + wire + applied state converge each minute | Managed process checkpoint restart → independent uninterrupted successor bytes | Same matrix of complete journeys |
| Controller editing, hosts, rules and group reassignment work through real picking | Existing focused `P6-22` cases | EN mouse and JA touch | EN 1280×900; JA 430×860 with touch | Owner/operator, exact peer application | Real fixture preparation and checkpoint restart | Retained full controller journeys; shared picker also used by passenger service |

Station continuation previously used desktop input for both locales; this refactor retains that. JA narrow touch remains explicit in P6-22, westbound passenger service and the existing construction/platform/terrain matrices. The shared picker uses completed tool/pan draws and computes its final coordinates after evidence capture.

## Configurations and remaining acceptance

All optional/capacity configs share unique run roots, bounded console errors, JSON reports, screenshots/traces and the page/peer diagnostic recorder. Optional servers keep configuration ownership and log through `log-browser-process.ts`; capacity-owned children keep their lifecycle and log each start via `captureProcess()`. Fleet/station capacity continue using their canonical managed worker as before. Selection rules, worker counts, timeouts, expectation budgets and measurement windows are preserved. Full payload capture adds diagnostic overhead to capacity runs; compare performance measurements with the instrumentation policy recorded.

The complete default suite is deliberately left to the user for local execution. Increment 2’s original 200-case campaign remains unchecked historical acceptance; do not restart it automatically. Full extended/continuation/capacity acceptance remains separately recorded in the execution checklist.


## User default-run follow-up (17 November 2026)

`tmp/browser-run-vfQMaq/summary.json` contains 408 results: 400 passed, seven failed and one timed out, with zero retries and no skipped cases. The failures were introduced by test helper assumptions during migration:

- The mixed-state refinement required fleet, queue and stationGraph together. Six fleet cases legitimately have fleet/queue without stationGraph; the station-statistics case has stationGraph/queue without fleet. Refinement now requires only explicitly requested sections. Exact graph/save assertions retain every field. A Deno regression checks independently decoded fleet, station and geometry fixtures and rejection of a requested missing section.
- The synthetic demo test waited for a draw after deliberately hiding the page, where painting is suspended. Its two-frame absence-observation window is restored. The ready-draw helper also records completed demo draws independently of the live connection gate and requires the rendered reduced-motion preference to match the current media query. A held-frame control checks preference changes cannot consume an earlier ready draw.

The complete default result remains failed historical evidence. Focused repair results and artifact roots are recorded in `tmp/web-browser-test-overhaul-checklist.md`; a combined clean default-suite acceptance remains separate.
