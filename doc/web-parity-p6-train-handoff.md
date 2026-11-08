# P6 train/harbor handoff — 8 November 2026

P6 starts with real purchased, deployed and saved trains, shared Simple/TAT
controllers, delegation groups, passenger counters/boarding origins and one
future callback per deployed train. [P5 acceptance](web-parity-p5-acceptance.md)
closes browser/portable train scope with its explicit qualifications. This handoff
specifies future station integration; it grants no platform, passenger or fare
gameplay acceptance.

## Source authority and stop positions

Stable ExAV archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`:

| File | Raw Git blob SHA-256 |
| --- | --- |
| `core/world/rail/YardRailRoad.cs` | `7db261e01be0c6c0dd0e7bbb95ed970161378f28c0963482e718e8eb8c15a798` |
| `core/world/rail/Station.cs` | `583af2748ca5ebdbaa1a850010d5bea09cad7f74eef353177385a96ef172c1fb` |
| `core/world/rail/Train.cs` | `0130739ef685f252a8e1cd639fcf6d0908d3e3da6ef028e0418f3cb72cdfd404` |

These blobs were read directly from the pinned archive during this review.
Yard lines 44/47 calculate the head's stop index. With platform length `L` and
train length `N`, use C# integer division **toward zero**:

- Same heading as platform: `(L + N) / 2 - 1`.
- Opposite heading: `(L - N) / 2`.

Literal review vectors for a four-cell platform, indexed 0–3 from its base:

| Formation | N | Same heading index | Opposite heading index | Eligible at that index with host |
| --- | ---: | ---: | ---: | --- |
| Short | 3 | 2 | 0 | Yes |
| Equal | 4 | 3 | 0 | Yes |
| One car overlength | 5 | 3 | 0 | Yes: `-1 / 2 == 0` |
| Two cars overlength | 6 | 4 | -1 | No: outside platform |

Any out-of-range position, wrong rail index or unresolved host returns zero
without calling a controller, unloading, boarding or ringing. Do not introduce
a blanket `N > L` rejection. These eight source-derived vectors are P6 acceptance
inputs; they are not claims that production yards exist. Existing independent
[`yards` fixtures](parity/train-fixtures.json) contain 14 call-order/overlength/
host/index witnesses. P6 must consume these through real constructed platforms,
including host removal, reconnection and save continuation in both directions.

## Runtime adapter and ordering

[`FleetControllers.cs`](../src/FreeTrain.Online.Simulation/FleetControllers.cs)
already exposes `FleetHarborReference.Station(Location, StationId, Revision)` and
`AtHarbor`. A future yard resolves its host Station and passes **the station
location** as the shared controller's settings key. `FleetHarborCall.RailLocation`
keeps the head's actual yard rail location separately. Multiple platforms of one
station therefore share harbor settings. Signals retain their own location and
never become passenger stations. `StopTime` currently dispatches signals only;
P6 must add real yard resolution after the source stop-index/host gates.

Resolve the train/group's effective shared controller on each call. Simple waits
30 minutes on the first Station call and proceeds later; unconfigured TAT waits
10 then proceeds. Preserve absent harbor settings and same-location reuse under
TRAIN-D012. Host identity/revision must come from actual station state; the
existing Station reference validates shape and does not prove host resolution.

The yard calls the controller first, retaining any handler-side reversal or
TURNING re-placement. Then apply the following branches using the incoming
`callCount` and returned source duration/action:

| Call/result | Yard effects after controller/handler effects |
| --- | --- |
| First (`0`), positive wait | Unload once, including fare; no boarding/bell |
| First, zero or negative | No unload, boarding or bell |
| Later, positive wait | No repeated unload, boarding or bell |
| Later, `-999` or `-1000` | Bell only; suppress boarding |
| Later, any other nonpositive result (including reverse) | Board, then bell |

Return the resulting train and decision to `FleetMovement.Tick`. Positive waits
increment StopCallCount, preserve handler changes/forwarding, set
StoppingAtStation and schedule the wait without a movement charge. Nonpositive
results reset forwarding, apply the central sentinel/reversal and reset callCount
before movement. Thus forwarding boarding suppression must use the returned
sentinel, before the central reset. TURNING's early reversals remain observable.
Timing uses the callback-entry head, not a re-placed head. Publish station, train,
occupancy, accounts and queue changes as one detached successor; rejected callbacks
must leave all unchanged. P6 must decide how to expose transient bell effects only
after successful publication, so retries/reconnects cannot replay departure audio.
The existing six bell previews are not that departure event implementation.

## Passenger, fare and hourly companions

Keep the three stock capacities distinct: normal, seated and packed. Source
packing capacity clamps seated capacity into `[0, normal]` and computes
`seated + 2 * (normal - seated)`. Information occupancy uses normal capacity and
can exceed 100%. Zero-capacity stock stays valid. Source Station boarding uses
`available = max(0, population - gonePassengers)`, then the minimum of packed
capacity and truncation of `available * amenity * 0.01f * 0.3f`. Preserve the
source single-precision intermediate order; do not replace it with a normal-
capacity cap or decimal percentage arithmetic.

Boarding increments gone/loaded counts and records the **current head's placed
state** as boarding origin. A zero-population station increments its train log,
notifies and returns before calling the train loader. Unloading returns the old
passenger count, clears passengers/origin, updates station import/train logs,
averages, land-value/traffic notifications and books `passengers * fare * distance
* 2` when an origin exists. Distance truncates the source 3D Euclidean length;
retain checked Int32 products under TRAIN-D008. A controlled P5 witness is
39 passengers × fare3 × truncated sqrt(29)=5 ×2 = 1,170. Missing origin earns no
fare. These arithmetic witnesses are distinct from passenger-generation gameplay.

Outside exchange already runs at resulting countdown50 before consist shift:
unload/fare, then `min(100, normalCapacity)` boarding unless forwarding. Forwarding
clears the origin and skips boarding. Preserve that normal-versus-packed
distinction when integrating stations and preserve fare-before-operating-expense
account order. Actual station supply and booked station-service revenue remain P6.

At the source aligned hourly callback, truncate `gonePassengers * 0.8f` and each
accumulated loaded/unloaded count times `0.9996f`. Daily averages use the source
float factor `24.0f * (1.0f - 0.9996f)`. Daily callbacks charge station operating
cost and rotate import/export/train logs. P6 must add independent float/truncation
vectors, hour/day-equality FIFO traces with waiting/departing trains, zero supply,
packed/forwarding companions, real host reachability changes and exact reopen/
checkpoint continuation. P7/P8 retain broader population/development/economic
integration; controlled station populations must be labeled until those exist.

## Remaining P6 acceptance

Implement real station/platform/passageway ownership, host resolution, catchment,
passenger state and atomic station effects before claiming this adapter as
gameplay. Verify the eight length/direction cases, missing/wrong/rebuilt hosts,
shared station location keys, first/later/pass/forwarding/reverse/TURNING order,
fares, hourly companions and durable peer continuation through EN/JA browser
workflows. P10 owns historical station/world import. Current Save9/protocol7 is
unchanged by this handoff; any P6 persisted/transported station state must follow
the current-format-only policy and explicitly update the applicable contracts.
