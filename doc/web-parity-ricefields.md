# P7-18 ricefields — 17 November 2026

Both bundled ricefield styles are available in the EN/JA Land and ground cover
task (`O`). They share the existing two-corner map/touch/coordinate workflow,
fixed starting level, eligible/skipped review, Confirm/Cancel and free removal.
The catalog now contains 45 ground choices: 25 farmland, 18 forest and two rice.
Ricefield help explains the source's two charges and absence of harvest income.

## Exact source behavior

Authority is ExAV 2.1.1.12, Git `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/`. The [P7 source contract](web-parity-city-contract.md), CITY-D008/014/016,
and [independent city fixtures](web-parity-city-fixtures.md) remain binding.
Use the pinned source rather than the older root desktop checkout: the latter's
LandBuilder wrapper lacks the archived area spend call.

| Binding | Portable implementation |
| --- | --- |
| `plugins/org.kohsuke.freetrain.land.ricefield/plugin.xml` | Both actual contribution IDs, origins `(0,0)` and `(0,32)`, price 1,500,000, grid 1×1, AgriculturalPopulation base 5. |
| `src/RiceFieldBuilder.cs` | Inclusive normalized rectangle, X outer/Y inner. Min index 0 takes precedence over max index 2 for singleton dimensions; other cells use index 1. Variant is persisted as `xIndex*3+yIndex`. Skips retain the original rectangle indices; removing neighboring tiles does not retile survivors. |
| `RiceFieldBuilder.create` and `core/contributions/land/LandBuilderContribution.cs` | One Subsidiaries spend of 1,500,000 for each successful owned placement, followed by price × full selected area, including skipped cells. Four successful cells cost 12,000,000, with receipts `1500000,1500000,1500000,1500000,6000000`. Two successes in a three-cell rectangle cost 7,500,000. An all-blocked singleton still costs 1,500,000. The authoritative review shows the actual total. |
| `core/world/land/LandVoxel.cs`, `World.isReusable` | Each cell must be at its own ground level and empty or silently reusable. Owned property, farmland and rice can be replaced without buying the old entity or land. Rail/buildings and forest Mountain owners remain protected. Global water adds no new prohibition. |
| `src/RiceFieldVoxel.cs` | Immediate owned ground, contribution price as entity value, real station listener. Replacement, rectangle removal and general demolition remove the provider and both membership directions. No crop growth, harvest, random draw, farming revenue, upkeep or rice timer exists. Base 5 agricultural hourly demand truncates to zero. |
| Rice picture and sprite array | All nine exact crops per style, 32×16, using spring/summer/autumn/winter source sheets and existing day/night conversion. Map and 3×3 catalog mosaic use the same recipe selectors, masks and alpha picking. Ground tiles draw at every traversed height cut. |

All state reuses City1 ground/producer/ownership, the existing station lifecycle,
and saved game clock. Appearance follows the fixed 365-day game calendar; no host
DateTime or timezone is consulted. Midnight, February 28→March 1, June 1,
September 1, December 1 and year rollover preserve authored tile identities,
random streams and accounts. There is no rice-specific land-value deposit or
producer outside the declared agricultural listener.

## Authority, persistence and qualifications

The existing land preview/command boundary validates role, epoch, content,
geometry, minute, reviewed owner state and exact total. Preview/cancellation do
not mutate canonical state. A commit stages all cells, listener changes, allocator
and ordered receipts, then publishes one coherent successor. Late allocator or
later receipt overflow abandons the whole candidate. Selections retain the
4,096-cell limit. A receipt retry cannot charge or attach twice.

Save11/protocol11/City1 and prepared catalog identities are unchanged. Saved
variant numbers retain their exact rectangle art after reopening and subsequent
edits. The native inherited remover's rebuild quirk has the same explicit
portable teardown adaptation as [P7-16](web-parity-static-ground.md); this is not
native remover acceptance. Native/optional/full-city/performance and overall P7
acceptance retain their separate gates; creek authoring is P7-19.

## Focused evidence

Only relevant selections were run, never the complete C#, Deno or browser suites.

- 152 unique C# cases across ricefield, static ground, forest construction,
  land trading, city state/persistence/population, object removal and calendar
  filters. Ricefield-specific checks include both styles, all nine tile variants,
  singleton/reversed/blocked rectangles, owned reuse, static replacement, real
  rail/forest neighbors, water/bounds/level/capacity rules, real listeners,
  first activation, authority, incorrect cost, stale/replayed commands, late
  allocator/receipt rollback and six uninterrupted versus reopened clock traces.
- 42 focused Deno cases cover canonical C# delta application and removal, all
  18 rice crops × eight appearances, alpha picks/cuts, mosaic preview, exact
  pricing validation, calendar boundaries and related ground/forest/city/removal,
  fixture-registration and localization regressions.
- EN keyboard and JA phone/touch browser journeys cover both styles, cancellation,
  map placement, ordered receipts, real four-provider catchment, exact peer state,
  save/reopen, backend checkpoint restart, picking and peer removal. The four
  existing EN/JA farmland/forest journeys also pass.
- Generated contracts/art, bundled content, locales, browser source policy,
  Svelte/build, project boundaries, whitespace and 703 independent city cases /
  197 pinned source files pass.

Logs, commands, corrections and artifact locations are recorded in
`tmp/p7-18-validation.json`. Browser checkpoints use `sessions.observe`, receipt
correlation, full peer convergence and bounded canonical/save assertions.
