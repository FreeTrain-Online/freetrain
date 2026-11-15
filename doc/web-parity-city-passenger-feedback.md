# P7-14 atomic passenger feedback — 15 November 2026

Delivered at bundled browser/portable scope. Real yard passenger callbacks now
consume their ordered land-value intents inside the detached minute. The bundled
traffic notification has no active source subscriber, so there is no traffic
counter, development subscription or bus model to add. Save11/protocol11/City1
and prepared content identities remain unchanged.

## Source bindings and consumers

Authority is stable ExAV 2.1.1.12, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`:

- `core/world/rail/Station.cs:342–385`: unloading calls the train first, updates
  imports/calls, deposits `min(Single(lastWeekImportsPerDay / 24), passengers)`,
  and notifies transport only for nonzero passengers. Positive-demand boarding
  retains checked integer products, Single amenity/30% multiplication and packed
  capacity. It deposits the loaded count before the train/station notifications.
  Zero demand returns before loading/depositing; depleted positive demand still
  invokes loading and emits a zero deposit.
- `core/world/development/LandValue.cs`: ordered Single `amount * frequency / 4`
  additions, the unshifted deposit cell, shifted public read and retained buffers
  remain the delivered CITY-D001 contract in `CityHeat`.
- `core/world/rail/GlobalTrafficMonitor.cs:16–21`: the sole delegate is invoked
  only for nonzero transport. A recursive search of all stable `trunk` C# finds
  its only proposed subscription at
  `core/world/development/DevelopmentAlgorithm.cs:59`, commented out. There is no
  reachable bundled transport subscriber. CITY-D016 preserves that disposition;
  later development reads actual station statistics through its separate contract.

`StationPassengerFeedback.Apply` consumes each newly returned
`TrainCallbackResult.PassengerEffects` in array order from `MixedSessionState.TickTrain`.
The validated train/station/fare/movement candidate is installed on the detached
session before consumption. Each positive land effect calls `CityHeat.Deposit`,
which refreshes matching real provider/owner land caps through `CityPopulation`.
The next scheduled callback sees the resulting field and provider metadata.
No callbacks are deferred until after publication, grouped by station, sorted by
train ID or summed into one deposit. Train/station notifications already have
their state in the candidate. Transport retains its typed notification with the
explicit empty bundled consumer set; no external or process-global callback is
invoked. Departure bells keep their separate transient audio publication.

For an older canonical railway-only world without an activated city field, the
first positive deposit creates the existing empty city field and its relative
600-minute timer on the same detached clock. A prospective heat binding allows
this without replacing an advancing clock. Exactly one timer is created; existing
city fields/timers are reused. This extends P7-06's explicit lazy activation
adaptation; activation does not scan historical terrain. Zero deposits do not
activate or revise an unused field. Controlled P6 supply retains `controlled`
provenance and never becomes a city producer.

## Atomicity, persistence and observations

Every field/metadata mutation is an immutable world successor. Session revision
accounts for these successors, including multiple callbacks and lazy activation.
Overflow, invalid finite/power results, callback failures and queue failures
abandon the entire detached minute: heat, both buffers, city/spatial owners,
station/fleet statistics and occupancy, cash/receipts, random streams and queue.
A failed minute retains the preceding successful transient observation, without
consuming it again.

`MixedSessionState.PassengerEffects` remains a last-successful-minute diagnostic
array. It is never polled as a consumption queue, serialized, transferred to a
peer or restored. Freeze/save/reopen do not replay it. Receipt retries return the
existing command result; reconnect and save replacement publish current heat.
There is no durable event history or added replay state.

The actual apartment/station/platform/train fixture completes a five-level
apartment naturally through production callbacks, removes the explicitly
controlled P6 provider and runs a purchased three-car train over constructed
rail/yards. Initial land zero caps real outgoing demand at 10. Amenity100 gives
three boarding passengers and a literal heat deposit **7.5**. The apartment's
shifted read samples that cell: land becomes **10**, and station demand becomes
**20**, preserving both catchment directions. A second station with the explicit
weekly import operand25 receives three passengers after actual travel. The source
Single cast/cap yields **1.0416666**, then heat **2.6041665**. Its transport intent
occurs once; the empty bundled subscriber set changes no traffic state. Its
zero-supply departure emits no land intent. The weekly operand is a controlled
statistics input, not a claimed simulated prior week.

EN/JA retain the delivered city population/value inspector and station demand/
waiting information. No new control or untranslated string is needed for this
simulation integration. MAN-A06/MAN-A16/MAN-E10, DEV-A05 and EXAV-06/07
`providers-feedback` obligations now bind `StationPassengerFeedback`,
`MixedSessionState`, `CityHeat` and the P7-13 inspection consumers.

## Focused verification

Fresh selections pass **175 C#, 46 Deno and seven unique Chromium workflows**,
with zero failures, skips or retries in those successful selections.
Commands, logs and hashes are recorded in `tmp/p7-14-validation.json`. Only
relevant C#, Deno and Chromium selections run; complete suites are not run.
C# covers literal boarding/unload heat and source arithmetic, two actual yard
callbacks in FIFO order (including second-callback rejection), sequential Single
rounding, lazy activation, real provider refresh, exact before/after reopen and
rollback of arithmetic, revision, random, queue, accounts and transients.
Existing passenger exchange/service, city heat/population, successor validation,
yard/controller/bell and station-information cases retain their scope. The
expensive P6-25 enumeration of every checkpoint boundary is excluded from the
final selection (an initial broader selection was stopped); the new
journeys exercise the relevant effect boundaries and real process restarts.

Deno applies actual C# deltas to immutable predecessors and checks exact heat,
provider metadata, both listener directions, duplicate rejection, malformed
buffers and absence of transient history, alongside city/station/mixed/audio and
fixture-selection contracts. Chromium exercises EN desktop/JA phone peers,
authority rejection, literal heat and exact saved bytes, receipt retry, reconnect,
replacement, rejected minutes, and real process restart before and after effects.
Existing EN/JA constructed service, station-information midnight and heat/restart
workflows are included.

The station-information midnight golden is refreshed because its actual journey
now deposits heat. Browser service assertions resolve the existing compact heat
references before comparing peer publications to full snapshots. Initial fixture
refresh mismatches and browser assertion/setup failures are corrected in the
final selections; no retry-dependent success supplies credit.
Native/optional/full-city/performance and overall P7 acceptance remain open.
