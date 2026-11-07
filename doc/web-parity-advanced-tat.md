# P5-20 ordered TAT rules — 7 November 2026

P5-20 delivers ordered Location-keyed stop and junction rules through the shared
controller manager, including masks, add/edit/copy/reorder/remove, confirmed
clear-all, shared updates and saved continuation. MAN-T08/W09/A12, DEV-P20 and
the rule/mask portions of SCN-009 are covered at browser/portable scope. TURNING
geometry is delivered by [P5-21](web-parity-fleet-turning.md); rule-file import/export remains P5-23 and Station/passenger
gameplay P6. No advanced-rule enable flag is introduced.

## Source/forms and translation contract

Authority is ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, raw `trunk/` objects, with the
[pinned 70-file review](parity/train-review.json),
[source text inventory](parity/train-localization-review.json),
[P5-01 contract](web-parity-train-contract.md#controllers-harbors-groups-and-files)
and [basic TAT implementation](web-parity-basic-tat.md). Raw source hashes and
excerpts were rechecked against the archive. Source inspection does not establish
original-desktop execution or native pixel fidelity.

All paths below are in `plugins/org.kohsuke.freetrain.tc.tat/src/`.

| Source | Delivered semantics |
| --- | --- |
| `TimeMask.cs:matches` | Five conjunctive predicates: month, day of month, weekday, hour and minute. Each uses -1 for wildcard. No year, holiday, train identity or enable predicate exists. The game calendar is the source fixed365-day calendar; weekday0 is Sunday. Impossible combinations, such as February31, are allowed and never match. |
| `StationAdvancedDialog.cs:createRule`, entry controls | Month1–12, day1–31, weekday0–6, hour0–23 and minute00/10/20/30/40/50, each with wildcard. The initially checked form action is Pass, despite `AdvStationRule`'s field initializer being Go. Entry masks initially are all wildcard; station Add retains the last committed entry. |
| `JunctionAdvancedDialog.cs:createRule` | Month/day/weekday/hour controls have the same ranges. Minute is implicitly wildcard and has no editor control. Straight is initially selected. Junction Add resets the mask fields while retaining the route selection. |
| Both advanced forms, `onSelectionChanged` | TRAIN-D005 restores `rule.day`, correcting the source's `rule.month` assignment to the nonwildcard day control. Edits retain April23 and December31 in the browser. |
| `Junction.cs:determineRoute` | Walk stored rules in order; first match selects Straight/Curve. No match uses the retained default route. Real deployment, movement and reversal guidance receive the current authoritative game time; followers continue to use predecessor paths. |
| `StationAdvancedDialog.cs:buttonAdd_Click` | Add rejects an identical five-field mask, regardless of action. Overlapping but unequal masks remain legal. Junction Add has no duplicate-mask check. |
| Station replace/copy/file-read paths | Replace and copy bypass Add's duplicate-mask check after structural validation. Copy appends an independent rule with the same values; replace retains its index. P5-23 still owns validated ordered file append and its corresponding bypass. This slice exposes no file operation. |
| Both forms, up/down/remove | Display and execution share stored order. The station ListView sorter/comparer is commented out in stable source. Browser Up/Down moves one position and does not sort or normalize masks. |
| Station `buttonAllClear_Click` | Explicit confirmation clears the location's entire list. Browser Clear all previews the operation; Apply confirms, Cancel preserves save bytes. The same bounded list operations are also offered for junctions as a browser convenience. |

Station action selection is source-bound as follows. These are actions selected
on matching rules, not independent boolean options.

| Source action / Japanese | Arrival (`callCount=0`) | Later calls |
| --- | --- | --- |
| `pass` / 通過 | Return0 immediately | Ignore and continue scanning |
| `stop` / 停車 | Return10 immediately | End scanning and wait for the next ten-minute boundary |
| `go` / 発車 | Ignore | Return0 |
| `reverse` / 折返発車 | Ignore | Return-1; central reversal then departure |
| `forwarding` / 回送 | Ignore | Return-999; forwarding then departure |
| `rforwarding` / 折返回送 | Ignore | Return-1000; reversal and forwarding then departure |
| `reverse2` / 反転折返 | Ignore | Source calls `checkTurn(train,-1)`; P5-21 owns handler-side poses/effects |
| `rforwarding2` / 反転回送 | Ignore | Source calls `checkTurn(train,-1000)`; P5-21 owns handler-side poses/effects |

Arrival considers only matching Pass/Stop rules, in order; otherwise it waits10.
Later calls ignore Pass and consider the departure actions or Stop, in order.
No applicable rule, or Stop, waits `10 - currentMinute % 10`; an exact boundary
waits10, never0. No `TimeMask.nextMatch` implementation or approximation is used.
P5-21 now exposes TURNING actions with their handler-side geometry; they are
never aliased to ordinary reversal. Portable
`reverseforwarding` is the explicit saved/wire tag for source `rforwarding`.

EN/JA labels retain the pinned forms' month/day/weekday/hour/minute, Pass/Stop/
Depart/reversal/forwarding and Straight/Curve meanings. List columns are labeled
in the same order. Local drafts, per-operation Apply/Cancel, independent controller
copies, conflicts and availability explanations are bilingual browser adaptations.
No native radio-button ordering or accelerator marker is interpreted as a new
runtime predicate. The split list/edit dialog replaces immediate native mutation:
opening, selecting and editing without Apply are read-only.

## Model, persistence and command boundary

`FleetTimetableRule` contains exactly the five mask fields and one action.
Immutable ordered rule arrays belong to a harbor or junction setting. A harbor
uses the explicit Advanced handler kind, with Minutes0 and Reverse=false; basic
handlers cannot carry rules. Changing a basic junction default preserves its
rules. Reapplying Advanced preserves its rules; switching to a different harbor
handler discards them, matching source handler replacement.

Save9/protocol7 gain optional inert `rules` arrays and the Advanced harbor kind.
Absent/empty rules are omitted when writing, preserving existing literal payloads.
No catalog/pack/version migration or executable deserialization is introduced.
Saved arrays retain duplicates and order. Typed controller copies clone setting
and rule records independently. Revision validation compares nested rule values
and order, including detached reconstruction after load.

The portable admission bounds are1024 rules per location and8192 total per
controller. Existing256-controller and8192-location limits stay in place.
Codec preflight bounds nested arrays before typed restoration; graph, wire and
browser semantic validation enforce context-specific masks/actions and total
capacity. Existing8MiB mixed-state,16MiB delta and overall save/snapshot byte
admission still apply. Capacity does not promise that every maximum can coexist.

`EditFleetRuleCommand` sends one rule or one indexed operation, never a whole
saved list. It remains below1KiB in the maximum-list test and uses the existing
16KiB command boundary, FIFO, participant role/connection, epoch and duplicate
receipt ledger. It checks the shared controller revision; junction changes also
check geometry revision and actual junction existence. Basic configuration cannot
smuggle bulk rule replacement around station Add's check. Copy/remove/reorder/
clear carry no rule payload; structural invalidity rejects the complete edit.

Publication validates the detached graph and state transition before installation.
Foreign driver leases reject affected configuration changes; accepted owned edits
clear held intent atomically. Replayed receipts do not append twice. No edit
moves trains, advances time, alters cash or replaces pending callbacks. A peer
change makes an active edit stale; world replacement closes the panel through the
existing shell lifecycle, with an additional epoch guard in the rules editor.

## Browser operation

Trains → Controller manager → select TAT → Configure controller. Select/add a
harbor location, choose Advanced rules and Apply. Reopen configuration and choose
Ordered timetable rules. An existing junction offers the same button alongside
its default route. A changed/new location must be applied before opening rules.
The signal inspector reaches the same harbor rules on the locally selected
shared train controller; the signal receives no assignment/override.

Add/Edit opens a local mask/action draft; Apply commits that operation. Copy
appends, Up/Down moves, Remove deletes the selected entry, and Clear all requires
Apply confirmation. Cancel discards the current uncommitted operation; Close
leaves previously applied edits in place. Mouse, native select keyboard navigation
and touch use the same controls; Observer can inspect but cannot edit. Native
dialog focus trapping/return and internally scrolling narrow/text-scaled layout
are inherited from `DecisionModal`.

## Verification and remaining gates

- **246/246 selected .NET tests**: fleet movement/controllers/manager, session and
  persistence. The44 new advanced cases include all17 independent P5-02 mask/
  ordered-rule fixtures, literal month/day/year/weekday/time boundaries, all six
  supported harbor actions, overlap/no-match/polling, duplicate-path differences,
  order/copy/remove/clear, malformed/oversized saves,8192-rule continuation,
  nested revision checks, roles/epochs/geometry/driver contention and receipt
  replay. Real two-signal trains exercise wait102→112, all departure sentinels,
  forwarding reset on ordinary track and byte-identical save continuation.
  Real junction heads/followers take the independently expected route after load.
- **92/92 selected client/contract tests**: `fleet-controllers`, `fleet-state`,
  shared contracts, `train-motion` and `mixed-state`. Context-invalid masks/actions,
  bounded commands, source Pass default, duplicate-mask equality, forbidden bulk
  replacement and unchanged-revision nested edits are covered.
- **7/7 selected Chromium cases**: new EN/JA advanced journeys, existing EN/JA
  manager/basic-TAT journeys and actual signal-inspector regression. Advanced
  journeys cover April23/December31 edits, minute wildcard/00, arrival/departure
  actions, duplicate Add rejection, duplicate copy/replace, reordered display and
  saved data, Cancel byte identity, retained Add entries, peer conflict, Observer
  inspection, controller copy isolation,
  confirmed clear, HTTP reopen, world-replacement closure and actual backend
  checkpoint restart. JA uses390×844 and150% text. EN/JA captures were inspected. A final advanced-only
  refinement run also passes2/2; the five neighboring regressions retain their
  preceding selected-run evidence.
- Production browser build, generated contracts/content, locales and Svelte
  diagnostics pass (zero errors/warnings; existing bundle-size advisory).
  Pinned70-file source review,744-fixture provenance check, project boundaries and
  whitespace checks pass.

Logs/input: `tmp/p5-20-*`; fresh gameplay-built browser fixtures:
`tmp/p5-20-tat-fleet.json`, `tmp/p5-20-manager-fleet.json`; inspected captures:
`tmp/p5-20-browser-evidence/advanced-tat/`. Browser checks used the production
client and Debug backend. No full web/backend suite or new native/IME/human-
fidelity, Linux-container, optional-suite or capacity acceptance is claimed.
P5-MANUAL-001 remains owner-deferred. P5-21 TURNING, P5-23 rule files, P5-24
integrated signals, P6 stations and later integrated recovery/performance keep
their own gates.
