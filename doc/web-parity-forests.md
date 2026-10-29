# P3-11 forests and cover — 29 October 2026

Game → New world → Generated relief now offers forest cover presets, custom
coverage, clustering, any mixture of the six bundled tree species, three source
ground treatments, and a connected flat starting-area reservation. Controls,
help, diagnostics and realized preview metrics are available in English and Japanese.

This is WORLDGEN-FOREST-1 under the reserved `terrain-generation-1` profile.
Distribution and starting-area selection are requested additions; scatter and
appearance use ExAV's reviewed source semantics. Save7, protocol5,
terrain-content-1 and the gameplay random-bank version remain unchanged.

## Content and source scatter

The authority remains ExAV2.1.1.12, archive883cf7d/SVNr115. The pinned forest
manifest and `ForestBuilder.createRandomTrees` are already bound by
[the terrain source contract](web-parity-terrain-contract.md). Content resolves
exact bundled pack/version/catalog-hash/contribution identities; the browser gets
selectable bindings from `/api/worlds/generated/forests`. Only these18 reviewed
bundled recipes have generation permission. Generic prepared-art inspection still
returns runtime-unavailable inventory, and optional catalogs cannot grant this
permission. No non-free content or derivative is added.

| Species | Source density | Sprite count | Base contribution |
| --- | ---: | ---: | --- |
| Cedar / 杉 | 3 | 9 | `{FBD0CD06-016A-4037-AB15-02040218CBAA}` |
| Pine / 松 | 2 | 6 | `{4B732903-B207-4693-9CF7-19AED4E42604}` |
| Camphor / 楠 | 1 | 9 | `{CE18E96E-336B-4C8A-80E6-F149DAEFD6E4}` |
| Maple / 楓 | 2 | 3 | `{88868086-3B45-46B5-A3F1-8D0E735DBD1C}` |
| Ginkgo / 銀杏 | 2 | 5 | `{77961EC1-0406-42C7-A5F5-ADEC3555400E}` |
| Beech / ブナ | 3 | 6 | `{DF43FB52-E197-451D-B4A3-1114BBFF69D9}` |

The base declaration preserves existing ground; `-m` uses the manifest's seasonal
mountain-ground picture; `-g` uses its referenced farmland grass picture. These are
ground treatments of the same species. The existing renderer draws ground overlays
only on flat Mountains and applies the original seasonal/night picture recipes.

Per selected cell, `2×density` fair binary draws determine tree count, including
zero. Each tree draws source x/y0–15 and a sprite index, producing
`X=x+y`, `Y=((-x+y)>>1)+8`. The source selection sort exchanges positions by screen Y
while retaining sprite indices in draw order. Realized triples are stored in
canonical Mountain forest decorations and packed saves. Rendering, seasonal changes,
join, reconnect and save load never resample them. Flat forests use transparent
Mountain occupancy; slopes retain their exact preexisting corners and footprint.
Entity values use the source price100; generated trees are unowned.

## Controls, reservation and distribution

- Mask coverage0–90%; presets None0, Sparse15, Standard35 and Dense70. The selected
  cell count is `floor(eligible×coverage/100)`. Coverage is a mask target: zero-tree
  binomial outcomes stay empty. Actual forest cells/tree totals are reported
  separately; no retry conditions scatter on a nonzero result.
- Clustering0–100 blends per-cell independent UInt32-hash noise with cubic-smoothed
  spatial noise. The wavelength is `max(4,min(H,V)/8)` in canonical XY. Low scores
  are selected first, with row-major index resolving ties. High clustering creates
  broader groves; it does not change relief, coasts, basins or channels.
- Any nonempty mixture of the six species is allowed when coverage is positive.
  Bindings are sorted ordinally by pack/contribution before choosing a species,
  so reordering a palette does not change appearance. Exact bindings and their
  original caller order remain part of request provenance/fingerprint.
- Starting area0…floor(H×V/4) reserves an exact connected prefix of an existing
  flat, same-height, dry component. Canonical XY face adjacency is required;
  diagonal or H/V image contact is insufficient. Select the largest eligible
  component, breaking ties by the least starting-stage cell hash, then grow a BFS
  from that cell in fixed row-major neighbor order. The component's cells have
  zero quarter offsets and ground at or above the global water level.
- Reservation does not reshape terrain. If no component is large enough, HTTP400
  `geographyImpossible` / `starting-area` names the failure and localized help
  suggests smaller area or different terrain/seed/size. No setting is reduced.
  The existing75% combined-feature space bound still applies.
- Generated forest eligibility requires natural reusable ground or an existing
  Mountain, with ground at/above water; the reserved area is excluded. Underwater,
  cut, Terrace and Creek cells are rejected. Source manual forest eligibility is
  documented separately; interactive authoring remains P3-16.
- Other foliage remains unavailable until its installed recipes have reviewed
  placement/render/save handlers. Its exclusion is visible in localized help.

Starting-area, mask and scatter use independent named stage seeds. Every selected
cell gets a local UInt32 xorshift stream initialized by `Hash(scatterSeed,index)|1`;
bounded draws use multiply-high. This replaces the source's shared `System.Random`
with a reproducible generation-only sequence, preserving its scatter operations.
All existing gameplay streams remain untouched. Forest tuning does not alter
starting-area selection or any ground/corner/water value. A selected cell that
produces no trees consumes no entity ID.

The stage shares the original job's work meter and cancellation token. Grid-sized
arrays, one fixed-width sort and at most six trees per cell bound work and memory;
there is no H×V×Z buffer or retry loop. Existing admission/transfer/graph caps,
30-second worker deadline,600-second candidate retention, four global jobs and
Owner/epoch/revision/connection/content fences remain. Starting masks are transient
preview witnesses. Saves preserve the actual clear ground and realized forests,
plus provenance; loading does not regenerate a reservation or impose a permanent
no-build/no-tree zone on future editing.

## Actual preview and verification

Candidate preparation remeasures eligible/selected/forested cells, tree count,
reserved cells and largest connected level patch from the retained actual state.
Bit-packed forest and starting masks accompany the exact wet-surface mask: dark
green marks realized trees and gold marks reservation in the bounded overview.
The live isometric renderer uses the retained real recipes. The misleading
“0 decorations” label is now confined to empty worlds.

[27 independent fixtures](parity/forest-fixtures.json) use Python canonical XY
components, min-plus source terrain, manifest density/count values, fixed-point
forest selection and realized scatter. They freeze actual forest-record and
reservation-grid hashes across all18 treatments, mixed palettes, signed seed
extremes, clustering/coverage limits, coasts and explicit infeasible reservation.
The source draw-vector test independently checks zero outcomes and position-only
sort. Additional tests check dry slopes, water/headroom extremes, meaningful grove
clustering, palette order, unchanged terrain/water and untouched gameplay RNG.
HTTP/WebSocket checks publish exact trees to two participants and reopen checkpoints.
Browser checks cover EN/JA narrow controls, ranges/species/starting failures,
draft invalidation, late-result cancellation, modal Cancel, exact confirmation,
season changes, canvas identity, reconnect and Save7 reopening.

The independent actual-save checker validates all forest surfaces, source diamond
positions, sorted Y, sprite/count/value limits, allowed contribution identities,
flat canonical connectivity and reservation exclusion. The water checker traverses
the same actual quarter corners while skipping variable-length tree payloads.

```sh
python3 scripts/forest-fixtures.py --check
dotnet run --project tools/TerrainTransportProbe -c Release -- \
  --capacity 1500 1090 45 forest-dense tmp/forest-dense
python3 scripts/check-forest-topology.py tmp/forest-dense/world.json
```

Probe modes are `forest-sparse`, `forest-dense` and `forest-coast`; the latter adds
two lakes/two width3 channels to maximum rough mountains and a north coast.
Full-size browser tests use `playwright.capacity.config.ts --grep 'minimum parity
extent forest'`. Packaged smoke uses `check-map-capacity.py --generate-world` and
`--restart` with those exact saves. Fresh measurements/check results are recorded
in [the forest review](parity/forest-review.json); working logs/images/saves live
under `tmp/p3-11/`.

P3-12 generation UX completion, P3-13–16 editing/authoring, final capacity acceptance
before P3-17 and overall P3 remain open. Existing P2/native/deferred-browser,
original-runtime/full-city/import gates keep their actual scope. BGM remains deferred.

## Current-consumer capacity evidence

Two1500×1090×45 scenes use seed Int32 minimum, all six `-m` treatments,90%
coverage and clustering80. Dense uses hills and a64-cell reservation; coastal uses
maximum rough mountains, north sea30%, two3% lakes, two width3/meander80 channels
and a16-cell reservation. Both saves reopen byte-exactly. Actual-save oracles
independently check every tree and reservation; the coastal wet-edge flood retains
exactly two enclosed lakes and the north outlet. Supported admission stays
H/V16–2048, at most1,700,000 cells, Z8–64 and water1…Z−4.

| Scene | Forest cells | Trees | Generate (s) | Save bytes | Chunks |
| --- | ---: | ---: | ---: | ---: | ---: |
| Dense hills | 1,356,851 | 3,187,085 | 7.528 | 102,116,959 | 2,078 |
| Coastal water/forest | 861,488 | 2,023,925 | 16.226 | 69,986,519 | 1,424 |

CLI time values are measured generation only; use the review's raw records for
save/restore timings and retained managed memory. Native process peak RSS was not
available from this CLI probe; browser-workflow process sampling supplies the RSS
measurements below. A rendered full-size viewport and the EN/JA forms were visually
inspected; native-device or original-runtime fidelity acceptance is not inferred.

The first consecutive browser run exposed server RSS6,506,659,840 bytes, above the
unchanged6GiB budget. `TerrainWire` now shares repeated exact definition values,
source footprint arrays and realized tree triples within its immutable geometry
read. There is no global value cache or forced production-server GC. This reduces
full-size geometry duplication without changing packed bytes, entity IDs, picking
or actual trees. Both consecutive cases pass after this fix:

| Scene | Preview (s) | Confirmed ready (s) | Pan paint p95 (ms) | Server peak RSS (GiB) | Browser peak RSS (GiB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Dense hills | 14.833 | 38.483 | 9.7 | 4.511 | 3.227 |
| Coastal water/forest | 18.642 | 29.349 | 5.3 | 5.535 | 3.622 |

Existing30-second preview,60-second ready,33.4ms paint and6GiB server/browser
budgets remain unchanged. Process RSS is sampled every250ms; retained page JS after
GC is686.7MiB/498.3MiB. Exact candidate confirmation, viewport picking, preserved
canvas, reconnect/download and checkpoint restart pass for both. These are the
current forest consumer's checks; the final full P3 capacity gate remains open.

The final Linux/Arm64 image is
`sha256:108bae30ca9db09225ccd40d581bed72831b3a69a0f5dc4a1fd652e13be884d0`.
A disposable4-CPU/6GiB container reproduces both exact saved candidates:
2,078/1,424 chunks and102,116,959/69,986,519 bytes. Preview takes15.390/25.476s;
chunk integrity, atomic publication, save/reopen and checkpoint restart pass.
Cgroup generation/publication/reopen peaks are4,814,962,688/4,849,963,008 bytes;
the second interval begins after restarting into the dense checkpoint. The final
data directory contains only its writer lock and checkpoint. The container and
volume are removed after verification.

The copied final distribution has identical server/browser canonical catalogs,
238 assets,505 unique media and4 exact notices. Every referenced media/notice hash
passes. Only the bundled content root is present; the reviewed MIT decoder notice
is included and Skia dependencies/artifacts are absent. This verifies the base
package; optional startup modes are separately tested with synthetic external
packs and do not certify the unresolved real optional suite.

All599 .NET tests,129 client tests and142 Python cases (141 pass, one existing skip) pass. Full ordinary Chromium passes90/90, including18 terrain cases. Forest capacity passes2/2; optional source/prepared startup modes each pass4/4. Release/frontend/Linux builds, generated contracts/content/locale checks,27 forest/11 relief/21 coast/20 water fixtures, pinned40-file/77-fixture/59-text source review, decoder/distribution checks, project boundaries and whitespace pass. These close P3-11 only; the remaining gates above stay open.
