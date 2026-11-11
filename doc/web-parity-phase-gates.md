# Parity phase review — 21 October 2026

**P6 integrated review — 11 November 2026:** [P6 acceptance](web-parity-p6-acceptance.md)
closes bundled browser/portable station/passenger scope with controlled supply.
Every exit criterion and all14 source decisions have explicit dispositions;
[the P7/P8 handoff](web-parity-p7-p8-station-handoff.md) names consuming interfaces
and independent witnesses. Real city/economy, P10 import, native/human/optional
and P11 performance acceptance retain their owners. Fresh2,304 .NET/386 client/
35 Python/17 Chromium cases pass. Earlier entries retain their historical scope.

**P5 integrated review — 8 November 2026:** [P5 acceptance](web-parity-p5-acceptance.md)
closes bundled browser/portable train scope with Manual driving owner-removed.
The [P6 train/harbor handoff](web-parity-p6-train-handoff.md) specifies yard stop
indices, host/controller resolution, passenger/forwarding order, fares and hourly
companions. Fresh moderate validation: 369 .NET / 75 client / 26 Python unit cases
and 16 Chromium workflows pass. P1 final review, P2/native/human/optional
qualifications and P11 performance findings retain their named owners; no station/
passenger gameplay or full resource/release acceptance is granted. Earlier dated
entries below retain their historical scope.

**Owner audio decision — 27 October 2026:** [BGM is deferred to a future improvement project](web-audio-policy.md). TinySynth demonstrated feasibility but produced audible pops/crackles and has been removed with its player and music controls. Sound effects and six bell previews remain. Digital audio, including reimagined MIDI compositions, is allowed; BGM/MIDI playback is no longer a P2/P9 or parity exit requirement.

**P2 remaining acceptance update — 27 October 2026:** [bundled audio, localization and final regression evidence](web-parity-p2-acceptance.md) implements MIDI/bell playback and a scalable language/content-presentation structure. Existing source-defect/optional-suite deferrals and native/device/fidelity qualifications remain explicit; overall P2 stays open.

**P2-01 update — 25 October 2026:** [canonical bundled inputs and preparation](web-parity-bundled-preparation.md)
are delivered through default builds, static output and the server's inert
catalog registry. P2 has started; full recipe/rendering/optional acceptance stays
open. P1-01–04 are implemented, with P1's final evidence/coverage review still open.

**P1-04 update — 22 October 2026:** [stable entity/voxel/footprint contracts and exact profile dispatch](web-parity-spatial-contracts.md) are implemented through live network reads, Inspector and Save 5 (`reference-spatial-4`, content/pack v2). Earlier slice identities/counts below remain dated evidence. P1 phase-gate review and gameplay/native acceptance remain open.

**Current save policy — 22 October 2026:** [only Save 5 is supported](web-parity-runtime-policy.md). Saves 2/3/4 are rejected without conversion; earlier compatibility and verification statements below record the previous slices only.

**P1-03 update — 22 October 2026:** [versioned world-owned random streams](web-parity-random.md) now persist in Save 4 under ruleset `reference-spatial-3`. Only the current save format is supported. P1-04 entity/footprint/pack contracts and later gameplay/native gates remain open. Earlier slice identities/counts below retain their dated scope.

**P1-02 update — 22 October 2026:** [canonical clock/calendar/scheduler](web-parity-clock.md) now runs in the live reference world with Run/Pause/Step, ruleset `reference-spatial-2` and Save 3. Canonical Save 2 upgrades explicitly at source start time, paused. The P1-01c static-world/Save 2 descriptions below record that earlier slice.

**Owner policy update — 22 October 2026:** [prototype Save 1 support is retired](web-parity-runtime-policy.md). Unsupported inputs report an error and preserve the current world/files; automatic and manual prototype writes are disabled. Earlier Save 1 compatibility/continuation statements below are historical and superseded. Canonical spatial Save 2 now ships with [P1-01c](web-parity-reference-world.md); full-world persistence remains open.

## P0 coverage baseline: closed

Final review at implementation base `332ac5f`, against stable ExAV 2.1.1.12,
archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
The P0 exit condition is coverage and reviewable specifications. It does not
require the inventoried features to be implemented. P0-01–09 supply that evidence:

| P0 exit criterion | Reviewed evidence | Result |
| --- | --- | --- |
| Every baseline root and bundled plugin has explicit scope/status | [Baseline](parity/exav-baseline.json), [ledger](parity/exav-ledger.json), [integration](web-parity-exav-integration.md): all eight roots, 1,041 files, 56 manifests and 465 stable code assignments; support, source-only and exclusions retained | Met |
| Generated contributions are enumerated | [Bundled review](web-parity-content.md): 482 direct declarations plus one registered child, with recipes/wrappers separate; [optional review](web-parity-optional-plugins.md): 23,417 generated children, selected profile and duplicate/source-only copies explicit | Met |
| Reachability, action destinations and differences are reviewable | [Action review](web-parity-actions.md): 87 families / 46 routes, all 483 contribution decisions and all 56 bundled dependency closures; source defects have implementing owners | Met |
| Initial geometry/content/state schemas and priorities are reviewable | [Geometry](web-parity-geometry.md), [persistence](web-parity-persistence.md), [transition decisions](parity/exav-domain-transition.json), [scenario specifications](web-parity-scenarios.md) and [phase estimates/owners](web-parity-report.md); independent version axes and prototype Save 1 disposition explicit | Met |
| Separate optional content, dependency and permission records | [Canonical V2 specification](web-parity-optional-plugins.md): 4,933 files, 452 manifests / 450 selected, resource gaps and terms retained; separate directory/credits contract and historical compatibility profiles | Met |
| Credit delivered UX without claiming source parity | [Coverage report](web-parity-report.md) separates implementation foundations from source specification, translation candidates and runtime acceptance; native/device evidence remains qualified | Met |

All eight explicit-source `--check` commands passed afresh: inventory, content,
actions, geometry, persistence, scenarios, optional and integration. All **105**
Python tests passed. Source checks read pinned Git blobs, including where the
archive working tree has approved corrections. Check commands are documented in
the linked reviews; local logs are under `tmp/parity-p1/p0-*.log` and
`tmp/parity-p1/python-tests.log`.

This closes **P0 coverage/specification only**. Existing source records retain
their implementation statuses. Import/build migration, granular feature review,
English/Japanese presentation, source-defect decisions, runtime/continuation and
capacity evidence stay assigned to P1–P11. UX3-14/17, A-03/A-14 and UX3-GATE remain
open for their native/device observations. The owner's 21 October instruction to
continue into P1 authorizes the domain slice below; it does not supply those
observations or establish browser gameplay acceptance. Earlier dated P0 closure
qualifications in the source reviews record the state before this final review.

## P1-01: spatial primitives

**Follow-up:** [client/server spatial integration](web-parity-spatial-integration.md)
now delivers protocol 3, client geometry and server-checked alignment targeting.
The initial domain-only delivery and its validation are recorded below. P1 and
canonical-world migration remain open.

Implemented in [Simulation](../src/FreeTrain.Online.Simulation/Heading.cs)
with [independent vectors](../tests/FreeTrain.Online.Tests/SpatialTests.cs).
The API supplies eight validated headings, rotations, angles/axes, explicit
prototype cardinal adapters, location steps/distance/snapping/inclusive bounds,
H/V/Z storage conversion, horizontal and voxel bounds, and camera-independent
projection. Each `WorldSpace` carries its own dimensions and origin; there is no
global current-world dependency.

Source identities come from [P0-04's hashed review](parity/exav-geometry-rules.json):

| Pinned source path under `trunk/` | SHA-256 | Ported symbols |
| --- | --- | --- |
| `core/world/Direction.cs` | `53bc4122b0df7c8c5f4909cc2982f1474354c4ab91efe049f2c316510069ec96` | indices, offsets, rotations, angle, axis tests |
| `core/world/Location.cs` | `c0096ebe0ace4e1c61e237654bbbf32b1a425a943ecd622010377d103adbf126` | toward/direction, distance, inBetween, align4To/align8To |
| `core/world/World.cs` | `2d446eddb4c3ceee24334b3ffe29e8f227ac4bec96f781eed6ff2fb2293ee0d2` | toHV, toXYZ, isInsideWorld(Location), fromXYZToAB |

Tests cover all eight vectors/rotations and all 64 angle pairs, explicit
0/1/2/3 → 0/2/4/6 prototype conversion, diagonal rejection, distance truncation,
snapping ties and integer-half thresholds, odd/even origins, storage edges,
negative signed shifts, source negative-row truncation, projection vectors and
independent world origins. This implements the direction portion of
EXAV-SCN-001; its rail-pattern legality/matrix remains P4 work. The initial
distance vectors also support EXAV-SCN-013; fare/economy behavior remains open.

**Boundary decision P1-SPATIAL-001:** retain Int32 spatial intermediates and source
normal-range results; use checked arithmetic to reject overflow explicitly rather
than wrap a location or depend on a NaN-to-int conversion. This bounds the supported
spatial API. It does not decide financial overflow (EXAV-SCN-D04), broaden valid
world dimensions, or claim source overflow fidelity. Source debug-only direction
preconditions become argument errors. Negative storage rows retain the source's
truncating inverse and are not promised to round-trip. Horizontal containment
matches the source's deliberate lack of a Z check; voxel containment adds that
check explicitly. No deprecated removal formula is reused.

This is a domain implementation slice. The live prototype, its Direction enum,
protocol 2, content identity and Save 1 codecs remain in use. No ExAV wire/save
format is emitted yet. No new user-visible text is introduced; domain exceptions
are internal and must receive localized presentation when exposed by commands.
MAN/DEV control mapping and bilingual browser acceptance attach to those future
workflows rather than these internal primitives.

P1 remains open. Next: source-minute clock/calendar and scheduler fixtures,
versioned isolated random streams, then stable entity/footprint contracts and
tested ruleset/prototype compatibility dispatch before replacing live geometry.
P1-01 does not close full footprint/terrain/rail behavior, session integration or
the native UX handoff.

Validation: **160 .NET tests passed**, including **31 new spatial cases**, using
`dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj -c Release
--no-restore -m:1 -nr:false -p:UseSharedCompilation=false` with local sockets
enabled for MSBuild/testhost and network tests. The full suite includes prototype
Save 1, continuation and multiplayer regressions. All 105 Python tests and
`scripts/check-project-boundaries.py` pass. No browser run was needed for this
domain-only change. Local .NET evidence: `tmp/parity-p1/dotnet-verified.log`.

## Baseline naming — owner direction, 21 October 2026

Canonical game types, namespaces, directories and tests use ordinary project
names, without an `Exav` prefix. ExAV identifies the source authority in provenance
and research artifacts, not an alternate gameplay mode. The spatial types now
live directly in `FreeTrain.Online.Simulation`; tests are `SpatialTests`.

Original-FreeTrain gameplay is an obsolete baseline. The retained runtime types
now live under `Prototype/` in `FreeTrain.Online.Simulation.Prototype`, with explicit
obsolete-baseline notices. Reference content and the old manual controller carry
the same notice. Direction adapters live in that compatibility namespace, so the
canonical rules have no dependency on the prototype. This isolates the existing
reference/Save 1 behavior while subsystems are replaced; it does not make the
obsolete rules authoritative. See the [simulation boundary](../src/FreeTrain.Online.Simulation/README.md).

## P1-01c: canonical live reference world

[Source, implementation, workflow and verification evidence](web-parity-reference-world.md)
now closes this slice: default H/V world, eight-heading source-normal loop, actual
server/client rendering/picking/targeting, independent identity and strict spatial
Save 2 with real replacement and restart checks. Prototype Save 1 remains unsupported.
Clock/scheduler, random streams, general entity/footprint and full pack dispatch
remain P1-02–04; complete gameplay/content/native gates remain open.

## P1-02: source-minute clock, calendar and scheduler

[Implementation, source/decision/translation bindings and evidence](web-parity-clock.md)
now cover absolute minute 130080 start, fixed 365-day calendar and source holidays /
vacations, daylight-before-FIFO-before-end-of-turn ordering, repeated callbacks,
cancellation/GEO-002 repair and world-local queue restoration. Live Run/Pause/Step,
English/Japanese date display and strict Save 3 continuation ship under ruleset
`reference-spatial-2`; canonical Save 2 upgrades explicitly and Save 1 remains
unsupported. 191 .NET, 80 client, 64 Chromium (10 canonical / 54 historical) and
105 Python cases pass; builds/generated checks, source hashes and project
boundaries pass. P1 stays open for P1-03 random streams and P1-04 complete entity /
footprint / pack contracts. Full gameplay consumers, source speed UI, complete-city
continuation and native/device gates remain open.

## P1-04: stable entities, voxel footprints and profile compatibility

[Delivery evidence](web-parity-spatial-contracts.md) and
[the pinned eleven-file review](parity/spatial-contracts-review.json) bind source
Cube/Entity/Voxel/World/structure/traffic contracts to immutable domain graphs,
world-local IDs and exact pack-qualified definitions. Live reads, deltas and
inspection retain entity identity; Save 5 persists and validates the complete
reference graph/allocator alongside clock and random continuation. Saves 1–4 are
rejected under the existing owner current-format-only policy.

**263 .NET / 83 client / 105 Python** cases pass. Browser/build/source verification
and remaining limits are recorded in the delivery evidence. P1-01–04 foundations
are implemented; the P1 gate still requires its final evidence/coverage review.
Full content, gameplay consumers, complete-city continuation/capacity and
native/device UX remain open with their owning phases.

## P2-01: canonical bundled inputs and preparation

[Delivery evidence and remaining work](web-parity-bundled-preparation.md) cover
386 pinned source inputs, 56 UTF-8 manifests, deterministic plugin dependencies,
448 exact prepared contribution records, 213 converted bitmap sheets and 141
seasonal picture bindings. Server/browser catalogs and hashes agree. Default
builds/Docker use the bundled input snapshot without historical roots or implicit
archive/optional discovery. Historical assets stay explicitly frozen/regression
content. Every unimplemented recipe remains diagnosed; P2 is open for executable
recipes, effects/rendering/picking, full galleries and optional build/startup.
