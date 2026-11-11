# P6-25 station continuation and recovery — 11 November 2026

This step verifies current Save10/protocol9 station continuation using the
production codec, station and train dispatchers, atomic storage and backend.
Stable ExAV `883cf7d81cf49db25ff38cd3a25d0f140afe31a0` remains the behavior authority.
There is no production behavior, save format, wire, content or locale change.
The [station contract](web-parity-station-contract.md),
[transport](web-parity-station-transport.md),
[passenger service](web-parity-passenger-service.md) and
[runtime policy](web-parity-runtime-policy.md) retain their scope.

## Continuation witnesses

[`StationContinuationTests.cs`](../tests/FreeTrain.Online.Tests/StationContinuationTests.cs)
adds eight cases. Stations and platforms are constructed, normal rails built,
stock purchased and trains deployed through gameplay domain operations. Controlled
population is attached to a real Traffic owner through the P6-16 provider boundary;
this remains an explicitly controlled P7 input, not evolving city supply.

Four scenarios run two actual connected stations with Simple passenger service,
TAT Reverse, Reverse2/TURNING and Daily departure at midnight. The reference run
compares every minute with a second run reopened before every scheduled callback.
Every actual callback also supplies cuts immediately before, at and after dispatch.
Each cut writes a checkpoint through a separate SaveStorage lifetime, reads it
through a new lifetime, checks exact round-trip bytes, the next three minute
successors and the complete final successor against uninterrupted execution.
The midnight case requires a train and both stations' hour/day callbacks all due
at 1440; this verifies actual FIFO continuation rather than only a clock fixture.
The service case requires a positive stationUnload fare after actual travel.

Complete canonical comparisons include both listener directions and provider
metadata, platform/host ownership, timer registrations and order, passenger
origins, logs/averages, random streams, occupancy, allocator and ordered accounts.
Every deployed train retains exactly one callback. Station hour/day registrations
must match the queue after each resumed minute; handler identities are unique.

A fifth case applies immediate station placement, host removal, rebuild and
explicit platform rehosting on independent live/resumed branches during a real
station wait. Disk checkpoints surround each edit; exact bytes and the next minute
must agree, including host identity, listeners, timers and construction bookings.
Generic delayed construction remains a P7 companion, not station placement.

Three HTTP/storage cases reject corrupt JSON, missing station recipes and pack
hashes at startup, discard only the transient checkpoint under the owner policy,
preserve the named rejected slot, upload an actual boarded station world, reject
the bad slot without changing that world, write a valid checkpoint and reopen the
server with the exact boarded state and rejected named file intact.

## Browser and process witnesses

[`station-continuation.spec.ts`](../web/tests/browser/station-continuation.spec.ts)
consumes four freshly exported domain boundary saves and their uninterrupted
oracles in English and Japanese. Each journey saves slot2, downloads exact bytes,
rejects corrupt/version/pack/dangling-host and oversized inputs, loads the retained
slot, reconnects, stops/starts the backend and continues through the oracle.
Service starts immediately before boarding and continues through arrival/unload
at the second station; Reverse and TURNING start before the departure callback;
Daily starts immediately before midnight. Each final download must match every
byte of the uninterrupted oracle. Socket publications to both peers must agree
at each continued minute, including graph, fleet, queue and accounts.

The same cases run on a Linux container runtime with current managed assemblies
mounted read-only and isolated writable save directories. This is process/runtime
portability evidence; clean distribution packaging remains P6-27. Reconnect and
transfer bounds also consume the existing client transfer/remote-state tests.
The P6-24 lost-acknowledgement/idempotent retry and Observer cases remain the
ambiguous-outcome regression; no new retry or receipt semantics are introduced.

## Verification and reproduction

Fresh verification passes **602 Release .NET station/save/recovery tests**,
including all eight new cases, with no skips (`tmp/p6-25-backend-final.log`).
All **eight new cases also pass on Linux arm64** (`tmp/p6-25-linux-unit.log`).
The container supplies the .NET 10 runtime; current managed test assemblies and
the previously retained managed VSTest runner are mounted read-only, with
`DOTNET_ReadyToRun=0`. The four exported oracle pairs, minute bounds, required
pack identities and SHA-256 hashes are indexed in `tmp/p6-25-oracles.json`.

**20 targeted client transfer/remote/station state tests** pass
(`tmp/p6-25-client.log`). The production build, generated contracts/content,
EN/JA locales and Svelte/TypeScript checks pass with zero Svelte errors/warnings
(`tmp/p6-25-build.log`); the existing bundle-size advisory remains. Project
boundaries and `git diff --check` pass.

The final Chromium host pass is **11/11**: all eight EN/JA continuation journeys
plus the three existing P6-24 lost-acknowledgement/authority/readiness scenarios
(`tmp/p6-25-browser-final.log`). An earlier host pass also completes all eight;
these are repeated cases, not extra distinct coverage. All **eight continuation
journeys pass on Linux arm64**, with actual container stop/start and socket-level
peer assertions (`tmp/p6-25-browser-linux-arm64.log`). No cases skip in these runs.
The `freetrain-online:p4-23` image supplies only the Linux .NET runtime; current
managed server output and content are mounted read-only. This does not grant
clean-image distribution credit.

An initial emulated amd64 attempt using `freetrain-online:p5-29` times out opening
the Session menu after slot load. The next case is interrupted and the remaining
six do not run (`tmp/p6-25-browser-linux.log`). No amd64 browser acceptance is
claimed. The native arm64 run executes the same production code and all eight
journeys successfully; no production behavior correction was required. Test
harness refinements observe actual socket publications and keep simultaneous
host/Linux runner artifacts separate. The initial sandbox .NET runner could not
bind its local test socket; verification reruns use authorized local execution.

From the repository root:

```sh
FREETRAIN_STATION_CONTINUATION_OUTPUT="$PWD/tmp/p6-25-station.json" dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~P6_25'
dotnet test tests/FreeTrain.Online.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~SaveRejectionTests|FullyQualifiedName~FleetStorageRecoveryTests'
```

From `web/`, `deno task test:browser:station-continuation --workers=1` builds the
client, generates and requires all four fresh oracle pairs and executes all eight
browser cases. Set `FREETRAIN_ONLINE_TEST_CONTAINER_IMAGE` to a local .NET 10 Linux
runtime image to repeat the same process workflows there. Direct browser runs
without `FREETRAIN_STATION_CONTINUATION_OUTPUT` explicitly skip; the task fails on
missing fixtures and reported acceptance runs supply every fixture.

Historical desktop imports remain P10. P6-26 capacity/soak, P6-27 clean packaging,
P6-28 phase acceptance, native/original-runtime, other browser engines, optional
content and full-city P7/P8/P11 gates remain open at their recorded scopes.
