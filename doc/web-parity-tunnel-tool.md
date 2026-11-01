# P4-14 tunnel workflow and terrain restoration — 1 November 2026

The **Tunnel / トンネル** task (**U**, after Girder viaduct) delivers bundled
contribution `{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}` through the persistent
map, authoritative preview/confirmation and canonical mixed-world saves.
Stable ExAV 2.1.1.12, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/world/rail/TunnelRail.cs`, is the behavior authority. Its reviewed
source hash is `df1eb9b225a260f95dfa32bc24ada420c33f0e25d29bc28ddc1a9ab4d13afbd7`.

## Construction and removal

[TunnelRailwayConstruction](../src/FreeTrain.Online.Simulation/TunnelRailwayConstruction.cs)
uses the shared source cardinal line snap, retaining the first point's Z. Equal
absolute X/Y differences select X. Coincident snapped points reject under
RAIL-D001; checked coordinates, stored-world bounds and the 8,192-cell route
budget apply before candidate publication.

Construction must be **at or above water level** and include at least one actual
Mountain occupant at the selected level. Bare ground, an underground relation,
a Terrace, an existing tunnel or a compatible railway alone does not satisfy the
Mountain predicate. Each occupied non-Mountain cell must contain rail with both
route ports. Compatible straight/junction/special rails retain their exact
components, artwork, owner and revision. Other occupancy rejects the entire
operation. The approved RAIL-D005 build-car fence applies to the entire span.

Every replaced Mountain becomes a tunnel with its **NE, SE, SW, NW quarter-height
offsets** saved on the rail. Other empty cells become ordinary straight rail,
including cells below or above local ground. Neither operation elects or tears
down piers. Existing ground levels and surface classifications stay unchanged.
Source Mountain replacement deliberately loses its trees and ownership: these
are named in bilingual help, and every replaced Mountain owner is listed in the
preview. Source overlays are not implemented as a separate portable Mountain
payload; independently owned terrace backing survives, including beneath a
replaced Mountain. Unrelated forests, terraces, identities and corners survive.

**GEO-006 / RAIL-D004 is retained:** build visits both endpoints; removal visits
**[start, destination)**. Removal restores only matching, aligned tunnel cells
without a car. It skips other subtypes, ordinary gap rails, wrong directions,
empty cells and occupied tunnels. The preview includes the full route so the
retained destination stays visible. Reverse the route, or extend the removal
route one cell, to remove the retained endpoint. Each restored Mountain receives
fresh identity, exact saved corners, no trees and no ownership. Platforms and
future road/accessory dependencies reject restoration rather than losing another
component owner; P4-20 owns their integrated cross-tool policies. Generic rail
and object demolition continue to protect tunnel backups.

Actual booking is operation-specific: **12,000,000 per newly created cell**
(tunnel or ordinary gap rail); **4,000,000 per actually restored tunnel cell**.
Compatible retained rails, removal skips and the destination cost nothing.
An otherwise legal zero-change removal still records the source zero expenditure.
There is no generic height multiplier, underwater zero-cost exception or separate
forest-value charge. Shared account, cash/rescue, allocator and voxel limits
remain authoritative.

## Terrain boundaries, persistence and rendering

Removing a non-flat natural Mountain leaves a hole in an otherwise continuous
terrain boundary. The terrain validator derives a read-only boundary map from
the rail's existing canonical corner backups. It checks the original natural
continuity using those corners while actual terrain queries and rendering see
the removed Mountain. This preserves exact ground cells without reclassifying
natural terrain as cut. Restoration removes the boundary entry. The map is
reconstructed from validated tunnel definitions before packed save restoration;
no second serialized backup or extra owner is introduced. Overlaps, off-ground
backups, duplicate boundaries, unavailable bindings and mismatched natural corners
fail before exposure. The browser applies the same continuity rule before
accepting snapshots, mixed deltas or complete chunk transfers.

HTTP and WebSocket allow the additive protocol6
`RailwayQuery.infrastructure = "tunnel"`. FIFO confirmation revalidates the exact
predecessor, role/connection, epoch, geometry and content before one geometry /
terrain / allocator / booking publication. Preview and cancellation leave save
bytes unchanged. Promotion preserves clock/random, provenance, construction mode,
world epoch and canvas/camera. Save8, protocol6, catalog bytes and required pack
identities remain unchanged; tunnel backups were already canonical fields.

The existing prepared source frames supply two axis pairs at anchor **(0,16)**:

| Route direction | Pattern | Variant | Background X | Foreground X |
| --- | --- | --- | --- | --- |
| N/S | 10 | 1 | 32 | 96 |
| E/W | 4 | 0 | 0 | 64 |

Each crop is 32×32. Background stage10 precedes the reserved P5 car slot;
foreground stage40 follows it. No additional ordinary rail sprite is drawn for a
tunnel. Source traversal, alpha-mask inspection, geometric route picking,
underground cuts and per-column cache signatures use the existing renderer.
ExAV's S/W neighbor-based car invalidation suppression has no active car-update
consumer yet; moving trains and their invalidation optimization remain P5.

## Browser workflow and translation review

The two-point task supplies Lay/Remove, automatic starting-level detection,
manual level, map/keyboard coordinates, route/cost/loss preview, Confirm and
Cancel. Height cuts expose underground targets. Changing height on an existing
tunnel draft requests a fresh authoritative quote; only a current legal result
can confirm. U, the accessible toolbar icon, Help, Escape, tool/panel changes,
role changes, stale responses and uncertain-outcome guards use the shared task
lifecycle. Shared time is not paused by local tools.

This implements the tunnel portions of MAN-M03/W02/A04/E12 and DEV-P14/P24.
The immutable [source-text inventory](parity/railway-localization-review.json)
retains its hashes; P4-22 owns overall translation reconciliation.

| Source record | Delivered resources | Disposition |
| --- | --- | --- |
| RAIL-TEXT-026, トンネル | `tunnelTask`: Tunnel / トンネル | Historical donor meaning retained |
| RAIL-TEXT-027, 山肌を突き抜けるためのトンネル | `tunnelDescription`: Tunnel through a mountainside / source Japanese | Source-bound English translation; historical donor reworded for the actual workflow |
| 敷設 / 撤去 / 費用： | Shared action and cost resources | Actual newly-created/restored-cell booking |
| Eligibility, corners, deliberate losses, half-open removal, U shortcut and lifecycle guidance | Dedicated `tunnel*`, `confirmTunnel` and shared railway resources | Authored EN/JA browser adaptations |

## Verification

The complete .NET suite passes **976/976**. Tunnel-focused coverage passes
**20/20** (including existing tunnel contract tests). It executes all nine
independent TUNNEL fixture rows, four cardinal orientations and both removal
orders, source patterns/crops/corner backups, actual new/restored-cell charges,
compatible rail/junction preservation, wrong-port rejection, car protection and
charged-cell exclusions, bounds/coincident/water guards, natural surface
continuity, partial restoration, stale plans, preview byte immutability,
save/reopen/checkpoint-host continuation, FIFO/role/disconnect/epoch/content/replay
fences and malformed backup rejection. Forest/ownership loss is distinguished
from preservation of unrelated forests, remote terraces and terrace backing
beneath the replaced Mountain, through canonical save/reopen/restoration.
Car/other future consumer fixtures are trusted boundary fixtures; their real
runtime gameplay remains with P5/P6/P7.

**195/195 client tests** pass. New checks cover tunnel command/preview schema
identity, both exact back/front crop pairs in all eight season/light variants,
before/after dispatch, alpha picking, cuts, immutable snapshots and malformed
natural-boundary backups. **161 Python tests, one existing skip**, pass; default
source review and all **1,484 independent railway fixtures**, generated contracts,
content/locales, project boundaries and whitespace checks pass. Production build
has zero Svelte errors/warnings and the existing Vite bundle-size notice.

The dedicated Chromium workflows pass **3/3**, covering all four directions,
source-sheet pixels, a buried ordinary gap at a height cut, actual alpha inspector
picking, both removal orders, exact download/upload/reopen, cancellation,
below-water and missing-Mountain rejection, keyboard U, cut map picks,
two-peer stale quotes, canvas retention and Japanese narrow DPR2 confirmation.
The test fixture authors canonical Mountain rows; this is not world-generation
parity evidence. Initial browser failures were test expectations: localized water
wording and reselecting an already active tool (which intentionally toggles back
to Inspect). Those test setups are corrected.

Evidence logs are `tmp/p4-14-*`; focused captures are in
`tmp/browser-evidence-7h3SLW/tunnel/`. EN cut art and Japanese narrow confirmation
captures were visually inspected. The final shared Chromium railway regression passes **34/34**, covering
normal rail, Bridge, Girder, Slope, rendering and Tunnel; its captures are in
`tmp/browser-evidence-AY1zsr/`. Original-runtime/human pixel identity,
applicable native/cross-browser checks, real moving-car behavior, integrated
cross-tool matrices and capacity/release gates retain their existing phase owners.

## Owner follow-up: portals on sloped hills

Sloping Mountain terrain is eligible at either portal; terraces are not required.
The route remains horizontal at the starting integer Z, on a cardinal axis and
at or above water. At least one actual surface Mountain must intersect that
level. A route entirely beneath all surface Mountains does not satisfy that
predicate, even though its cells are underground. Automatic height derives from
the first picked column; the second point does not change the route level.

A new focused regression passes **1/1** after the recorded full-suite run above:
seed0 Hills,40×40×16, no starting clearing, entirely natural terrain. It finds
opposing non-flat Mountain portals at the same Z with higher ground between,
builds without terraces, verifies tunnel portals and buried ordinary rails,
saves/reopens, removes in both orders and restores the original corners/ground
cells. Log: `tmp/p4-14-generated-hill.log`. This adds generated sloped-hill
coverage; no placement rule was relaxed. A user's individual rejected draft still
requires its preview reason/coordinates to identify the exact obstruction.

## Owner extension: forgiving portal selection

**OWNER-TUNNEL-PORTALS-001, 1 November 2026:** the owner requested that each end
may be selected on the mountainside or one voxel before/inside it, with the
actual tunnel route calculated before confirmation. This is a browser authoring
extension; the ExAV construction/removal algorithm and source booking stay exact.

[TunnelPortalSelection](../src/FreeTrain.Online.Simulation/TunnelPortalSelection.cs)
searches at most one cell either way along the snapped cardinal axis at each
end. A portal candidate is a non-flat Mountain or an existing tunnel with
non-flat saved corners. Automatic level additionally searches one level below
or above the detected starting level; manual level stays fixed. It first favors
the requested level, then the smallest total endpoint movement, with a stable
longer-span tie break. If no portal pair is nearby, the original exact route is
checked as before. Distant endpoints are not silently moved.

One geometrically resolved route enters the authoritative tunnel preview. There
is no iterative search for a route around an obstacle. Every cell of the final
span still obeys occupancy, car, water, binding, work and account rules. The
preview echoes the requested query while returning actual route cells; markers,
route text, automatic height and the displayed cost use the resolved span.
Confirmation re-resolves from the same fenced predecessor. Removal remains exact
and half-open; no portal adjustment runs during demolition.

The additive protocol6 `RailwayQuery.tunnelRouting` values are `exact` (default),
`portals` (fixed level) and `portals-auto` (automatic level). The browser sends the
latter two only for Tunnel Lay. Both map picks use visible column disambiguation
in automatic mode, so the second click on a higher hillside is not shifted by
intersecting the old construction plane. Query/late-response keys include the
routing mode. Unsupported modes and non-exact modes on other tools/removal reject.
Save8, protocol6, catalog bytes and serialized world state fields are unchanged.
English/Japanese help explains the tolerance and the resolved path review.

The screenshot's exact `(498,490,6) → (511,490,6)` route was checked against a
read-only captured copy of the local 400×600×24 world: current code accepts it.
The running server returned **HTTP400 / invalidInput** before checking terrain,
consistent with an older process not yet understanding Tunnel requests. No live
world replacement or construction was performed. A failed request now has distinct
EN/JA feedback explaining that an updated game may require a server restart and
page refresh, instead of the generic target-check failure. A local save copy and
reproduction logs remain ignored under `tmp/p4-14-portal-*`; no personal world
fixture is checked in. Tests instead use a small, literal natural ridge with the
same two portal corner patterns (`4400` / `0044`) and twelve buried cells.

Final follow-up checks: **988/988 .NET** and **196/196 client** pass, with a clean
production build (zero Svelte errors/warnings; existing Vite size notice),
generated contracts/content/locales, source review/1,484 fixtures, boundaries and
whitespace. The domain/session cases cover all nine combinations of outside /
on-slope / inside endpoint picks in four headings, fixed versus automatic levels,
obstruction protection, distant/bounds guards, source-exact fallback, actual cost,
query echo, stale confirmation and exact save/reopen/restoration. The final shared
Chromium railway regression passes **36/36**, including **5/5** Tunnel workflows.
It verifies outside/on-slope/inside picks, actual map clicks into the higher hill,
automatic adjustment to level6, repeated preview without cumulative height drift,
actual portal markers/route/cost, cancellation, exact reopen/restoration and
HTTP400 guidance/retry without mutation. Log:
`tmp/p4-14-portals-browser-regression.log`; captures:
`tmp/browser-evidence-lfrwYc/tunnel/`. The resolved natural-portal preview capture
was visually inspected. These replace the earlier narrower follow-up run, whose
single failure compared freshly restored entity order instead of positional
geometry; that assertion now compares positions and corners.

## Owner follow-up: commit-induced reconnect in development builds

The owner reported successful placement followed by several seconds on the
connection screen. Measurements on an isolated copy of the current 400×600×24
mixed world identified a Debug-build commit exceeding the remote client's existing
six-second receipt deadline. Successful Release runs had hidden this difference.
The client retries the same request at two-second intervals, then reconnects when
its receipt budget expires; the source command may already have committed.

The original Debug timings were Preview3016ms, Apply4182ms, Successor1419ms and
transition admission274ms: **8,891ms** total. Full terrain validation was repeated
at each graph handoff, and Apply recalculated a plan already derived from the
exact immutable predecessor. This fix preserves the existing receipt timeout.

- A completed `TerrainWorld.Replace` now records its validated predecessor token
  and exact changed-cell/entity IDs after its full graph and identity checks pass.
  `MixedWorld.Replace` reuses that certificate only for the exact predecessor;
  uncertified imported graphs still receive full transition validation. Terrain
  wire publication retains unchanged records and reports exact removals/changes.
- A legal tunnel quote carries an internal handler marker and immutable candidate.
  Apply checks the exact source object, tunnel marker, legality and account limit,
  then publishes that candidate and books against the current clock/accounts.
  Another tool's quote cannot be substituted. The session still creates a fresh
  authoritative plan on confirmation; stale-world and role/epoch/content fences
  remain unchanged.

The same Debug copy now measures Preview1542ms, Apply54ms, Successor55ms and
admission105ms: **1,756ms** total. Logs:
`tmp/p4-14-reconnect-debug-timing.log` and
`tmp/p4-14-reconnect-debug-timing-fixed.log`. The temporary timing harness that
read the private world copy was removed; no personal-world fixture is checked in.

The permanent browser regression authors an independent natural 400×600×24 ridge
and performs the first tunnel placement, a second placement in the mixed world,
and both source removal orders. Against an actual **Debug** server it passes
**1/1**: placements1834/1828ms, removals3239/3241ms, eight promotion chunks,
**zero socket closes, bootstrap reconnects, resyncs or gated-screen transitions**,
one unchanged canvas/camera and exactly four account transactions. These are local
end-to-end measurements, not P4-23 latency/capacity acceptance. The test server
configuration can be selected with
`FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug` (default Release).
Log: `tmp/p4-14-reconnect-debug-browser.log`; inspected capture:
`tmp/browser-evidence-SJLTYz/tunnel/tunnel-large-world-same-connection.png`.

The final portable suite passes **991/991 .NET** and **196/196 client**, plus a
clean production build, generated/content/locales, source review/1,484 fixtures,
boundaries and whitespace. New domain checks reject another tool's prepared
quote, verify precise terrain deltas/reference retention and refuse an uncertified
terrain graph with an invalid predecessor revision. The final shared Release
browser regression passes **37/37**, including the large-world case. Log:
`tmp/p4-14-reconnect-browser-regression.log`; captures:
`tmp/browser-evidence-AHm2Ai/`. Restart the development server
to load the changed backend assemblies; no save/protocol/catalog migration is
introduced. Native, moving-train and later capacity/release gates remain open.
