# Building catalog — 1 November 2026

The first catalog categories are **Signals** (two contributions) and **Railway
accessories** (54 stationary contributions). Further categories can be added as
their workflows are delivered; this does not close the remaining P9 catalog gate.

The UX takes cues from `freetrain-modern`'s `MainWindow.Catalog.cs`,
`MainWindow.CatalogTypes.cs` and `MainWindow.Layout.cs`: searchable source-art
cards, a selected-entry indicator, dimensions/prices and a right-hand selection
window associated with construction. The Svelte implementation adds independent
browsing and a category switcher. It uses this repository's verified recipes,
translation metadata and authoritative preview/confirm boundary.

- **Game → Building catalog** opens the catalog without activating a building
  operation. Changing categories or searching changes only local browsing state.
- Selecting **Signals (F)** or **Railway accessories (A)** opens its category
  automatically. Cards replace these tools' object dropdowns. Selecting a card
  activates its corresponding tool when construction is available; observers can
  browse and select cards while staying in Inspect.
- Each category retains its selected contribution across tool switches and
  incoming world/clock updates. Search text is independent for each category
  while browsing. Cards show actual source art, bilingual names, accessory
  dimensions, completion prices or the signal placement cost.
- Accessories collapse into **nine entries** covering all **54 variants**.
  Following the sister fork's structure catalog, entries share the original
  plugin, family, footprint, height and price. Inline numbered buttons choose the
  exact baked variant; different authors/plugins, heights and prices stay in
  separate entries. A card's preview, selected button and build tool update
  together. The two signal entries offer four inline direction buttons instead
  of a direction dropdown. Preview artwork follows the selected direction.
  Accessory bitmap previews appear in the catalog; the building task shows the
  selected variant, dimensions and price without duplicating its bitmap.
- Cards and the railway object inspector also show **Author** and **Plugin**.
  Author and plugin title come directly from the owning plugin's
  original XML `<author>`/`<title>` metadata in the verified catalog, with names
  preserved verbatim. Missing author/title fields display localized “Not specified”; no author
  is inferred from a plugin namespace or shared image sheet.
- Switching to another contribution or signal direction invalidates a pending preview request/quote
  and disables confirmation. Within a tool, the point and signal direction stay
  selected. Merely browsing another category or closing/reopening the catalog
  with **Choose from catalog** preserves the current quote.
- Desktop places the catalog on the right, the task card on the left and keeps
  the full map canvas underneath both overlays, with map targeting available
  between them. The catalog does not acquire the primary
  panel's map-input lock. On phones it becomes an expandable, scrolling upper
  sheet above the task card; initial task framing accounts for both sheets.
- The catalog has labeled category/search fields, native keyboard-operable card
  buttons, selection indicators and focus return. Escape in the catalog closes
  it; Escape on the canvas retains the existing draft/tool behavior. Pending
  mutations disable card selection. Opening or browsing does not change shared
  time, saved state or camera coordinates.

`lib/building-catalog.ts` declares category metadata, grouping and contribution identities;
`BuildingCatalog.svelte` handles browsing; `lib/catalog-preview.ts` shares verified
recipe/image loading and fitted preview images. `ObjectProvenance.svelte` and
`lib/object-provenance.ts` resolve XML metadata through each contribution's owning
plugin; `lib/bundled-catalog.ts` shares verified catalog loading between previews
and attribution. `shell-model.ts` owns local
selection and `TrackControls.svelte` consumes the selected contribution directly.
The historical five-item catalog remains confined to the prototype regression
harness; live canonical worlds use this catalog.

Verification covers EN/JA independent browsing, search/no-results, per-category
search, all 54 nonempty artwork previews, matching tool/category activation,
selection/quote invalidation, retained point/direction, focus/Escape, phone
layout, observer access and unchanged saves/camera. Existing signal/accessory
browser regressions now select through the catalog and retain placement,
removal, construction progress, peer publication and exact reopen coverage.

Checks completed on 1 November 2026:

- Production build and generated/content/localization/Svelte checks pass, with
  zero Svelte errors or warnings; all **210 client tests** pass.
- Catalog, signal and stationary construction Chromium suite: **9/9** pass.
- Final catalog plus shell/UX1/UX2/UX3 Chromium suite: **39/39** pass, including
  **36 existing shell regressions**. Together these cover **45 distinct browser
  scenarios**; the three catalog cases run in both suites.
- Desktop EN/JA, all accessory preview images and the phone layout were visually
  reviewed. Local screenshots: [accessories](../tmp/browser-evidence-jLrkts/catalog/accessories-en.png),
  [signals](../tmp/browser-evidence-jLrkts/catalog/desktop-en.png),
  [Japanese](../tmp/browser-evidence-jLrkts/catalog/desktop-ja.png),
  [phone](../tmp/browser-evidence-jLrkts/catalog/narrow.png).

The XML attribution follow-up passes the production/generated/content/locale/
Svelte checks, **211/211 client tests**, and **9/9 Chromium catalog, railway
inspection/rendering and accessory workflow tests**. EN/JA checks assert the
literal XML author/title, including distinct accessory
authors. Updated [catalog screenshot](../tmp/browser-evidence-1uGxgD/catalog/accessories-en.png)
and [phone screenshot](../tmp/browser-evidence-1uGxgD/catalog/narrow.png) were reviewed.

The grouped-variant follow-up passes the production/generated/content/locale/
Svelte checks, **212/212 client tests**, and **9/9 Chromium catalog, signal and
accessory workflow tests**. Checks cover all54 inline selections and nonempty
previews, exact source IDs (including malformed-looking IDs), EN/JA grouping,
signal directions and save/reopen, retained target points and quote invalidation.
The [grouped accessory cards](../tmp/browser-evidence-aY1iaj/catalog/grouped-variants-en.png)
and [inline signal directions](../tmp/browser-evidence-aY1iaj/catalog/desktop-en.png)
were visually reviewed.
