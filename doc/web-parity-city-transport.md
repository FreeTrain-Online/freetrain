# P7-05 city persistence, transport and authority — 12 November 2026

P7-05 publishes the existing [city state](web-parity-city-state.md) and
[construction lifecycle](web-parity-city-construction.md) through the canonical
live session, save/checkpoint, snapshot, delta and bounded-transfer paths. It
continues the pinned ExAV `883cf7d` source contract and independent P7-02 fixtures.
It does not implement heat diffusion, city placement tools, rendering, automatic
development or the economy. Those retain their subsequent checklist owners.

## Contracts and restoration

The current contracts are **Save11, protocol10 and City format1**. Only Save11 is
read and written. Save10 schema/files are retained under
`prototype/protocol/historical/save-v10` as rejection evidence, with no conversion.
The mixed ruleset/content identity, catalog version/hash, random algorithm/version,
terrain packing and world/account/queue limits are unchanged. Existing protocol9
peers receive HTTP426. Current literal saves and browser fixtures use v11 names.

`CityContracts` and `CityWire` explicitly encode every durable city field:

- Properties, structures, caller/ownership/revisions, compatible half components,
  subsidiary valuation inputs and construction cells/doors/ground/callbacks.
- Realized ground, initial producer definition/height, voxel-specific fences and
  dummy fence hosts, all in the existing spatial identity namespace.
- Both Single heat buffers, conductivity and retained borders; development
  settings; search phase/budget/work/site/selected plan; stale non-null ULV samples.
- Exact construction, subsidiary, heat and development callback descriptors.
  Registration cookies, FIFO orders and captured intervals survive restoration.

The common world clock and complete named random bank remain in the existing save
envelope. Mixed queue must equal the saved clock. Int64 values use canonical
strings. Enum tags and required/null fields are explicit. Provenance remains the
validated original world request plus each structure's saved construction caller;
loading neither regenerates terrain nor infers the caller from current ownership.

Reconstruction validates content/pack identity, complete ownership/footprints,
allocator monotonicity, revisions, construction lifecycle, producer references,
fence hosts, search station/site/selection, buffers/settings and callback queue
membership. Indexes are derived only after validation. Restoring never rediscovers
catchment or repairs asymmetric listener edges. Unknown, missing, duplicate,
malformed and unavailable-pack input rejects before replacing the valid world.
Rejected upload/named-file/prepare input is preserved. The existing startup policy
for invalid transient checkpoints is unchanged: report and discard them, as
specified in the runtime policy; valid checkpoint restart is tested here.

## Live continuation and atomic minutes

`MixedSessionState` installs city bindings beside railway construction, trains and
station/platform timers on the same world-local `Clock`. City construction invokes
the P7-04 kernel directly. Potential fresh subsidiary identities are bound before
completion scheduling. Freeze/save/reopen retain actual handles, never redraw
intervals or consume random values. Cancelling a site removes every site handle.

Whole minutes run on detached copies and validate before publication. Failed
callbacks, bookings, arithmetic, missing consumers or bounds discard the entire
candidate, including geometry, clock, accounts and random changes. No transient
passenger/bell effects escape a failed minute.

Actual land value is an explicit trusted `CityLandValue` consumer; there is no
constant/default market input. `CityDispatcher` is the trusted handoff for later
heat/development/subsidiary callbacks. Until their implementation owners deliver
those consumers, a due unsupported callback fails the minute and preserves the
previous state. The authority returns `operationUnavailable` for Step and pauses
realtime retries while cancellation, saving and loading remain available. Test adapters are explicitly controlled fixtures and never loaded
as executable save data. Existing railway/train/station dispatchers remain active;
loading through an existing host retains its trusted adapters.

## Producer boundary and listener directions

`StationProvider.Input` distinguishes `controlled`, `city-fixed`, `city-variable`,
`city-half` and `city-ground`. A real city input requires a completed actual city
producer. Its population expression must exactly equal the immutable initial
catalog binding, including variable-height multiplication. XY size uses source
area; fixed/variable Z uses the initial height. Half and land listeners use the
source default 1×1 listener dimensions. Provider location comes from its spatial
owner. Land value stays explicit, finite integral input; no station total or
constant producer population is invented.

City owners cannot publish controlled or unbound listener payloads; unrelated
owners cannot publish city kinds. Controlled P6 fixtures retain their input kind.
Detached completed producer descriptors may await actual family attachment in
P7-07/13. Bound listeners must validate their completed lifecycle. Changes to
listener metadata or ordered edges revise the corresponding city/spatial owner
atomically, including station-first advertising and station removal.

Both producer-to-station slots and station-to-producer membership arrays persist
independently. The fixture exercises the source fifth-station replacement:
`[1,2,3,4]` becomes `[1,2,3,5]`, and station5 receives no reverse membership. Reopen
preserves this asymmetry. No station scan repairs it.

## Authority and bounded publication

`POST /api/city/preview` provides read-only structure inspection and a zero-cost
construction cancellation preview to authenticated roles. `CityQuery` fences the
world epoch, session revision, minute, content and actual owner revision. Preview
runs candidate admission without publishing or consuming the world's RNG.

`cancelCityConstruction` joins the existing command FIFO. Current Owners and
Operators may cancel a timed site; Observers reject. Commands recheck every query
fence, pass the existing packet validator, and produce ordinary receipts. Receipt
replay, stale requests and epoch replacement use the established session behavior.
Completed demolition and new placement remain with their source-family steps;
this boundary does not introduce a public fixture injection or arbitrary state
replacement command.

The client exposes `previewCity`, strict response parsing, `cityQuery` and
`currentCityDraft`. Requests and responses require the current joined state; an
intervening revision, minute, epoch, content or owner change invalidates a draft.
Snapshots carry a complete city component. Deltas replace that component together
with changed/removed spatial owners, queue, station listeners and accounts. The
merged graph and successor revisions validate before reveal; stale bases or a
partial removal require resynchronization.

Existing limits apply: 8MiB mixed component, 16MiB delta and 256MiB complete state;
48KiB transfer chunks with total/count/length/hash checks. City owners and total
construction cells are bounded by 8192, fences by 32768, and callbacks by the
existing 65536 limit. Heat lengths must exactly match the admitted world's bordered
map. Oversized city state rejects atomically; P7-05 grants no new capacity or
performance acceptance.

## Verification and handoff

`CityPersistenceTests` covers exact Save11 reopen, all four real producer kinds,
listener asymmetry, stale cache/search data, city-only worlds, Traffic road codecs,
construction/random continuation, repeated FIFO cookies, failure rollback,
unknown/malformed/missing-pack input, file/world protection, authority and chunks.
The transport witnesses are captured from those deterministic fixtures; they are
cross-language codec evidence, not replacements for the independent source oracle.

Focused .NET regressions also cover station/fleet/terrain/reference persistence,
station demand/session behavior, railway accessory sessions, contracts, network,
state transfers and retired-save rejection. Focused Deno tests cover city,
mixed/station state, contracts, transfers and remote-client behavior. Chromium
uses EN/JA peers for coherent cancellation, role/revision/epoch checks, receipt
replay, rejected uploads, reopen and valid checkpoint restart, plus existing
station transport/hour/day regressions. No complete unit or web suite is run.

Fresh final results: **581 selected .NET, 166 focused Deno, three Chromium and 31
Python tests pass**. The 29 city persistence cases include all 129 structure / 66
ground /three road client/server metadata comparisons. All 703 independent source
cases and 197 original source files pass; no complete unit/web suites run. .NET
compilation and Svelte diagnostics have zero warnings/errors. Vite retains its
existing advisory about bundle chunk size.

Commands, final counts, tool versions, logs and artifact hashes are recorded in
`tmp/p7-05-validation.json` and the ordered checklist. P7-01 review still validates
its original runtime-policy hash against a byte-exact archival copy at
`doc/parity/p7-01-runtime-policy.txt`; source review/fixture digests and expected
outputs are unchanged. Current format policy is documented separately.

Next: **P7-06 actual heat-based land value**, followed by P7-07 producer construction
and attachment. Family tools/rendering, live development, P8 economy, optional
packs, native/human review, P10 imports, P11 performance and P7 phase exit remain
open.
