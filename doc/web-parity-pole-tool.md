# P4-17 electric poles — 1 November 2026

The **Electric poles / 架線柱** task (**E**) delivers all four bundled pole
contributions through point picking or XY entry, automatic/manual height,
authoritative preview and confirmation. Poles are visual accessories; no train
power, wiring network or simulated electrification is introduced.

## Source and construction

Behavior authority is stable ExAV 2.1.1.12, archive
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, under
`trunk/plugins/org.kohsuke.freetrain.rail.electricPole/src/`:

| File | Pinned raw SHA-256 |
| --- | --- |
| `ElectricPoleContribution.cs` | `3840f1b27b04a4c1bea77caa0f96b35b48c9d91c47152eb41af5eef8a762d57f` |
| `ElectricPole.cs` | `8465edfc5d97bc4b4b28f2f427770b99942022d61723d2d0007d394e36de48a0` |
| `BuilderImpl.cs` | `3ef6096b427f78d66ff593c54129810cdec73bd24cf0c75100db33528be9e890` |
| `RemoverImpl.cs` | `d325de0014dc288f69e0037f28182e66f9c46d82211c6b8883dc9053fc68e62a` |
| `MenuContributionImpl.cs` | `802596b91b51e5345c616e0251f05c45f4a3c4f36c8762cd8b1c5522bd12049c` |

The [railway contract](web-parity-railway-contract.md#signals-poles-and-other-accessories),
RAIL-D011, TrafficVoxel accessory setter and independent P4-02 pole vectors are
the implementation inputs. [PoleRailwayConstruction](../src/FreeTrain.Online.Simulation/PoleRailwayConstruction.cs)
requires exactly two opposite cardinal rail ports (patterns 4 and 10), independent
of subtype name. Ordinary rail, cardinal girder, bridge, garage, tunnel and signal
rail qualify; curves, diagonal rail, junctions, slopes, empty Traffic and
non-Traffic obstacles reject placement. No terrain reclamation, support election
or support teardown occurs. N/S uses saved accessory axis 1, E/W axis 0. Both
server and browser reject other pole axis values before exposure; the prior
slope component-preservation fixture now uses an actual E/W axis rather than a
heading value.

| English / Japanese selector | Contribution ID |
| --- | --- |
| Double pole (1) / 両柱(1) | `{DDF7F835-8100-45fd-8AE7-EC498796D387}` |
| Double pole (2) / 両柱(2) | `{DD7D4548-5E0E-48e4-AB6E-64A443C240F7}` |
| Single pole (1) / 片柱(1) | `{86E56A60-19DF-4ba7-BFE8-ABFBDF5DFA0A}` |
| Single pole (2) / 片柱(2) | `{FD1635C1-05BF-41af-9185-4891B983E2A4}` |

Placement replaces the single accessory slot, including a different pole or a
crossing. Source non-null replacement does not invoke the old accessory's
`onRemoved`; pole `onRemoved` itself is empty. Removal clears any ElectricPole
regardless of selected type, preserving rail, road and car components without
regenerating a crossing. A car does not exclude either operation. Replaced or
removed accessories are explicitly listed in the loss preview. Traffic owner,
allocator, rail recipe/pattern/backup/controller, terrain and supports remain
intact. Removing the last pole from otherwise empty Traffic removes its voxel
and spatial owner. Normal rail detachment can leave the pole behind; explicit
pole removal clears that remaining attachment. Elevated orphan poles can be
addressed with manual height. Removal at a point without a pole is reported as
an unavailable action, adapting the native remover's silent no-op.

Both operations book **zero**, with no expenditure records, including when the
account journal is full. The native cost display and inherited price-capable
contribution do not imply a spend caller. Preview/cancel preserves exact save
bytes; confirmation revalidates current geometry/content/epoch and authorization.
The shared railway command adds a bounded `poleType` and `pole` discriminator;
point endpoints must agree. Preview identity includes the type, and legal point
previews require exactly one cell. HTTP preview and WebSocket commit both bind
the operation. Existing stale-response, Observer, receipt and uncertain-outcome
fences apply. Save8/protocol6 remain unchanged; no new saved fields are needed.

## Rendering, picking and localization

The existing exact pole recipes draw two 32×32 crops, anchor `[0,16]`: background
at `axis*32` and foreground at `axis*32+64`. Live dispatch places accessory-before
before rail-before and accessory-after after rail-after, reserving the car pass
for P5. Season/light variants, height cuts, occlusion and alpha-mask inspection
use the persistent mixed-world renderer. Picks expose the pole's qualified
recipe, component and durable Traffic owner separately from rail art.

View provides **Show electric poles / 架線柱を表示**, an affirmative adaptation
of the source hide-poles menu. It hides both passes and their visible-pixel picks
locally, preserving the saved world, costs, placement/removal legality and other
clients' presentation. The presentation/cache identity includes this setting;
turning poles back on restores the same pixels. Like the other View switches,
it is local to the current view.

RAIL-TEXT-063 maps to `poleTarget`; 064 uses the shared authoritative cost display;
065/066 use shared lay/remove controls; 067/068 map to `poleTask`; 069 maps to
`showPoles` with positive checkbox semantics. Bundled Japanese type names are
retained, with reviewed English Double pole / Single pole labels. Point help,
replacement/car behavior, zero costs, non-electrification qualification, removal
and diagnostics ship in both app locales and Help. Native temporary menu/window
placement adapts to the existing tool strip and View panel. Source review JSON
retains its specification status; this report records shipped dispositions.

MAN-M03/A12 and DEV-P06/P10 map to this task's catalog, point controllers,
pattern-derived axis and exact source sprite dispatch. Trusted car/road/crossing
fixtures verify the infrastructure boundary; actual moving trains, road gameplay
and integrated cross-tool acceptance retain P5/P7/P4-20 ownership.

## Verification

- Full Release .NET **1,015/1,015** passes (`tmp/p4-17-tests-final5.log`).
  Coverage includes all four types/axes, independent eligibility vectors and all
  28 normal/junction patterns, special subtypes, actual slope/Mountain obstruction,
  diagonal girder rejection, car/road/crossing coexistence and replacement,
  normal-rail detachment and orphan cleanup, zero booking with a full journal,
  exact save/reopen continuation, malformed axis rejection, plan substitution,
  stale/Observer/input fences and point preview immutability.
- Client **207/207** passes (`tmp/p4-17-client-final2.log`), including type-specific
  preview identity, one-cell validation, both axes/all types in eight season/light
  variants, exact crops/pass order/anchors, accessory recipe/owner picks, cuts,
  malformed axis rejection and hide/show restoration without snapshot mutation.
- Python **162 checks, one skip** passes (`tmp/p4-17-python.log`). Default and
  explicit pinned-archive source review and **1,484** independent railway fixtures
  pass (`tmp/p4-17-source.log`); project boundaries and whitespace pass.
- Production build, generated contracts/content, locales and Svelte checks pass
  (`tmp/p4-17-build-final.log`). The existing large-chunk advisory remains.
- Initial focused Chromium **3/3** passes (`tmp/p4-17-browser4.log`), covering
  EN/JA, all four type choices/four approach directions, actual source pixels,
  detached preview, replacement, removal, HTTP save/reopen, peer publication,
  E/Escape, map point selection, Observer and actual 430×850 controls. Captures
  were visually inspected in `tmp/browser-evidence-SieN3P/pole/`.

Final shared Release Chromium regression **54/54** passes, including poles
**3/3** with the source visibility option and unchanged saves across hide/show
(`tmp/p4-17-browser-final.log`). It also covers normal track, bridge, girder,
garage, signal, slope, tunnel, object removal and railway rendering. Final EN/JA
pole captures in `tmp/browser-evidence-NHaFnJ/pole/` were visually inspected;
the 430×850 capture retains the fixed cost/confirmation controls.
Native/human pixel identity and integrated P4-20–24 network/capacity/release gates
remain open; optional plugins receive no new bundled behavior credit.
