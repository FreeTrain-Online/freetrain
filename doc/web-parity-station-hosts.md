# P6-11 station host discovery and graph changes — 9 November 2026

Platforms now discover actual constructed station hosts and connect automatically
at construction. Detached domain operations support explicit connected-platform
parents, recursive disconnection and reparenting. The connection editor remains
P6-12; lane editing and passenger/controller/departure effects retain their later
P6 owners. Save10/protocol8 already persist the complete graph and remain unchanged.

## Discovery and automatic selection

[StationHostConnections](../src/FreeTrain.Online.Simulation/StationHostConnections.cs)
ports `Platform.listHosts(8)`, `otherEnd` and the host setter from stable ExAV
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, using the
[P6-01 contract](web-parity-station-contract.md#host-selection-and-listener-graph-decisions)
and [P6-02 literal fixtures](parity/station-fixtures.json). The original
`Platform.cs`, `Station.cs`, `ThinPlatform.cs` and `FatPlatform.cs` were inspected
at that Git revision; the independent fixture and source-review checkers also
verify their pinned source bytes.

Membership is the union of the inclusive range8 X/Y rectangle between the base
and `otherEnd(L)`, at platform z±1, and the L actual center-column coordinates
across every valid world height. `otherEnd` uses L rather than L−1, but is not an
extra full-height column. Any occupied station structure member can qualify,
including one whose anchor is outside the rectangle. Iterating actual validated
members implements the same membership without scanning empty cells.

`Platform.get` discovers thin platforms through their yard Traffic cells and wide
platforms through their opaque center strip. A wide yard alone does **not** return
its platform in the source: `ThinPlatform.get` checks its own nested
`RailRoadImpl`, and `FatPlatform.get` checks center voxels. Discovery preserves
that distinction even though browser picks intentionally resolve either wide
component to its logical platform identity. Self, disconnected platforms,
decorative station-like accessories and cosmetic passages cannot be candidates.

Candidate results are unique, in numeric stable-ID order, and retain the direct
host ID/revision/location plus the actual resolved Station ID/revision/location.
Automatic construction selects only a **direct Station**. STATION-D002 explicitly
replaces source Hashtable enumeration with stable station-ID order; this is no
nearest-station guarantee. Thin and wide construction publish the chosen host,
ordered reciprocal child and changed station spatial revision in the same
detached successor. New platform revision remains zero. Preview is pure; existing
construction epoch/content/geometry/minute/price, role and receipt checks apply.

## Graph changes and continuation

`Reparent` operates on private dictionaries over an already validated StationGraph.
It detaches the old parent, clears the platform, walks a copied ordered child list
iteratively, disconnects all descendants, then checks the requested target's
remaining reachability. A connected platform is an explicit valid parent. Choosing
a descendant first disconnects it, so the target loses station reachability and
the whole affected subtree remains disconnected. Children never follow the new
parent automatically. Same-host assignment returns the original graph and, through
the domain preview/commit, the original session without revision or timer changes.
Self and unknown IDs reject; graph construction continues to reject dangling,
nonreciprocal, unresolved and cyclic inputs before any world publication.

The world-level preview checks current platform and selected host revisions and
candidate membership. Commit requires the identical source world. Changed existing
identities increment once, wide/station spatial owners receive matching revisions,
and the existing station lifecycle retains timer registrations, FIFO order and
accounts. Reparenting adds no account transaction, passenger/log mutation,
controller call or departure event. The existing removal paths disconnect actual
descendants and cancel only the removed owner's timers. Removal, rebuilding a
station at the same location and later construction do not rehost existing
disconnected platforms. Newly constructed platforms make their own direct-station
choice using current identities.

EN/JA construction help now explains automatic connections and distinguishes
unconnected pass-through from pending connection editing and passenger service.
This wording is a browser help adaptation of the pinned source behavior; no new
content assets, translations of contribution names, credits or dependencies were
introduced. The independent platform name/bell/host editor remains P6-12.

## Verification

Fresh final checks on 9 November 2026:

- **1,034/1,034 relevant Release .NET tests** pass, including all17 new host
  discovery/graph tests and existing station/platform, mixed-world, fleet, train
  and harbor regressions. No skips.
- **355/355 web units** pass (353 ordinary plus two retention).
- **19/19 selected Chromium scenarios** pass without skips, including both new
  EN/JA constructed-host journeys and the existing station construction/name,
  transport, rendering, thin/wide platform, authority and retained-track workflows.
  The final English/Japanese host captures were inspected.
- **28/28 station Python tests**, all **821 independent literal fixtures** and
  raw pinned-source checks pass. Generated contracts/content, EN/JA localization,
  Svelte checks, production build, project boundaries and whitespace checks pass.
  Svelte reports zero errors/warnings; the existing Vite bundle-size advisory
  remains.

The new domain tests consume all four literal range fixtures and eight graph
fixtures. They construct actual station geometry for the boundary probes, build
thin/wide platforms in every heading, distinguish stable ID from distance, test
full-height top-level links and the excluded extra column, find a structure by a
non-anchor member, reject a wide-yard-only candidate, explicitly connect real
platform chains, reparent to another station and to a descendant, and remove/
rebuild both a station and a wide parent. Stale identities and stale plans preserve
the original save; unchanged accounts, queues, random streams and service counters
are checked. Current-format reopen and uninterrupted successors are byte-identical.

The new web unit exercises reciprocal reparent/disconnect deltas, required logical
and spatial revisions, unchanged queues/accounts and immutable previous snapshots.
Two new Chromium journeys use EN desktop and JA430×860 actual touch, construct
three real thin/wide/vertical connections, observe them on a second client, verify
numeric direct-station choice and exact HTTP reopen/backend checkpoint restart,
then remove/rebuild the host and prove that existing platforms remain disconnected.
A new platform selects a currently available station, and no departure event or
passenger counter effect is emitted. These use the normal platform UI and actual
station construction commands, not injected fixture worlds.

The localized help made a retained-track target fall underneath a control in two
existing narrow touch journeys. Their shared test helper now checks actual
`elementFromPoint` canvas exposure and pans through ordinary map input before the
real tap. It preserves the coordinate/height/retained-owner assertions.

```sh
dotnet test tests/FreeTrain.Online.Tests -c Release --no-restore --filter 'FullyQualifiedName~Station|FullyQualifiedName~Platform|FullyQualifiedName~Mixed|FullyQualifiedName~Fleet|FullyQualifiedName~Train|FullyQualifiedName~Harbor'
DENO_JOBS=4 deno task --cwd web test
deno task --cwd web build
deno task --cwd web test:browser:run station-hosts.spec.ts thin-platform.spec.ts wide-platform.spec.ts station-building.spec.ts station-building-domain.spec.ts station-transport.spec.ts station-rendering.spec.ts
python3 -m unittest discover -s scripts/tests -p 'test_station*.py'
python3 scripts/station-fixtures.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/review-stations.py --check --source-root /Users/dmark/dev/freetrain-exav-archive
python3 scripts/check-project-boundaries.py
git diff --check
```

Logs: `tmp/p6-11-dotnet-final.log`, `tmp/p6-11-web-unit-final.log`,
`tmp/p6-11-web-build-final.log`, `tmp/p6-11-browser-final.log`,
`tmp/p6-11-{python,fixtures,review,boundaries}.log`. Final browser captures are
under `tmp/p6-11-browser-final/`.

The initial browser run passed15/19; two new test failures assumed a mixed graph
before the first construction, and two existing touch tests hit an obscuring
control after the help text changed. Earlier domain iterations corrected test
geometry and immutable-array comparison. Those runs do not replace final counts.

Native/original-runtime/human fidelity, other browser engines, optional content,
full-suite/capacity/soak, Linux/container and deployment acceptance are not credited.
The relevant .NET selection includes existing fleet capacity regression cases;
passing those tests is not a new capacity measurement or P6-26 acceptance.
