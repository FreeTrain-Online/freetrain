# P5-10 purchase and owned-stock browser workflows — 3 November 2026

P5-10 delivers **Game → Trains / ゲーム → 列車**, with separate **Purchase stock /
車両購入** and **Owned stock / 保有車両** destinations. Deployment/withdrawal
controls remain P5-11. The [trading domain](web-parity-fleet-trading.md),
[stock registration](web-parity-rolling-stock.md) and
[source contract](web-parity-train-contract.md) remain authoritative.

## Source, presentation and workflow bindings

Authority remains ExAV `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`.
The existing [hashed train review](parity/train-review.json) binds
`TrainTradingDialog`, `TrainControllerDialog`, `AbstractTrainContributionImpl`,
`Train.sell` and account operations. Source company/type/nickname hierarchy,
permitted compositions, author/description and units are exposed with stable
contribution identities. A duplicate display name cannot select another stock.
The declared `JR東日本` attribution is preserved as “JR East”.

All six bundled definitions have exact permitted length selectors, quantity,
source-car formation previews, minutes per voxel, base fare, effective amenity
and server-quoted total yen/normal/seated/packed passenger capacity. Preview
frames use existing prepared PNGs and recipes, including authored colors and
reverse-tail wrappers. Capacity totals explicitly cover the selected quantity.
The fare field is a base coefficient; help retains the source
`base fare × passengers × truncated 3D distance × 2` formula without granting
P6 fare/passenger gameplay credit. Safe admitted lengths retain P5-03's checked
source price-width restrictions.

The generated [stock catalog](../web/src/generated/stock-catalog.json) preserves
source fields separately from runtime fleet validation metadata. All 30 English
company/type/name/author/description fields in
[stock-en.json](../content/localization/exav-stable-bundled/stock-en.json) bind
exact contribution/field/original text and prepared catalog hash. Changed
originals use source wording; locale checks reject stale/missing review coverage.
Japanese retains exact source text. New controls, diagnostics and units ship in
both locales. These translations do not silently correct source attribution.

[MAN-T07/W07/A10 and purchase/resale workflow01](web-parity-manual.md#p5-10-purchase-and-owned-stock-browser-workflows)
and [reciprocal action bindings](web-parity-actions.md#p5-10-purchase-and-owned-stock-browser-workflows)
now reach real purchases, naming and resale.

## Transactions and draft safety

`POST /api/fleet/preview` accepts a bounded authenticated, compatible request.
Quotes bind exact pack/version/hash/contribution, formation, quantity or stable
TrainId, world epoch, session revision, shared minute and content identity.
`tradeFleet` revalidates that quote at the FIFO boundary, checks its expected
price, validates complete persistence/transport admission and publishes once.
Existing role, connection-generation, request-ID receipt/retry and unresolved
outcome rules apply. Changed time, pause state, inventory, metadata or world
replacement fences old quotes. Quotes never pause shared time; pause the clock
if a running world makes a quote stale before confirmation. Cancellation and
late/disposed previews cannot mutate the world.

First purchase works from terrain or mixed infrastructure without an existing
fleet. Root/simple/delegation and stock IDs are allocated in a detached candidate
through the shared allocator; every first-published identity has revision zero.
Cancellation/rejection preserves profile, cash, allocator, callbacks and RNG.
Confirmation publishes the whole graph and one RAIL_SERVICE purchase receipt
together. The existing 15-billion-yen initial account/bankruptcy rules remain.

Owned stock is distinct from the purchase catalog. `renameFleet` uses TrainId
and retained train revision, trims names and applies the existing 1–256
character/no-control-character fleet invariant. The established unsaved-draft
dialog protects close, navigation and retargeting. Peer changes retain the
draft and require Reset; locale changes preserve it. Default Return, composition
guards, modal Escape/focus restoration and Observer mutation disabling use the
existing panel/command infrastructure.

Sale quotes/confirmations accept only **Unplaced** stock in both UI and server.
Placed inventory asks for withdrawal; the broader domain ability to sell placed
stock is not exposed as a shortcut. Sale removes only the selected train,
records exact source depreciation revenue and retains allocator high-water,
remaining stock and controllers. Empty inventory/search, unavailable stock and
rejected/stale quotes have localized diagnostics. Observers retain browsing.

Save9/protocol7 and pack/catalog hashes remain current. The coordinated contract
gains two additive commands and the quote request/response; saved-state schemas,
canonical bytes and runtime limits are unchanged.

## Verification

Fresh focused verification on 3 November 2026:

- **106/106 selected .NET tests**, including five new session cases: terrain/mixed
  first purchase, detached cancellation, revision-zero graph publication, exact
  totals, clock/price/role fences, rename conflicts, resale, placed-sale rejection
  and canonical reopen. Existing trading/persistence/contracts are included.
- **81/81 selected client tests**: source translations/lengths/formations, exact
  above-2^53 quote parsing and malformed totals, plus affected contracts,
  fleet/mixed replay, remote-client and shell-model cases.
- **6/6 selected Chromium cases**: four new trading workflows and existing
  catalog keyboard/narrow/Observer and fleet Save9 persistence cases. Real
  two-peer purchase/rename/resale, cancelled/stale quotes, repeated confirmation,
  same-request receipt replay, keyboard/locale/drafts, placed-sale restriction,
  unavailable reference-world diagnostics, exact HTTP save/reopen, retained
  canvas and Japanese 390×844/200% text are covered.
- Clean production build, generated/content/locales (zero Svelte errors/warnings;
  existing Vite bundle advisory), raw pinned-source review, independent train
  fixtures, stock-registration review, project boundaries and whitespace pass.

Logs: `tmp/p5-10-{selected-dotnet,selected-client,selected-browser,web-build,source-review,fixtures,stock-review,project-boundaries}.log`.
Inspected captures: `tmp/p5-10-browser/fleet-purchase-en.png`,
`fleet-purchase-ja-narrow.png` and `fleet-owned-ja.png`.
Initial browser checks exposed missing explicit dropdown labels, an incorrect
recipe angle option and lost focus when the requesting button disabled during
the HTTP quote; all were corrected. A later assertion used a 10-billion initial
balance instead of the existing 15-billion account; its expected value changed.

No complete web suite was run. Native/human full-gallery qualifications,
optional-suite runtime acceptance, deployment, complete manager/group/tracking
controls and P5 recovery/capacity/phase acceptance retain their numbered owners.

Reproduce selected browser checks from `web/` after building web and Debug server:

```sh
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE=../tmp/p5-10-browser deno task test:browser:run fleet-trading.spec.ts fleet-persistence.spec.ts building-catalog.spec.ts --project=canonical --workers=1 --grep 'purchase, stale|Japanese narrow purchase|placed inventory|reference worlds expose|catalog keyboard selection|fleet Save9'
```

## Owner Trains dialog layout follow-up — 3 November 2026

The desktop Trains dialog now puts catalog filters and compact stock/owned-train
rows on the left, with selected details and actions on the right. At widths of
1,024px and above it uses a bounded wider panel over the full-size map canvas.
While picking a tail, the panel temporarily narrows to expose more of the map.
Its desktop height stays stable when placement changes the available actions,
so a rapid second confirmation click cannot fall through to the canvas.
Long columns scroll independently; narrow screens retain the stacked sheet. X/Y/Z share one row, and placement instructions
are available under Help. Language selection uses the existing Settings panel.

Production/generated/content/locales pass with zero Svelte errors/warnings and
the existing bundle advisory. **7/7 selected purchase/deployment Chromium cases**
pass, including no vertical scrolling for normal purchase and owned-stock views
at **1,024×768 and 1,280×900**, compact bundled rows, EN/JA map placement/camera
retention and Japanese390×844/200% text, drafts, focus, Observer and continuation.
The initial attempt to reserve map space shrank the canvas and exposed the
green page backing below the dialog. Owner correction removes that reservation;
layout checks now require the canvas to cover the full map stage. The canvas
retains its dimensions during map picking; map checks wait for the layout paint.
EN purchase/owned, deployment footprint and JA narrow captures were visually inspected. Logs: `tmp/trains-columns-build.log`,
`tmp/trains-columns-browser.log`; captures: `tmp/trains-columns-browser/`.
Only the selected browser suites ran.

The background correction passes a fresh **7/7 selected Chromium** and production
build. Logs: `tmp/trains-background-fix-{build,browser}.log`; final inspected
captures: `tmp/trains-background-fix-browser/`.
