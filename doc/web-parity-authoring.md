# P3-16 — map authoring mode

30 October 2026. **Game → Modify terrain** provides Owner-controlled entry/exit
with explicit confirmation and a saved world mode. While enabled, **Terrain
selection → Author forests and creeks** offers all six bundled species and all
three ground treatments, plus deterministic decorative creeks. Existing corner,
rectangle, repair, terrace and cut tools remain available outside the mode.
Overall P3, final P3-16a capacity acceptance and P3-17 remain open.

## Source contract and workflow policy

Authority is the pinned ExAV revision
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`: `Core` construction flag,
`ForestBuilder.create/createRandomTrees`, `CreekBuilder.computeRoute/apply`,
`World.isReusable` and `LandBuilderContribution.create`. The
[terrain source contract](web-parity-terrain-contract.md#forests-creeks-booking-and-ownership)
records P3-TERRAIN-006/008/009 and literal forest/creek expectations. No original
Windows runtime or native GDI comparison is claimed.

The old construction flag has no placement, clock or economy consumer. The new
entry/exit workflow is explicit browser policy: Owner changes a shared persisted
boolean; Owner and Operator can initialize scenery while it is enabled;
Observer can inspect and request compatible read-only previews. It neither
relaxes occupancy/bounds/resource checks nor pauses shared time. The mode has no
money, role or legality exemption.

Scenery tools explicitly use the source's **unowned initialization** branch.
Resulting entities are unowned and booking is zero; a forest's entity value still
stores its contribution price. This is a selected initialization operation,
not a financial benefit inferred from the mode flag. Owned forest placement's
inclusive-rectangle SUBSIDIARIES booking remains part of the later economy
integration; these tools do not advertise or implement that operation. Generic
scenery demolition and land acquisition retain their later subsystem scope.

* Forest placement normalizes inclusive X/Y bounds, iterates x-major/y-minor,
  and accepts at most 64 selected cells at one Z. Ground equality and either
  reusable occupancy or Mountain are required. As in source, underwater ground
  is eligible. Mountain geometry and stable ID are preserved; decoration/value
  and ownership are replaced. Reclaimed creeks receive fresh IDs. All 18 prepared
  species/ground declarations are selectable through localized controls.
* Source binomial scatter, X/Y mapping and position-only Y sorting use a **copy
  of the saved forest stream** for preview. Preview, cancellation, rendering and
  rejected commits consume no authoritative draws. Commit reruns the fenced
  selection and publishes the resulting stream with the world. A zero-tree cell
  remains unchanged, including existing decoration and ownership. An all-zero
  eligible selection can be confirmed and advances only its source draws.
* Creek placement preserves endpoint order, seeds the frozen P1 generator with
  unchecked Int32 `((x1*128+y1)^(x2*128+y2))`, and walks cardinally at fixed Z.
  Diagonal intent chooses left/right with source draws. Up to 64 inclusive route
  cells are admitted. Ground mismatches and non-reusable obstacles are skipped
  without rerouting. Existing reusable creek bits are ORed; endpoints and isolated
  zero-bit tiles keep their source meanings. Every placed creek gets a fresh ID.
  This decoration changes neither ground nor the global water plane.

The preview shows exact changed/skipped cells, before/after corners,
forest/ownership loss, total realized tree count and creek connections as named
compass directions. Map diamonds highlight affected cells and skipped route
cells separately. Trees use existing realized forest recipes after confirmation;
no random preview reroll or new artwork is introduced. EN/JA help explains
limits, zero-tree outcomes, unowned initialization and decorative water semantics.

## Authority, drafts, timing and durable state

`setAuthoringMode` is an additive protocol5 command with expected world epoch
and geometry revision. Authority and current connection are checked at the FIFO
boundary. Entry/exit increments geometry revision even though the shape is
unchanged, invalidating every participant's captured terrain preview and pending
confirmation. An idempotent request is harmless. The UI captures its review
fence, clears targeting during review, and rejects stale reviews before sending.
Clock changes alone do not invalidate a geometry review.

`plantForest` and `placeCreek` extend the existing authoritative terrain query
and confirmation path. Optional prepared forest identity and exact realized
preview decorations are strictly schema-bound; ordinary terrain operations
reject mixed forest input. Source route order is preserved by the client rather
than applying rectangular normalization to its expected preview position.

Each commit builds detached state, validates full geometry/allocator/resource
admission and Save7 serialization, then publishes world and copied random state
atomically. Mode changes retain clock, pause, RNG, allocator, entities and geometry.
Save7/protocol5 already represent mode, realized trees and creek connections;
no durable format or content identity changes are required. Two-client mode
deltas and actual checkpoint restart preserve the authored state exactly.

The existing canvas, camera, image cache, primary-panel lifecycle and sticky tool
Confirm/Cancel are reused. Escape, panel/tool switches, connection/role loss,
epoch changes and geometry changes clear or invalidate reviews, including
in-flight responses and two-point anchors. Scope switches remount the appropriate
controls so a rectangle action cannot become an authoring action accidentally.
Existing remote-client receipts and uncertain-outcome handling prevent replay.

## Verification

Fresh logs, tested file hashes, capacity measurements and screenshot provenance
are indexed in [the authoring review](parity/authoring-terrain-review.json).
**715 .NET cases, 142 client cases and 142 Python cases (one existing skip)** pass.
Full Chromium passes **113/113**, with a final **4/4** authoring run after the last
UI corrections; optional source and prepared configurations pass **4/4** each.
Release solution/probe and production web builds pass; Svelte reports zero errors
and warnings. Generated contracts, content, EN/JA locales, 40 pinned source files,
77 literal fixtures, 59 text bindings, project boundaries and whitespace checks
pass. Desktop and Japanese 390px DPR2 screenshots were visually inspected.

Reproduction uses the repository's component commands:

```sh
dotnet build FreeTrain.Online.slnx -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet test FreeTrain.Online.slnx -c Release --no-build --no-restore --disable-build-servers -m:1
deno task --cwd web test
deno task --cwd web build
python3 -m unittest discover -s scripts/tests
python3 scripts/review-terrain.py --source-root /path/to/pinned/archive --check
python3 scripts/check-project-boundaries.py
# In web/: choose unused test server/web ports if local development is running.
deno run -A npm:@playwright/test@1.64.1 test
deno run -A npm:@playwright/test@1.64.1 test --config playwright.optional.config.ts
# Repeat optional tests with FREETRAIN_ONLINE_OPTIONAL_PREPARED=1.
dotnet run --project tools/TerrainTransportProbe -c Release --no-build --no-restore -- --authoring-capacity 1500 1090 45 tmp/p3-16-capacity
```

Tests cover default/off/on permissions, unchanged shared time and ordinary tools,
FIFO pending edits versus mode changes, stale epochs/revisions, disconnected
owners, exact read-only scatter, zero-tree outcomes, all 18 treatments,
underwater eligibility, literal straight and independently derived seeded diagonal
creek routes, joins/skips, invalid mixed/oversized selections, stream isolation,
exact save/reopen continuation, two network peers and checkpoint restart.
Browser journeys cover Owner confirmation/cancel, exact forest results, creek
creation, mode persistence and exit, peer and delayed-preview invalidation,
Operator/Observer behavior, Escape/panels and Japanese narrow keyboard workflows.

The full-size sequential probe enters mode, selects 64 forest cells (63 receive
trees for the saved source stream), places a separate 64-cell creek and exits.
All four realized Save7 files reopen byte-exactly. Forest/creek preview takes
6.4/1.6 ms; commits take 787–1,106 ms including mode changes. This is a functional
continuation result, **not** a pass of the provisional 250 ms authoritative
feedback target. Dense edited-world publication and final editing latency remain
in P3-16a acceptance before P3-17. Native/deferred-browser, P2, full-city and
historical-import gates retain their recorded status.

The environment is local macOS Arm64 with .NET10.0.402 and Deno2.9.8. The
repository pins Deno2.9.7; component commands were run rather than claiming the
exact-version aggregate script passed. Existing native/deferred-platform gates
remain unchanged.
