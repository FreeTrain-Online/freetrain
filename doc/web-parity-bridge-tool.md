# P4-12 bridge workflow — 31 October 2026

The **Bridge / 鉄橋** task delivers the bundled red steel railway bridge,
`{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}`, through the existing persistent map,
EN/JA construction controls, authoritative preview/confirmation and canonical
mixed-world persistence. Stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`, remains the authority.
This records browser/portable implementation and automated visual QA; original
runtime, native and owner/human fidelity acceptance retain their separate gates.

## Source rules and construction

[BridgeRailwayConstruction](../src/FreeTrain.Online.Simulation/BridgeRailwayConstruction.cs)
extracts `core/world/rail/BridgeRail.cs`, `AbstractLineController.align`,
`Location.align4To` and the shared support callbacks. The
[railway contract](web-parity-railway-contract.md),
[independent literal fixtures](web-parity-railway-fixtures.md) and pinned review
remain the source specification.

The second point takes the first point's Z. The larger absolute XY displacement
selects the cardinal axis, with X winning ties. Coincident snapped points reject
under RAIL-D001. Checked wide differences and the existing 8,192-cell work limit
bound the operation before mutation. The preview echoes the original request for
stale-response comparison and exposes the actual snapped cells; the route summary,
construction ghost and A/B preview markers use those authoritative cells.

Every occupied target must have a rail with both ports along the span. This applies
to endpoints and middle cells equally. Compatible rails retain their complete
subtype, artwork and identity, including a compatible junction; there is no new
straight-subtype-only restriction. Incompatible curves, perpendicular tracks,
nonrail Traffic and terrain occupants reject. Source `canBeBuilt` never checks
for water, local ground height or a usable normal-track start. Dry, elevated and
otherwise empty underground cells follow the actual occupancy rule. The approved
RAIL-D005 car protection fence rejects the whole build, including a compatible
existing rail containing a car. There is no reclamation.

New runs begin with Begin. Subsequent new cells use End at the destination or
just before an existing rail, otherwise Middle. A single new cell remains Begin.
For mode Begin/Middle/End = 0/1/2, the exact source image index is:

| Travel | Pattern | Image index |
| --- | --- | --- |
| North, −Y | 10 | 3 + mode |
| East, +X | 4 | 2 − mode |
| South, +Y | 10 | 5 − mode |
| West, −X | 4 | mode |

Pier election starts enabled and alternates **only after a newly created rail**.
An existing rail resets the artwork run without resetting or advancing that
alternation. The shared source scan checks every voxel below the rail, starting
at Z0, including below ground. An obstruction suppresses the whole pier column
without rejecting the bridge. Otherwise actual ground through railZ−1 receives
body/top cells. No separate pier booking occurs.

The four already prepared bridge-pier frames are bound directly, without copying
media or changing the content catalog: `system:bridge-pier/0..3` reference the
bridge recipe and select source `pier/0/0`, `pier/1/0`, `pier/0/1`, `pier/1/1`.
Their durable sprite values are 2..5, distinct from existing default/slope values
0/1. X-axis bridges use the first row and Y-axis bridges the second; the final
cell below the rail uses the top crop. Server and generated browser profiles
agree. The renderer selects one exact pier frame and bypasses the special-rail
recipe's default index filter for that support frame. The bridge itself selects
both source back/front frames at stages 10/40, around the reserved P5 car pass.
Alpha picking, inspector identities, cuts and completed-presentation readiness
continue through the existing railway renderer.

## Removal, costs and atomic publication

Removal visits both endpoints and only clears matching bridge rails with the
selected direction and no car. A wrong subtype, wrong orientation, empty cell or
occupied bridge is unchanged. Platform dependencies reject rather than orphaning
a future P6 object. Eligible road/pole components remain; crossing accessories
are removed when their rail disappears. Only an actually removed bridge rail
calls support teardown. Every pier below that rail is a loss regardless of owner,
with owner and supported-owner IDs listed in preview under GEO-004/RAIL-D006.
Skipped and unrelated columns keep their piers, and higher foreign rails survive.
Normal-track removal retains its own special-rail no-op/support semantics.

The quote uses the contribution's actual booking rather than the inherited
AbstractLineController road estimate. Both operations charge **every inclusive
cell**, including unchanged rails and removal skips, using each column's actual
ground: `6,000,000 × M(z−ground)` for build and `2,000,000 × M(z−ground)` for
removal, where `M(h) = −2h` below ground and `h+1` otherwise. If the starting
column has `ground <= z <= water` and `ground < water`, the entire booking is
zero. That does not reject a legal build or suppress removal; a zero expenditure
transaction is still recorded. Later water columns do not activate the early-zero
rule. Checked arithmetic, existing cash/top-up/journal rules and railway/account
budgets apply.

The detached quote representation is shared as `RailwayConstructionPlan`, while
normal-track and bridge algorithms remain separate. The additive protocol6
`RailwayQuery.infrastructure` chooses `normal` (default for existing clients) or
`bridge`. The existing `/api/railway/preview` and `editRailway` boundary dispatch
to the selected implementation. The preview cannot authorize a price or supply
candidate IDs/graphs. The FIFO confirmation rechecks role, participant generation,
world epoch, geometry, content, legality, booking and complete transition before
one publication. Terrain-to-mixed promotion remains detached until confirmation,
retaining terrain, clock/random, provenance, mode and the same world epoch.
Transport receipt deduplication books a duplicate once; a stale host replay rejects.
Save8/protocol6 and catalog bytes/pack identities remain unchanged.

## Player workflow and translation

The distinct toolbar button and **B** shortcut open Bridge with Lay/Remove,
map picks or keyboard XY, automatic/manual level, snapped preview, exact cost,
new-pier count, complete losses and Confirm/Cancel. The shared two-point task
hides normal-track bend routing for bridges. Detailed source rules fold beneath
the form; cost and confirmation stay together in the footer. Escape, tool/panel
changes, locale changes, input guards, stale/aborted responses and reconnect use
the existing task lifecycle. Local actions preserve the shared clock, persistent
canvas/camera and decoded assets. Mixed-world terrain editing remains P4-20.

This supplies the bridge portions of MAN-W02/E12, MAN-A03/A04 and DEV-P24/P28
through a browser task rather than a native modal controller. Moving trains,
platform gameplay and the full cross-tool dependency matrix retain P5/P6/P4-20.
The immutable [source text review](parity/railway-localization-review.json)
keeps its hashes; the feature wording dispositions are:

| Source text | EN/JA delivery | Disposition |
| --- | --- | --- |
| RAIL-TEXT-022, 鉄橋 | `bridgeTask`: Bridge / 鉄橋 | Historical Bridge donor retains its meaning |
| RAIL-TEXT-023, 鉄骨の赤鉄橋 | `bridgeDescription`: Red steel railway bridge / 鉄骨の赤鉄橋 | Retranslated against pinned ExAV; historical “Reinforced bridge” omits the source material/color |
| AbstractLineController 敷設 / 撤去 / 費用： | Shared `railLay`, `railRemove`, `cornerCost` | Actual bridge booking replaces the inherited road quote |
| Coordinate, snapped span, compatibility, support, zero/no-op charging, preview and confirmation guidance | `bridgeHelp`, `bridgeRules`, `bridgeRemovalHelp`, `confirmBridge`, shared railway feedback | Authored EN/JA browser controls and explanatory adaptations |

P4-22 retains overall source-text/contribution reconciliation.

## Verification and remaining scope

- The final broad .NET run passes **907/907**. The final focused bridge run passes
  **18/18**, including five subsequently added literal port-compatibility cases.
  These cover all four directions, exact back/front indices and top/body support
  references, all 14 applicable cardinal/coincident bridge fixture rows, single
  new runs, alternation across existing rails, first/later water costs, charged
  car/wrong-subtype skips, bounds, blocked below-ground supports, owner-agnostic
  pier losses, preview byte immutability, promotion, exact save/reopen/remove and
  FIFO/role/generation/content/replay fences. The source-only unsnapped diagonal
  fixture stays invalid at the plugin boundary; the live controller snaps it.
- **183/183 client tests** pass. New coverage validates the infrastructure request
  fence, exact six bridge image pairs and four pier crops in all eight seasonal
  light variants, saved-state validation and survival of support frames through
  recipe selection.
- **161 Python tests, one skip** pass. Generated contracts/content/locales,
  default and explicit pinned-source review/1,484 fixtures, project boundaries
  and whitespace checks pass. The production build has zero Svelte errors or
  warnings; Vite retains its informational bundle-size warning.
- **22/22 Chromium Bridge/Track/Slope/renderer cases pass**, verifying four
  directions, short/long spans, map/keyboard,
  snapped markers, cancellation, repeated charged no-op builds, exact prepared
  bridge/pier pixels, cuts, existing rails/blocked supports, two-peer promotion,
  canvas preservation, EN/JA narrow DPR2, stale drafts, Observer rejection,
  duplicate receipts and exact save/reopen/remove. Adjacent Track/Slope/renderer
  regressions are included in the final run.

An earlier broad .NET run passed 906/907 with an unrelated randomized default
startup `coast-coverage` exception in `SaveRejectionTests`; the unchanged test
passed in the clean 907/907 recheck. The first browser run passed its three EN
cases and reached the JA capture, then failed because its test helper awaited
English connection text after a Japanese reopen. The helper now follows the
active document locale. Neither issue is counted as an acceptance pass from its
failed run.

Logs: `tmp/p4-12-*`. Final browser captures: `tmp/browser-evidence-j6si7W/bridge/`.
Representative EN build/preview, source-pier cut and JA narrow DPR2 captures are
visually inspected. No hosted deployment, native/original-runtime fidelity,
other browser engines, moving-train acceptance, optional-suite acceptance or
measured P4 capacity is claimed. P4-13 owns the next girder workflow.
