# P7-10 variable-height city buildings — 15 November 2026

The existing city catalog and reviewed construction/removal workflow now support
all 55 bundled variable-height definitions, including their authored variants.
P7-07–09 fixed construction, rendering and playable-producer behavior remains
covered by focused regressions. This delivers bundled browser/portable scope;
P7-11 and later catalog, provider-refresh, growth and phase acceptance gates remain.

## Source and domain

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`. The source contract and independent
P7-02 definition, variable-recipe and provider fixtures remain unchanged.
`VarHeightBuildingContribution`, `VarHeightBuildingController`,
`VarHeightBuilding`, `MultiSelectorController` and `ConstructionSite` supply the
height bounds, vacancy, price, stack, construction, ownership and provider rules.

The EN/JA height input displays four floors per voxel, steps by four and uses the
definition's exact minimum/maximum. Quotes carry an explicit integer voxel height;
changing it clears the reviewed draft and rebuilds footprint, price and actual
stack preview. Catalog cards quote the minimum-height total price. Authored
orientation/variation identities and author/plugin attribution remain exact;
55 newly reachable strings have source-bound reviewed English translations.

The existing `FixedCityQuery`/`editFixedCity` transport names are retained for API
continuity. Their optional `height` field is mandatory for variable placement,
absent for existing fixed requests and forbidden for removal. The server checks
bounds, complete selected volume, implicit underground/outside cells, costs,
epoch, affected sites, permissions and picked owner revision before publishing.
Catalog placement spends unit price times height; legacy variable placement uses
its own empty-volume predicate, permits elevated vacant sites and adds no upfront
charge. Fixed legacy/com rules remain distinct. Automatic mixed-family admission
and growth remain P7-25–29.

Actual construction callbacks allocate a fresh completed owner, initialize the
subsidiary before attaching the variable listener, multiply the original population
expression by the selected height, and bind footprint area and height separately.
This preserves child truncation before multiplication and the source cap
`land * area * max(1, height / 4) + 10`. Completed entity value is unit price times
height. Whole-owner demolition from any member spends `500000 * area`, detaches
both listener directions and cancels callbacks atomically. Construction sites
advertise no provider. Removing/rebuilding refreshes the actual producer and its
height; comprehensive changing-land-value provider refresh remains P7-13.
Save11, protocol10, City1, prepared content identities and capacity limits remain.

## Rendering and source crop accommodation

The actual terrain scene traverses every selected voxel. It uses source top-first
precedence with descending top indexes, then bottom/middle selection, preserves
overlay order, and renders the existing generic construction stages. Variable
height cuts draw source gray `#808080` base silhouettes, including members whose
art is invisible. Alpha picks resolve to the stable owner; full volume occupancy
continues to obscure terrain/water and invalidate affected renderer columns.
The preview uses the same selected height and portable artwork as completion.

Two Shinjuku Mitsui variants contain the 14 historical malformed crops documented
in [ASSET-CROP-01–14](web-parity-recipes.md#crop-corrections-to-complete-during-post-assets).
The existing owner decision authorizes bounded accommodation. This implementation
keeps each intersection with the pinned 353×352 source sheet at the same world
pixel position. An omitted left margin translates the anchor by exactly that
margin. Empty intersections and negative-height requests become invisible art;
voxel ownership is retained. No negative-size or out-of-sheet request executes.
This is a portable runtime accommodation; canonical asset conversion remains
POST-ASSETS-07, alongside the separate farmland/stair dispositions.

| Requests | Executable accommodation |
| --- | --- |
| 01, 04 | Entirely outside the left edge: invisible. |
| 02 | `(0,0,16,32)`, anchor `(-16,16)`. |
| 03 | `(0,0,16,48)`, anchor `(-16,32)`. |
| 05 | `(0,88,16,16)`, anchor `(-16,0)`. |
| 06, 11 | Historical height −8: invisible. |
| 07 | `(0,88,16,32)`, anchor `(-16,16)`. |
| 08 | `(336,0,17,32)`, anchor `(0,16)`. |
| 09 | `(336,0,17,48)`, anchor `(0,32)`. |
| 10 | `(352,0,1,40)`, anchor `(0,24)`. |
| 12 | `(336,88,17,16)`, anchor `(0,0)`. |
| 13 | `(336,88,17,32)`, anchor `(0,16)`. |
| 14 | `(352,88,1,24)`, anchor `(0,8)`. |

`scripts/variable-city-art.py` deterministically derives the valid masks and records
original rectangles/anchors, source revision and all eight prepared surface hashes.
Generated-contract checks verify this artifact. Runtime adaptation returns a cached
separate catalog, rejects changed provenance and preserves the original catalog's
14 diagnostics. Missing stack frames still block readiness. Independent browser
oracles read pinned XML and sheet pixels rather than the production stack selector
or accommodation masks.

## Focused verification

Exact commands, final counts, log hashes and browser reports are recorded in
`tmp/p7-10-validation.json`: **54 selected C#, 49 Deno and 14 unique Chromium
workflows pass** across the recorded focused runs, with zero skips/retries in
the successful cases. The final three-case P7-10 run is clean. Its independent
gallery verifies **112,640,000 pixels and alpha picks** across 880 assemblies.
No complete C#, Deno or browser suite was run.

- C#: fixed construction/session/kernel/state regressions plus all 159 independent
  variable definition-height cases, selected-volume legality, upper obstacles,
  natural completion at heights 2/5/8, real demand/area/Z inputs, callback cleanup,
  authority/cost rejection and byte-exact saved continuation.
- Deno: city edit/state/rendering, content recipes, localization, map and removal;
  all 18 independent variable-layer fixtures, all 55 definitions at min/max in
  eight appearances, portable crop provenance and missing-art rejection.
- Chromium: EN keyboard and JA narrow touch min/max height review, actual natural
  completion beside a gameplay-built station, scaled provider/demand, gray cut
  picking, removal/rebuild, Operator publication and exact site/completed reopen;
  all 55 definitions at minimum/maximum heights in eight seasonal/light variants
  compared against independent XML crop pixels and alpha picks. Related fixed
  producer, author/catalog, Observer, draft/locale and rendering workflows pass.

Generated/content/locale checks, Svelte diagnostics, production build, immutable
source-fixture/review checks, project boundaries and whitespace are also checked.
Native/original-runtime human fidelity, optional packs, capacity/performance,
canonical asset conversion, P7-11+ families and P7 exit retain their existing gates.
