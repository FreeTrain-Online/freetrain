# P4-21 mixed-world persistence and continuation — 1 November 2026

The browser/portable P4 persistence gate is complete. Save8/protocol6 remain
current. This step adds integration regressions; it changes no gameplay, prices,
save schema or profile registration. Source geometry and booking expectations
remain in the [independent railway fixtures](web-parity-railway-fixtures.md) and
individual tool suites. Byte comparisons here establish persistence and
continuation equivalence, not an independent source behavior oracle.

## Combined network

`MixedContinuationTests` builds a legal **128×160×10** terrain-backed world through
session construction commands. Its **197 railway voxels** include ordinary and
junction track, connected flat/ramp/elevated track, bridge and girder rails with
elected piers, two tunnel corner backups, garage rails, a signal/controller
reference, all four electric poles, surface and underground ramps with their
fillers/supports/walls/ground flags, all 54 completed stationary variants, and an
active two-level construction site. All three speed lists and a slope limit are
present. Owner authoring mode and realized forest decoration remain saved.

The initial terrain is an explicit legal test setup with two Mountain occupants;
it is not a generator acceptance case. The scenario starts during a working day,
completes the 54 accessory variants, then adds the retained active site. Working
hours, callback order and construction RNG continue through save/reload. No
prospective road/car/platform binding is installed. Their trusted interface
fixtures remain in P4-20; real gameplay remains P5/P6/P7.

The save contains the exact installed bundled pack, version **2**, catalog hash
`316dc169c56d3096bb9e49031ab10d47a2d9015c960b730ecb394f8371dc8b6e`.
The generated network has session revision **2179** and SHA-256
`a454c8ce0e16383322f575b857f8ad0d08e5d7811be0f5c4dc996d3b1e587003`.
These identify this run, rather than introducing a new permanent pack pin.

## Regression boundaries

- Twelve removal cases save before and after normal, bridge, girder, tunnel,
  garage, signal, pole, speed-list, surface-ramp, underground-ramp, completed
  accessory and active-site removal. Live and reopened sessions execute the same
  legal command independently. Tunnel/garage cases include reverse half-open
  endpoint removal. Every case opens real filesystem checkpoints before/after
  removal and compares a further 1,000 minutes against uninterrupted continuation.
- A cumulative sweep removes **all 54 stationary variants and all four poles**
  from one world, reopening before each removal and starting a fresh checkpoint
  session after each result. Complete canonical bytes compare allocator/IDs,
  owner revisions, topology, terrain/backups, ground flags, account journal/cash,
  ordered speed lists, clock/scheduler/RNG, authoring mode and required packs.
- The intact world advances **2,200 minutes**, with repeated intermediate
  reopenings, until its retained site completes. All random streams produce the
  same subsequent draw. Construction callbacks and completion booking agree.
- Real HTTP validate/upload/download, all three named slots, slot-2 replacement,
  simultaneous distinct participants, socket reconnect and server startup from
  checkpoint preserve the complete network. The test socket reassembler now
  reunites packed terrain owners with railway owners retained in the header;
  the full entity count is asserted after reassembly.
- Corrupt, missing-handler, absent-pack, incompatible-hash and oversized startup
  input retain the current world/epoch and checkpoint input. Sparse oversized
  checkpoint/slot files exercise bounded reads without posting 256 MiB. Correct
  input subsequently loads and saves successfully. Pack/hash failures report
  `incompatibleSave`; malformed graphs report `invalidSave`.
- EN and JA (430×850) browser workflows save to slot 2, download exact bytes,
  confirm slot replacement, observe a peer epoch change, reconnect/reload, reject
  corrupt/incompatible/oversized uploads and recover through valid upload.
  Oversized browser input uses a sparse local file and is rejected before upload.

## Verification and reproduction

- Full Release **1,114/1,114 .NET** (`tmp/p4-21-dotnet.log`). After the socket
  reassembly assertion, final mixed/network regression **67/67** passes
  (`tmp/p4-21-targeted-final.log`). There are **20 new .NET cases**.
- Client **220/220**; production build, generated contracts/content, locales and
  Svelte checks pass (`tmp/p4-21-client.log`, `tmp/p4-21-build.log`). The existing
  large-chunk build advisory remains.
- Python **162 tests, one skip**, 66 pinned railway sources, 1,484 independent
  vectors, 64 accessory bindings and project boundaries pass. Whitespace passes.
- Final EN/JA Chromium **2/2** passes (`tmp/p4-21-browser-final2.log`). Initial
  test assertions incorrectly expected the save panel to survive replacement
  and supplied files before asynchronous availability enabled the input. Final
  tests wait for enabled input and verify authoritative epochs/canonical bytes.
  Captures in `tmp/browser-evidence-hiLVMR/mixed-continuation/` were visually
  inspected. Reconnect proxy-reset messages did not cause page errors.

Run the required mixed-network browser acceptance lane after a Release .NET build:

```sh
deno task --cwd web test:browser:continuation
```

This generates a temporary gameplay-built network through `MixedContinuationTests`
and requires both browser cases. Missing or failed fixture generation fails the
lane. Comprehensive validation (`test:browser:all`) includes this lane.

To generate a retained scenario artifact manually from the repository root:

```sh
FREETRAIN_MIXED_NETWORK_OUTPUT="$PWD/tmp/p4-21-network.json" \
  .tools/dotnet/dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore \
  -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~MixedContinuationTests
```

Then use `playwright.continuation.config.ts` with the same absolute
`FREETRAIN_MIXED_NETWORK_OUTPUT` value. The default browser configuration excludes
these cases; the dedicated configuration fails if its required fixture is absent.
Ordinary backend regression also builds and tests the scenario. The generated save
is an execution artifact, not a committed expected-output oracle.

The owner-requested network was uploaded to the running local game, saved via
the **slot2 HTTP endpoint**, and compared byte-for-byte with the live download
and `src/FreeTrain.Online.Server/data/saves/slot2.json`. Prior live-world and slot-2
bytes are retained under `tmp/p4-21-live-world-before.json` and
`tmp/p4-21-slot2-before.json`. The network remains active and paused.

P4-22 multiplayer/UX, P4-23 capacity and P4-24 integrated acceptance remain open.
Historical desktop import remains P10. Optional-suite compatibility, native and
human-art fidelity qualifications retain their existing status. This local
filesystem/server restart evidence does not replace P4-23's Linux distribution
and measured legal-network capacity gate.
