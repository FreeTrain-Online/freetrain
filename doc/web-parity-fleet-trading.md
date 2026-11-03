# P5-06 purchase, inventory and resale domain — 3 November 2026

P5-06 is complete at detached domain scope. [FleetTrading](../src/FreeTrain.Online.Simulation/FleetTrading.cs)
provides purchase/sale quotes and atomic commits over actual P5-03 stock,
P5-04 fleet/allocator and P5-05 Save9/protocol7 state. Browser controls remain
P5-10/11, placement/withdrawal P5-07 and movement P5-08. No new player-facing
text ships here; English/Japanese presentation remains with those controls.

## Source and requirement bindings

Authority is ExAV 2.1.1.12/V2, Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. The
[pinned P5-01 review](parity/train-review.json),
[trade contract](web-parity-train-contract.md) and
[P5-02 fixtures](parity/train-fixtures.json) supply source semantics;
none was regenerated from the implementation.

| Source file | Pinned SHA-256 | Binding |
| --- | --- | --- |
| `core/controllers/rail/TrainTradingDialog.cs` | `7124579b0f78952904039471605c424a2a47af41668f0db2d904b6b0c00b32c6` | `getTotalPrice`, formation validation and `buttonOK_Click`571–581: one time, root membership, simple controller, one RAIL_SERVICE spend |
| `core/world/rail/Train.cs` | `0130739ef685f252a8e1cd639fcf6d0908d3e3da6ef028e0418f3cb72cdfd404` | purchase time, `remove`/`sell`163–197: withdraw, cancel callbacks, detach membership/listeners, completed365-day depreciation; null date means no earn call |
| `core/world/accounting/AccountManager.cs` | `402292886da4fa0464dc28639aef67ff98c578519e74e978e7ee49d2e2a6936a` | `transact`/`spend`/`earn`65–89: cash arithmetic, bankruptcy increments and distinct expenditure/revenue history |

Reciprocal [action](web-parity-actions.md#p5-06-trading-domain) and
[manual](web-parity-manual.md#p5-06-trading-domain) bindings connect `trade`,
MAN-T07, MAN-W07, MAN-A10/A11 and DEV-P19/P24. Catalog browsing, confirmation,
keyboard/focus and local selection/listener workflows retain their later owners.

## Atomic purchase and sale

`QuotePurchase` resolves an exact selected stock/pack binding and validates its
allowed formation × quantity against current inventory. Its quote exposes both
per-formation capacities and exact Int64 batch price/car/capacity totals.
P5-03's original Int32 formation arithmetic and100-car/100-quantity,
128-train/1,024-car guards remain. Explicit patterns admit only declared lengths;
single-car parameterized formations retain tail-over-head precedence.

Purchase allocates TrainId then original CarIds in order from the existing
shared high-water mark. All new stock is Unplaced, with one current purchase
minute, root membership and explicit shared simple controller. Cars add no
second spatial footprint or callback. Root membership advances once per batch;
one `trainPurchase` RAIL_SERVICE expenditure covers the whole batch. There is
no affordability predicate: existing accounting subtracts and applies its
configured bankruptcy increment until cash is nonnegative. Rescue funding
remains separate from revenue. Checked price/ID/member/session/cash/sequence
arithmetic and journal/construction-reservation capacity reject before publication.

Portable default names are `TR<stable TrainId>`, replacing the source's static
process-global `iota`. Names therefore stay independent of other worlds/restarts
without another durable counter. Later manager controls can rename them; names
never identify stock.

A quote holds a fully validated detached candidate and its source session,
construction/accounts identity, revision and complete clock boundary. Commit
rejects another/replaced session, competing committed edits, changed time,
pause revision or queue. Replay against the committed successor rejects.
Cancellation discards the quote, consuming no live IDs, cash, random draws or
callbacks. This immutable API does not replace the later network command receipt
deduplication layer.

`QuoteSale` uses stable TrainId and `formationPrice/(completed365DayYears+2)`.
SCN-026's1,000,001 yields **500,000/500,000/333,333/250,000** at
days0/364/365/730. A missing purchase date yields zero and appends **no** earn
record; a present date with zero valuation still invokes source earn(0).
Domain sale supports placed stock, while the later browser baseline must retain
withdraw → quote → confirm rather than offer a placed-sale shortcut.

The sale successor removes train/cars and parent membership, clears every Inside
Traffic slot and advances each affected spatial owner once. Outside cars occupy
no slots. It preserves terrain/rail/road/accessory/platform/support/speed-limit
data, allocator high-water, shared controllers and neighboring trains.
`MixedSessionState.Successor` cancels exactly the sold train callback, retaining
neighbor/construction callbacks and queue registration/order high-water. Canonical
state stores no UI listeners; no domain listener reference survives removal.
Local browser listener/selection cleanup remains P5-25/26.

## Revenue persistence and publication

`ConstructionAccounts.Earn` records nonnegative Revenue separately from
Expenditure, with source cash/top-up behavior and exact restore validation.
Both cannot be positive in one entry. Changed receipts, overflow, invalid top-ups
and rewritten journal prefixes reject before exposure. Save/full/chunk/delta
publication and browser journal replay preserve exact Int64 revenue.

Save9/protocol7 gain an **optional zero-default `revenue`** Counter on transactions,
omitted when zero. Existing expenditure-only canonical bytes remain unchanged.
Both current schemas, C# DTO/codec, generated TypeScript and browser validation
change together. No version bump is needed for this unambiguous zero-default
extension; no older-format parser or migration is added. Coordinated builds
remain required: a pre-extension strict decoder rejects the unfamiliar nonzero
field rather than discarding it. Pack/catalog hashes and stock limits are unchanged.

## Verification

Fresh checks on3 November2026:

- **244/244 focused .NET tests**, including22 trading cases and affected fleet,
  persistence/continuation, booking, stock, stationary-construction and contract
  regressions. Covers all six bundled families, explicit mixed patterns,
  actual128-train/1,024-car purchases, cancellation/stale/replay guards,
  cash/ID/revision/journal exhaustion, SCN-026, placed mixed Inside/Outside sale
  with subgroup/shared controller/neighbor/construction, exact save/delta and
  restored one-minute continuation. Log: `tmp/p5-06-focused-dotnet.log`.
- **61/61 selected client tests**, contracts/fleet-state/mixed-state/state-transfer.
  Two immutable peers apply the sale; malformed receipts/queue/group/journal
  links, exact above2^53 values and overflow are covered.
  Log: `tmp/p5-06-selected-client.log`. No complete web test run.
- **1/1 selected Chromium case**,3.9seconds: real two-peer Save9 inventory load
  with sale revenue, Step delta, download, rejected graph/receipt uploads and
  reload/reconnect, retained canvas and no page errors.
  Log: `tmp/p5-06-selected-browser.log`. This loads a completed-sale fixture,
  not browser trading controls. No screenshot/new visual/native credit.
- Clean web build/generated/content/locale checks pass with the existing bundle
  size advisory (`tmp/p5-06-web-build.log`). Source/fixture/stock-registration,
  railway handoff and project-boundary checks pass in
  `tmp/p5-06-{source-review,fixtures,stock-review,handoff,project-boundaries}.log`.

The independently authored [sale delta](../prototype/protocol/fixtures/delta-fleet-sale.json)
has SHA-256 `2ce4e362179eba2870e55f4fc14bc324ba98c51be1b6a55779c9f73e350eb247`.
It starts from unchanged P5-05 fleet literals: reversed Raicho train17 has two
50,000,000 cars, purchase minute100 and age below one year at130081.
Expected revenue50,000,000 takes cash7,000,000,000→7,050,000,000; owner7
revision0→1 clears its car, subgroup16 revision0→1 loses train17, queue becomes
empty, and train20/controllers/allocator remain. Expectations were authored from
source arithmetic and existing literal inputs, not captured from production.
The real `FleetTrading` sale matches this complete delta structure. SCN-026's
odd formation price uses a single-car synthetic stock (an odd total cannot be
two equal Int32 per-car prices); separate real-stock cases cover multi-car cleanup.

Reproduce from the repository root:

```sh
dotnet test tests/FreeTrain.Online.Tests/FreeTrain.Online.Tests.csproj --no-restore --filter 'FullyQualifiedName~FleetTradingTests|FullyQualifiedName~FleetTests|FullyQualifiedName~FleetPersistenceTests|FullyQualifiedName~RailwayBookingTests|FullyQualifiedName~MixedPersistenceTests|FullyQualifiedName~MixedContinuationTests|FullyQualifiedName~StationaryConstructionTests|FullyQualifiedName~RollingStockTests|FullyQualifiedName~ContractTests' -m:1 -nodeReuse:false -p:UseSharedCompilation=false
python3 scripts/review-trains.py --check
python3 scripts/train-fixtures.py --check
python3 scripts/review-stock-registration.py
python3 scripts/check-railway-handoff.py
python3 scripts/check-project-boundaries.py
```

From `web/`:

```sh
deno task build
deno test -A --no-check tests/contracts.test.mjs tests/fleet-state.test.ts tests/mixed-state.test.ts tests/state-transfer.test.ts
FREETRAIN_ONLINE_TEST_SERVER_CONFIGURATION=Debug FREETRAIN_ONLINE_BROWSER_EVIDENCE=../tmp/p5-06-browser deno task test:browser:run fleet-persistence.spec.ts --project=canonical --workers=1
```

Later steps retain deployment/motion, controls, command authorization/receipts,
full checkpoint recovery and moving capacity. Native/human/optional and other
phase qualifications retain their recorded scopes.
