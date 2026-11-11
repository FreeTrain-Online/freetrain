# P7-04 generic construction contracts — 11 November 2026

This slice executes **detached** fixed/variable city construction using the
existing world-local clock, random bank, spatial allocator and accounting.
[The pinned source contract](web-parity-city-contract.md),
[independent fixtures](web-parity-city-fixtures.md) and
[canonical city state](web-parity-city-state.md) remain its inputs. Live city
save/transport/authority is P7-05; actual land-value sampling, family placement,
reclamation and station listener attachment follow P7-06/07/10. Save10/protocol9,
prepared content identity, supported limits and shipped EN/JA text are unchanged.

## Source and production bindings

Authority is stable ExAV 2.1.1.12, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, subtree `trunk`.
The exact original-file hashes below are also in the immutable P7-02 fixture
source ledger; `scripts/city-fixtures.py --check --source-root ...` verifies them
against the original Git revision. The fixture generator and expected values
were not changed to fit the implementation.

| Source symbol | Production consumer / independent witness |
| --- | --- |
| `ConstructionSite.VoxelImpl`, constructor / `canProceed` / `clockHandler` / `registerClockHandler` | `ConstructionLifecycle.Initial`, `CanProceed`, `Progress`, `Delay`; shared by `StationaryConstruction` and `CityConstruction`; all 16 gate vectors and five literal traces |
| `MultiSelectorController.onClick`, `CompletionHandler.handle` | `CityConstruction.StartBooking`, durable `CityStructure.Caller`; catalog caller starts a timed site and books the selected fixed unit price / variable height price to Subsidiaries |
| `FixedSizeStructController`, `VarHeightBuildingController`, Commercial/variable development plans | `LegacyPlayer` makes no price booking and completes owned; `Automated` makes no price booking and completes unowned; automatic search execution remains P7-25–27 |
| `Commercial`, `PopulatedStructure`, `VarHeightBuilding`, `SubsidiaryCompany` constructor | Completion removes the site, allocates a fresh identity, initializes source valuation and subsidiary timing, and adds the real population descriptor only on completion |
| `VarHeightBuilding.remove`, `PThreeDimStructure.remove`, site removal | `RemovalBooking` and `Remove`; completed fixed/variable demolition spends `500000*sizeX*sizeY` using checked Int32 source product, then Int64 booking; site cancellation makes no booking |
| `TimeLength.random`, subsidiary constructor `registerClock` | Saved `time-length` stream; delay truncates `NextDouble()*20160+20160`; exact callback descriptor retained, never redrawn on restoration |
| `Clock`, `PriorityQueue` | Existing `Clock.Restore` / `Advance`; all four independent FIFO/cancellation vectors, eight-voxel registration order and two simultaneous completions |

Source hashes:

| Original source file | SHA-256 |
| --- | --- |
| `core/world/structs/ConstructionSite.cs` | `01ca68c326b92a636a2993df016b549afec2a2bb4ee1b1a36d2cb50f1a88fab8` |
| `plugins/jp.co.tripod.chiname.lib.generic/MultiSelectorController.cs` | `0123f4548b14b7d4af8ca7dadb43f588c26e72d42bfc86ece74a0b12a7af2c62` |
| `core/world/structs/Commercial.cs` | `116a7ba515bb57f10a561cbee1699a3e3ca18cdf1684a372056457a9e03c230d` |
| `core/world/structs/VarHeightBuilding.cs` | `e87d03b9b270f6958e671482fc8da16eb8741a31f7ee156252335a1cb90995d5` |
| `core/world/subsidiaries/SubsidiaryCompany.cs` | `fed908354ba713f3a9d96bdd41724f5b8a23bf64761c4eecd2cef68cd1beb9e6` |
| `core/world/TimeLength.cs` | `aa61fda8d34f58729a8bcee201132258cbde29188bb8500fb6f4b847acf54b33` |

## Lifecycle and atomic publication

`CityConstruction.Start` is a reusable primitive for an already cleared, level
footprint. It checks full/invisible occupancy with `CityOwnership.Placement`;
reclaimable owners require the caller's atomic teardown before this primitive.
It does not implement later family/control-mode reclamation policies. It rejects
half structures, which have a separate immediate/shared-owner lifecycle.

Site cells are allocated in **Z, X, Y order**. Every non-southwest voxel draws
`Next(4)` for its door; the southwest corner skips that draw on every floor.
Each unfinished voxel owns exactly one one-shot handle at `40+Next(80)` minutes.
Sites are unowned, nonreclaimable, zero-valued and have no subsidiary or producer.
Ground/door state persists independently of the future rendering layer. The shared
kernel preserves empty → bone1 → bone2 → bone3 → walled, the decorative
hole/steel/woods/machine branch and its extra turn, work hours 09:00–17:59,
weekend rejection and below/above floor minima. Weekday holidays do not block work.
A denied gate draws only the next retry delay. A walled voxel never reschedules.

`AdvanceOneMinute` restores detached copies of the existing clock and random
bank. It dispatches all due work in FIFO order and returns one validated
`CityCandidate`. Other railway/train/station/heat/development/subsidiary timers
require an explicit trusted `CityScheduledCallback` consumer when due. A missing
consumer fails the minute instead of silently removing or simulating that timer.
P7-05 owns the integrated live dispatcher and public codecs. The pending handles
for newly completed subsidiaries are bound before their scheduling; their
callbacks cannot execute in the same minute as completion.

The last walled voxel completes once. The site disappears, a fresh completed
owner is allocated, and ownership follows the **saved caller**, rather than
being inferred from the site's unowned flag. Fixed population descriptors are
prepared before subsidiary initialization; variable population descriptors follow
it, matching the source constructor order. Actual `StationCatchment.Attach`
requires P7-05/07's producer boundary and is not invoked as controlled supply.
Completed values use `CityValuation.Reprice` and the source structure price with
an explicit trusted **actual total-land-value input**. No default land price or
constant valuation is installed. The tests' land value 20 is labeled fixture input.

Cancellation removes all of that owner's pending handles and has no completion,
refund, price booking or random draw. Completed demolition also cancels the
subsidiary timer, removes producer descriptors and voxel fence references, and
makes the source removal booking. A cancelled callback cannot later resurrect an
owner. Opening/reading state creates no registration; browser panels retain the
existing railway lifecycle without dispatch side effects.

`ConstructionBooking.None` remains distinct from a zero-valued spend call.
Catalog zero price still creates a Subsidiaries transaction. City starts respect
the existing 8,192-entry journal bound **including reservations for pending railway
accessory completions**. City completion has no additional price booking. Queue
admission retains the existing 65,536-action limit and 119-minute maximum site
delay. Checked allocator/order/time/revision/valuation/account arithmetic and
whole-envelope validation happen before returning a publishable successor.
A failure in the second equal-time completion discards the first completion,
its producer, subsidiary callback and timing draw as well as the failed work.
No prior world, allocator, account, random state or queue is changed.

## Saved randomness, settings and later consumers

CITY-D008/012/020 retain their recorded adaptations; no new source correction is
introduced. Process-static generators are represented by the existing saved
`RandomStreamsState`; no second city RNG bank or process-wide mutable settings
is activated.

| Consumer | Saved stream / order / owner |
| --- | --- |
| Site constructor/progression/retry | `construction`; exact Z/X/Y door-delay and branch order now executed by both city and accessory sites |
| Random land / forest | `land-selection` / `forest`; retained P7-02 bounds and legal-cell X/Y traversal; execution belongs to P7-16/17 |
| Creek | Existing coordinate-local endpoint-seeded generator; consumes no saved stream |
| Reachable heat diffusion | No random draw; durable q/back/rho, per-world settings and captured FIFO timer; execution P7-06 |
| Development | `development`; retained source selection/search/land-size order and `CitySearchPlan` continuation; execution P7-25–27 |
| Subsidiary timing / market | `time-length` for constructor timing now; `subsidiaries` for later conditional market draw, then timing; market dispatch remains P8 |
| Catalog/gallery/panels | Local UI/preview state; never draws from the authoritative bank or registers a construction callback |

`CityDevelopmentSettings` remains per-world durable data. No static source
parameter assignment, timer restart or frequency edit is performed by this slice;
P7-28 owns complete safe settings admission and controls. FIFO registration order,
not feature priority, determines ties among construction, station resets,
development, diffusion and departures.

## Verification and qualifications

`CityConstructionTests` consumes the independent gates, traces, constructor-order
and FIFO fixtures. Runtime tests additionally cover caller-specific charges and
ownership, full state/account/clock/random JSON continuation, multi-floor
completion, cancellation, zero-call distinction, journal reservation, arithmetic
rejection and whole-minute rollback after an earlier successful completion.
Existing `StationaryConstructionTests`, `StationBuildingConstructionTests`,
`ClockTests` and `RandomTests` protect accessory behavior, immediate station
construction and the shared scheduling/random contracts.

Focused execution results are recorded below and in
`tmp/p7-04-validation.json`, with commands, logs, tool versions and artifact hashes.
No entire unit or web suite is required or claimed. The Chromium regression is the
existing **railway accessory** EN/JA lifecycle and 54-variant art gallery, including
peer/reload/cancellation, observer, keyboard and narrow-layout checks; it grants no
city browser, native/human, optional-content, capacity or P7-exit acceptance.

Fresh results: **146/146 focused .NET tests**, including 14 new city lifecycle
cases; **58/58 focused web tests**; **3/3 Chromium accessory workflows**;
**31/31 focused city Python tests**. The build has zero warnings/errors;
Svelte checking has zero warnings/errors. Vite retains its existing large-chunk
advisory. All 703 source fixtures / 197 original files, P7-01 review, project
boundaries, generated contracts, content and both locales pass. No complete
suite was run. The sandbox initially blocked the .NET runner's loopback socket;
the focused run passed with local test-runner access.

Reproduce the affected checks:

```sh
dotnet build tests/FreeTrain.Online.Tests --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false
dotnet test tests/FreeTrain.Online.Tests --no-build --no-restore -m:1 -nodeReuse:false --filter 'FullyQualifiedName~CityConstructionTests|FullyQualifiedName~CityStateTests|FullyQualifiedName~StationaryConstructionTests|FullyQualifiedName~StationBuildingConstructionTests|FullyQualifiedName~ClockTests|FullyQualifiedName~RandomTests'
deno test --cwd web -A --no-check tests/railway-renderer.test.ts tests/mixed-state.test.ts tests/railway-edit.test.ts tests/object-removal.test.ts
deno task --cwd web build
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task --cwd web test:browser:run tests/browser/stationary-construction.spec.ts --project=canonical --workers=1
python3 -m unittest discover -s scripts/tests -p 'test_city*.py'
python3 scripts/city-fixtures.py --check --source-root /path/to/freetrain-exav-archive
python3 scripts/review-city.py --check
python3 scripts/check-project-boundaries.py
git diff --check
```
