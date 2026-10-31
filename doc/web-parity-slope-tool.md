# P4-11 slope browser workflow — 31 October 2026

The distinct **Slope / 勾配線路** task wraps the source-derived
[P4-10 domain](web-parity-railway-slopes.md) with live EN/JA construction,
removal, source artwork, picking, shared-session commands and exact continuation.
Authority remains stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
This is browser/portable implementation and automated visual QA. Original-runtime,
native and owner/human sprite-fidelity acceptance retain their existing gates.

## Task and interaction

[SlopeControls](../web/src/SlopeControls.svelte) exposes Lay/Remove, cardinal
uphill direction, XY coordinates and the base/selected level. A map pick normally
uses the actual visible rail/ground level; keyboard XY uses the existing
cut-aware ground/rail detector. Typing a level disables Auto. A ramp rises one
voxel over four ordered horizontal positions, occupying both levels in every
column. The default uphill direction is East (+X); the direction labels explicitly
state the canonical coordinate change. There is no terrain-following pathfinder.

Preview shows the resolved lower base and elevation, all eight member positions
and their rail/filler/support/wall type, exact source booking, new pier count and
every removed pier with its owner and supported-owner identity. The canvas ghost
outlines **all eight cells**, including invisible occupancy: solid rail outlines,
dashed paired cells, dotted new piers and crossed pier losses. Rejected build
previews retain the intended footprint when its coordinates fit Int32. They never
allocate IDs, alter terrain, promote a world or book expenditure.

The existing live renderer dispatches the exact saved per-column surface or
underground patterns and support/wall art, retains stored ground-hole flags and
omits invisible filler pixels. The source upper two rail positions also use
invisible crops: the lower two sprites draw the incline across them. Pixel checks
expect two visible rail crops and two invisible ones, rather than inventing art
for the upper rail positions. The S/W elevated pier uses P4-10's exact second
core crop. Height cuts clip every ghost/member/support at its actual level.
The reveal action raises the cut to the highest preview member. Preview outlines
are geometric overlays; constructed railway uses the prepared source recipes.

Removal accepts any of the eight canonical member positions, including invisible
fillers and lower supports. The Slope map picker includes that occupancy without
requiring opaque pixels; a cut is needed to reach buried members. A map removal
pick detects the owning climb. Keyboard selection permits any exact member but
requires the selected direction to match its owner; mismatches have explicit
EN/JA feedback. Confirmation removes the entire ramp once, preserving eligible
road/pole components and showing all lower-pier losses regardless of owner under
RAIL-D006. The ordinary Track remover retains its source slope charged-no-op
behavior. Whole-entity demolition is not exposed by this task.

The Slope button uses a side-profile railway ramp icon, with an inclined deck
and supports. **S** selects it while the map is focused; repeats keep the same
task selected. Modifier shortcuts, text fields, composition and edit permissions
retain the existing input guards. All map-tool tooltips advertise their shortcuts: Inspect (I), Track (T), Slope
(S), Terrain (G) and Remove (R). One shared tool descriptor supplies the key
handler, tooltip and accessible shortcut metadata. G opens terrain controls
only when they are available; disabled-world/role and input-focus guards still
apply. EN/JA Help lists the same keys.

The terrain toolbar entry stays visible. It is enabled in terrain worlds and
greyed out with a localized availability description in worlds where editing is
unavailable, including mixed railway worlds. The Game menu follows the same
availability rule. The owner explicitly chose this presentation on 31 October;
this does not bring mixed-world terrain editing forward from P4-20.

The persistent canvas, camera, decoded assets and shared clock survive task
changes and commits. Opening a narrow task applies the existing map-framing
adaptation. Cost and Confirm/Cancel remain together in the task footer; detailed
footprint/help lists fold independently. Escape clears a draft before returning
to Inspect; panel/tool/world/role/disconnect resets cancel requests. Local UI
operations do not pause shared time.

## Protocol and authoritative command boundary

The additive protocol6 `SlopeQuery` / `SlopePreview` contracts use
`POST /api/railway/slope/preview` and the `editSlope` command. Save8/protocol6 and
the complete mixed snapshot/delta/chunk formats remain unchanged. No preview
price, owner ID or candidate graph is accepted as a command input.

[SlopeSession](../src/FreeTrain.Online.Session/SlopeSession.cs) shares Track's
detached terrain-to-mixed promotion and expected epoch/geometry/content fence.
It calls `SlopeRailwayConstruction` on the FIFO boundary and rechecks legality,
capacity, booking and the complete mixed transition before publishing once.
An Observer can preview but cannot commit. Existing participant generations,
role checks, receipt deduplication and uncertain-outcome recovery apply. Illegal,
stale or disconnected commands preserve the predecessor and accounts.

Preview member ordering is canonical by XYZ, so save/reopen yields the same
response even if serialized voxel order differs from construction order. The
client verifies the schema, exact money representation, eight unique legal
members and four rail positions. It compares every echoed query field and fences
late responses by connection generation, abort, epoch, geometry and content.
A changed draft cannot accept a delayed preview or clear a newer draft after a
late receipt. A lost acknowledgement leaves the outcome unresolved without
replaying construction.

## Source workflow and translation reconciliation

This implements the railway portions of **MAN-T03 / MAN-A04**, including the
ramp-to-flat/elevated connection journey; **MAN-W02/E12 and DEV-P26/P28** retain
source costs, demolition, slope sprites and stored bounds. P5 still owns all
moving-train checks. Cross-tool terrain editing/protection remains P4-20; shape
terrain before railway construction.

| Source/dependency | Delivered EN/JA binding | Wording disposition |
| --- | --- | --- |
| RAIL-TEXT-015/016, 敷設/撤去 | Shared `railAction`, `railLay`, `railRemove` in the Slope task | Lay/Remove retains source meaning; historical Place is reviewed in railway context |
| RAIL-TEXT-017, 費用： | `slope-cost`, shared `cornerCost` and exact formatted booking | Actual slope price replaces native cost widget |
| RAIL-TEXT-018, 勾配線路 | `slopeTask`: Slope / 勾配線路 | Compact English task title adapts historical Slope tracks |
| RAIL-TEXT-019/020, 設置できません/撤去できません | Authoritative localized obstacle/bounds/binding/car/platform/overflow/capacity and missing-slope/direction errors | Native error boxes become actionable task status; source protections retained |
| Direction arrows, placement/removal target, level, preview/confirmation, ghost, support/loss details and cut help | `slope-direction/x/y/level/preview/confirm`, shared cancellation/Auto and `slope*` EN/JA resources | Authored browser adaptations, not additional historical source strings |

The immutable [78-string source review](parity/railway-localization-review.json)
retains its source/donor hashes. This table records feature wording review for
RAIL-TEXT-015–020; P4-22 retains overall translation reconciliation.

## P5 movement-transition handoff

For cardinal climb `d` and ordered position `i=0..3`, rail position is
`base + i*d + (0,0,floor(i/2))`. Patterns are
`28 + 2*d.index + i` (surface) or `44 + 2*d.index + i` (underground), selected
using **baseZ** against local ground for each column. Levels 0 and 3 have zero
Z change; level1 changes +1 only when travelling along climb; level2 changes −1
only when travelling opposite climb. Other headings have zero Z change and must
separately satisfy the rail's ports. `SlopeRailwayConstruction.Pattern/ZDiff`
and the four authoritative preview rail positions expose these reviewed values.
Paired cells are real occupied members and never car positions. Existing P4-10
independent vectors cover every slope pattern and travel heading. These authoring,
connection and persistence checks do not establish train movement or timing.

## Verification

The focused browser suite is
[slope-construction.spec.ts](../web/tests/browser/slope-construction.spec.ts),
with [session tests](../tests/FreeTrain.Online.Tests/SlopeSessionTests.cs) and
[client regressions](../web/tests/railway-edit.test.ts). The session tests cover
all four climbs, eight removal members, exact byte continuation, flat/slope/
elevated attachment, source transition vectors, FIFO collisions, role/generation
checks and rejected requests. Browser tests cover map and keyboard preview,
cancel/confirm, two peers, receipt/recovery, EN/JA narrow DPR2, all cardinal
surface/underground ramps, mixed-column art, elevated piers, stored flags and
cuts. Prepared-source opaque pixels are independently projected onto the actual
canvas, alongside screenshot inspection.

Reproduce from the repository root:

```sh
.tools/dotnet/dotnet test FreeTrain.Online.slnx --no-restore -m:1 -p:UseSharedCompilation=false
.tools/dotnet/dotnet build src/FreeTrain.Online.Server -c Release --no-restore -m:1 -p:UseSharedCompilation=false
deno task --cwd web build
deno task --cwd web test
python3 -m unittest discover -s scripts/tests
python3 scripts/railway-fixtures.py --check
python3 scripts/review-railway.py --check
python3 scripts/check-project-boundaries.py
cd web
FREETRAIN_ONLINE_TEST_SERVER_PORT=6180 FREETRAIN_ONLINE_TEST_WEB_PORT=6273 deno run -A npm:@playwright/test@1.64.1 test --project canonical slope-construction.spec.ts
```

Fresh verification passes **894/894 full .NET tests**, including six new slope
session cases, **181/181 client tests**, and **161 Python tests with one existing
skip**. Release and production builds pass; Svelte reports zero errors/warnings.
Generated contracts/content/locales, all 1,484 pinned independent railway fixtures,
source review, project boundaries and whitespace checks pass.

Chromium's broad run passes **140/141**. Its single failure expected the canvas
to become loading during a deferred night-sheet decode, despite the existing
policy of retaining a ready, usable scene. That assertion now checks retained
readiness and pending presentation separately. The focused rerun also exposed an
existing rail pixel-test race: a height-cut request was tagged before its image
completed, so it could capture the preceding cut as its light/season oracle.
`data-presentation` now distinguishes requested-presentation completion from
retained-buffer readiness, including progressive viewport rendering. Both pixel
tests wait for completion; their exact source-pixel, restored-image and picking
assertions remain. No scene replacement or picking policy changed.
The **final 10/10 Chromium cases pass**: all six slope workflows, three existing
railway-rendering cases and the deferred terrain-sheet case. This is not a claim
of a second complete 141-case run. The broad run includes all **6,504** bundled
recipe season/light combinations.

Representative final screenshots under `tmp/browser-evidence-Ts2WyM/slope/` were visually
inspected, including English connected rail, Japanese narrow DPR2, cardinal
surface/underground ramps, cut paired geometry, mixed-column holes and elevated
pier losses. These are automated browser captures inspected by the coding agent,
not native or owner/human fidelity acceptance. The source upper rail crops being
invisible was verified against the prepared recipes, rather than filled with
invented track artwork.

Logs: `tmp/p4-11-dotnet.log`, `tmp/p4-11-client-final.log`,
`tmp/p4-11-web-build-final.log`, `tmp/p4-11-release.log`,
`tmp/p4-11-python.log`, `tmp/p4-11-source.log`,
`tmp/p4-11-boundaries.log`, `tmp/p4-11-browser-broad.log` and
`tmp/p4-11-browser-final-verified.log`. Local .NET/browser services require
loopback socket access. Final results and local evidence paths are also recorded
in the ordered P4-11 checklist entry. Special infrastructure, speed limits, full cross-tool teardown,
P4 capacity/release, moving trains, native/human, other-engine and optional-suite
acceptance retain their later or deferred gates.


### Owner toolbar follow-up — 31 October 2026

The restored terrain entry, S shortcut and ramp-profile icon pass **181/181
client tests**, production build with **zero Svelte errors/warnings**, and **3/3
focused Chromium cases**: English map/keyboard slope construction (including
terrain enabled-before/disabled-after promotion and repeated S), Japanese narrow
DPR2 operation using S, and the existing prototype pan/zoom/shortcut regression.
EN/JA toolbar captures under `tmp/browser-evidence-MiSVEq/slope/` were inspected.
Logs: `tmp/p4-11-toolbar-build.log`, `tmp/p4-11-toolbar-client.log` and
`tmp/p4-11-toolbar-browser.log`. Domain, save/protocol and P4-20 scope are unchanged.


The subsequent shortcut-label follow-up applies the same title format to every
map tool, adds G for terrain and retains the localized unavailable explanation
on the disabled terrain button. Updated browser checks verify all five tooltip/
shortcut pairs, G before construction and blocked G afterward. Production build,
**181/181 client tests** and **3/3 focused Chromium checks** pass. Logs:
`tmp/p4-11-shortcut-{build,client,browser}.log`; browser evidence:
`tmp/browser-evidence-ZivNnl/slope/`.
