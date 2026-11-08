# Full fleet continuation and recovery — 7 November 2026

P5-27 verifies the current Save9/protocol7 fleet across save, reconnect and
checkpoint boundaries. The production codec, movement dispatcher and atomic file
writer required no changes. Canonical Manual driving remains retired under the
[owner decision](web-parity-fleet-manual.md).

## Contract and coverage

This slice consumes the pinned ExAV source and independent literals in the
[train contract](web-parity-train-contract.md), [fixture review](web-parity-train-fixtures.md),
[fleet transport contract](web-parity-fleet-transport.md) and
[runtime save policy](web-parity-runtime-policy.md). It does not change source
semantics, content bindings, translations, versions or admission limits.
The source train fields, one-shot Clock registration/FIFO semantics, TAT shared
references and file protection implement MAN-F01–07, MAN-T06–08,
MAN-W07–10, MAN-A10–13/17 and DEV-P14/18–20/24 at current fleet scope.
[Reciprocal actions](web-parity-actions.md#p5-27-fleet-continuation-and-recovery)
and [manual bindings](web-parity-manual.md#p5-27-fleet-continuation-and-recovery)
record the same scope.

[`FleetContinuationTests.cs`](../tests/FreeTrain.Online.Tests/FleetContinuationTests.cs)
adds twelve cases:

- Six continuation scenarios reopen at **every minute**, including quiet minutes
  immediately before/after callbacks: legal gameplay-built two-train octagonal
  ring, opposing collision retries, real signal fixed wait/reverse departure,
  signal TAT Reverse2/TURNING, Outside51→50 exchange with forwarding and
  Outside0→return. Each recovered run matches the uninterrupted final save and
  each subsequent minute byte for byte. Occupancy, unique registration IDs and
  exactly one future callback per deployed train are checked throughout.
- Placement, explicit reversal, withdrawal and redeployment execute on both sides
  of reopening, with exact follow-on state and callback cancellation.
- Real group create/move/rename and shared TAT/delegation assignment commands
  execute on independent hosts before/after reopening. Successive movement and
  saves retain the same references, group ordering and revisions.
- Three sale cases surround the 365-day purchase anniversary by one minute;
  original purchase minute 100 and exact ¥45m/¥30m/¥30m receipts survive reopening.
- A partial orphan temporary file simulates an interrupted writer. Opening a new
  writer preserves exact prior checkpoint/slot bytes. A destination directory
  forces failure after temporary-file writing but before replacement; cleanup
  removes only the failed writer's temporary file and preserves the prior files
  and orphan. The retained fleet checkpoint decodes with the original graph.

Relevant existing tests also run freshly: literal callbacks and queue order,
operating-cost boundaries, all Outside choices and the full 100→50→0→return trip,
reversal/TURNING, wait/departure, shared controller/group edits, world-local
random/account/clock isolation, rejected graph/version/size payloads and protected
startup checkpoints. Complete saved bytes include move/stop counts, forwarding,
boarding origin, purchase time, accounts, random streams and both queue copies.
No passenger/station adapter test grants P6 gameplay credit.

[`fleet-continuation.spec.ts`](../web/tests/browser/fleet-continuation.spec.ts)
uses the ring produced by the unit matrix and its uninterrupted minute 116 oracle.
EN and JA owner workflows with a Japanese peer save slot2, download its exact
bytes, reject corrupt/version/pack/dangling-controller/oversized uploads, reload
the protected named slot, reconnect, stop/start the actual backend process and
continue sixteen single minutes. The resulting download must equal the complete
uninterrupted unit oracle, including both trains' formations, all queue identities,
operating transactions and tick. Each peer adopts the same fleet/occupancy/queue.

The existing browser backend fixture accepts
`FREETRAIN_ONLINE_TEST_CONTAINER_IMAGE` to repeat these same assertions against a
Linux runtime. Current managed server output and source mount read-only; the
worker's isolated save directory is the writable mount. Browser assets still
come from the current local production build. This verifies portable fleet
operation and process/checkpoint restart, not clean distribution packaging (P5-29).

## Verification

- Relevant .NET unit selection: **466/466** Debug; `tmp/p5-27-focused-unit.log`.
- Selected client unit tests: **54/54**; `tmp/p5-27-client-unit.log`.
- New recovery cases plus world-isolation cases in Linux arm64: **14/14**;
  `tmp/p5-27-linux-unit-complete.log`.
  The existing `freetrain-online:p4-23` image supplies only its .NET 10 Linux
  runtime; mounted current assemblies supply the tested code. The managed VSTest
  runner uses `DOTNET_ReadyToRun=0` to avoid host-specific SDK precompiled code.
- Selected Chromium owner/peer recovery workflows: **2/2 host** and **2/2 Linux
  container**, with both languages enabled and no skips;
  `tmp/p5-27-host-browser-complete.log` and `tmp/p5-27-linux-browser-complete.log`.
  Initial test-fixture failures were corrected: localized Step matching, distinct
  authenticated peer identities and a stable Docker Desktop save bind-mount root
  across case reset. Containers use an init process and disable core dumps.
- Production build/generated/content/locales/Svelte checks pass (zero Svelte
  errors/warnings; existing bundle-size advisory); `tmp/p5-27-web-build.log`.

Validated input SHA-256: `7af265eaee18aa71c159a3e81d77bae34c74939dfa9637578fc70c89f0919116`;
uninterrupted final oracle: `e2685d41916595b3a6d2f15fd2332696899ab52cba8d56ac921d0aa72613c490`.

Reproduce the relevant backend selection from the repository root:

```sh
FREETRAIN_FLEET_CONTINUATION_OUTPUT="$PWD/tmp/p5-27-ring.json" dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetMovementTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~FleetStorageRecoveryTests|FullyQualifiedName~FleetTradingTests|FullyQualifiedName~SaveRejectionTests|FullyQualifiedName~RollingStockTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
```

From `web/`, after that command generates the validated ring and `.expected`:

```sh
deno task build
deno test -A --no-check tests/fleet-state.test.ts tests/fleet-controllers.test.ts tests/fleet-groups.test.ts tests/save-transfer.test.ts tests/state-transfer.test.ts tests/mixed-state.test.ts tests/remote-client.test.ts
FREETRAIN_FLEET_CONTINUATION_OUTPUT="$PWD/../tmp/p5-27-ring.json" FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug deno task test:browser:run fleet-continuation.spec.ts --project=canonical --workers=1
```

Add `FREETRAIN_ONLINE_TEST_CONTAINER_IMAGE=<local-dotnet10-runtime-image>` to the
last command to repeat the actual process recovery workflow on Linux. The new
browser cases explicitly skip without their unit-generated input; reported
P5-27 runs enable both cases. No exhaustive suite, other browser engine, native
display, optional-pack, historical import, full-city or moving-capacity acceptance
is claimed. Those gates retain P5-28–30/P10/P11 and their existing owners.
