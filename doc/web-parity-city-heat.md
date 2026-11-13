# P7-06 heat-based land value — 13 November 2026

`CityHeat` executes the pinned ExAV `883cf7d` `LandValue` behavior in the
canonical mixed city runtime. Live `MixedSessionState` and detached
`CityConstruction.AdvanceOneMinute` dispatch heat directly and use actual land
value for construction valuation. Trusted fixture adapters remain explicit;
no executable callback or constant valuation is loaded from saves.

## Source behavior

The production kernel preserves CITY-D001/002/003:

- q/back start at zero. Only interior rho starts at bare conductivity (0.80f);
  borders start at zero. First activation does not scan existing terrain.
- H-major/V-minor storage includes both borders. Deposits and conductivity use
  `[h,v]`; public values use `[h+1,v+1]`. Source-valid border reads remain valid;
  invalid indexes reject. XY converts through the existing H/V mapping.
- Deposits use Single `amount * currentFrequency / 4` before addition. Negative
  deposits persist until diffusion. Public values use double `Math.Pow` with
  the source widened Single exponent, truncate to Int32, then multiply by ten.
- Diffusion swaps the two buffers and writes only the interior, in descending
  H/V order. Each buffer's borders therefore alternate rather than being reset.
  Odd/even V selects the source diagonal neighbors. Bare, sea and mountain cells
  use the source Single weighted sum; road enforcement uses positive differences,
  Single squares/sum/product, double square root, then a Single cast. No fused
  arithmetic or balancing limits are introduced.
- `Enable` creates a detached first activation with a relative ten-hour repeated
  callback on the existing world clock. It allocates no spatial identities or
  random draws. An existing city is returned unchanged. Restoring never invokes
  activation, recreates timers or reconstructs buffers. A saved timer's captured
  interval stays separate from the current frequency used for new deposits.

The existing live Save11 upload/checkpoint path makes heat execution reachable for
any admitted city dimensions/state, without an experiment harness or a fixed map.
The initialization helper is available to the first city construction workflows;
this slice does not promote existing terrain-only saves or manufacture a city when
loading them. Playable city creation/placement retains P7-07/09 ownership.

Fixed/variable structure valuation samples the source integer center and
multiplies by footprint area, rather than averaging or summing the field. Half
structures sample their anchor. Int32 casts/products and accumulated Int64 inputs
are checked. Invalid powers, non-finite deposits/diffusion intermediates,
conductivity and schedule/queue overflow reject before publication. Whole-minute
execution remains detached: a later callback failure discards heat, geometry,
accounts, clock, random and transient effects together.

## Conductivity notifications

`MixedWorld.Replace` supplies the source voxel assignment/removal notifications
from changed occupancy and the explicit road-constructor notification when a road
is attached/replaced. It scans only notified columns. Restoring a world performs
no scan. Retained Mountain corner/tree/outlook changes, Traffic component setters,
road removal while Traffic remains, and water-level changes alone do not imply
an extra notification. Removing the entire Traffic voxel does notify.

A scan ascends Z and overwrites the remembered road for **every** Traffic,
including null. A road wins over sea and mountain. Street/highway/railballast use
0.999f with pavement or the Single average with bare conductivity otherwise;
footpaths/unknown styles use bare conductivity. Without a road, sea uses 0.03f,
then mountain uses 0.4f, then bare land. No initialization rescan or automatic stale
rho repair is hidden in a query, preview, save or restoration.

## Persistence and publication

Save11, protocol10 and City format1 already contain all required fields; there is
no new format or migration. Both buffers, retained borders, rho, settings and exact
callback registration/order/due minute/captured interval round-trip unchanged.
A protocol10 schema omission is corrected: a city-only mixed queue is valid, and
a city component requires its queue, as fleet/station components already do.
Generated types remain unchanged. Existing bounded transfer/admission limits apply.

Heat changes publish through the ordinary atomic city component and shared queue.
The browser does not recompute authoritative diffusion. No new controls or strings
are added; English and Japanese peers exercise the same current contract.

## Focused verification

`CityHeatTests` consumes all **11 independent heat traces, eight coordinate vectors
and nine conductivity vectors** from the unchanged P7-02 fixture corpus. These
include impulse, zero/negative/decay, both V parities, borders, pavement/shoulder,
sea/mountain precedence and upper null Traffic. Additional witnesses cover actual
corner-terrain preview/commit, stale road detach and whole-Traffic removal,
world isolation, changed frequency versus captured timer interval, alternating
border buffers, exact reopen, finite/cast/product failures, default actual
construction valuation and rollback after equal-time later callback failure.

The committed `city-heat-v11.json` fixture is generated by the production session
with a deposit immediately before its first ten-hour callback. The independent
traces provide the numeric oracle; the generated snapshot provides cross-language
codec/publication evidence. It contains no controlled dispatcher or passenger
supply. Reproduce it with an absolute output path:

```sh
FREETRAIN_CITY_HEAT_OUTPUT=/absolute/path/to/web/tests/fixtures/city-heat-v11.json \
  dotnet test tests/FreeTrain.Online.Tests --no-restore --filter FullyQualifiedName~CityHeatTests
```

Focused .NET regressions cover city state/construction/persistence, mixed terrain
and persistence, clock, cross-tool dependencies, stationary sessions and contracts.
Focused Deno tests cover city/mixed state, transfers, remote-client and contracts.
Two Chromium cases cover EN/JA peers, Observer rejection, one diffusion, receipt
replay, exact reopen and actual checkpoint restart, plus existing city authority
and equal-time rollback. Full suites are not run.

Fresh results: **242 selected .NET, 88 focused Deno and two Chromium tests pass**.
Exact commands are recorded in `tmp/p7-06-validation.json`.
Builds have zero .NET warnings/errors and zero Svelte diagnostics. Vite retains
its existing large-chunk advisory. The 703 independent fixture cases and all 197
pinned source files verify unchanged.

Producer attachment/land-value metadata refresh remains P7-07/13. Applying ordered
passenger deposits within the committed minute remains P7-14; the existing
transient effect list is not polled or relabeled as consumption. Public land
inspection/trading remains P7-15, playable road witnesses remain P7-23, full settings
admission remains P7-28, and capacity/native/optional/P7 acceptance remain open.
