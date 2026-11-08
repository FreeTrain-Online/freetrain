# P6-03 canonical station state — 8 November 2026

Complete at detached state scope. The [source contract](web-parity-station-contract.md),
[independent fixtures](web-parity-station-fixtures.md) and
[train handoff](web-parity-p6-train-handoff.md) supply the inputs. This step adds
no construction command, service callback, passenger generation, live rendering
or browser station gameplay. P6-04 owns booking/scheduler lifecycle and P6-05
owns durable saves/transport. Save9/protocol7 and prepared catalog bytes remain
unchanged.

## Exact portable bindings

[`StationProfiles.Build`](../src/FreeTrain.Online.Content/StationProfiles.cs)
attaches a typed station profile to the existing bundled RailwayProfile without
adding active railway/tool handlers. The existing 213 railway bindings remain
unchanged. The new profile binds:

- All **35** exact StationContribution declarations: qualified definition/recipe,
  source name (group fallback where name is absent), X/Y footprint, height,
  Int64 purchase price, Int32 daily operation cost and Gray height-cut color.
  Each contributed orientation remains its own exact definition.
- **149** source geometry recipes: eight thin, two wide-center, one yard,
  42 passage and 96 stair recipes. Typed frame data retain exact picture IDs,
  rectangles, anchors and invalid-source-crop status. Their qualified references
  resolve the existing P2 seasonal/lighting media; no replacement art is generated.
- All **six** departure bells: qualified contribution, source name, manifest-relative
  WAV path, prepared media URL and SHA-256. Selection is state; playback and
  committed departure events remain P6-20.

The three material-specific stair-index23 recipes remain diagnostic, covering
the six material/lighting cases from P6-02. Their `(388,4,32,36)` crops remain
unchanged and `Ready` is false. P6-15/27 retains EXAV-GAL-001 correction and
visual acceptance. Binding the diagnostics does not authorize rendering them.

## Immutable graph and spatial ownership

[`StationGraph`](../src/FreeTrain.Online.Simulation/Stations.cs) snapshots its
input sequences into immutable arrays/frozen lookup tables. It retains separate
station building and logical platform identities/revisions, world-local automatic
name sequences, names, direct host and reciprocal ordered children, lane masks,
direction/length, roof choices, bell references and exact yard index/lane/Traffic
membership. Plain/passage/stair outlooks retain their material, bridge and upward
choices. Standalone passages remain cosmetic Traffic components; they do not
become service stations or additional spatial owners.

Station state includes gone/accumulated loaded/unloaded counters and three
transport logs (imports, exports and trains). Each log retains today's and
yesterday's Int32 totals and the source double-precision weekly accumulator and
previous-week result. Derived demand and averaging calculations retain their
later consumers. Full scheduled-action records can retain registration, FIFO
order, due minute, handler and interval. Null timers represent an inactive detached
fixture, with both station timers present or absent together. P6-04 must bind and
validate active queues, alignment/relative first delays and cancellation before
any live construction is enabled.

[`MixedWorld`](../src/FreeTrain.Online.Simulation/MixedWorld.cs) carries the graph
alongside terrain, railway and fleet state using the existing allocator and
combined SpatialIndex:

| Member | Canonical occupancy |
| --- | --- |
| Station building | One owned, nonreclaimable, zero-value SpatialEntity covering the exact full-height footprint; opaque, including 1×1 buildings. No ConstructionSite or railway payload. |
| Thin platform | Logical ID shares the allocator but has no whole-voxel SpatialEntity. Each yard belongs to its actual transparent Traffic entity. |
| Wide platform | Logical ID also owns the opaque center strip. Lane0 is right90, lane1 left90; either/both/neither lane can remain. Yard Traffic owners stay separate. No roof. |
| Yard rail | Existing aligned straight normal rail geometry plus exact platform-art reference and graph membership; no car or road ownership is transferred to the platform. |
| Cosmetic passage/stair | Separate state on an actual Traffic cell or thin yard; never a host merely because it resembles station art. |

Thin art selection retains `direction.index + (hasRoof ? 1 : 0)`; its eight
independent drawing vectors also verify the selected crop. Opacity follows source
PThreeDimStructure.VoxelImpl / FatPlatformVoxel inherited
Voxel behavior, rather than RailStationaryStructure's special transparency rule.
Thin roof bounds retain `length/4 <= index < length-length/4`.

Validation checks the whole graph before returning a MixedWorld: identity and
allocator uniqueness across spatial/fleet/logical members, exact footprints,
ownership/value/transparency, rail alignment, complete lane/index membership,
recipe/bell availability and orphan platform rails. Both host-reference directions
must agree, every connected chain must resolve to a real StationBuilding, and
self/dangling/cyclic/unresolved/duplicate children reject. Decorative
railStationary definitions cannot resolve as service station definitions.

Successors preserve the graph by default, reject reused IDs and kind/definition/
location/geometry repurposing, and require one-step revisions for changed state.
Value-equal immutable arrays remain legal without artificial member revision
increments. Passage changes require their Traffic owner's revision. Fleet's
first-purchase detached copies also preserve station state. Failed validation
leaves the original world and allocator unchanged. Host discovery and recursive
reparent/disconnection actions remain P6-11/12; this step validates their input
and output graphs without activating those actions.

Current save and wire admission explicitly reject a world carrying StationGraph,
including an empty graph with retained name sequences. Snapshot, direct mixed
encoding and spatial encoding have the same guard. P6-05 must replace the guard
with explicit versioned contracts; silently losing the graph is inadmissible.

## Verification

[`StationStateTests`](../tests/FreeTrain.Online.Tests/StationStateTests.cs) adds
**14** production tests. They consume all35 literal station definitions, all32
thin/four-heading roof cases, all16 wide/lane-mask cases and the eight host-graph
witnesses, plus all eight literal thin-art selectors/crops. They compare all149
recipe frames and six bell resources against the
prepared catalog, and exercise malformed references, decorative-host rejection,
spatial/fleet/allocator collisions, logs/counters/timer validation, detached input
immutability, revision/ID reuse, passage ownership and save/wire rejection.
No expected fixture values are generated from production.

Verification on 8 November 2026 (.NET SDK10.0.402, Deno2.9.8,
Playwright1.64.1):

- Focused production tests: **14/14 pass**, including all literal geometry,
  station, host-graph and thin-art cases above; `tmp/p6-03-dotnet-focused.log`.
- Full final Release backend suite: **1,681 passed,zero failed/skipped**;
  `tmp/p6-03-dotnet-final.log`.
- Client tests: **315 pass** (313 ordinary plus two retention);
  `tmp/p6-03-web-unit.log`.
- Generated contracts, content, locales, Svelte/TypeScript and production build
  pass with zero Svelte errors/warnings; `tmp/p6-03-web-build.log`. The existing
  large-bundle advisory remains.
- Chromium: **19/19 pass**; `tmp/p6-03-browser-final.log`.
- Full Python: **221 run,220 pass,one existing skip**;
  `tmp/p6-03-python.log`.
- Independent fixture check: **821 cases/24 families/38 pinned sources pass**;
  `tmp/p6-03-fixtures.log`. P6-01 source review, project boundaries and whitespace
  pass; `tmp/p6-03-review.log`, `tmp/p6-03-boundaries.log`.

The initial sandboxed .NET runner could not bind its local test socket. Successful
backend/browser runs use local IPC/loopback permission. Reproduce from the
repository root:

```sh
python3 scripts/station-fixtures.py --check
python3 scripts/review-stations.py --check
python3 scripts/check-project-boundaries.py
python3 -m unittest discover -s scripts/tests
dotnet test FreeTrain.Online.slnx -c Release --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-editing.spec.ts fleet-controller-manager.spec.ts signal-integration.spec.ts
```

The browser workflows regress the historical station-editor harness plus existing
canonical shared Simple/TAT/signal/TURNING workflows in EN/JA, peer conflicts,
save/reopen and checkpoint continuation. They do not construct real P6 stations.
Native, other engines/devices, optional-suite, container and P6 capacity acceptance
remain outside this step.
