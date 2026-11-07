# P5-26 moving-fleet cross-tool and lifecycle regression — 7 November 2026

Purchased, deployed stock now supplies runtime evidence for the car boundaries
previously exercised by P4 trusted fixtures. The new cases use bundled stock,
three-car consists, production construction/deployment and scheduled callbacks.
No car component, pose or callback is inserted by a fixture. Empty terrain is
seeded; the tunnel journey additionally seeds actual Mountains and literal corner
backups. Browser journeys create an empty world, build track and buy/deploy
through the UI, without ignored save-file inputs.

Stable ExAV **2.1.1.12**, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, remains the authority.
[P4's dependency matrix](web-parity-cross-tool.md),
[railway decisions](web-parity-railway-contract.md),
[occupancy/guide handoff](web-parity-p5-railway-handoff.md),
[train contract](web-parity-train-contract.md) and
[literal train vectors](web-parity-train-fixtures.md) retain their pinned source
bindings. Prices, endpoint rules, train simulation, Save9/protocol7 and product
controls are unchanged by this regression slice.

## Moving-stock matrix

[FleetCrossToolTests.cs](../tests/FreeTrain.Online.Tests/FleetCrossToolTests.cs)
asserts handwritten outcomes through the FIFO session boundary. Final boundaries
check complete three-car occupancy, unique pending train registrations and exact
canonical save/read/write consistency. Rejections compare exact save bytes.

| Operation | Actual moving-stock evidence |
| --- | --- |
| Terrain / normal rail | Terrain edits at car hosts reject. A legal branch quote becomes stale when a consist enters its endpoint; refreshed construction rejects. Normal demolition skips cars and preserves the whole consist. |
| Bridge / girder | Trains move on built special tracks. Construction rejects occupied routes; dedicated removal preserves occupied hosts while charging every inclusive cell. At Z3/ground2, three removal visits cost 12,000,000. |
| Garage | Half-open conversion retains occupied garages. Two matching car visits still book cumulative subtotals 200,000 + 400,000; the destination stays untouched. |
| Tunnel | Construction reclaims Mountains and retains exact backups. Half-open removal of two occupied cells changes no corners and costs zero. A legal extension quote reaches an unreclaimed Mountain; train entry invalidates it. |
| Signal | Occupied placement/removal rejects. A blocked site becomes legal only after the tail clears and a fresh quote is obtained. Existing signal/controller suites supply shared effective-controller references, location reuse and referenced deletion. |
| Ramp | All four cardinal ramps and approaches are built, then a purchased three-car train moves onto each ramp. Whole-ramp removal becomes stale; refreshed removal checks all car hosts and rejects without partial slope/fleet/account publication. |
| Supports | Normal demolition retains source GEO-004 lower-pier teardown under car-skipped cells. Bridge/car hosts survive, the quoted lower pier disappears, and the one eligible special-rail no-op visit costs 8,000,000 at Z3/water0. Dedicated bridge/girder removal keeps piers under car skips. |
| Poles / speed / whole-object removal | Occupied Traffic accepts zero-cost pole/speed changes. Whole-object line demolition preserves ordered occupied skips, removes eligible hosts and cleans only absent speed members. Pole removal preserves the real rail/car host. |
| Stationary accessories | Placement at an actual car host rejects exactly. A legal construction site elsewhere keeps its callback registration while the three-car train moves; both lifecycles and complete occupancy reopen exactly. |

At ground2/water0, normal demolition across two car skips and one eligible cell
costs **6,000,000**. The current browser whole-object Remove workflow instead
retains its **zero-cost** policy. These are separately asserted operations.
P4's road/crossing/platform fixtures remain interface evidence; their gameplay
still belongs to P6/P7.

## Authority and lifecycle

Mixed geometry revision changes with train movement. FIFO commits reject an old
quote as `staleRevision`, then plan refreshed commands against current occupancy.
Newly legal work also requires a fresh quote. Rejection preserves the exact
current state, including previously scheduled movement and operating bookings.

Owner/Operator/Observer, pending/completed receipt deduplication, connection
expiry and epoch replacement run with a real deployed fleet. Same-ID retry after
reconnect returns the original receipt; a new ID carrying an old fleet query
rejects. Idle connection expiry removes mutation authority while stock stays
deployed. [The owner removed canonical Manual driving](web-parity-fleet-manual.md):
there is no current train-control lease to renew/expire. Obsolete Manual/lease
commands remain unavailable; prototype leases receive no canonical credit.

[fleet-cross-tool.spec.ts](../web/tests/browser/fleet-cross-tool.spec.ts) adds six
isolated Chromium journeys:

- English desktop and Japanese **390×844, 200% text** running fleets traverse
  every construction task without pausing time, changing car identities or
  booking work. Blur, visibility and pointer cancellation end held panning.
  Canvas identity and prepared images survive; sound enabling/bell preview works
  during Run, and role changes gate every tool.
- Cold join to a running purchased fleet retains WebSocket and image responses;
  gameplay controls remain hidden until a complete live draw.
- An Operator moves into an Owner's branch quote. The stale draft cannot confirm;
  refreshed construction rejects; whole-object removal skips both selected car
  hosts and removes the vacant endpoint at zero cost.
- A retained deployment reply cannot resurrect sold selection/ghosts. A retained
  reversal reply cannot restore old-world targeting after real save replacement.
- A dropped accepted reversal acknowledgement plus disconnect leaves exactly one
  reversed consist. Reconnect sends no replay. Explicit same-ID retry deduplicates;
  a fresh ID with the old query rejects.

Existing signal, timetable/group, construction, terrain, rendering, trading,
deployment, shell and sound-event suites provide companion coverage. Audio here
is preview/lifecycle evidence; station bell/passenger gameplay remains P6.

## Regression repairs

The fixture-enabled two-peer ring journey exposed a real retained-scene bug.
Viewport patches clip operations to the current raster bounds; publishing those
operations back into a small map's complete scene discarded offscreen cars.
Panning back could therefore leave cars absent from both pixels and picks after
movement/replacement. Small maps now retain their complete immutable scene;
large maps retain their existing viewport-only operation publication. A direct
offscreen-edit/pan regression and the real three-car ring journey cover this.

The full client suite exposed a renderer test assuming scene preparation starts
after one microtask. P5-25's asynchronous color-catalog preparation adds promise
boundaries. The test now waits for the actual scene request before releasing it,
preserving all front-buffer/progressive-rendering assertions.

The P4 translation audit found `RAIL-TEXT-078` still recording pre-P5 signal help.
Delivered EN/JA wording and its digest now match the shipped shared-controller
controls; pinned Japanese source/donor provenance remains unchanged. The Python
historical-inventory check also follows the former `controller` panel mapping to
Trains → Controller manager after Manual removal, while retaining ledger bytes.

The broad browser sweep exposed a Japanese pole-visibility assertion comparing
a retained raster with a fresh raster at a different pixel phase. It now starts
both comparisons from full presentations at the same panel/camera, preserving
exact pixel equality and the unchanged-save assertion. Terrain pixel checks wait
for the completed presentation rather than asset readiness alone. River-only
journeys explicitly request zero lakes, instead of assuming a river destroys a
separately requested lake.

## Verification

- Release .NET **1,648/1,648**, including **23/23** new integration cases.
- Client **311/311**, including the new offscreen-retention case and memory check.
- Python **189 checks: 188 pass, one existing skip**.
- Production/generated/content/locales/Svelte pass with zero Svelte errors or
  warnings and the existing bundle-size advisory.
- Train source review (**70 files**), train fixtures (**744 cases / 20 pinned
  files**), railway source/translation review (**66 files / 78 texts**), project
  boundaries and whitespace pass, including explicit pinned-archive reads.
- Focused Chromium **6/6** and shared shell/sound Chromium **12/12** pass.
  EN/JA captures in `tmp/p5-26-browser-evidence/fleet-cross-tool/` were inspected.
- Broad canonical Chromium initially **193 pass / five fail / five input-gated
  skips** across 203 selected cases. All five failures are resolved in the final
  **6/6** terrain/pole/moving-ring rerun. The additional ring case exposed the
  production scene-retention bug; the other repairs are documented above.
  Logs: `tmp/p5-26-browser-regression.log` and
  `tmp/p5-26-browser-repaired-final.log`.
- Final rebuilt-client fleet/rendering/tracking/runtime sweep **34/36** passes;
  the two remaining EN/JA operation cases reached a missing generated `.return`
  companion. The companion is generated by the independently checked
  `Equal_minute_return_waits_or_enters` test, without source changes; the final
  **2/2** operation rerun passes. Thus all **36 selected cases**, including the
  five previously input-gated cases, are verified. This is a focused final-build
  sweep, not a claim that the complete 203-case sweep was repeated after repairs.
  Logs: `tmp/p5-26-browser-final-regression.log` and
  `tmp/p5-26-browser-operations-final.log`. Final inspected EN/JA captures:
  `tmp/p5-26-final-browser-evidence/fleet-cross-tool/`; the repaired real-ring
  pixels/picks are captured in `tmp/p5-26-repaired-final-evidence/`.

Runs used macOS26.7.1 arm64, .NET SDK10.0.402, Deno2.9.8 and the pinned
Playwright1.64.1 Chromium. Literal test-source and inspected capture hashes are
in `tmp/p5-26-input-evidence-hashes-final.log`; all logs are under `tmp/p5-26-*`.
Reproduce the new cases from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --filter FullyQualifiedName~Cross_tool
cd web
deno task build
deno task test:browser:run fleet-cross-tool.spec.ts --project=canonical --workers=1
```

Full fleet recovery/process continuation and Linux/container verification remain
P5-27; moving capacity/performance P5-28; content/distribution P5-29; integrated
phase acceptance P5-30. No new native, other-engine, optional-pack, passenger or
full-city acceptance is claimed.
