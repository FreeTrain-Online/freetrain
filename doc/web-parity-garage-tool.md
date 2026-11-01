# P4-15 garage infrastructure — 1 November 2026

The **Train garage / 機関庫** task (**H**) delivers bundled contribution
`{93481807-D998-46d7-B1BE-CB2B5D086D67}` through the existing map, authoritative
route preview, confirmation and canonical Save8/protocol6 transport. The terrain
shortcut remains G. [GarageRailwayConstruction](../src/FreeTrain.Online.Simulation/GarageRailwayConstruction.cs)
is derived from stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`,
`trunk/plugins/org.kohsuke.freetrain.rail.garage/src/TrainGarage.cs`, SHA-256
`1505ec0b2283f13116a7b5276e859235389f5c11c67344fb76eee7e259578c76`.
The pinned [source contract](web-parity-railway-contract.md), RAIL-D002–005/010/013,
and independent P4-02 GARAGE vectors remain the behavior inputs.

## Construction, removal and booking

The shared source cardinal snap retains the first point's Z and selects X on
an equal-distance tie. Distinct snapped endpoints and world/checked-coordinate/
8,192-route-cell limits apply. Every inclusive build cell must equal its actual
ground level. Empty cells are allowed; occupied cells must contain rail with both
ports on the selected axis. Wrong-axis rail, bare scenery and traffic without rail
reject the complete candidate. The RAIL-D005 service boundary rejects build over
any car. Every selected cell becomes garage rail, including existing compatible
straight, junction, bridge/girder, signal and garage rail. Existing Traffic
components and owner IDs survive, changed owners advance revision, and empty
cells allocate new IDs. The inherited immutable graph checks remain authoritative.

Tunnel backup and slope ownership cannot be discarded by component replacement:
these targets reject with explicit bilingual instructions to remove them with
their dedicated tool first. Full cross-tool dependencies remain P4-20; this slice
does not claim tunnel-to-garage terrain conversion. No piers are elected or torn
down. Garage removal preserves attached components and changes only the rail.

Build spends **6,500,000 per inclusive cell**, even replacement of a garage.
Removal visits **[from,to)** and converts matching-axis garage rail to ordinary
straight rail. A car protects conversion; it **does not** protect billing. Each
matching visited garage adds **200,000** to a subtotal, then **every visited cell**
spends that subtotal, including zero and nonmatching cells. For five garages,
forward removal records 200,000 / 400,000 / 600,000 / 800,000 and leaves the last
garage. Reversing that same route records 200,000 four times, totaling 800,000.
All source spend calls retain ordered RailService journal entries within one
atomic revision. Both preview and commit reserve the complete charge-entry count
against the 8,192 journal budget. A full journal rejects without partial conversion
or expenditure. No-op/coincident selection is rejected in the established UI.

The persistent renderer dispatches the exact two source crops per axis:
background `[32*i,0,32,27]`, foreground `[32*i+64,0,32,27]`, anchor `[0,11]`.
X uses i=0, Y uses i=1. Rail-before/rail-after passes, alpha picking, height cuts
and all seasonal/night variants use the existing executable recipe pipeline.
There is no extra ordinary rail sprite beneath the garage.

## Browser, localization and P5 boundary

The task supports coordinate entry, map picks, automatic/manual height, lay/remove,
exact server cost/protected-cell preview, cancel/Escape, role checks and stale
world/revision/content fences. EN/JA help explains actual endpoints and cumulative
billing; shared time and the map/camera survive the workflow. Save/reopen retains
rails, component/owner IDs, allocator, revisions and each booking entry; continuation
matches uninterrupted execution.

RAIL-TEXT-070 maps 機関庫 / historical “Train Garage” to `garageTask`.
RAIL-TEXT-071's decorative qualification maps to `garageDescription` and
`garageRules`; the reviewed English deliberately does not promise maintenance
from descriptive wording. The added route/removal/help/capacity wording is new
browser instruction, shipped in both app locale files. The P4-01 source review
JSON retains its historical specification status, as required by its validator;
this report supplies the runtime/translation disposition.

GarageRail inherits SpecialPurposeRailRoad's straight guide: it retains the
occupying car's direction and refuses new off-axis attachments. Removal explicitly
checks any car before replacing rail. These are P5 interface constraints, exercised
with canonical stationary occupancy fixtures only. There is **no reachable garage
maintenance, repair, fueling, storage management or running-train mechanic** in the
reviewed contribution. Moving cars/controllers, native/human pixel identity,
P4-20 cross-tool integration and P4-21–24 full-network/capacity/release acceptance
retain their later owners.

## Verification

- Release .NET **1,000/1,000** passes (`tmp/p4-15-full-final.log`), including
  the independent literal garage inputs, four axes and multiple lengths, ground/
  compatibility rejection, protected-car/gap/zero-spend ordering, prepared-plan
  substitution, journal admission, observer/stale fences and exact saved continuation.
  The focused garage filter passes **10/10** (`tmp/p4-15-garage-final.log`).
- Client **200/200** passes (`tmp/p4-15-client-final.log`). The railway renderer
  includes both literal garage crop pairs, anchors, alpha picking and cuts in
  all eight season/light variants; focused renderer **17/17** passes.
- Python **162 checks, one skipped** passes (`tmp/p4-15-python.log`); source review,
  all **1,484** independent railway fixtures, project boundaries and whitespace pass.
- Production web build and generated/content/locales/Svelte checks pass
  (`tmp/p4-15-web-build.log`); Release server build has zero warnings/errors.
- Garage Chromium **3/3** passes within the shared railway regression: four axes
  in EN/JA, non-ground rejection, detached preview/cancel, exact pixels, two peers,
  ordered half-open removal charges, upload/reopen, mixed normal/empty targets,
  map picks, H/Escape, stable canvas and observer controls. An additional real
  **430×850** narrow-layout run passes **1/1**
  (`tmp/p4-15-browser-narrow.log`, `tmp/browser-evidence-fWDfmC/garage/`).
  Inspected EN/JA axis captures are in `tmp/browser-evidence-xLQqH1/garage/`;
  the narrow capture shows the actual responsive layout and reachable confirmation.

The final shared Release railway Chromium regression passes **48/48**
(`tmp/p4-15-browser-regression.log`, `tmp/browser-evidence-xLQqH1/`), covering
normal/bridge/girder/garage/tunnel/slope construction, object removal and live
railway rendering. The final 430-pixel variant of the garage interaction scenario
was also run separately and passes as recorded above.
