# P5-21 TURNING compositions — 7 November 2026

P5-21 implements source `checkTurn`, `reverse2` and `rforwarding2` as
handler-side consist operations, followed by the existing central negative-result
handling and movement. Both actions are available in the EN/JA advanced TAT
editor and preserved through commands, controller copies, Save9 and protocol7.
SCN-009's physical turning scope is covered at browser/portable scope; actual
Station/yard passenger exchange and bells remain P6 companions.

## Source and ordered effects

Authority is ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects. The
[pinned review](parity/train-review.json), [independent 36 TURNING fixtures](parity/train-fixtures.json),
[TRAIN-D006 disposition](web-parity-train-contract.md) and source-bound
[translation inventory](parity/train-localization-review.json) retain their
original hashes. This is source-derived behavior, not original-runtime capture.

`plugins/org.kohsuke.freetrain.tc.tat/src/AdvancedStationHandler.cs:36–194`
provides these branches, in order:

| Source branch | Portable result |
| --- | --- |
| Argument other than -1/-1000 | Return -1 with no handler change; no TAT action invokes this branch. |
| Captured head is not Inside | Return the requested negative without handler reversal. Normal Outside callbacks bypass harbor dispatch; direct boundary tests cover this helper branch. |
| Inside head | Capture its location, reverse the train, then traverse from the captured old head through its new predecessors. Do not undo this first reversal on a fallback. |
| A traversed car is not Inside, lacks rail, has a curve, or has a third rail port | Return the requested negative with the first reversal retained. The missing-rail guard is unreachable in an admitted portable graph: reciprocal occupancy already requires rail and initial reversal reads it. It remains a defensive source branch, not a supported malformed save. |
| One car, straight diagonal | Return **-1000 even for reverse2**. Central handling therefore enables forwarding. |
| Multiple cars with equal X/Y end displacement | Return the requested negative after the first reversal. Preserve this exact source predicate; do not replace it with a general diagonal test. |
| Remaining straight poses | Choose the old-head or old-tail anchor using the source direction predicate, remove individual cars, optionally reverse their unplaced order, re-place tail-to-head using `dir1` and current rail guidance, optionally reverse again, then return the requested negative. Re-placement deliberately omits slope zdiff. |

Port enumeration is sorted numerically for the source scan; stored railway port
order is not always numeric (notably pattern10). Named cars may exchange physical
slots while their definitions and original indices remain attached to their IDs.
The train's callback is neither canceled nor registered by the handler. Car and
train revisions normalize once when the complete callback publishes.

`Train.cs:332–370` captures the old Inside head before the handler, clears
forwarding after the harbor result, interprets -1000 as central reversal plus
forwarding and other negatives except -999 as central reversal, resets stop
calls, then attempts movement. The existing continuation uses that captured old
head's speed-limit location. Occupancy checks use the handler's current car
slots plus other trains, including its own tail as an obstruction. They allow a
vacated old slot without overwriting another train.

TRAIN-D006 preserves source-ordered effects inside an immutable candidate.
Insufficient rail space, an occupied re-placement slot, a world-boundary escape,
a duplicate slot or unusable guide rejects the complete detached minute. The
source world, cars, accounts, clock, callbacks and queued neighbors survive
unchanged. A follower that reaches a branching junction while its predecessor
is still unplaced also rejects rather than inventing a predecessor route.

## TAT, persistence and presentation

Arrival still considers only Pass/Stop, with the ten-minute default. Later calls
walk stored order, stop at the first applicable departure action or Stop, and
retain all mask and polling rules from [P5-20](web-parity-advanced-tat.md).
The new tags are `reverse2` and `rforwarding2`; ordinary source `rforwarding`
retains the existing portable `reverseforwarding` tag. Junctions reject both
TURNING tags. No enable flag, bulk replacement path or new command is added.

EN **Turning reverse / Turning forwarding** correspond to source JA
**反転折返 / 反転回送**. The editor explains straight-track re-placement and
the single-diagonal-car forwarding exception. Existing local Apply/Cancel,
duplicate-mask rules, revision/epoch fences, driver permissions, bounded inert
rule lists and controller-copy isolation apply to both actions. Read-only helper
APIs cannot silently treat a TURNING action as a duration; live dispatch supplies
the world and train for its handler effects.

## P6 passenger and bell companions

`core/world/rail/YardRailRoad.cs:61–85` calls the controller first, then processes
the returned result before central train handling. P6 must retain these explicit
expectations when actual yards/Stations are introduced:

| Situation | Ordered companion expectations |
| --- | --- |
| Arrival returns positive10 | Handler decides to wait; yard unloads; central increments StopCallCount and registers the positive wait. No departure loading or bell. |
| Later reverse2 returns -1 | Finish handler reversal/re-placement first; yard loads passengers against that handler pose and rings the bell; central clears forwarding, reverses, resets stop calls and moves. |
| Later rforwarding2 returns -1000 | Finish handler changes; yard skips loading and rings the bell; central reverses, enables forwarding, resets stop calls and moves. |
| One diagonal car with reverse2 | The actual result is -1000, so the yard must skip loading and ring the bell before central reversal/forwarding. Do not choose boarding behavior from the configured action name. |
| Curve, junction or mixed-Outside fallback | Retain handler mutations before yard loading/bell selection from the returned negative, then central interpretation. |
| Invalid portable re-placement | Reject the complete candidate; do not publish passenger, bell, accounting or queue effects. |

These are integration expectations; this step creates no Station population,
boarding, catchment, yard or departure-bell gameplay.

## Verification and limits

[Production tests](../tests/FreeTrain.Online.Tests/FleetTurningTests.cs) consume all
36 independent handler and pre-movement central poses in both original car
orders (72 cases). They compare exact per-car identities, original indices,
locations, headings, reversal flags and result sentinels before/after save,
publish both intermediate poses through actual occupancy, and retain exact
pending callbacks. The two abstract Outside fixture locations are relocated to
a real external border away from all return candidates, preserving their source
no-return meaning.

Eight actual signal journeys cover one/three cars, both cardinal travel
orientations and both actions, arrival200/departure210, old-head delay to214,
forwarding reset, move/stop counts, a queued neighbor and byte-identical save
continuation. A trusted Station adapter separately executes the one-car diagonal
reverse2 callback; actual Station gameplay remains P6. Additional cases exercise
ordered mask/Stop barriers, command/copy/save round trips, helper early returns,
a handler-vacated destination slot, and atomic missing-space/foreign-occupancy/
world-boundary failures.

- Selected backend movement/fleet/controller regressions: **329/329**, followed
  by the final TURNING subset **91/91** after three additional boundary/regression tests.
- Selected web controller, fleet-state, protocol and state-transfer tests: **72/72**.
- Selected Chromium EN/JA TURNING and existing advanced ordered-rule journeys:
  **4/4** in `tmp/p5-21-browser-final.log`.
- Production web build, generated contracts/content/locales and Svelte checks
  pass (zero Svelte errors/warnings; existing bundle-size advisory).
- Explicit pinned-archive 70-file source review and 744-fixture verification,
  project boundaries and whitespace checks pass.

Browser checks edit/cancel/apply both actions, reopen through HTTP save upload,
advance actual signals through ten keyboard single steps, inspect exact cars,
headings and one callback, then restart from a real checkpoint. Japanese uses
390×844 and 150% text. Captures are in `tmp/p5-21-browser-evidence/turning/`.
The EN/JA editor captures were visually inspected; the narrow editor scrolls
with its Apply/Cancel footer visible. Logs and generated inputs use `tmp/p5-21-*`.

No full web/backend suite, native/IME/human-fidelity, Linux-container,
optional-suite or capacity acceptance is claimed. P5-MANUAL-001 remains
owner-deferred. Nested groups, rule files and integrated infrastructure acceptance
retain P5-22 onward; Station/passenger/bell gameplay remains P6.
