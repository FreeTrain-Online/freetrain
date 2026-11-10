# P4-05 mixed-world persistence and dispatch — 31 October 2026

**Current boundary:** [P5-05](web-parity-fleet-transport.md) supersedes the historical Save8/protocol6 values below with Save9/protocol7.

P4-05 connects the [mixed state](web-parity-railway-state.md) and
[atomic booking unit](web-parity-railway-booking.md) to session, save and wire
boundaries. Next is **P4-06 normal routes, attachment and detachment**. This is an
infrastructure slice: railway construction, rail rendering/picking and later train,
station and road workflows remain their implementing steps. The default new-world
flow still creates the existing terrain profile; no historical prototype command
is enabled against mixed worlds. The EN/JA read-only shell identifies a loaded
railway world, keeps unsupported construction controls disabled and explicitly
indicates that railway display and tools remain unavailable.

The behavior authority remains the P4-01 ExAV stable archive pin and P4-02 literal
vectors. Persistence/transport failure cases are modern contract tests. They are
not original-runtime captures or railway gameplay acceptance.

## Version and profile contract

| Boundary | Current identity |
| --- | --- |
| Canonical save | **Save8**, [schema](../prototype/protocol/historical/save-v8/save-v8.schema.json) |
| Wire | **protocol6**, [schema](../prototype/protocol/schema.json), generated TypeScript and C# DTOs |
| Mixed ruleset/content | `mixed-railway-1` / `mixed-railway-content-1` |
| Bundled pack | `exav-stable-bundled`, version `2`, exact catalog SHA-256 `d8a407b6dd1827ff4caa13e4c4d8bfcba629a4c453ef74ec871a12f9467c7d21` |

The new mixed identity registers the reviewed 87 railway state bindings alongside
terrain handlers. Catalog bytes, asset identity and the separate reference-loop
and terrain rulesets retain their identities. New browser metadata is generated
from the bundled catalog plus explicit reviewed bindings, and tested against C#
registration. Unknown packs, contribution/recipe aliases, wrong hashes, extra
packs and unregistered future road/car/platform bindings fail closed.

Save8 is current-format-only for all three canonical profiles. Save1–7 and future
versions return `incompatibleSave`; no migration, version relabeling or regeneration
is attempted. Previous Save7 schemas/files are retained under
`prototype/protocol/historical/save-v7` for rejection evidence. The existing
checkpoint failure policy is superseded by ONLINE-SAVE-003: failed startup
checkpoints are discarded and fresh checkpointing remains enabled; named saves
and rejected uploads are preserved. Protocol5 peers receive
the existing incompatibility response; matching static/server artifacts are required.

## Durable composition

[MixedSessionState](../src/FreeTrain.Online.Simulation/MixedSessionState.cs) wraps
one immutable `MixedConstructionState` in the established provenance, construction
mode, clock, pause, random-bank and session revision contracts. It has no second
clock or random stream. Journal timestamps cannot be newer than the clock and
journal revisions cannot be newer than mixed geometry. Terrain geometry revision,
mixed geometry revision and session revision retain their distinct meanings.

The packed terrain payload preserves actual cells, ownership, forest realizations,
terrace backups and provenance without rerunning a generator. Its allocator high-water
mark remains the terrain subset's recorded mark. The top-level spatial payload holds
railway owners only; `mixed.nextEntityId` and `spatial.nextEntityId` must agree and
must cover terrain's mark. Load combines these into one validated occupancy index.
The mixed payload stores every Traffic slot, exact definition, pattern/subtype/art,
tunnel backup, signal reference, slope ownership, paired-cell flags, pier supported
identity, initial cash/settings and complete ordered journal. Source genre GUIDs
and every Int64 value use canonical strings. Cash, rescues and next journal sequence
are validated by replay rather than trusted independent totals.

[SaveCodec](../src/FreeTrain.Online.Persistence/SaveCodec.cs) bounds the envelope
before DTO allocation, rejects duplicate/unknown/missing/null fields, validates
terrain and exact installed handlers, reconstructs the complete graph and replays
accounts before returning a load handle. Handles can only be constructed by the
codec. The receiving session rechecks its exact profile before replacement.

## Session and transport publication

The session FIFO handles mixed capture/load, run/pause/step and trusted tool
handoffs. Capture freezes mutable headers before detached encoding; geometry and
accounts are immutable. The trusted `ApplyMixed` hook checks role, connection,
epoch/session revision, complete successor geometry/identities and the journal
prefix, then admission before publication. It accepts no browser graph or price.
Later tools must derive legal facts and source booking inside their authorized
command path. Existing terrain-only mutation paths remain unavailable on mixed
state until they use combined occupancy and booking.

[MixedWire](../src/FreeTrain.Online.Persistence/MixedWire.cs) publishes terrain,
combined entities and one complete bounded `mixed` component. Deltas replace that
component atomically and include changed spatial owners and explicit owner removals;
rail removal does not implicitly remove retained piers. Terrain changes use their
existing cell/occupant delta fields. Unchanged immutable mixed geometry/account
publications are shared across clock updates. Browser delta application validates
the whole proposed graph, allocator monotonicity, owner revisions/kinds and journal
prefix before replacement; clock-only updates retain the validated graph.

Packed snapshot transfers move only terrain owners into packed bytes and retain
railway owners in the header. The browser reunites both before full validation and
reveal. Existing chunk length/hash/epoch/revision/coherence checks remain required;
partial transfers never publish. Exact definitions, overlaps, slope membership,
art/flags, backups, pier references, component slots and cash replay are validated
on both sides. Neither disconnected rail nor zero-rail mixed terrain is rejected.

Initial mixed admission is **8,192 railway voxels, 1,024 slope owners and 8,192
journal entries**, with an additional **8 MiB complete mixed-component limit**.
These are deliberate initial infrastructure bounds, not final P4/P8 city-capacity
claims. Existing 256 MiB save/state, 48 KiB chunks, 16 MiB delta/socket and terrain
structural limits remain. Admission includes actual railway owner/component bytes
and the existing conservative packed-terrain bound, including headers. Counts and
byte limits reject before publication. The shared difference builder checks each trusted mixed successor against its
actual 16 MiB delta size before publication; P4-23/P8 retain measured larger-city/history work.

## Fixtures and verification

The hand-authored [mixed save](../prototype/protocol/historical/save-v8/mixed-v8.json),
[snapshot](../prototype/protocol/fixtures/snapshot-mixed.json) and
[removal delta](../prototype/protocol/fixtures/delta-mixed.json) extend the existing
independent terrain transport fixture with disconnected normal rail, tunnel backup,
signal, pier, rescue and zero-spend records. Removal retains an orphaned pier and
changes tunnel art with a revised owner. These are literal contract expectations,
not a fixture-update mode that records production output. Tests additionally consume
all 28 normal/junction patterns and 15 legal P4-02 slope vectors, preserving every
stored paired-cell flag/art selection through save/reload.

Reproduce from the repository root:

```sh
.tools/dotnet/dotnet test FreeTrain.Online.slnx --no-restore -m:1 -p:UseSharedCompilation=false
deno task --cwd web build
deno task --cwd web test
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway.py --check
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
git diff --check
```

The full backend regression passes **794/794** .NET cases
(`tmp/p4-05-dotnet-final.log`). Final targeted mixed verification passes **29/29**,
including the two subsequently added exact generated-binding and maximum-admission
cases (`tmp/p4-05-targeted-final.log`). The initial limits admit and round-trip
8,192 railway voxels plus 8,192 journal records, and reject the next journal entry.
Client tests pass **157/157** and the final production build/type/generated/content/
locale checks pass (`tmp/p4-05-web-test-final.log`, `tmp/p4-05-web-build-final.log`).
Debug/Release builds, default and explicit pinned-source railway checks, Python
**161 tests with one existing skip**, project boundaries and whitespace pass.

Chromium covers **123 distinct scenarios across the broad and final runs**.
The broad run passed **117/122**: one terrain readiness timeout and four prototype
map cases failed after pan cleanup left the shared clock running. Cleanup now waits
for authoritative Paused before closing. The final clean-build run passes **7/7**,
covering all five previously failing cases, the pan cleanup and the new mixed-save
load/step/save plus malformed-peer reveal test. This is combined execution evidence,
not a claim of one clean full 123-case run. Logs: `tmp/p4-05-browser.log` and
`tmp/p4-05-browser-final.log`. No performance or native rendering credit is inferred.
These local logs are execution evidence, not committed source-oracle inputs.
Existing native/P2/deferred-browser and later-phase gates remain open.
