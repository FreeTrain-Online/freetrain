# P3-12 generation UX — 30 October 2026

Creation now offers north-up top-down and source-oriented isometric terrain
overviews, a compass in each projection, realized whole-map metrics, measured-size
presets, New seed, deterministic same-seed regeneration, indeterminate progress,
Cancel and Retry preview. The existing Owner confirmation replaces the world with
the retained candidate's exact bytes. Creating, regenerating, cancelling or viewing
a preview does not pause shared time or consume gameplay random streams.

## Retained geometry and metrics

`Session/TerrainPreview.cs` reads the decoded retained Save7 state during detached
preparation, before the candidate becomes visible. Each sample carries its ground
byte, little-endian packed NE/SE/SW/NW quarter corners and water/forest/reservation/
clear-flat flags. Up to 128 samples on each axis add at most 65,536 binary bytes
(87,384 base64 characters) to the creation response. A common integer sample stride
preserves the H/V footprint's proportions. Existing full ground arrays and candidate
digests remain unchanged; this overview is not stored in the save or live protocol.

At stride one, the overview shows each retained cell's realized corner geometry.
Larger maps use a labeled schematic sample with height scaled to the same stride;
tiny features can disappear in the image. Full-map metrics are never sampled.
The isometric overview uses solid terrain columns and symbolic cover colors;
the live renderer supplies the detailed prepared artwork after confirmation.

Both projections use canonical XY coordinates. North is straight up in top-down
view and up-left in isometric view; East is right and up-right respectively.
Stored H/V edges are not compass directions. The compass, source diamond corner
order and asymmetric-row transforms have literal client checks. This implements
the new generation policy WORLDGEN-D001, not additional legacy parity behavior.

Whole-map metrics report any-surface-water cell percentage, realized forest
treatment cell percentage, quarter-accurate ground height range, clear flat dry
cell count and the largest connected area of those cells at one height. Forest
treatments remain occupied even when their realized scatter has zero trees.
Clear availability excludes other decorations and uses natural ground, zero
corner offsets and canonical XY face connectivity. A reserved starting area stays
clear. Tool legality, costs and ownership are checked by the later editing slices;
the metric is geometric availability. Earlier forest metrics still report the
pre-forest reservation search area and are labeled separately.

The three generation size buttons select 64×64×16, 128×128×32 and 256×256×64,
with water at one quarter of vertical capacity. Custom dimensions retain the
P3-16a limits. New seed uses a cryptographic signed Int32 draw only in local UI;
same-seed regeneration repeats all options, replaces the retained preview and
produces the same candidate digest for unchanged content. Relief presets retain
their existing settings. These choices are not persisted as player preferences.

## Cancellation, invalidation and confirmation

Preview work has a local generation token, abort controller and captured epoch,
revision and content/participant identity. The server retains its existing
connection/job/content fence. Input changes discard the old candidate. Cancellation
immediately releases the form and the shell busy state, aborts the request, clears
confirmation and cancels an already retained candidate by ID. A new preview can
start immediately. Older success/error/finally handlers cannot replace a newer
candidate, feedback or busy state; a delivered obsolete candidate is cancelled by
its own ID. Actual server worker leases remain held until those workers finish.

Revision/epoch/content changes invalidate pending and ready previews. Disconnect
and content incompatibility remove the panel through the existing session/reveal
gate; unmount aborts pending work and releases shell busy state. Returning to a
ready connection starts a fresh form. Expiry has both a timer and a synchronous
commit guard. All preview sources use a 35-second client timeout. Empty creation
now observes cancellation while awaiting its FIFO start/completion, abandons a
late start and passes cancellation into detached overview work, matching the
existing generated/heightmap behavior. Heightmap preparation passes cancellation
into its overview too.

Progress is indeterminate: the HTTP API does not report measured intermediate
stage percentages. Preparing and replacing have distinct localized feedback.
Retry always requests a new preview. An uncertain commit is consumed and never
replayed automatically. Confirmation sends the retained ID and both hashes;
modal Cancel/Escape retains the candidate and restores the launching button's
focus. Preview mode changes only presentation and preserve candidate identity.

EN/JA controls use labels, keyboard-accessible native selects/buttons, a labeled
progress bar, live status text and a readable compass. Preview modes are ordinary pressed-state buttons, operable with Enter/Space. The existing panel scroll,
narrow sheet and modal isolation are reused. The main map canvas is preserved.

## Main-map rotation follow-up

The owner's requested SimCity-style four-orientation main view is feasible as a
separate renderer enhancement. A quarter-turn must rotate canonical XY relative
to the camera while retaining world coordinates and authoritative state. It needs
matching inverse picking, source traversal/occlusion, exposed terrain faces and
quarter corners, direction-dependent rail/train/structure recipes, height cuts,
construction previews, viewport/cache invalidation and camera framing. A CSS
rotation or preview compass change would not supply those behaviors.
Audit recipes for alternate views before promising equivalent artwork from all
directions; missing views need additional art or an explicitly reviewed approximation.

P3-12 keeps the main renderer's existing orientation. The requested enhancement
is tracked as STRETCH-ROTATION in the local parity checklist, separately from
legacy parity and the P3-12 preview projections. It requires asymmetric four-view
fixtures, per-orientation picking/construction checks, EN/JA accessible rotation
controls and full-size interaction evidence before acceptance.

## Verification

Fresh results and file hashes are recorded in
[the generation UX review](parity/generation-ux-review.json). Logs and screenshots
are under `tmp/p3-12/`. Six .NET overview cases include literal canonical-face
connectivity counterexamples, an independent actual-save fine-height/occupancy/XY
flood oracle, a full 1500×1090×45 bounded overview and cancellation. Three client
cases freeze compass vectors, asymmetric staggered quarter geometry and sampled
height scaling. Seven new Chromium scenarios cover both views, seed/presets,
deterministic regeneration, exact confirmation, modal focus/Escape, cancellation
and out-of-order completion, retry, revision/epoch/disconnect/content transitions,
Japanese 390px layout, keyboard selection and expiry. An older Japanese forest
failure fixture now requests 216 cells: the current shoreline shelves provide a
201-cell level patch, so the former 200-cell request correctly succeeds. Existing
terrain, role,
transport and save tests remain applicable.

Fresh verification passes **634 .NET tests, 132 client tests, 142 Python cases
(141 pass, one existing skip), all 97 ordinary Chromium cases, and both optional
startup modes at 4/4 each**. Frontend/probe builds, generated contracts/content/
locales, source review, topology fixtures, project boundaries and whitespace pass.

Two consecutive 1500×1090×45 dense/coastal forest workflows also pass. The added
overview stays within the existing budgets: preview <30s, ready from preview start
<60s, local view switch <100ms, pan paint p95 <33.4ms and sampled server/browser
peak RSS <6GiB each. The image and input metrics below are local Chromium results;
RSS is sampled every 250ms and raw records remain in the review.

| Scene | Preview (s) | Ready from preview start (s) | Preview switch (ms) | Pan paint p95 (ms) | Server/browser peak RSS (GiB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| forest-dense | 15.064 | 38.097 | 73.9 | 8.0 | 4.476 / 3.233 |
| forest-coast | 17.811 | 28.897 | 75.3 | 5.3 | 5.324 / 2.975 |

Exact CLI candidate/save digests, actual water-mask totals (sea plus enclosed
lakes), bounded samples, mode switching, preserved canvas, viewport picking,
reload/download and checkpoint restart pass. These close P3-12 only. Overall P3,
P3-13 onward, final capacity acceptance before P3-17, existing P2/native/platform
and deferred-browser/full-city/import gates remain open. Original Windows runtime
and new Linux distribution acceptance are not inferred from local Chromium or
the prior slice's recorded package evidence.
