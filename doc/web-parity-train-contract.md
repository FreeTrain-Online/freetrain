# P5-01 train/controller source contract — 2 November 2026

**P5-01 is complete at source-specification scope.** The
[P5-02 companion fixtures](web-parity-train-fixtures.md) supply independent
source vectors and existing-interface checks. This review adds no train gameplay, controller command, browser UI,
save/protocol version or runtime-limit change. The entry contract remains
Save8/protocol6. P5 runtime and phase acceptance remain open.

Authority is stable ExAV **2.1.1.12/V2**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Reads use raw pinned Git
objects rather than the archive's corrected working tree. The
[source/operation/decision review](parity/train-review.json) binds **70 files,
18 operation families, 18 decisions, eight dependency chains and 34 scoped
declarations** to P0 hashes, symbols and exact line excerpts. Direct train,
controller, loader and form reviews are distinguished from reused P0/P1/P4
dependency contracts with pinned-symbol verification. No desktop assembly ran.

The [translation review](parity/train-localization-review.json) records **638
source literals and 55 scoped catalog texts** with original Japanese/source
hashes, historical English donors, classifications and implementing owners.
These are scope/meaning dispositions, not shipped translations. Runtime fixtures,
actual content registration, EN/JA operation, original-runtime comparison and
human/native fidelity are later gates.

Use the [P4 railway handoff](web-parity-p5-railway-handoff.md),
[speed vectors](parity/speed-limit-handoff.json),
[geometry/dependency review](web-parity-geometry-dependencies.md),
[source scenarios](web-parity-scenarios.md),
[reciprocal action bindings](web-parity-actions.md#p5-01-traincontroller-workflow-bindings)
and [manual reconciliation](web-parity-manual.md#p5-01-traincontroller-source-reconciliation).
The checklist under local `tmp/` remains the ordered implementation record;
this contract supplies semantics and evidence.

## Stock definitions, trading and deployment

V2 `AbstractTrainContributionImpl` consumes company/type/nickname, author,
description, price, fare, amenity, speed and optional trip range. Required fare
parsing replaces old FreeTrain's hard-coded fare assumptions. Amenity clamps to
50…200. Trip range short/middle/long maps to 0/1/2; absent/unknown falls back to
0. Speed slow/medium/fast/superb maps to 4/3/2/1 source minutes per voxel;
arbitrary parsed Int32 speed has no source positivity check.

`ParamTrainImpl` requires a body, then replaces head and tail when supplied.
**At length one, tail replaces head** if both exist. `PatternTrainImpl` ignores
composition whitespace, resolves characters to exact car definitions, accepts
only declared lengths and clones returned arrays. Duplicate lengths fail source
dictionary insertion. Neither factory establishes a universal 100-car domain
ceiling. The purchase form sets minimum one and leaves its NumericUpDown maximum
implicit; resolving that library default and setting measured portable limits is
TRAIN-F001, owned by P5-03/P5-28. A preview's search for a suitable display
formation does not define permitted gameplay lengths.

Per-car normal and seated capacities remain independent. Train-level seated
capacity substitutes that car's normal capacity when seated is outside
0…normal, then packed capacity is `seated + 2*(normal-seated)` after the source
aggregate guard. Reverse wrappers inherit both capacities from their base car.
The color-test car has zero capacity and remains real bundled authored content.
The review includes **all trainCar support declarations**, even where the P0
consumer map assigned them only to P2; a ready picture is not a gameplay binding.
P5-03 must validate the entire reference graph and source numeric boundaries.

The source purchase catalog groups company/type/nickname, preserving duplicate
nicknames by appending spaces to dictionary keys. Browser identity must use
contribution IDs rather than those display keys. The quote multiplies
`price(1) * length * quantity` through the form's decimal values, then converts
to long. Purchase uses the same cloned current time for every new train in a
batch, adds them to the root group and spends once to RAIL_SERVICE. It has no
separate affordability predicate; retain existing accounting/bankruptcy behavior.
Portable quote, allocation, inventory and booking commit atomically.

Sale refunds `price(length)/(completed365DayYears+2)` with integer truncation;
null purchase time produces no receipt. Domain `sell` withdraws placed stock,
cancels callbacks and detaches membership/listeners. The **reachable sale UI
enables only unplaced trains**. Preserve withdraw → quote → confirm sale in the
baseline browser workflow instead of assuming domain capability authorizes a new
placed-sale shortcut. SCN-026 supplies the anniversary/missing-date expectations.

Deployment anchors the tail at the selected rail, starts with `rr.dir1`, walks
with guide and a temporarily placed head, then installs all cars. It rejects
missing/occupied track and duplicate locations. **It does not apply slope
zdiff** (GEO-008 / TRAIN-D003). Preserve that walk as a detached candidate; do
not fix it by substituting the motion walker. A browser orientation choice must
produce a valid equivalent source/reversed pose. Clicking an already selected
train reverses it in the source placement tool. Withdrawal clears all Inside
slots and unregisters the train callback while keeping owned stock.

## State, movement, timing and accounting

Car state is immutable Unplaced, Inside(location, heading), or
Outside(last external location, heading, countdown). Only Inside occupies a
Traffic slot. Original car identity/index/type is distinct from current array
order. Reversal uses each Inside car's current `guide().opposite`, reverses the
array and toggles the formation flag; follower lookup uses original index and
that flag. It is not merely opposite of each stored heading on a curve.

At callback entry capture the original Inside head. Ask current rail for a stop
result, including its mutations. A positive result increments stopCallCount,
schedules that wait, sets **StoppingAtStation** and returns. This is true for
signal waits too (GEO-005); do not invent StoppingAtSignal transitions. Present
the source's generic departure-wait meaning with separately identified harbor.
For a nonpositive result, reset forwarding first: -999 sets forwarding, -1000
reverses and sets forwarding, other negatives reverse, zero proceeds. Then reset
the stop counter.

Movement computes guide, XY step and **current rail's zdiff for the incoming
heading**, then evaluates the source HV/Z visible-border predicate before
destination track reciprocity. A junction head asks its effective controller;
followers reuse their predecessor's placed location. Any Inside destination car
blocks, including the same train's tail. A connected occupied candidate does not
trigger dead-end reversal. A missing candidate reverses once and retries.

For nonnull candidates preserve native timing when next is Outside or the
captured original head was not Inside; otherwise use limits at that captured
head's location, even after handler-side or central reversal. Priority is
Low → Medium → Fast, with exact source equality branches, not clamping. Positive
waits bypass this path. First placement uses native speed. The P4 handoff's
32 arithmetic rows/seven timing boundaries remain P5-02/P5-13 inputs.

Successful moves, including Outside countdown shifts, increment moveCount.
Moves 1/5/9/13/17 charge `(length*20 + passengers/20)*2000`, with integer
division. Waiting/blocked callbacks do not count. SCN-014's three cars and
39 passengers charge 122,000 each time. Purchase/sale and operating bookings
belong to P5; full economic histories and reporting remain P8.

**TRAIN-D001 / EXAV-DEP-013 / SCN-D03:** source reverses on missing forward
track, then dereferences null when the reverse candidate also fails. The portable
contract retains the performed reversal and ordered handler effects in a detached
candidate, records EmergencyStopping, performs no position shift/movement charge,
and registers exactly one future retry using captured-prior-Inside limit timing
or native timing when not Inside. It never dereferences null. This is an explicit
engineering adaptation, not evidence that native source retries successfully.
P5-02 must derive its full pose/time/queue expectations before P5-12 implementation.

## Outside return and rendering

Source exit creates countdown 100. Each nonzero callback decrements it; the
callback after zero attempts return, with missing track restarting at 10.
Candidate order is the source's five cardinal/diagonal matrix transforms, with
integer truncation, tested for reciprocal rail **before occupancy**. An occupied
first connected candidate blocks without trying later connected candidates.
Outside reversal uses the resolved return reference and complements countdown
to `100-timeLeft`; without a return it leaves the car state unchanged.

At resulting countdown 50, before shifting the consist, unload then load
`min(100, normalCapacity)` unless forwarding. Forwarding survives this exchange
and clears on any later nonpositive Inside stop result, including zero on
ordinary rail. Keep source boarding origin and truncated 3D-distance fare
arithmetic. P5 uses controlled passenger inputs to verify this path; actual
station populations, yard exchange, fares and bells remain P6 integration.

Use source 8+4 symmetric/color and 16+8 asymmetric frames, reverse wrappers,
curve offsets, slope offsets and train reversal independently. Level planar
angle has sixteen positions although world headings have eight. Traffic's
before/car/after pass order and P4 bridge/tunnel/garage/signal/pole occlusion stay
authoritative. P5-09 must run actual stock through old/new region invalidation,
alpha picking and cuts; this review adds no screenshot/native pixel acceptance.

## Controllers, harbors, groups and files

Simple and delegation are built-ins; manual and TAT are the two manifest
providers. User-created named controller instances are shared objects assigned
to trains/groups, not a per-train type toggle. Source manager hides built-ins,
offers create/configure/rename/copy/delete, and copies with BinaryFormatter.
Portable copies use typed deep clone with fresh IDs and preserved ordered data.
**TRAIN-D009:** reject referenced deletion with a usage list until explicit
reassignment; source merely removes the collection entry and leaves references.

**TRAIN-D002 / EXAV-DEP-014 / ACTION-015:** delegation is actually registered.
The named source root leaves controller null; lookup returns null immediately
because it differs from delegation. Only exhausted all-delegating chains use
simple fallback. The portable contract initializes a simple root, resolves null
to simple, traverses delegation ancestors, and rejects missing nonnull IDs,
duplicate membership, self/descendant cycles and root reparenting atomically.
The source has no group deletion action; baseline groups may be created, named
and moved without introducing deletion. P10 owns imported malformed graphs.

Simple chooses straight, waits 30 minutes on the first Station call then zero,
and ignores non-Station harbors. Manual's first call passes with Shift, otherwise
waits five; later Up/depart wins over Down/reverse, otherwise another five-minute
wait. Junction keys check valid left before valid right, then straight. Replace
User32 polling with existing participant leases, bounded expiry and neutral reset
on blur/disconnect/generation change; held input is never durable or replayed.

**TRAIN-D010 — correction to the P4/P5 planning assumption:** native
`SignalRailRoad.getStopTimeSpan` calls **the passing train's controller**, only
within one eighth-turn of its direction. It owns no controller assignment.
P4's nullable saved `RailComponent.ControllerId` is a reserved web field, not
native signal semantics. For baseline P5 keep it null and explicitly diagnose/
reject nonnull values at the new codec boundary; never use it as an override.
Signal properties may locally select which shared train controller's harbor
settings to edit. Two trains can therefore obey different settings at one signal.
This refines P4's handoff wording without changing its infrastructure acceptance.

TAT junctions and harbors are keyed by **Location**, not TrainId, SignalId or
newly allocated entity identity. A yard supplies the resolved host Station;
a signal supplies itself. Unconfigured junctions go straight; unconfigured
harbors use ten minutes then depart. Station-specific settings therefore apply
through its platforms, while a signal is an independent non-passenger harbor.
Retain source same-location settings/reuse, including absent harbor entries.
Source config refresh removes only missing junction keys; browser query/open/
cancel must remain read-only, with that cleanup in confirmed configuration work.

Basic TAT offers pass, fixed duration, once-daily time and advanced rules, with
basic reverse-on-departure options. Daily equality waits a full 1,440 minutes:
arrival 479/480/481 for departure 480 waits 1/1440/1439. Masks compare month,
day, weekday, hour and minute conjunctively, wildcard -1. Junction UI supplies
minute wildcard; station UI supplies wildcard or 00/10/20/30/40/50.

Advanced rules execute in **stored order**. First call considers matching pass
or stop, defaulting to ten minutes. Later calls apply go/reverse/forwarding/
reverse-forwarding/stop, or poll the next ten-minute boundary. `nextMatch` throws
and is unused. Station Add rejects identical five-field masks regardless of
action; replace, copy and file append bypass that check. Preserve those distinct
paths after structural validation. Up/down changes execution order. **The stable
source's ListView sorter and comparer are commented out**, qualifying older
source-review shorthand about sorted display. Do not restore a sorter or invent
an enabled-rule flag from that shorthand.

**TRAIN-D005:** both station and junction editors mistakenly put `rule.month`
into the nonwildcard day control. Restore `rule.day`; an unchanged edit must
retain its mask. This corrects UI data integrity without changing mask matching.

**TRAIN-D006:** `checkTurn` reverses before validating poses and can return
without undoing that reversal. It can remove/re-place individual cars while
leaving the train timer intact. The one-car nonsharp branch returns **-1000
even for a requested -1**, which can activate forwarding during ordinary
reverse2. Preserve those ordered source effects, then yard effects, then central
sentinel interpretation; a pure-duration mock is insufficient. Invalid portable
re-placement aborts the complete detached callback rather than leaving partial
occupancy. P5-02/P5-21 require independent per-car/early-return pose vectors.

Source `.ftdd` read/save uses binary `AdvancedStationHandler.RuleCollection`;
SOAP is commented out. Read appends rules in order. Portable import uses bounded
inert allowlisted records with preview and atomic validated append, never runtime
legacy object deserialization. Export is a versioned **portable JSON rule file**,
clearly labeled; native-writable binary export is not implemented or promised.
Original inputs survive failure. This P5 rule exchange does not implement P10
`.ftgd`/`.ftgt` world-save conversion.

## Numeric, lifecycle and presentation boundaries

**TRAIN-D007/008:** admit only positive Int32 native speeds, without clamping
values above four or changing the source speed-limit equality branches. Check
future due times against P1 calendar limits. Preserve original source evaluation
width/order/truncation for price(length), capacities, operating expense and fare
products. Checked Int32 intermediates and checked long/decimal conversions reject
out-of-range inputs rather than silently wrapping, saturating or expanding the
economic domain. Preserve ordinary source outputs. Invalid content/quotes fail
before mutation; unexpected callback overflow aborts detached advancement under
the existing clock policy. No new runtime numeric limits ship in this review.

Keep P1 GEO-002 cancellation and equal-time queue ordering unchanged. Extend
MixedSessionState's construction-only callback assumptions before real trains
arrive. Every candidate includes cars/occupancy, controller effects, counters,
queue and accounting atomically, with one source-required pending train callback.
Saves retain purchase date presence, forwarding, boarding origin, original car
indices/current order and shared references. They omit UI listeners, credentials,
held keys and camera. Current-format-only rejection and pack identity policies
remain; actual fleet codec and recovery are P5-05/P5-27.

Train information with nested selection and one-time Move is distinct from local
continuous tracking. Percentage uses normal capacity and can exceed 100% with
packed passengers. Source Outside locations are placed references; Move/follow
may frame that external reference, while Unplaced disables them. Browser bounds,
sale/replacement cleanup and stable TrainId selection must be explicit.

The reachable color-test workflow selects template/body/three line colors,
previews day/night and exports `colorMap` XML. Native dialog edits a shared car
definition; portable preview/export stays local, while explicit shared apply
persists a **world-local override** affecting all test-car users in that world.
Never mutate the process-global prepared catalog or another session. Preserve
the source palette and 180/255 darker shades; zero-capacity stock is valid.

The translation inventory classifies 206 presentation/dynamic literals,
279 technical/format tokens, 143 numeric-mask/designer values and ten content
diagnostics. Numeric choices should be generated/formatted; XML/IDs/paths stay
invariant. Designer placeholders and symbols such as the source selection
button's font-dependent `6` need explicit accessible browser adaptations, not
literal player labels. Proposed terminology distinguishes Empty stock, Reverse,
Turn formation, Withdraw/Sell and normal/seated/packed capacity. Each implementing
slice still reviews/shares EN/JA resources and tests actual controls/drafts/help.

## Verification and handoff

The checker validates authority/hashes/excerpts, exact operation/declaration and
dependency coverage, reciprocal MAN/DEV/action links, explicit dispositions,
translation/donor completeness and separation from runtime claims. Its corruption
tests deliberately rehash malformed records so semantic guards must reject them.
It imports no production movement code. P5-02 supplies independent executable
movement/controller fixtures rather than treating this checker as parity proof.

```sh
python3 scripts/review-trains.py --check
python3 scripts/review-trains.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests -p 'test_train_review.py'
python3 scripts/check-railway-handoff.py
git diff --check
```

TRAIN-F001 keeps measured admission/default-control bounds with P5-03/P5-28;
TRAIN-F002 keeps expanded independent pose/shared-signal/null-retry fixtures with
P5-02; TRAIN-F003 keeps existing TAT resource, P2 human/native and optional-suite
qualifications with P5-03/P5-29. These are named follow-ups, not hidden approval
gates or runtime completions. P6 retains real platforms/passengers/fares/bells,
P10 historical worlds/full-city recovery and P11 integrated optimization,
including owner-deferred P4-CAP-001/002. Other-engine/native checks stay deferred.

Fresh verification on 2 November 2026:

- Default review and explicit archive verification pass: all 70 raw Git hashes,
  bound symbol excerpts and 638 literal expressions at their pinned lines match.
  Logs: `tmp/p5-01-review.log`, `tmp/p5-01-source.log`.
- **Nine/nine evidence-corruption tests pass**, including omitted support cars/
  providers, lost dependencies/text, changed donors, unsafe signal/null/arithmetic
  policies and false runtime/shipped claims: `tmp/p5-01-corruption.log`.
- Full Python regression: **172 tests run, 171 pass and one existing skip**
  (34.788 seconds), including the nine new checks: `tmp/p5-01-python.log`.
- Existing P4 source/78 translation dispositions, 1,484 independent railway
  fixtures, P4/P5 handoff and backend project boundaries pass:
  `tmp/p5-01-railway-review.log`, `tmp/p5-01-railway-fixtures.log`,
  `tmp/p5-01-handoff.log`, `tmp/p5-01-boundaries.log`.
- Local Markdown targets/anchors, checklist step order/status and whitespace
  checks pass. No .NET/gameplay/browser/container/native run is credited for
  this source/specification step; their earlier evidence keeps its original scope.
