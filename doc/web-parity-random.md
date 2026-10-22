# P1-03: versioned random streams — 22 October 2026

**P1-04 update — 22 October 2026:** [spatial contracts](web-parity-spatial-contracts.md) now use Save 5, ruleset `reference-spatial-4` and content version 2. Save 4 is retired without conversion; the implementation and counts below record the earlier P1-03 slice.

The canonical world now owns ten independent, versioned random streams. Save 4
stores their complete state, alongside the source-minute clock and rail graph.
Continuation preserves every stream's next draw through save/load, epoch
replacement, checkpoint shutdown and process restart. The live reference world
has no gameplay random consumers yet; this delivers the P1 foundation for them.

## Source ownership and portable decisions

Stable ExAV **2.1.1.12**, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, is the gameplay authority.
[The review](parity/random-review.json) records 17 hashed source dependencies,
generator owners, consumer handoffs and decisions **P1-RANDOM-001–005**.

ExAV uses `System.Random`, with a derived `RandomEx` for stock-market helpers.
Most generators are static and unsaved. `Economy._random` is explicitly stored in
the world's `otherObjects`; `SingleEffect.rand` and `RandomTable.rand` are separate
static generators. Creek construction uses a fresh endpoint-seeded generator.
UI selectors own transient generators. These distinctions determine the port's
consumption boundaries:

| Durable stream ID | Source generator owner | Consumer phase |
| --- | --- | --- |
| `construction` | `ConstructionSite.random`, including voxel initialization and progression | P7 |
| `development` | `SearchPlan.rnd`, including `Rand`'s two draws | P7 |
| `forest` | `ForestBuilder.rnd / createRandomTrees` | P2/P7 |
| `land-selection` | `RandomLandBuilder.random` | P2/P7 |
| `stadium` | `Const.rnd`, shared by game, opponents and stadium | P7/P8 |
| `stock-economy` | `Economy._random`, shared by trends and event scheduling | P8 |
| `stock-effects` | `SingleEffect.rand` | P8 |
| `stock-tables` | `RandomTable.rand`, shared by branched/for-each tables | P8 |
| `subsidiaries` | `SubsidiaryCompany.random`, market selection | P8 |
| `time-length` | `TimeLength.rnd`, including subsidiary clock delays | P5/P7/P8 |

Each world owns a separate bank. Consumers sharing a source generator must share
the corresponding stream; they must preserve source call order, bounds, branch
conditions and arithmetic when implemented. There is no per-entity split or
merged global generator. Persisting previously unsaved generators deliberately
adds deterministic continuation to ExAV's source behavior. It does not reproduce
a historical unseeded desktop run or import its private RNG state.

`RandomStreams.ForCreek` creates an operation-local generator using the source's
unchecked Int32 expression `((x1*128+y1)^(x2*128+y2))`. Swapping endpoints produces
the same sequence; unrelated world draws cannot change it. P3/P7 will implement
its full placement transaction. Half-voxel and generic selector randomization
remain P9 UI proposals: confirmed values must enter commands explicitly, with no
authoritative stream consumption from browser rendering or local selection.

`RandomEx`'s derived distribution helpers and gameplay consumption remain P8.
The source's asymmetric `Next2DExt` loop and nontermination at `halfRange=0` are
recorded for that implementation; no corrected distribution is silently supplied
by this foundation. SCN-022–024 and the full-city growth trace remain future
consumer acceptance; a portable generator alone does not execute those fixtures.

## Frozen algorithm and seeding

[GameRandom and RandomStreams](../src/FreeTrain.Online.Simulation/RandomStreams.cs)
implement **subtractive-55-v1**, independently of the host runtime's RNG. The
[pinned .NET compatibility implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Random.CompatImpl.cs)
supplies the algorithm reference; its MIT notice is retained in
[NOTICE.txt](../src/FreeTrain.Online.Simulation/NOTICE.txt) and copied into backend
build/publish output. The prototype's Park–Miller generator supplies no rules or
state to the canonical implementation.

The algorithm uses 55 Int32 slots modulo 2147483647 and a next-write index.
Initialization uses the 161803398 seed constant, stride 21 and four mixing passes,
with explicit unchecked Int32 arithmetic and the seeded algorithm's special
Int32.MinValue handling. Each raw draw subtracts the slot 21 positions ahead,
normalizes the result and advances the index modulo 55. State captures contain
immutable copies; restoring copies values into a new generator.

- `Next()` returns a value in [0,2147483647).
- `NextDouble()` multiplies one draw by `1.0 / 2147483647`.
- `Next(max)` and ordinary `Next(min,max)` scale and truncate one sample.
  `Next(0)`, `Next(1)` and equal-bound ranges still consume a sample.
- Signed ranges wider than Int32.MaxValue consume two draws, with the second
  choosing sign, following the compatibility algorithm's large-range scaling.
- Negative maxima and reversed ranges fail before consuming a draw. New RNG
  APIs or alternative sampling policies require an explicit version decision.

Bank **version 1** derives a seed for each exact stream ID from SHA-256 of:

```text
freetrain.random-streams.v1\n<invariant signed decimal root seed>\n<stream ID>
```

Here `\n` means one LF byte; UTF-8 has no BOM or trailing LF. The first four
digest bytes are read little-endian and masked with `0x7fffffff`. Registration
order, culture, string hash randomization, wall time and session IDs do not affect
seeding. The reference root seed is **0**; domain construction also accepts an
explicit Int32 root seed. Equal seeds yield repeatable sequences in independently
owned worlds. Isolation concerns independent consumption and state ownership.

The fixed ten-ID registry is captured in ordinal ID order. Missing, duplicate and
unknown streams fail; no stream is silently reseeded on load. New stream IDs,
seed derivation, algorithm changes or incompatible state layouts require an
explicit bank/save version decision; older saves can be rejected under the current
iteration policy. Gameplay consumers and optional-pack dispatch must extend
that contract deliberately in their implementing slices.

## Saves, sessions and presentation

The current identity is **reference-spatial-3**, protocol **3**. The unchanged
asset-free pack/content remains `reference-spatial-content-1`. The ruleset change
prevents a client supporting only earlier canonical rules from accepting the new
world implicitly. Random state belongs to durable authority and is not exposed
in snapshots or browser preferences.

[Save 4's schema](../prototype/protocol/historical/save-v4/schema.json) and independent
[initial-state fixture](../prototype/protocol/historical/save-v4/reference-v4.json) add
`random`: bank version, algorithm, root seed and ten `{id,state}` records. Each
state contains `index` in [0,54] and 55 values in [0,2147483646], with at least one
nonzero value. Loads restore these values directly; they do not replay draws from
the root seed. Integer fields use JSON numbers; clock Int64 fields retain their
canonical decimal strings. Quoted Int32 values, extra/missing/null fields and
duplicate JSON properties fail strict codec validation.

Only **Save 4** is supported for reads and writes. Per owner direction during rapid
slice iteration without a save-game corpus, Saves 2/3 are rejected with
`incompatibleSave`; their parsers and upgrade paths have been removed. Rejection
preserves the active world, epoch and input files. Old schemas and fixture files
are archived under `prototype/protocol/historical/` solely for rejection tests
and historical references. Prototype Save 1 also remains unsupported.

Unknown algorithm/bank versions return `incompatibleSave`. Malformed bank state,
including unknown/duplicate/missing stream IDs, returns `invalidSave`. Validation
finishes before replacing the active world or changing its epoch. Existing FIFO,
role checks, rejected-file protection, upload confirmation and checkpoint recovery
use the same codec. The bank has no global mutable state, serialized CLR types,
delegates, hidden fallback or system-clock dependency.

The session remains the single mutation owner. Capture, read-only queries,
pause changes and the reference world's empty clock turns consume no random
draws. Scheduler continuation rebinds callbacks to the restored world's streams.
No new UI text is introduced; existing English/Japanese save error messages and
confirmation workflows are reused. Full speed controls, content, gameplay and
native/device acceptance remain open.

## Verification

Initial P1-03 working tree based on **88292bd**, 22 October 2026
(before the owner-directed removal of Save 2/3 support recorded below):

- **230 .NET cases pass**, including 39 new cases for independent initialization /
  SHA-256 seed vectors, seeded runtime comparisons, scaling and consumption,
  table wraps, session/subsystem isolation, creek boundaries, scheduler rebinding,
  Saves 2/3 upgrades, malformed-state rejection and actual checkpoint restart.
- **81 client cases pass**; production build, generated contract/content checks
  and Svelte/TypeScript checks pass with zero warnings/errors.
- **65 Chromium cases covered**: all 11 canonical and 53 historical cases passed
  in the full run. One historical multiplayer case timed out selecting a train;
  its isolated retry passed. English/narrow Japanese screenshots are retained.
- **105 Python cases pass**. Project boundaries, 17 pinned random source hashes,
  stream/fixture/profile consistency and existing generated inventory/content /
  rail-gallery checks pass. Backend publish retains the MIT notice.

Commands: `dotnet test FreeTrain.Online.slnx -c Release --no-restore
--disable-build-servers -m:1`, `deno task --cwd web test`, `deno task --cwd web
build`, Playwright from `web` using `PLAYWRIGHT_BROWSERS_PATH=/private/tmp/freetrain-p1-02-browsers`,
`python3 -m unittest discover -s scripts/tests` and
`python3 scripts/check-project-boundaries.py`. Local socket access is required by
the test runner and browser servers. Logs are `tmp/p1-03-tests-final.log`,
`tmp/p1-03-client.log`, `tmp/p1-03-web-build.log`, `tmp/p1-03-browser.log`,
`tmp/p1-03-browser-retry.log`, `tmp/p1-03-python.log` and
`tmp/p1-03-publish-final.log`; browser evidence is under `tmp/p1-03-browser-evidence/`.
Earlier backend test attempts exposed fixture/assertion issues and JSON numeric
permissiveness; the final suite includes the corrected validation and passes.
Publish completed with `--disable-build-servers -m:1`; an earlier attempt without
those environment-specific flags stalled and was stopped.

This is source-derived foundation evidence with a supplementary runtime algorithm
comparison. Full-world consumer traces, historical desktop comparisons, native
devices and P1-04 entity/footprint/pack contracts remain open.

## Current-format-only cleanup verification

Per owner direction, the Save 2/3 DTOs, deserialization branches and upgrade
tests are removed. Their schemas/fixtures are archived; active client schema
checks now exercise Save 4. Rejection coverage uses the retired canonical fixtures
and verifies the world/epoch remain unchanged, with separate startup checkpoint
checks confirming rejected files survive manual and shutdown save attempts.

**233 .NET, 80 client and 11 canonical Chromium cases pass.** Release and frontend
builds, generated checks and project boundaries pass. Logs are
`tmp/current-save-only-tests.log`, `tmp/current-save-only-client.log`,
`tmp/current-save-only-browser.log`, `tmp/current-save-only-build.log` and
`tmp/current-save-only-web-build.log`. The initial P1-03 results above remain dated
evidence for that earlier tree; no migrations for old development saves remain.
