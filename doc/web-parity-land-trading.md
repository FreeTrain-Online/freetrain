# P7-15 land ownership and trading — 15 November 2026

Delivered at bundled browser/portable scope. The **Land ownership** tool (`O`)
buys and sells inclusive rectangles through authoritative review and confirmation.
English/Japanese controls support map corners, coordinate entry, a fixed level,
eligible/skipped counts, per-cell skip reasons, reclamation details and exact
debit/credit. The general map inspector shows live land value and property
ownership, including for Observers. Inspection and cancelled previews leave the
canonical save, queue, random streams, cash and allocator unchanged.

## Source contract and quote disposition

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`:

- `core/controllers/RectSelectorController.cs` and `Disambiguators.cs`: the first
  corner must be on exact ground. Normalize XY, retain the first corner's Z and
  visit X outer/Y inner. Later cells need not share ground. Underground/outside
  cells are occupied source sentinels; they cannot become property. Water is a
  plane and does not by itself forbid buying an otherwise vacant ground voxel.
- `core/controllers/land/LandPropertyController.cs`: purchase reclaims only
  unowned, silently reclaimable entities. Spend entity value, run its removal
  effects, reread the voxel, then spend live land value for each newly empty cell.
  Whole-owner removal is deduplicated by the staged reread, preserving receipt
  order. Owned objects and protected unowned objects are skipped.
- `core/world/land/LandPropertyVoxel.cs`: properties are owned, silently reusable,
  zero-entity-value, one-voxel owners. Sell only this type, booking one Subsidiaries
  earn call per cell, including zero. `Voxel.landPrice` reads current land value.
  **CITY-D005:** no acquisition-price state or purchase-receipt lookup is added.
- **CITY-D004:** the native preview does not simulate reclamation or include its
  nested removal fees. The existing browser disposition stages the actual buy
  traversal instead. The six independent P7-02 trade vectors exercise the same
  `LandTradeTraversal` engine as production. The shared-owner vector books
  **50, 7, 10, 20, 30**, totaling **117**, versus the native preview's **80**.

`LandTrading` realizes that traversal against actual canonical owners. Current
bundled silently reclaimable owners are one-cell unowned city ground and
P3 decorative creek cells. Their teardown clears fences, actual producers and
both listener directions. Fixed/variable/half structures retain their source
protection; a selected rectangle cannot demolish or partly acquire them. The
shared multi-cell/nested-fee vector tests the generic source traversal, rather
than inventing a reclaimable bundled building. Actual multi-cell protected
buildings and multi-cell buy/sell rectangles also have runtime coverage.
Ground covers used in the fixture are explicit canonical inputs.
[P7-16 now delivers static/mixed farmland authoring](web-parity-static-ground.md);
forest/rice/creek authoring remains P7-17–19. Acquiring property does not create population or a development
model. Automatic development remains P7-25–29 and P8 retains market/report UI.

MAN-M05, MAN-O05, MAN-A16, MAN-A20 and EXAV-11's `land-trade` obligations bind the
Land ownership task, map overlay, value inspector, staged trade and Subsidiaries
receipts. The shipped EN/JA help describes anchor/level, eligibility, reclamation
and current-value resale. Property rendering uses the pinned
`{0E7A9F09-4482-4b78-8A8D-F59F02574B1B}` SimpleSprite at `[0,0,32,16]`, anchor
`[0,0]`, with verified day/night pixels and alpha masks for all seasons. Runtime
recipe preparation preserves the original catalog and content identities and
composes with the existing variable-building art accommodation.

## Authority, atomicity and continuation

`POST /api/land/preview` returns a bounded `LandPreview`; `tradeLand` rechecks the
issued quote and expected amount inside the existing authority FIFO. Quotes use
epoch, geometry, content, source minute, mode, affected owners, cell classifications,
current values and ordered reclamation costs. Unrelated clock progress can retain
a review; changed value/ownership requires renewed review. Existing 720-minute
quote expiry, role checks and network receipt retry/reconnect behavior apply.
The browser distinguishes buyable, sellable, owned, protected, underground,
outside and non-property cells; the overlay marks eligible and skipped locations.

One successful trade publishes one world successor. Every ordered debit or credit
uses Subsidiaries and that successor's revision. `ConstructionAccounts.EarnMany`
retains individual source sale receipts while keeping intermediate results private.
Late value, allocator or second-receipt overflow abandons every staged change.
The selection is bounded to **4,096 cells** before enumeration; oversized drafts
are rejected atomically. Account/occupied-cell/transport bounds remain enforced.
This is bounded authoring scope, not a P11 full-city performance claim.

First heat activation is detached during preview/inspection and becomes durable
only on a successful trade, with exactly one existing-format heat timer. Existing
City1 properties, owners, accounting, heat, queue and random state already carry
all required continuation. **Save11/protocol11/City1 and prepared pack identities
remain unchanged.** No acquisition-price field, migration or replay history is
introduced. Exact property-bearing and sold-state saves reopen through replacement
and process restart. Actual heat diffusion changes an inspected property's value
**30→20**, rejects its old sale quote without mutation, then credits **20** after
renewed review.

## Focused verification

Fresh focused evidence: **87 C#, 60 Deno and four unique Chromium workflows**
pass, with zero skips or retries in the successful selections. Final commands,
selected counts, logs and artifact hashes are recorded in
`tmp/p7-15-validation.json`. Only relevant C#, Deno and Chromium selections run;
no complete suites run. C# covers source vectors, canonical rectangles and actual
reclamation/listener teardown, protected structures, water/bounds/level rules,
current-value resale, zero values, first activation, roles, quote revalidation,
network retries, exact reopening and later-cell/later-receipt overflow. Deno
checks real C# deltas, immutable predecessors, malformed quotes, exact ordered
amounts, all eight sprite appearances and existing city rendering/transport,
localization, shell and fixture selection contracts. Chromium covers EN desktop,
JA phone touch, cancellation, peers, inspection, wrong amounts, receipts,
replacement, real restart and actual changing-value quote/inspection boundaries.
The existing EN/JA heat/restart workflow also passes.

Source fixture checks verify 703 cases / 197 pinned files. P7-01 source review,
project boundaries, generated contracts/art, content/locales, Svelte and production
build checks pass. The source reviewer now reads the exact historical P7-01
station handoff snapshot: the live document was extended by P7-14, which had
invalidated its old digest. The reviewed hash and source contract remain intact.
Native/optional/full-city/performance and overall P7 acceptance retain their gates.
