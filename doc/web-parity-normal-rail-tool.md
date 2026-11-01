# P4-09 playable normal railway — 31 October 2026

The web app now builds and removes actual normal rail through a compact English/
Japanese **Track** task. Click two XY points or enter coordinates with automatic
start-level detection or a manual construction level, review the authoritative
route/cost/loss preview, then Confirm or Cancel. Endpoint attachment and normal junction branching use the source rules.
New terrain worlds remain terrain-only until the first successful confirmation;
preview, cancellation and rejected commands preserve their exact saved bytes.

## Player workflow and adaptations

Open **Track** from the rail icon. Select **Lay** or **Remove**, then click A and B.
**Auto-detect start level** is enabled by default. The first map click uses the
completed scene’s rail-or-ground picker; typed start coordinates use the actual
ground or higher existing rail in that XY column. The detected level is shown in
the task. Editing the numeric level switches to manual selection; the checkbox
restores automatic detection. After A, the next click selects B; the A/B buttons
explicitly retarget either endpoint. All four coordinate fields and the level are keyboard
accessible. Selecting another tool, opening a panel, replacing/disconnecting the
world or pressing Escape clears the review. A second Escape leaves the task.
Space + drag and middle drag pan without choosing endpoints. The narrow task
scrolls internally with persistent cost/loss summary and Confirm/Cancel; opening
it shifts the map into its exposed area. Preview and confirmation preserve the camera and canvas.

The first point fixes the route's construction level. The second point stays on
that same plane even when its ground elevation differs: normal rail does not
follow changing terrain or invent a slope. In automatic mode, the local height
cut bounds the descending rail-or-ground scan; a cut below ground can deliberately
select an underground start. Manual mode uses the entered level for both map
points, preserving elevated/underground construction. If A is typed and B is
picked on the map, detection happens before picking B so its XY projection uses
the correct height. Retargeting A detects again; B alone never changes the level.

The server still requires an existing rail or `Z <= local ground` at A. Thus
manual elevated construction starts on higher ground or existing elevated rail;
an arbitrary empty elevated A is not legal. Both endpoints and all route cells
must be inside the stored H/V/Z world. Construction level and the local height
cut remain separate. Use View to inspect buried rail, or Reveal preview when its
level is above the cut. Opening View clears the review; preview again after
returning. Detection uses geometry rather than visible sprite alpha and changes
no terrain, IDs, booking, saves or server rules.

Green/orange route lines show the authoritative lay/removal trace, A/B markers
show the actual selected coordinates and crosses mark affected objects below the
cut. The task reports exact decimal money, route-cell and elected-pier counts,
car protection and an expandable list of every reclamation/accessory/pier loss.
Pier losses include **both owner and supported-rail owner IDs**. Removal retains
GEO-004: it tears down every pier below every visited cell regardless of owner,
including empty/no-op/car-protected cells. Special/slope rails can remain while
incurring source charges; normal removal is not their future dedicated tool.
The task states this before confirmation. Cancel never demolishes anything.

## Owner-requested routing heuristic

**Closest legal route** is an owner extension, distinguished from ExAV parity.
The planner compares two shortest same-level eight-direction paths between the
**exact** chosen points: the source diagonal-first sign-vector walk and the
reversed opposite-end walk (straight first in the chosen direction). It prefers
a legal path, then the lower actual source construction booking; equal quotes
retain source order. Each candidate goes through the same full topology,
terrain, water, reclamation, support and capacity checks. No coordinates move,
terrain is not flattened and no right-angle rail patterns are invented.

This bounded heuristic can avoid an obstruction on one bend placement. It does
not search arbitrary longer detours or promise a route through every traversable
world. Both explicit route orders remain available. **Removal never chooses an
alternative automatically**: Closest uses source order, while Straight first
must be selected deliberately. This prevents cost optimization from changing
which existing infrastructure is demolished. The two-point/plane interaction can
be reused for roads; road topology, costs and legality remain P7 work.

`NormalRailwayPlan.ReverseBend` retains the chosen order through exact-source
revalidation. The default P4-06/07 APIs still use the unchanged source walk.
Source behavior/price authority remains ExAV stable `883cf7d`, documented in the
[railway contract](web-parity-railway-contract.md), [literal fixtures](web-parity-railway-fixtures.md)
and [construction handoff](web-parity-railway-construction.md).

## Authority, publication and persistence

`POST /api/railway/preview` accepts the bounded `RailwayQuery`: expected epoch,
geometry revision and content version; From/To, Lay/Remove and route choice.
Observers may read previews. It returns the echoed query, legality/reason, exact
cost, chosen bend, before/after rail patterns, elected piers and explicit losses.
Neither a price nor a candidate graph is accepted from a client.

`editRailway` joins the existing session FIFO. Owner and Operator contexts are
rechecked at the boundary; Observers, disconnected generations and stale outer/
inner epochs, geometry or profile reject before publication. Confirmation
recomputes route selection and legality, applies source booking and validates
mixed save/delta admission before publishing the immutable successor. Geometry,
IDs and journal are atomic. Preview IDs reserve nothing. The existing bounded
participant/request receipts deduplicate same-connection retries; a lost reply
is unresolved, reconnect never replays it, and Track confirmation remains disabled
until the browser is refreshed after reviewing recovered state.

On first success, the exact installed railway profile wraps the existing terrain
with the same clock, pause state, random streams, construction mode, provenance
and allocator. The source construction account starts with **15,000,000,000**;
terrain-only cash was a placeholder zero. One normal operation advances the
mixed/session revisions and books its actual charge. The epoch remains the same.
The publisher sends a complete snapshot for this profile handoff; the client
accepts only the exact supported terrain-to-mixed transition with identical
packs/map/provenance in that epoch. Other profile replacements retain the normal
bootstrap path. Subsequent changes use coherent mixed deltas, including rail
changes without a terrain revision. Assets and the canvas remain shared.

Save8/protocol6 remain current. These are additive query/command contracts; old
save formats stay unsupported. The 8,192 railway-voxel and 8,192 journal-entry
limits remain admission bounds, not measured city capacity. At P4-09, terrain
editing in mixed worlds remained unavailable. [P4-20](web-parity-cross-tool.md)
now supplies that handoff with shared occupancy, allocation and stale-draft fences. Slopes, bridge/girder/tunnel/garage/signal/pole tools,
speed limits, moving trains, stations and roads retain their later phase owners.

## Workflow and translation reconciliation

This slice implements normal-track portions of **MAN-T02 / MAN-W01 / MAN-A03**,
with normal demolition/level behavior in MAN-M03/W02/A14 and **DEV-P24/P28**
booking/stored bounds. [Actions](web-parity-actions.md#p4-09-normal-track-workflow)
and [manual](web-parity-manual.md#p4-09-normal-track-browser-adaptation) link back here.
Speed limits and special/slope infrastructure do not receive credit from these
normal-track workflows.

| Controls/dependency | Modern binding | Source/translation disposition |
| --- | --- | --- |
| Lay/Remove | `rail-action`, `railLay/railRemove` | RAIL-TEXT-003/004/010/011; retain Japanese 敷設/撤去, review historical Place/Remove as Lay/Remove in the Track context |
| Start/end point prompts and XY | `rail-pick-from/to`, `rail-from-x/y`, `rail-to-x/y` | RAIL-TEXT-007/008/009; historical end-point donor and newly translated start prompt adapted to map + keyboard selection |
| Track task, actual cost | `trackTask`, `rail-cost`, shared `cornerCost` | RAIL-TEXT-012/013; Track construction/費用 meaning retained; compact title and formatted source booking replace a native window/road-derived quote |
| Blocking obstacle | `railObstacle` | RAIL-TEXT-014; historical There are obstacles adapted to actionable EN/JA terrain/structure feedback |
| Auto-detect/manual level, route choices, preview/confirm/cancel, help, stale and domain errors, losses and identities | registry `rail-auto-level`, `rail-level/route/preview`, existing `confirm/cancel-draft`; `rail*` resources in both app locales | Authored web adaptations/owner extension; not verbatim source translations. Existing localized shared labels retained |
| Road construction title | RAIL-TEXT-006 | Not shipped as a rail label; its generic-controller inherited quote/title is not the normal-rail authority |

The immutable [78-string source review](parity/railway-localization-review.json)
retains Japanese/source hashes and historical donor provenance. This table records
feature wording review for the normal-controller subset; other P4 families and
P4-22's full translation reconciliation remain open. Help includes routing limits,
fixed-plane/cut behavior, demolition losses and pan gestures in both languages.

## Verification

Verification passed 872/872 full .NET tests, followed by 8/8 focused session
tests including one additional independent lower-cost/tie-break case; 171/171
final client tests and 161 Python tests (one existing skip). The broad Chromium
run passed 129/131: its only failures were two assertions that rail controls were
still disabled. Those expectations were updated to the new playable scope, and
both pass in the final 8/8 focused run alongside all five Track workflows and the
saved mixed-rail pixel/inspection workflow. There are no unresolved failures.
Production build, generated contracts/content/locales, default pinned railway
fixtures/review, project boundaries and whitespace checks pass.

Reproduce the focused work from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests --no-restore -m:1 -p:UseSharedCompilation=false --filter FullyQualifiedName~RailwaySessionTests
cd web
deno task build
deno task test
FREETRAIN_ONLINE_TEST_SERVER_PORT=5290 FREETRAIN_ONLINE_TEST_WEB_PORT=4390 deno run -A npm:@playwright/test@1.64.1 test --project=canonical railway-construction.spec.ts
```

Local logs are `tmp/p4-09-dotnet.log`, `tmp/p4-09-session-tests.log`,
`tmp/p4-09-client-tests-final.log`, `tmp/p4-09-client-build.log`,
`tmp/p4-09-browser.log`, `tmp/p4-09-browser-final.log` and the corresponding
Python/source/boundary logs. The final eight-case browser run additionally
selects the corrected terrain/mixed tests and saved-rail rendering test. Final
screenshots under `tmp/browser-evidence-5Kc0wl/track/` were visually inspected.

The implementation has dedicated session, client and Chromium workflow coverage.
Final commands, counts and evidence are recorded with P4-09 in the local ordered
checklist. The browser cases cover two map clicks, coordinates, cancellation,
attachment, a literal pattern-23 branch, removal, two peers, exact save/reopen,
terrain-profile promotion without bootstrap/camera/canvas/time replacement,
elevated piers and their full loss list, buried cut views, delayed/stale previews,
Observer rejection, participant receipt deduplication, lost acknowledgements and
Japanese narrow DPR2 keyboard operation. The elevated scene is a labeled authored
terrain fixture; it does not claim UI terrain construction or native source play.

Native/human sprite fidelity, physical IME/display/accessibility, other browser
engines, optional-suite acceptance and P4 capacity/full cross-tool gates retain
their recorded open/deferred statuses. Screenshots are automated visual QA, not
native or human acceptance.

### Automatic level follow-up — 31 October 2026

Following the owner's report that level-one track was hidden beneath higher
ground, automatic start-level detection is now the default. Map starts use the
completed scene's reviewed rail-or-ground picker, including existing elevated
rail. Keyboard starts use actual ground or higher rail in the selected column.
Both respect the local height cut. Mixed keyboard/map selection detects A before
projecting B, and manually typing a level turns detection off. Restoring Auto
invalidates the old preview and updates the detected height. B keeps A's level;
this does not implement ramps or terrain-following railway.

Verification passes **172/172 client tests**, production build with **zero Svelte
errors/warnings** and **10/10 Chromium Track/renderer cases**. New browser cases
build and visibly inspect surface rail on ground level four, extend it across
lower ground, then select the resulting elevated rail at its rail height. They
also cover typed A/map B, typed coordinates, manual override/Auto restoration,
underground cuts and Japanese narrow auto controls. Existing renderer and
multiplayer/receipt workflows pass. No backend/save/protocol changes were needed.

Logs: `tmp/p4-09-auto-level-{build,client,browser}.log`. Visually inspected
screenshots: `tmp/browser-evidence-Eqxy3R/track/track-auto-level.png` and
`track-ja-narrow.png`. The saved world and previously constructed rails retain
their original geometry; only new local endpoint selection changes.
