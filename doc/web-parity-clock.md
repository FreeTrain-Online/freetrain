# P1-02: clock, calendar and scheduler — 22 October 2026

The canonical reference world now runs the stable ExAV source-minute clock.
Run/Pause/Step are authoritative Owner/Operator actions; Observers see the same
confirmed time. Local panels, targeting, focus and input resets do not pause shared
time. The existing disconnect/shutdown safety pause remains in effect.

## Source bindings and calendar

Stable ExAV 2.1.1.12, archive `883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/core/world/{Time,TimeLength,Clock}.cs` and `trunk/core/util/PriorityQueue.cs`
are the authority. [Hashed review and explicit decisions](parity/clock-review.json)
record provenance, translations and remaining scope. This is source-derived
verification, without a desktop-runtime comparison.

[GameTime](../src/FreeTrain.Online.Simulation/GameTime.cs) uses absolute integer
minutes. Start is **130080**, year 1, April 1, Saturday, 08:00. A year always has
365 days; February always has 28. Weekday is absolute days modulo seven, Sunday
zero. Day is [06:00,18:00); seasons start in March, June, September and December.
The source's fixed holiday table and school-vacation predicates are preserved,
including the absence of substitute holidays. These are game rules, not current
civil-calendar holidays. Midnight's UntilTomorrow is 1440 minutes.

The calendar accepts zero through **1128717404863199** absolute minutes, keeping
year numbers representable as Int32. Checked arithmetic rejects overflow. The
live clock pauses at that upper boundary; a step there is rejected. No civil
calendar, timezone, wall-clock date or prototype DateTime enters these rules.
English/Japanese presentation uses the source's ten-minute display truncation;
each step still advances exactly one minute. Narrow layouts expose the date and
Step in the Game menu. Exact source minutes remain in authoritative snapshots and
saves even when several successive steps share the same display label.

## Callback contract and decisions

[Clock](../src/FreeTrain.Online.Simulation/Clock.cs) owns its queue and an immutable
copy of its explicit named callback bindings. No singleton, global world, static
queue, serialized delegate, reflection activation or wall timer is used.

1. Increment one source minute.
2. Notify daylight invalidation at exactly 06:00/18:00.
3. Dispatch due actions by due minute, then ascending insertion order.
4. Detach and clear the end-of-turn batch, then invoke it in registration order.

A one-shot requires a future absolute time or positive relative delay. Repeated
registration requires positive first delay and interval, with the first delay
optionally defaulting to the interval. Repeated actions reschedule **after** their
callback returns, so registrations made inside that callback precede its next
occurrence at equal time. Repetition retains its cancellation cookie while each
new insertion receives a new ordering value. UnregisterOneShots removes all
pending one-shots with the same handler identity; repeated wrappers have separate
cookies. A callback can cancel another due action before it runs. End-of-turn
handlers registered by due callbacks run this turn; those registered by an
end-of-turn handler run next turn.

**GEO-002 disposition:** remove the executing entry before invoking it. Cancelling
itself never deletes the next queue entry. Cancelling a repeated cookie from its
own callback suppresses rescheduling. This intentionally repairs the source's
unconditional removeMin-after-callback defect and its self-cancellation requeue.
Normal positive-duration order is preserved. Zero/past scheduling, negative
intervals, unbound actions, reentrant advancement and ordering overflow fail
explicitly. Queue state is limited to 65,536 pending actions. Handler exceptions
propagate and fault that clock; it cannot advance or be saved afterward. Recovery
requires a prior successful checkpoint, avoiding silent continuation or replay of
partially committed callbacks. Clock-range rejection happens before advancing.

## Contracts and continuation

The ruleset is now **reference-spatial-2**. Protocol remains **3** and the unchanged
asset-free pack/content hash remains `reference-spatial-content-1`. The profile
change makes the tick field's new meaning explicit: **absolute source minutes**,
rather than the previous neutral zero. Snapshot/delta revision advances for time
and pause changes; track revisions and geometry remain independent. A fresh
world starts paused. The existing adapter advances at most one minute per 50 ms
pump while running, with no unbounded catch-up. Source desktop speed choices and
unbounded fast mode remain MAN-T01/MAN-A01 work for P9; no new speed control is
claimed here.

[Save 3](../prototype/protocol/save-v3.schema.json) adds paused state and a clock
graph: absoluteMinute, nextOrder and pending named action records (registration,
order, due minute, handler ID, interval). Long values are canonical decimal
strings. The independent [sample](../prototype/protocol/saves/reference-v3.json)
shares the runtime contract. Standalone scheduler continuation serializes queue
values and rebinds them to the new world's callback table, preserving tie order
and repeat cookies without retaining old-world delegates. End-of-turn callbacks
are transient, matching the source's NonSerialized field.

The reference world currently registers **no gameplay callbacks**. Its Save 3
reader therefore rejects every nonempty action queue as unbound; later slices
must register their semantic actions and extend the strict save schema with those
subsystems. This is scheduler infrastructure, not delivery of train, growth,
economy, weather or population timers. No pending action is silently discarded.

Canonical **Save 2** receives an explicit, validated upgrade: preserve the rail
graph, initialize an empty clock at 130080 and pause. Writes always use Save 3.
Prototype Save 1 remains unsupported. Unknown rulesets, packs, clock fields,
invalid ranges, noncanonical numbers and unbound actions fail before world/epoch
replacement. Existing slot, upload/download and checkpoint paths use the same
codec and FIFO. Save 3 preserves time and pause across replacement/restart;
session revisions reset with the new epoch and are not durable game state.

## Workflow and evidence

MAN-T01/MAN-A01 now have confirmed pause/run and source-calendar presentation,
including narrow English/Japanese access and Observer enforcement; their complete
speed/menu equivalence remains P9. MAN-A16's shared-time/no-local-pause boundary is
exercised here. The developer manual has no separate clock API requirement; DEV-P28 remains the
voxel-boundary contract. The scheduler's portable authoring surface now has
explicit world-local named bindings and independently ordered callbacks; full
plugin registration, content dispatch and gameplay consumer acceptance remain
P1-04/P2/P5–P8.

[ClockTests](../tests/FreeTrain.Online.Tests/ClockTests.cs) contains independent
calendar/boundary vectors, all 365 holiday/vacation days in years 1 and 4, daylight /
FIFO / end-of-turn traces, cancellation, repeat-cookie identity, serialized
continuation, range/overflow/reentry/failure decisions, two-session isolation,
role checks, pause/reset behavior and Save 2/3 validation. Existing HTTP,
WebSocket, epoch replacement, rejection preservation and actual checkpoint restart
cases now exercise Save 3. Client cases cover both languages, leap-year avoidance,
source display precision, clock deltas and strict structural saves. Canonical
browser cases cover shared clock controls, Observer views, panels while running,
save continuation and narrow Japanese presentation.

Verification from the working tree based on `9db3707`:

- **191 .NET cases pass**; Release build has zero warnings/errors.
- **80 client cases pass**; production build, generated contracts/content and
  Svelte/TypeScript checks pass.
- **64 Chromium browser cases pass**: 10 canonical, 54 explicitly historical.
  English and narrow Japanese DPR 2 screenshots were visually inspected.
- **105 Python cases**, project boundaries, all five pinned source dependency
  hashes and `git diff --check` pass.

Commands: `dotnet build/test FreeTrain.Online.slnx -c Release`,
`deno task --cwd web test`, `deno task --cwd web build`,
`deno run -A npm:@playwright/test@1.64.1 test` from `web`,
`python3 -m unittest discover -s scripts/tests`, and
`python3 scripts/check-project-boundaries.py`. In this environment .NET uses
`--disable-build-servers -m:1`; the test runner/HTTP and browser checks need local
socket access. Chromium was installed under `/private/tmp/freetrain-p1-02-browsers`
and selected with `PLAYWRIGHT_BROWSERS_PATH`. Logs are `tmp/p1-02-*`; the final
backend log is `tmp/p1-02-tests-final.log`, full browser log
`tmp/p1-02-browser-all.log`. One earlier backend run hit a connection reset in the
existing oversized-upload HTTP test; subsequent complete runs passed. The first
browser attempt required installing the pinned browser. These are fresh working
tree results, not a committed-revision or physical-device certification.

P1 remains open for random streams and complete entity/footprint/pack contracts;
P2–P11, source speed UI, complete-city continuation and native/device acceptance
remain open.
