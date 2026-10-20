# P0-04 ExAV geometry dependency review

**Source review complete for the four agreed chains — 20 October 2026.**
Authority is stable ExAV **2.1.1.12**, archive Git
`883cf7d81cf49db25ff38cd3a25d0f140afe31a0`, `trunk/`. Reads use pinned Git
objects, including original manifest bytes, independently of working-tree edits.

The [maintained dependency evidence](parity/exav-geometry-dependency-review.json)
pins **63 files**, **14 caller/dependency findings** (`EXAV-DEP-001`–`014`) and
**33 acceptance case descriptions**. Together with the initial 56-file geometry
review, this covers **97 distinct files**. These are source expectations and
implementation handoffs, not executed C# tests or Windows captures.

The [geometry specification](web-parity-geometry.md) incorporates the corrections
below. The [generator](../scripts/review-exav-geometry.py) validates source hashes,
all four chains, endpoint membership and case/phase dispositions; it embeds this
review in [the behavior artifact](parity/exav-geometry-behavior.json) and includes
all reviewed files in its historical hash comparison. The discovery ledger remains
unchanged: this is semantic review evidence layered on its file inventory.

| Chain | Reviewed boundary | Implementing owner |
| --- | --- | --- |
| `EXAV-DEP-DRAW` | Contributions/resources → pictures/loaders → sprites → native crop/key/effect calls | P2, picking adaptations P3 |
| `EXAV-DEP-TERRAIN` | H/V ground and water → HoleVoxel/cut callers → mountain/slope/pier/tunnel/structure consumers → selection predicates | P2/P3/P4/P5 |
| `EXAV-DEP-HOST` | Host candidate scan → station/connected-platform identity → parent/child links → removal and yard stop eligibility | P4/P5/P6 |
| `EXAV-DEP-TIMING` | Bundled initialization → world-selected speed lists → controller results/mutations → central movement, timers and outside return | P1/P5/P9/P10 |

## Drawing and image selection

`EXAV-DEP-001`–`004` trace `PictureContribution`/`Contribution.loadPicture` and
`ResourceUtil` through `Picture`, `SurfaceLoaders`, sprite implementations,
`DirectDraw.cpp` and `AlphaBlender.cpp`.

- **Picture fallback is conditional.** Missing seasonal day uses spring day.
  Missing seasonal night generates night from that season's explicitly supplied
  day, otherwise reuses spring night. `getNightOverride` examines only the first
  nested override, so a later sibling night declaration is not automatically used.
  Authored night sheets bypass automatic darkening.
- **Images reload lazily.** The bitmap loader returns pixel `(0,0)` as color key;
  Picture installs it after loading. Sunrise/sunset, explicit clock changes,
  view-mode changes, new world and lost surfaces dirty/release pictures. A season
  boundary alone does not reset the picture cache; existing surfaces can retain
  the old season until another reset.
- **Night conversion has a format boundary.** Ordinary RGB channels divide by
  four, with three light markers mapping to bright colors. Native 555/565
  quantization differs from 24/32-bit. Unknown 16-bit mode returns `E_NOTIMPL`,
  which the managed loader ignores. Raw night crops establish authored artwork,
  not native automatic-night equivalence.
- **Crop/anchor formulas survive the wrapper.** Drawing subtracts the sprite
  offset, uses its crop origin and size, and clips source/destination together.
  Shape draws the keyed silhouette. Alpha calls a keyed half-blend; in 24/32-bit,
  each channel uses `floor(dst/2)+floor(src/2)`. It does not consume bitmap alpha.
  Mapped/hue sprites override ordinary draw only; inherited shape/alpha bypass
  those color transformations.
- **Alpha compositions mask overlaps first.** `AlphaBlendSpriteSet` masks cells
  at `xx<=x, yy>=y, zz>=z` using projected positions so shared pixels do not blend
  twice. Empty cells remain NullSprite. This differs from blending every voxel
  independently.

Two defects require explicit decisions in the implementing slice. The native
`Surface.HitTest` returns **equality with the color key**, despite its opaque-pixel
comment, and permits coordinates equal to Width/Height. `SimpleSprite.HitTest`
passes sheet coordinates without applying crop/anchor. No core/plugin C# consumer
was found beyond interfaces and implementations; the reviewed voxel picker does
not use this API. Separately, `DirectSprite.offset` recursively returns `offset`
instead of `_offset`. Do not turn either defect into a browser picking contract.
No correction is implicitly approved by this review.

## Ground, water, cuts and picking

`EXAV-DEP-005`–`007` trace the drawer into voxel and terrain consumers.

`shouldDrawGround` queries the voxel at surface z with `false` and the voxel at
z−1 with `true`, with top/bottom world guards. Slope filler/support flags are
computed from local ground at creation and stored; later terrain edits do not
recompute them. A mixed ramp can select underground/surface artwork per horizontal
position. These facts affect ground holes even when all sprite crops match.

The drawer passes `cutZ-z+1`, so the top included voxel receives **1**.
`MountainVoxel.draw` subtracts one and therefore draws its flat cap at the cut;
`BridgePierVoxel` does not subtract one, so its alpha-at-zero branch remains
unreachable from this drawer. A single global cut adjustment would change one
of these behaviors. Mountain trees draw after ground without using the adjusted
cut parameter. Station structures draw normal art only when the cut is above
their top, otherwise their base footprint gets the station's Gray silhouette.

Ground/water/underground chips remain distinct: winter alone chooses the winter
ground crop; full-height water uses a separately night-mapped shape color;
underwater ground and underground cut caps use separate sheet crops. Tunnel
removal restores four mountain corner heights, without restoring vegetation or
ownership. Tunnel invalidation suppresses concealed-car updates when a neighboring
tunnel exists on its S/W side.

Picking remains controller-dependent: rail-or-ground accepts rail or `z<=ground`,
ground accepts equality, same-level accepts the anchor height, and train placement
additionally checks rail/car ownership. Identical screen coordinates can resolve
to different z for different tools. Native HitTest findings do not alter this.

## Platform host connections

`EXAV-DEP-008`–`010` trace `Platform`, `Station`, thin/fat platforms, the properties
consumer and Hashtable-backed `Set`.

Range **8** searches an inclusive rectangle around `location` and `otherEnd` at
z±1, then full-height columns along the actual platform cells. `otherEnd` uses
`length`, not `length-1`. For `(10,10,2)`, east and length 4, the local rectangle
is x=2…22, y=2…18, z=1…3; full-height columns are x=10…13 at y=10. A station can
be found through any occupied structure voxel, not only its base location.

Candidate enumeration is unordered. The constructor selects the first **direct
Station** candidate; it does not promise nearest selection or auto-connect through
a nearby platform. The properties form can explicitly select a platform already
connected to a station. This narrows the earlier generic “nearby host” description.

Changing a host removes the old parent link, clears the host, recursively
disconnects a copied list of child platforms, then adopts the requested host only
if that host still resolves a station. Reparenting A from S1 to S2 leaves A's
children disconnected. Selecting A's descendant can disconnect that descendant
before the final station check and therefore fail adoption. The setter supplies
no general cycle validator.

Station removal updates the station collection first, notifies listeners,
disconnects platform children, then removes structure voxels. Thin/fat removal
restores normal rails; fat also removes platform voxels. The domain removal
methods rely on caller occupancy checks; the service must revalidate. A yard with
no resolved host returns zero immediately, so it performs no station stop,
boarding, alighting or departure bell callbacks. Disconnection does not search
for another nearby station automatically.

## Speed-limit lifecycle and train callbacks

`EXAV-DEP-011`–`014` trace the producer/consumer paths rather than treating
controller stop results as pure values.

The bundled speed-limit **menu contribution constructor** calls `SpeedLimit.init`.
That subscribes to `World.onNewWorld`. `World.setWorld` assigns the current world
before firing the event; the handler restores/creates its dictionary-rooted lists.
`getInstance` is not lazy. Missing contribution construction, or subscribing after
world selection, leaves null until a subsequent world event. Modern semantic
initialization must not depend on opening the menu. Existing manifest codebase/
deployment gaps retain the P0-02/P0-03 dispositions.

Positive station waits return before limit-based scheduling. Simple/manual/TAT
first waits are respectively **30/5/10 minutes**. TAT once-daily departure at the
current minute waits a full day. Advanced rules execute in stored order; later
waits poll the next ten-minute boundary rather than calling the unimplemented
`TimeMask.nextMatch`. `-999`/`-1000` feed the reviewed forwarding branches.

TAT `reverse2`/`rforwarding2` invoke `checkTurn`, which **mutates the train before
returning**: it reverses immediately, may return early without undoing that
mutation, and can remove/re-place individual cars without unregistering the train
callback. The central handler then processes its returned negative value. The
captured old head can consequently differ from the current head. P5 must specify
these composition/reversal cases explicitly; a pure stop-duration mock misses them.

The central callback has two newly explicit defects/gaps:

- If neither direction has a connected next state, it sets EmergencyStopping,
  then dereferences `next.isOutside` while next is null, before registering a
  retry. The earlier unconditional retry claim is corrected. A nonnull occupied
  candidate does schedule a retry, using the captured original Inside location
  even if reversal changed the head.
- Delegation returns the first controller unequal to the delegation singleton,
  **including null**. The named group constructor used for the world root leaves
  its controller unset. Default fallback applies only if traversal reaches the
  end with every visited controller delegating; it is not unconditional null
  recovery. Group reparenting also has no domain cycle check.

Outside countdown/failure/return uses native speed, with limits applying on a
later callback starting Inside. The first connected return candidate is chosen
before occupancy checks; blocking does not search later candidates. Forwarding
resets on any nonpositive Inside stop result, including ordinary zero-stop rail;
it is not a persistent “until next station” mode. Outside counter 50 still unloads
and suppresses reload while forwarding survives. Queue self/other cancellation
remains the separate `GEO-002` decision.

## Scope and reproduction

These four dependency reviews are complete at source/call-boundary level. Named
handoffs retain full catalog/effect conversion, terrain editing/growth, population/
accounting, timetable UI/import, save graphs and runtime fixtures with P1–P10.
This avoids treating every transitively referenced subsystem as a P0 prerequisite.
The new defect findings are recorded for explicit implementation decisions; this
batch changes no gameplay, save formats or source archive files.

**P0-04 remains open for the expanded girder/platform/passageway visual review.**
Runtime/native pixel comparisons and full-world rendering remain later acceptance
work, independently of this source review.

```sh
python3 scripts/review-exav-geometry.py --source-root /path/to/freetrain-exav-archive --check
python3 -m unittest discover -s scripts/tests
python3 scripts/parity-rail-gallery.py --check
git diff --check
```

Validation on 20 October 2026: **83 Python tests pass**, including six ExAV
geometry source/edge/guard checks. ExAV regeneration followed by `--check`,
historical geometry reproduction and `git diff --check` pass. All 63 dependency
hashes resolve to the pinned discovery ledger. No original runtime, native drawing,
browser or save-continuation run was performed by this source review.
