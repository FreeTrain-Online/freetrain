# P4-19 speed-limit authoring — 1 November 2026

The **Speed limits / 速度制限** task (**L**) authors level sections and cardinal
slope sections with **Low / 低速**, **Medium / 中速** and **Fast / 高速** choices.
It uses the existing map or keyboard-coordinate selection, detached authoritative
preview and confirmed FIFO command workflow. Removal clears membership; an
unlisted position reads **No limit / 制限なし**. No money is booked.

## Source and domain contract

Stable ExAV 2.1.1.12, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
is the authority. [The P4 contract](web-parity-railway-contract.md#speed-limit-authoring-and-crossings),
RAIL-D012 / EXAV-DEP-011 and [the pinned source/P5 handoff](parity/speed-limit-handoff.json)
identify all seven source files and their raw SHA-256 values.

[SpeedLimits](../src/FreeTrain.Online.Simulation/SpeedLimits.cs) stores three ordered,
immutable world-local lists. Every world initializes them without opening a menu.
The source Hashtable addition deduplicates within the chosen list; ArrayList
addition appends duplicates. Removal consumes one occurrence of each selected
location in **each** list. Direct list state may therefore contain duplicates or
cross-list membership, and save/reopen retains its order and multiplicity. The
interactive authoring guard rejects **any** overlap across all three lists before
adding anything. There is no replacement/overwrite shortcut.

Level selection follows the inclusive source sign-vector walk at one Z and
validates its incoming/outgoing RailPattern without trying to attach or detach
actual track. Coincident endpoints produce an empty, unavailable preview rather
than a selectable single-cell range. Every addition position must contain a
**TrafficVoxel**, including road-only or accessory-only Traffic; curves, junctions
and special rails remain eligible. It does not require straight normal rails.
Flat removal can pass through absent Traffic cells and clears selected list keys.
Removing an entirely unlisted section accepts a no-op without mutating the world
or promoting an untouched terrain world to the mixed profile.
The shared bounded route guard rejects extreme input before allocating a route.

Slope addition uses exactly four cardinal positions
`base + i * direction + (0,0,i/2)` for `i=0..3`. It tests those four Traffic
positions and does **not** require a slope owner or slope rail subtype. Slope
removal resolves any of the owning slope's eight rail/filler/support/wall members
to its canonical lower base, and verifies the chosen direction against that owner.
This removes the native controller's unchecked selected-direction assumption.
A four-position selection over standalone Traffic can be removed through level
ranges at the two elevations; source slope removal itself requires slope ownership.

[MixedWorld.Replace](../src/FreeTrain.Online.Simulation/MixedWorld.cs) cleans up
locations only when the TrafficVoxel disappears, across all infrastructure
replacement paths. Rail removal that leaves a road or pole retains the limit;
removing the last accessory clears it. Slope teardown clears all four positions.
Load validation rejects absent/non-Traffic membership before world replacement.
The native cleanup menu is adapted to automatic infrastructure-lifecycle cleanup;
there is no saved singleton, menu-initialization dependency or manual repair gate.

## Transport and browser behavior

Save8/protocol6 remain the current identities. `mixed.speedLimits` is an optional
object containing `low`, `medium`, `fast` arrays of canonical XYZ positions,
bounded to 8,192 entries **per list**. Empty state is omitted in publication/save
while the domain always has initialized lists. Current mixed documents without
limits retain their exact shape. Membership changes increment the mixed geometry
and session revisions for stale-draft fencing, while preserving entity revisions,
occupancy, allocator, terrain, accounting, clock and random state.

`RailwayQuery.infrastructure: speed` uses typed `speed`, `speedSlope` and
`speedClimb` fields; slope queries have identical input endpoints. HTTP preview,
WebSocket admission, FIFO replanning, role/epoch/geometry/content checks and client
preview identity all include this operation. Observer clients can see saved
limits but cannot submit edits. Cancel/Escape, stale results and replacement use
the established railway fences; opening the task does not pause shared time.

The three original 25×13 icons are source overlays, at voxel origin `[0,0]`,
after Traffic's accessory-after pass in H/V/Z traversal. Each icon uses its
original prepared day surface in every season and light condition, matching the
source controllers. Generated alpha masks derive from the hash-verified prepared
PNG; runtime decoding also checks image hashes and extents. They participate in
normal occlusion, cuts, localized column invalidation and alpha picking with the
actual Traffic owner. The inspector exposes the icon's source contribution.

**Show speed limits / 速度制限を表示** in View is a local visibility adaptation
(default on), extending the native tool-only overlay to a persistent inspectable
view. Hiding/showing does not alter saves or another client's view. Limits are
never inferred from decorative icons or rail owner IDs. Prospective road/car
fixtures establish the domain interface; P5/P7 still own actual gameplay and art
for those consumers.

## Localization and contribution dispositions

RAIL-TEXT-028/038 map to `speedTask`; 029/030/053 map to the level/slope section
selector; 031 maps to automatic cleanup described in `speedRemovalHelp`.
032–034 use the shared start/end selection plus `speedHelp`;
035/050 map to Add limit / 追加; 036/051 map to shared Remove / 撤去;
037/052 map to `speedValue`; 039–041, 044–049, 056–058 and 060–062 map to typed
Low/Medium/Fast resources. 042/054 map to `speedLimitTraffic`, 043/055 to
`speedLimitOverlap`, and 059 to the existing owning-slope removal diagnostics.
CORE-06, EXAV-09/13, EXAV-ACTION-003, MAN-M03/A12/A16 and DEV-P08 bind to
these authoring controls, lists, cleanup and saved state at P4 scope; train timing
remains P5. The source specification/localization review remains a source record; this report
records shipped EN/JA meaning and the lifecycle/UI adaptations.

| Bundled contribution | Disposition |
| --- | --- |
| `{CFC7C99B-4EDE-493B-9DD1-268633902901}` | Low icon: original surface, alpha mask, overlay/pick/provenance |
| `{CFC7C99B-4EDE-493B-9DD1-268633902902}` | Medium icon: same |
| `{CFC7C99B-4EDE-493B-9DD1-268633902903}` | Fast icon: same |
| `{998985DC-5A8E-4C72-96B6-56EDC545305B}` | Native menu/controller workflow adapted to L task and automatic cleanup |

## P5 handoff

[The machine-readable handoff](parity/speed-limit-handoff.json) supplies **32**
literal numeric boundary rows and **seven** timing boundaries from `Train.cs`.
Low always selects 4 minutes. Medium keeps native 4 and otherwise selects 3.
Fast keeps native 4/3 and otherwise selects 2. No membership keeps the native
numeric value. These are exact equality branches, not a generic min/max formula;
native 5, for example, becomes Fast 2. Nonpositive numeric rows characterize
arithmetic only and do not establish scheduler/input acceptance.

`clockHandler` captures the prior inside head state before move/reverse. A
positive station stop schedules its explicit duration and returns. After movement,
next-outside or prior-not-inside uses native timing; otherwise the lookup uses
**the prior head voxel**, not the next head location. Priority for direct cross-list
state is Low, then Medium, then Fast. P5 must execute these boundaries with moving
trains, reversing, blocking and scheduler continuation. Authoring/reload checks do
not claim moving-train timing parity.

## Verification

- Full Release .NET **1,036/1,036** passes (`tmp/p4-19-dotnet-final3.log`).
  Eleven new domain/session checks cover literal eight-bearing/bent flat walks,
  all speed choices and cross-list overlap, curve/junction/road/accessory
  eligibility, four cardinal slope ranges without requiring slope ownership,
  removal from every owning slope member, wrong-direction rejection, duplicate
  list semantics, absent-Traffic cleanup, zero booking with a full journal,
  detached preview, Observer/stale fences, exact saves, world isolation and true
  no-op removal that preserves an untouched terrain world/profile.
- Client **217/217** passes (`tmp/p4-19-client-final4.log`), including typed preview
  identity/four-cell shape, malformed speed/heading, absent/non-Traffic/oversize
  membership rejection, exact three source icons/alpha picks/pass order in eight
  season/light variants, cuts, hide/show, changed-column operation reuse and
  inspector invalidation after membership removal.
- Python **162 checks, one skip** passes (`tmp/p4-19-python.log`). Pinned source
  review/default review, **1,484** independent railway fixtures, the 64-entry
  accessory ledger, project boundaries and whitespace pass. The handoff's seven
  raw Git blob hashes are verified (`tmp/p4-19-handoff.log`); source numeric/timing
  vectors remain a P5 execution contract.
- Production build, generated contracts/content, locales and Svelte checks pass
  (`tmp/p4-19-build-final2.log`). The existing large-chunk advisory remains.
- Focused Chromium **4/4** passes (`tmp/p4-19-browser5.log`): EN/JA three-speed
  level sections, exact source pixel samples, overlap/no-limit/removal, four
  cardinal slope ranges, member removal/direction fences, demolition cleanup,
  peer publication, HTTP exact reopen/reload, world isolation, local icon
  visibility with exact pixel restoration, L/Escape/map picks, stale replacement,
  Observer and 430×850 confirmation. Initial captures in
  `tmp/browser-evidence-nQ0m6J/speed-limit/` were visually inspected.

Shared railway/catalog Chromium **64/65** passes all **60 existing** cases and
four speed-limit cases (`tmp/p4-19-browser-final.log`). Its new Japanese inspector
test sampled camera metadata before the resized frame updated; the test now
waits for rendered frames before deriving its click coordinates. Fresh isolated
speed-limit Chromium **5/5** passes against the final build, including actual
icon inspection, Japanese slope/narrow controls, and the final unlisted-section
no-op/profile preservation (`tmp/p4-19-browser-speed-final.log`). Final captures
in `tmp/browser-evidence-YMAhTR/speed-limit/` were visually inspected, including
`ja-narrow-slope-preview.png` and `ja-speed-inspector.png`.

Integrated P4-20–24, P5 motion, optional plugins and native/human fidelity
qualifications retain their recorded owners.
