# P4-20 cross-tool integration — 1 November 2026

Terrain editing remains available after railway construction. Corner, held-paint,
rectangle/repair, terrace/cut and map-authoring tools read the combined spatial
index and allocate from the combined high-water mark. Occupied cells retain their
source per-tool protections; legal sites elsewhere can still be edited. Ordered
skips, geometry, owners and allocator publish once, after canonical validation.
Terrain edits retain their zero booking and do not demolish infrastructure. Forest/creek authoring retains its copied-stream preview/commit behavior.

## Authority and dependency matrix

Stable ExAV **2.1.1.12**, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, is the behavior authority.
The [railway contract](web-parity-railway-contract.md),
[66 pinned source reviews](parity/railway-review.json) and
[1,484 independent fixtures](web-parity-railway-fixtures.md) remain normative.
This integration does not change RAIL-D004/005/006/008/010 endpoints, occupied
skips, owner-agnostic pier teardown, road coexistence or cumulative garage billing.

| Dependency | Construction / conversion | Removal / protection | Executable evidence |
| --- | --- | --- | --- |
| Normal / junction / special rail | Normal endpoints attach only under source topology rules. Specials retain compatible rails; garage replaces compatible rails, excluding tunnel backups and slope owners. Signals accept ordinary rail or vacant Traffic only. | Normal detach clears normal/junction patterns only; slope/special detach stays a charged no-op. Dedicated subtype handlers retain their axes, endpoints, skips and prices. | `CrossToolDependencyTests`, existing normal/bridge/girder/tunnel/garage/signal suites |
| Ramp rail, filler, support and wall | All eight cells must be vacant. Paired flags/art stay stored. Terrain reads every owning member as occupied, including invisible fillers. | Any owning member resolves the complete ramp. Car/platform guards inspect all four rail positions before removal. Tool removal preserves road/pole Traffic under fresh standalone owners; entity removal removes whole Traffic. | `CrossToolDependencyTests`, `MixedTerrainIntegrationTests`, existing slope suite |
| Elected piers | Exact floor-to-rail obstruction scan suppresses election without rejecting the rail. Decorative stationary piers have no support role. | Source teardown deletes every lower pier at its actual call sites, irrespective of supported owner, including normal no-op/car-skip visits. Other infrastructure and terrain remain. | Existing normal/slope/bridge/girder support tests; mixed terrain pier protection |
| Signals / controllers | Direction conversion refreshes crossing conditions. Pole/speed changes preserve rail and controller reference. | Signal removal restores normal pattern and refreshes crossing; occupied cars reject. Garage/source entity replacement can remove a signal/controller reference. P5 owns controller execution. | `SignalConstructionTests`, `CrossToolDependencyTests` |
| Road / crossing | No broad road exclusion. Exactly two opposite cardinal rail ports and both perpendicular road bits allow crossing creation in an empty accessory slot. This includes all cardinal slope patterns. Existing poles suppress creation. | Losing conditions removes only crossing; pole removal recreates crossing when conditions still hold. Dedicated rail removal preserves road. Missing crossing handler rejects atomically. | All 60 patterns × 255 road masks, signal axes, normal road-only build/detach and slope crossing teardown |
| Electric poles | All four types retain source cardinal eligibility and non-null accessory replacement; cars do not generally prohibit accessories. | Null removal refreshes crossings. Removing a last pole that would leave a car without a canonical Traffic host rejects `carOccupied`. Whole-object removal protects cars. | `PoleConstructionTests`, `CrossToolDependencyTests` |
| Stationary accessories / sites | Whole source footprint must be available; terrain can be edited elsewhere while callbacks remain scheduled. | Dedicated completed-object demolition keeps SUBSIDIARIES billing; site cancellation discards its callbacks. Generic completed-object removal stays unavailable. | `StationaryConstructionTests`, `StationarySessionTests`, mixed terrain callback preservation |
| Speed-limit membership | Traffic existence controls eligibility, independently of rail subtype, road or accessory. | Cleanup follows absent Traffic, retaining road/pole survivors and order/multiplicity. All whole-owner demolition paths clean absent members. No separate booking. | `SpeedLimitTests`, `CrossToolDependencyTests`, client/browser speed tests |
| Terrain / tunnel backup | Rail, road, accessory, car, platform, support and stationary occupancy are visible to terrain tools. Shared allocator prevents terrain/rail owner collisions. | Tunnel restoration stays with its dedicated handler; road/accessory/platform dependencies reject restoration and cars skip. Tunnel corners survive unrelated terrain/rail edits. | Mixed terrain matrix across seven rail subtypes, tunnel dependency/save checks |
| Terrace / ground flags | Track may stand on terrace ground above its backing voxel. Source terrain checks apply at selected/adjacent cells. | Removing the terrace under occupied upper cells rejects. Track demolition preserves terrace backup; subsequent terrace removal restores it. Ramp flags do not change after terrain edits elsewhere. | `MixedTerrainIntegrationTests` terrace and mixed-ramp cases |
| Prospective platforms / cars | Trusted explicit state bindings only; no registration from prepared art. Builds protect cars and cannot discard a ramp/tunnel dependency. | Platform-dependent destructive routes reject before charge; source car skips remain local. Signal/garage conversion retaining a rail can retain platform. Whole-object lines retain ordered protected/tunnel skips and remove eligible owners in one result. | Trusted canonical .NET fixtures; P5/P6/P7 real gameplay traces remain pending |

"No orphan support" means no unintended malformed owner, partial slope or
unreconciled teardown remains. Source GEO-004 deliberately does not maintain a
support-required invariant: a rail can remain after foreign/no-op pier teardown,
and a historical supported-owner ID may outlive a removed rail outside the exact
teardown range. These are recorded source states, not authorization to invent
extra ownership cleanup. Canonical validation still rejects a pier reference to
an unrelated live owner and overlapping spatial owners.

## Implementation and publication

[TrafficLifecycle](../src/FreeTrain.Online.Simulation/TrafficLifecycle.cs) extracts
`TrafficVoxel.onModified/shouldHaveCrossing`. It is used by normal, signal,
garage, elevated and pole setters and slope tool removal. The source
`RailPattern.createSlope` sets the same two opposite cardinal ports used by the
crossing predicate; there is no source flat-only or zero-zdiff exclusion.
Crossing cleanup losses are included in railway/slope previews. The crossing
state binding remains explicit in trusted interface profiles; bundled road/car/
platform handlers and their live rendering remain P5/P6/P7 work.

[MixedTerrain](../src/FreeTrain.Online.Simulation/MixedTerrain.cs) composes the P3
draft with mixed occupancy and allocation. A draft with no new terrain entities
can retain the terrain-only allocator; publication preserves the larger combined
allocator. Mode entry/exit and terrain commits use the same mixed successor,
admission, account, clock, random and pending-construction validation boundary.

Corner queries, mode changes, rectangle anchors, held painting and pending
previews now fence on **mixed geometry revision** in mixed worlds, and terrain
geometry revision in terrain-only worlds. Thus a peer pole/speed/rail edit
invalidates terrain review even if `terrain.geometryRevision` did not change.
Clock-only progress does not invalidate geometry review. Epoch/role/disconnect,
abort, uncertain outcome and no replay guards remain in force.

English and Japanese retain existing terrain controls and occupied/skipped-site
diagnostics; the mixed-world help now describes available terrain tools and
backup protection. Save8/protocol6 remain current; no schema or new client
mutation endpoint is introduced. Browser `validMixed` and rendering continue to
reject unavailable road/car/platform consumers rather than presenting fixtures
as gameplay.

## Verification

- Full Release .NET **1,094/1,094** passes (`tmp/p4-20-dotnet-final2.log`).
  The 58 new cases cover trusted coexistence/crossing and demolition interfaces,
  seven rail subtypes under terrain editing, paired/support/site protection,
  source skips and booking, tunnel/terrace backups, shared allocation, mode/RNG/
  callback preservation, stale review and exact reopen. Existing P4 subtype,
  foreign-pier teardown and accounting suites also run in this full result.
- Client **218/218** passes (`tmp/p4-20-client.log`), including a mixed-world
  held-paint regression that advances mixed revisions independently of terrain
  and cancels after an accessory-only edit.
- Python **162 checks, one skip** passes (`tmp/p4-20-python.log`). The source-root
  review verifies all **66 pinned source files**, the **1,484** independent
  fixtures, the **64-entry** accessory ledger and project boundaries pass
  (`tmp/p4-20-source.log`, `-fixtures.log`, `-ledger.log`, `-boundaries.log`).
- Generated contracts/content, EN/JA locales and Svelte checks pass; production
  build passes with the existing large-chunk advisory (`tmp/p4-20-check-final2.log`,
  `tmp/p4-20-build-final.log`).
- Shared terrain/railway/demolition/speed Chromium **55/56** passes
  (`tmp/p4-20-browser-regression.log`); the sole failure was the old P4-10
  assertion that terrain tools must be disabled after slope construction. It now
  verifies the enabled terrain task/title and G shortcut, then returns to Slope.
  Fresh final-build slope/cross-tool Chromium **8/8** passes
  (`tmp/p4-20-browser-final.log`), including the corrected connected-network case.
  Special infrastructure Chromium **31/31** passes
  (`tmp/p4-20-browser-infrastructure.log`): bridge/girder/tunnel/garage/signals/
  poles/stationary accessories plus the two new integration workflows.
- The two new EN/JA browser workflows exercise protected terrain rejection,
  detached legal preview, peer pole edit invalidation without terrain revision,
  safe terrain commit, preserved railway/accounts/time/canvas/camera, Owner
  authoring entry and exact HTTP save/reopen/reload. Japanese uses **430×850**.
  Final captures `tmp/browser-evidence-DWvBwA/cross-tool/en.png` and
  `ja-narrow.png` were visually inspected. Whitespace and report links pass.

The P4-21 persistence/continuation sweep, P4-22 multiplayer/UX sweep, P4-23 capacity
and P4-24 integrated acceptance remain separate open gates. Native/human art,
optional-suite acceptance and P5/P6/P7 real gameplay retain their owning phases.
