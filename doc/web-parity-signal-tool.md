# P4-16 signal infrastructure — 1 November 2026

The **Signals / 信号** task (**F**) delivers the two bundled signal contributions
through point picking or XY entry, automatic/manual height, authoritative detached
preview, confirmation and canonical Save8/protocol6 persistence. **I** remains
Inspect. Train/controller gameplay remains P5, passenger behavior P6.

## Source and construction

Behavior authority is stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, under
`trunk/plugins/org.kohsuke.freetrain.rail.signal/src/`:

| File | SHA-256 |
| --- | --- |
| `SignalRailController.cs` | `e7b3f72185f50c8a4b8eb6af1d9721a2e182445df33ee125237d5e46137cc855` |
| `SignalRailRoad.cs` | `7420c4057922bf5fb4e39d92e09aed7322a1251ef53901fbd4aac3888b527934` |
| `RailSignalContribution.cs` | `ab5abac8f359966a3ee3a5b9b1e07c6f8b5921b3206f9a1751c320d3f7c3555d` |

The [railway contract](web-parity-railway-contract.md#signals-poles-and-other-accessories),
RAIL-D005/011 and independent P4-02 signal vectors are the implementing inputs.
[SignalRailwayConstruction](../src/FreeTrain.Online.Simulation/SignalRailwayConstruction.cs)
accepts vacant cells and Traffic with no rail or ordinary SingleRailRoad, including
curves and tracks on a different axis. It replaces the track with a cardinal
straight signal pattern: N/S = 10, E/W = 4. Junction, slope, bridge, girder, tunnel,
garage and existing signal subtypes reject placement. Non-Traffic obstacles are
never reclaimed. A stationary car rejects both construction and removal under
the reviewed service boundary. Existing Traffic components, owner IDs and allocator
remain intact; new Traffic allocates exactly one owner. Neither operation elects
or removes piers, changes terrain or books money, even with a full account journal.

The type choices are Three-aspect signal / 三灯式
`{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}` (left side) and Shunting signal / 入替信号
`{6FA55842-E693-414f-A120-0A3D0C613B21}` (right side), each facing 0/2/4/6.
Removal requires a signal at that point and restores ordinary rail with the
signal's existing pattern, independently of the currently selected type/direction.
Changing a signal requires removal followed by placement; direct signal-to-signal
replacement is rejected by the source SingleRailRoad eligibility rule.

The shared railway request adds bounded signal type/direction selectors and a
`signal` infrastructure discriminator; its endpoints must identify the same point.
Other railway tools retain their route requirements. Client preview validation
requires exactly one cell for a legal signal quote. Type/direction participate in
request identity, so a late response cannot authorize another choice. Existing
role, revision/content/epoch, connection and uncertain-receipt fences apply.
Draft preview/cancel never allocates, charges or pauses the world.

## Rendering, localization and P5/P6 handoff

Live rendering uses the existing executable recipes: four `[32*i,0,32,24]` crops
with anchor `[0,8]`, plus the selected normal pattern sprite. **GEO-007** follows
actual dispatch: left-side directions 0/6 draw after the track and 2/4 before;
right-side directions 0/6 draw before and 2/4 after. Alpha picking targets the
signal definition and durable Traffic owner for both signal and track art.
Height cuts and all eight season/light variants retain the same pipeline.

`SignalControllerReference` exposes the durable harbor owner separately from the
nullable saved shared `ControllerId`. Placement leaves that ID unassigned; P4
does not fabricate a train controller. Existing assigned references round-trip
exactly through saves. Removal drops only the signal controller link and restores
normal rail. Future mutations must use the shared geometry/owner revision fences.
`DelegatesStopTime` publishes the source circular angle gate (within one eighth
turn, including wraparound). Actual train stop-time delegation, waiting state,
controller assignment/editing, moving-car protection and continuation belong to
P5. TrainHarbor does not imply a passenger platform: runtime no-passenger-exchange
checks remain P6. Optional automatic-block and monitoring plugins receive no
bundled behavior credit.

RAIL-TEXT-072/075 map the signal menu/window to `signalTask`; 073/074 use the
shared lay/remove controls, 076 maps to `railObstacle`, 077 to `signalTarget`,
and 078 to `railCar`. The Japanese contribution names remain 三灯式 / 入替信号;
English uses Three-aspect signal / Shunting signal. The source review JSON retains
its original specification status; this report records implementation dispositions.
New browser instructions, direction names, point/removal diagnostics and the P5
qualification ship in both app locales and the existing Help destination.

## Verification

- Full Release .NET **1,006/1,006** passes (`tmp/p4-16-tests-final2.log`).
  Subsequent focused signal verification adds dependency and full-journal/controller
  save cases: **8/8** passes (`tmp/p4-16-signal-final.log`). Coverage includes
  both types, all four directions, every ordinary pattern, independent literal
  eligibility/car vectors, special subtypes, actual Mountain/tunnel/slope owners,
  protected removal, no booking, stable IDs, plan substitution, heading wraparound,
  observer/stale/input fences and exact save/reopen continuation.
- Client **203/203** passes (`tmp/p4-16-client-final.log`), including one-cell
  preview validation, selector identity and signal crops/picking/cuts in all eight
  season/light variants. Existing GEO-007 tests cover both types' literal pass order.
- Python **162 checks, one skip** passes (`tmp/p4-16-python.log`); source review,
  **1,484** independent railway fixtures and project boundaries pass.
- Production build, generated contracts/content, locales and Svelte checks pass
  (`tmp/p4-16-build.log`). The existing large-chunk advisory remains.
- Focused Chromium **3/3** passes (`tmp/p4-16-browser2.log`): EN/JA type/direction
  controls, detached preview, rejection of an existing signal, ordinary restoration,
  two-peer publication, exact HTTP save/reopen, F/Escape, map point selection,
  Observer and actual 430×850 layout. Inspected EN/JA and narrow captures:
  `tmp/browser-evidence-yAQmLc/signal/`.

Final shared Release railway Chromium regression **51/51** passes
(`tmp/p4-16-browser-regression.log`, `tmp/browser-evidence-AF0lNr/`), including
signals **3/3** with exact source-pixel samples for both types/all four directions
in EN/JA, plus existing normal/bridge/girder/garage/tunnel/slope construction,
object removal and live rendering workflows. Native/human pixel identity,
integrated P4-20–24 cross-tool/network/capacity/release acceptance and actual
P5/P6 gameplay remain open.
