# P4-24 integrated acceptance and P5 handoff — 2 November 2026

**P4 is accepted at browser/portable infrastructure scope; the P5 handoff is
delivered.** On 2 November the owner deferred P4-CAP-001/002 to P11 so performance
bottlenecks can be addressed together after all parity features are implemented.
The measured misses remain open P11 findings, rather than passing results.
Save8/protocol6, gameplay and source prices are unchanged by this review.

## Integrated player journey and evidence

The owner reports substantial manual testing during development and accepts
smoke testing for this step. This review uses focused existing integration and
browser scenarios instead of repeating the full P4 regression/capacity release
runs. Browser journeys run in isolated test servers/save directories; they do
not replace the owner's running game or slot 2.

| Journey stage | Fresh check and existing independent evidence |
| --- | --- |
| Create/open terrain; lay, extend and branch normal rail | `railway-construction.spec.ts` two-map-point journey creates empty terrain, previews/cancels, confirms, extends and joins into a junction, observes a peer, removes and reopens. Exact source direction/attachment expectations remain the 1,484 P4-02 literals. |
| Connect surface track through a ramp to elevated rail; remove the ramp | `slope-construction.spec.ts` map journey checks all eight owned cells, unchanged preview bytes, exact booking, peer publication, source pixels, saved reopening, removal and running shared time. |
| Add/remove supported special infrastructure | Bridge four-direction, girder eight-direction, tunnel corner-restoration/both removal orders and garage cumulative-billing journeys check actual source art, supports, operation-specific endpoints, charges and reopening. |
| Signals, poles, accessories and limits | Selected signal/pole/speed journeys check types/orientations, replacement, shared publication and exact reopening; the stationary lifecycle journey checks pending completion/cancellation. The full network fixture retains all 54 stationary variants, all four poles and all three speed lists. |
| Cancel/reject illegal work and observe another participant | Normal/slope/special journeys include detached cancellation and illegal placement; EN/JA cross-tool cases reject terrain edits under rail, invalidate a preview after a peer's pole edit and retain exact clock/accounts/canvas/camera. P4-22 retains broader authorization/stale/lost-receipt evidence. |
| Save/open/reconnect/restart exactly | EN/JA full-network browser journeys save slot2, compare downloaded bytes, replace/reconnect/reload with a peer and reject corrupt/incompatible/oversize input. `MixedContinuationTests` exercises all delivered subtypes, twelve removal/reopen/checkpoint continuations, all-variant demolition and real HTTP/startup recovery. |

These are several isolated browser journeys plus one combined gameplay-built
network, not a claim that every action ran in one uninterrupted browser session.
Canonical-byte equivalence verifies persistence/continuation; it is not an
independent source oracle. The network's explicit test terrain and Mountain setup
remain distinguished from generation acceptance. Fresh smoke results are recorded
below; prior full regressions remain at their original dates/scopes.

## Source, decision, contribution and translation reconciliation

[Acceptance ledger](parity/railway-acceptance.json) enumerates all **14 operation
families, 14 decisions and 64 bundled definitions**, with reciprocal identities
from the immutable P4-01 review, implementing owners and delivered reports.
Its input hashes bind the source/translation/accessory/capacity/handoff records.
The original P0/P4-01 source-discovery status is preserved as historical evidence;
the separate acceptance and accessory ledgers supply implementation dispositions.

- All 60 patterns, 576 attachments, 720 detachments, four-way slopes and special
  axes bind the independent source fixtures and production tests. The 16 invalid
  junction detachments retain atomic rejection; no substitute pattern is invented.
- RAIL-D001–014 retain their reviewed dispositions. D006 retains owner-approved
  all-pier teardown at the source call sites, with explicit loss previews. Inclusive
  versus half-open removal, garage repeated subtotals, stored slope flags,
  zero-price accessories and exact bankruptcy/overflow behavior remain unchanged.
- The accessory ledger binds four special rails, two signals, four poles and
  54 stationary variants to exact prepared definitions/handlers/recipes. P6
  passages/platforms and P7 structures/roads keep their separate dispositions.
- All 78 source-text records bind reviewed current EN/JA keys or explicit browser
  adaptations through the P4-22 delivery review. Historical donor translations
  and source bytes retain their provenance; they are not independent authority.
- MAN-A03/A04 and P4 portions of MAN-A12/A14, REG-01–07/10 and the DEV/P4 links
  retain the [P4-22 requirement matrix](web-parity-construction-regression.md).
  Construction geometry/booking, source art and coherent publication have
  browser/portable evidence; real moving cars, passenger platforms and road
  gameplay still belong to P5/P6/P7.

Source-derived checks, current runtime captures and human acceptance are distinct.
This review checks pinned source bytes and inspected browser captures. The owner
reports manual testing but this is not a new per-sprite native fidelity signoff.
Existing P2 full-gallery/native/optional qualifications and deferred browser/device
checks remain open. No Windows desktop execution or physical touch/IME/display
acceptance is inferred from headless Chromium.

## Performance disposition

**Owner direction — 2 November 2026:** move both findings from the P4 gate to
P11 integrated performance work. Full parity implementation may reveal additional
bottlenecks; profile and address them together. Reassess a possible map-size-limit
increase in the same work. This explicitly supersedes the initial P4-24 decision
to retain these as P4 blockers; it does not change the measured results.

| Finding | Owner disposition |
| --- | --- |
| P4-CAP-001 | **Deferred to P11, open.** Keep the recorded p95 authoritative feedback target <250 ms at injected 100 ms RTT as a baseline. Empty273–281ms and heavy 3,868–3,950 ms remain measured misses. |
| P4-CAP-002 | **Deferred to P11, open.** Visible completion around one second empty/several seconds heavy remains a usability finding for integrated optimization. |

P11 must profile the complete game, address these and any newly exposed
bottlenecks, and record final target/limit dispositions. Compare authoritative
preview/commit-to-publication and visible completion separately, with cold/steady
state, 100 ms RTT, legal resource-heavy/full-city scenes and long simulations.
Reassess 1500×1090×45 and a possible larger map against measured resource budgets,
creation/editing, rendering/picking, transport, saves and checkpoint continuation.
No map limit changes in this planning update. Existing P3 latency acceptance
retains its original scope; this is a separate explicit owner deferral.

The phase criterion—representative construction/removal in supported directions
and elevations, atomic occupancy/costs and source rail art—has implementation and
verification evidence. With performance work reassigned to P11, P4-24 is checked
at browser/portable infrastructure scope. Existing native and later-phase
qualifications remain open. No capacity or Linux checks were rerun for this
owner-directed planning update.

## P5 handoff and remaining gates

[P5 railway interface contract](web-parity-p5-railway-handoff.md) publishes pinned
topology/guide/zdiff, car occupancy, signal/controller and speed scheduling
boundaries, legal guide/ramp witnesses and gameplay-built fixture inputs. It
retains EXAV-DEP-013/014 and timetable/reversal defects as P5 decisions. P5 may
proceed against these interfaces; moving-train acceptance remains P5.

P1 final review, P2 qualifications, native/deferred-browser checks, P5–P11 gameplay,
historical import, optional-suite compatibility, full-city/extended simulation,
POST-ASSETS and independent stretch goals retain their existing acceptance
status. The railway-capacity train/car proposals remain uninstantiated budgets.

## Fresh verification

- Release .NET **48/48** focused topology and mixed-continuation tests pass,
  including actual HTTP/startup recovery and per-subtype checkpoint restarts:
  `tmp/p4-24-dotnet.log`. The generated combined network is
  `tmp/p4-24-network.json`. This is a fresh subset, not a new full-suite count.
- Chromium **18/18** passes: normal map journey, connected ramp journey, bridge
  four-way/girder eight-way/tunnel restoration, EN/JA garage/pole/signal/speed
  workflows, stationary lifecycle, EN/JA cross-tool and EN/JA full-network recovery.
  Log: `tmp/p4-24-browser.log`; captures: `tmp/p4-24-browser/`. Inspected captures
  include `slope/slope-en-connected-network.png`, `cross-tool/ja-narrow.png` and
  `mixed-continuation/en.png`. This is visual smoke inspection, not native fidelity
  acceptance. Expected reconnect proxy resets produced no failed cases.
- Production build, generated contracts/content, locales and Svelte checks pass
  (`tmp/p4-24-build.log`); the existing large-chunk advisory remains.
- P4-01 review/78 translation dispositions, P4-02 **1,484 literal fixtures** and
  accessory bindings pass, including explicit pinned-source verification for the
  source/fixture reviews. The new handoff checker passes in default and explicit
  pinned-source modes. Project boundaries and whitespace checks pass.

The initial .NET sandbox run could not open testhost sockets; the authorized run
with local sockets passes. An initial browser invocation from the repository root
failed collection because helpers require `web/`; the completed run used `web/`.
Neither harness setup failure is credited as passing verification.

Reproduce the backend fixture and source checks from the repository root:

```sh
FREETRAIN_MIXED_NETWORK_OUTPUT="$PWD/tmp/p4-24-network.json" \
  .tools/dotnet/dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore \
  -m:1 -p:UseSharedCompilation=false \
  --filter 'FullyQualifiedName~MixedContinuationTests|FullyQualifiedName~RailwayTopologyTests'
python3 scripts/review-railway.py --check
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway-accessories.py --check
python3 scripts/check-railway-handoff.py
```

After `deno task --cwd web build`, run from `web/` (with `dotnet` on PATH):

```sh
FREETRAIN_ONLINE_TEST_SERVER_PORT=5180 FREETRAIN_ONLINE_TEST_WEB_PORT=4273 \
FREETRAIN_MIXED_NETWORK_OUTPUT="$PWD/../tmp/p4-24-network.json" \
FREETRAIN_ONLINE_BROWSER_EVIDENCE="$PWD/../tmp/p4-24-browser" \
deno run -A node_modules/@playwright/test/cli.js test --config playwright.config.ts \
  --project canonical --grep 'two map points preview/cancel/build|map slope preview/cancel|all four bridge directions|all eight girder directions|four cardinal routes, both removal orders|garage axes.*EN|signal types.*EN|pole types.*EN|speed-limit level sections.*EN|stationary source lifecycle.*EN|cross-tool terrain protection|complete P4 network slot 2'
```

Local artifacts use `tmp/p4-24-*`; ignored artifacts are evidence for this execution,
while committed ledgers/contracts remain the maintained inputs. Full Python/client,
capacity, optional-suite and Linux distribution runs were not repeated for this
documentation/interface-fixture step; their existing evidence retains its dates.
