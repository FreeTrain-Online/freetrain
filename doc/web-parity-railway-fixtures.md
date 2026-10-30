# P4-02 independent railway fixtures — 30 October 2026

**P4-02 is complete at independent-fixture scope. [P4-03 detached mixed state](web-parity-railway-state.md)
is now delivered. Next: P4-04 atomic booking.** The [literal vectors](parity/railway-fixtures.json)
and [read-only evaluator](../scripts/railway-fixtures.py) make the
[P4-01 source contract](web-parity-railway-contract.md) executable before railway
implementation. No live railway handler, save, content or protocol version changes.
P4 gameplay, native comparisons and the existing later-phase gates remain open.

The authority remains stable ExAV **2.1.1.12 / V2**, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Twenty-six source files are
bound to the hashes/symbols in [the railway review](parity/railway-review.json).
The explicit-source check reads raw pinned Git blobs, never the archive's mutable
working tree. Default verification requires only this repository.

## Expectations and provenance

The artifact stores **1,484 literal input/output cases, all 60 serialized patterns
and the eight directions**. Expectations were derived from the pinned source and
P0 tables independently of production implementation. The 192 existing P0 normal
attachment rows are reused verbatim; the rest expand source rules into literal
vectors. The evaluator never regenerates or overwrites expectations, imports
production code, launches legacy assemblies or uses rendered output as an oracle.
It checks the arithmetic/topology separately against the literals. These are
source derivations, not original-runtime captures or an external human review.

Artifact hashes bind the P4-01 review, scenario set, P0 geometry/behavior and
dependency review. Each case group names pinned sources, decisions and derivation;
stable IDs and coverage witnesses detect omitted cases. Future implementation
tests should consume these inputs/outputs directly, rather than rewriting their
expected results using a production planner.

| Group | Cases | Executable expectations |
| --- | ---: | --- |
| Attachments | 576 | All 12 normal patterns × open/well-connected × eight directions; all 16 junction and 32 slope patterns × eight directions |
| Detachments | 720 | Every serialized pattern × all 12 legal normal route edge pairs; normal clear/no-op, junction clear/convert/no-op/hazard, slope no-op |
| Connectivity | 5 | Two reciprocal same-Z neighbors; missing, wrong-facing, wrong-Z and unrelated-edge contrasts |
| Normal routes | 21 | Inclusive routes, one heading change in either direction, local occupancy/reclamation, endpoint charges, rejection and zero-mutation preview |
| Slope construction | 26 | All cardinal directions × surface/underground/mixed art, four rail and four paired cells, stored flags, eight occupied positions, ceiling/lateral bounds, water-relative booking |
| Slope removal | 22 | Resolve all four rail members in all cardinal directions; four protected car positions, malformed owner and wrong selected direction |
| Supports | 10 | Z0 obstruction scan, surface/body/top allocation, default/slope styles, buried/transparent obstruction, owner-agnostic type-filtered teardown |
| Normal removal | 3 | Charged no-op with foreign pier losses, car skips with unconditional teardown, invalid-pattern rollback contract |
| Special rails | 45 | Four bridge/eight girder axes, art/run/pier alternation, inclusive charges, first-cell water-zero quirk, half-open tunnel/garage removal, restoration and occupied skips |
| Boundaries/accounting | 21 | Stored HV versus visible border, negative/ceiling Z, Int32 coordinate admission, explicit work limit, widened products, Int64 overflow, default cash and finite bankruptcy top-ups |
| Drawing contracts | 6 | Traffic six-pass order, distinct pier/Mountain cut arguments, actual signal before/after dispatch |
| Trusted component interfaces | 29 | Special attachment, zero-price signals/poles, replacement semantics, road/crossing coexistence, Traffic-based speed eligibility/cleanup, stationary completion/cancel/demolition booking |

The four scenario bindings preserve the original inputs and expected values:

| P0 scenario | P4-02 binding |
| --- | --- |
| EXAV-SCN-001 | Eight literal direction vectors/rotations and the unsupported N/E cardinal corner; P0 attachment/pattern matrices |
| EXAV-SCN-002 | `ROUTE-dry-18m`: exact `(8,8,2)` through `(10,8,2)`, ground2/water0, three cells, **18,000,000** quote/commit expenditure and zero preview cash delta |
| EXAV-SCN-003 | `ROUTE-SCN003-z4/z6/z3/z2`: three times the source's per-cell **6m/18m/12m/24m** at ground4; `ROUTE-underwater` retains ground0/water2/Z1 rejection with zero quote |
| EXAV-SCN-031 | `DRAW-traffic-order`: road-before, accessory-before, rail-before, car, rail-after, accessory-after |

The checker verifies those transformations explicitly. These expanded cases do
not change the denominator of 31 P0 scenarios or grant them runtime parity credit.

## Reviewed sharp edges

**Junction detach can produce an invalid pattern.** The full matrix exposes 16
cases in which one requested edge matches a junction but its two remaining edges
form an acute pair. For example, pattern12 has `[1,4,5]`; detaching `[1,6]` leaves
`[4,5]`. `JunctionRailRoad.detach` calls `RailPattern.get`, whose angle assertion
fails and whose lookup returns null. The vectors retain `sourceOutcome:
null-pattern` separately from `portOutcome: reject-invalid-pattern`. This uses
P4-01's existing checked invalid-pattern/atomic-rejection boundary; it does not
invent a replacement curve or silently clear the junction. P4-03/06/07 must reject
the entire prospective invalid transaction with zero booking/support publication.

**No-op rail removal can still cost money and remove foreign piers.**
`REMOVE-noop-foreign-piers` changes no rail pattern but spends **16,000,000** and
removes three actual piers. A car prevents the normal detach/count, while support
teardown still runs at that visited column. P4-07/10/20 must enumerate every pier
and its owner in preview and remove them atomically, following the owner's
GEO-004/RAIL-D006 decision. Ownership is not a teardown exclusion.

**Special endpoints and billing stay operation specific.** Bridge/girder removal
includes the destination and charges wrong-subtype/train-skipped cells. Tunnel
and garage removal exclude it. Two matching garage cells spend **200,000 then
400,000**, totaling **600,000**; a matching cell followed by a nonmatching cell
still repeats the subtotal. Coincident special removal returns the source no-op;
coincident special build is rejected. A bridge first-cell underwater quote can
be zero while construction/removal still changes the scene. A later submerged
cell does not trigger that early return. These outcomes are separate from normal
underwater rejection and from protected build replacement.

**Stored slope art and flags survive terrain changes.** The mixed-ground fixture
and its later-ground-edit companion return identical stored rail patterns and
paired flags. Surface eligibility for support election is separate from the scan
for any lower occupant. `pierCalls` records the selected handler's election or
teardown call sites, not proof that a pier was created; `supports` tests actual
allocation/removal. Slope ownership and all paired-cell bounds are checked before
admission; a transparent occupant is still an obstruction.

All booking uses raw signed Int64 source money units. Widened multiplication
preserves **18,000,000,000** where source Int32 intermediates would wrap. Exceeding
Int64 rejects; a zero ending balance needs no bankruptcy top-up. The injected work
limit is a boundary-test parameter, not a new production railway size limit.

## Verification and implementation handoff

```sh
python3 scripts/railway-fixtures.py --check
python3 scripts/railway-fixtures.py --source-root /path/to/freetrain-exav-archive --check
python3 scripts/review-railway.py --check
python3 scripts/review-railway.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
python3 scripts/check-project-boundaries.py
git diff --check
```

The evaluator checks literal outcomes and verifies that its supplied input remains
unchanged, including preview cash/allocator/terrain/entities/clock/random/listener
sentinels. This establishes a read-only **oracle** contract; production preview
immutability, atomic commit/rejection, role/revision/epoch/content fences, exact
save/delta/restart continuation and controller/listener behavior still require
their implementing slices. The trusted component fixtures do not deliver moving
trains, roads, platforms, speed timing or construction-site scheduler integration.
The synthetic reference loop keeps its existing invariants.

P4-03 should consume the pattern/footprint/component inputs for canonical mixed
state validation. P4-04 consumes the money/reclamation/top-up cases; P4-05 persists
their actual state. P4-06 onward must test live planners and mutation against the
same literals, including losses and unchanged/skipped/charged cells. Existing
native/P2/deferred-browser, full-city capacity and historical-import gates remain
separate.

Fresh verification on 30 October 2026: default and explicit pinned-Git fixture
checks pass **1,484 cases /60 patterns /26 source files**. Both P4-01 source-review
modes pass. The complete Python evidence suite passes **161 tests with one
existing skip**, including 11 new oracle/corruption regressions. Terrain source
review, project boundaries and `git diff --check` pass. .NET/client/browser/native
suites were not rerun for this fixture/documentation-only step.
