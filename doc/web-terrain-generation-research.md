# Natural terrain generation research — 29 October 2026

Recommendation: prototype a drainage-aware terrain pipeline using a mature noise
library and a river-network-first method, then evaluate the result through
FreeTrain's actual tile conversion and renderer. The strongest initial candidates
are FastNoiseLite and Daniel Andrino's river-network terrain approach. This is
source/documentation research; the candidates have not been installed, executed,
benchmarked or approved for incorporation by this review.

The current generator's validity, deterministic replay, save/transport and resource
evidence remain useful. Those checks do not establish visual naturalness. Adding
fractal variation to individual masks does not supply the missing relationships
between drainage basins, tributaries, valleys, ridges and sedimentary plains.

## Fit with an isometric game

Terrain generators generally produce heightfields or spatial graphs. Their output
can be sampled into an isometric tile world; an isometric-specific library is not
required. Red Blob explicitly separates its height/biome data generation from the
choice of drawing system. [Terrain-from-noise explanation](https://www.redblobgames.com/maps/terrain-from-noise/).

For this repository, evaluate generation in canonical XY space, then sample the
stored H/V footprint and construct legal ground/quarter-corner geometry. The
projection and coordinate conversions are already in
[`WorldSpace.cs`](../src/FreeTrain.Online.Simulation/WorldSpace.cs).

My assessment of the current implementation:

- [`ReliefTerrain.cs`](../src/FreeTrain.Online.Simulation/ReliefTerrain.cs) builds
  three-octave relief before the geography stages. It has no drainage-area,
  sediment-transport or erosion model.
- [`WaterTerrain.cs`](../src/FreeTrain.Online.Simulation/WaterTerrain.cs) chooses
  basins and routes channels after relief/coast generation. Its warped guides
  create local bends, but do not generate an integrated tributary/ridge system.
- [`NaturalTerrain.Normalize`](../src/FreeTrain.Online.Simulation/HeightmapTerrain.cs)
  lowers incompatible ground differences and converts quarter corners. That
  conversion can materially change a raw heightfield. Visual comparisons need to
  include the final game geometry, not just attractive external heightmap renders.

These are code-based engineering findings, not results of an external-generator
benchmark.

## Shortlist

| Project | What the reviewed sources provide | License found | Suggested role in FreeTrain |
| --- | --- | --- | --- |
| **[FastNoiseLite](https://github.com/Auburn/FastNoiseLite)** | Official C# implementation; OpenSimplex2/2S, cellular, Perlin/value noise, fractals and domain warping. | [MIT](https://github.com/Auburn/FastNoiseLite/blob/master/LICENSE) | Best initial noise dependency candidate. Provides terrain fields; drainage and landform composition remain project work. |
| **[terrain-erosion-3-ways](https://github.com/dandrino/terrain-erosion-3-ways)** | Python examples contrasting noise, erosion simulation and terrain built around a river network. | [MIT](https://raw.githubusercontent.com/dandrino/terrain-erosion-3-ways/master/LICENSE.txt) | Strongest initial algorithm prototype for coherent ridges/valleys/tributaries. Adapt a small reviewed subset rather than adopting the full environment. |
| **[Sebastian Lague: Hydraulic Erosion](https://github.com/SebLague/Hydraulic-Erosion)** | Readable C# particle/droplet erosion demonstration in Unity. | [MIT](https://raw.githubusercontent.com/SebLague/Hydraulic-Erosion/master/LICENSE) | Candidate bounded erosion pass; extract and test the numerical kernel without a Unity runtime dependency. |
| **[Mapgen4](https://github.com/redblobgames/mapgen4)** | TypeScript terrain, rainfall, biomes and river-network generator with an oblique-rendered [interactive demo](https://www.redblobgames.com/maps/mapgen4/). | [Apache-2.0](https://github.com/redblobgames/mapgen4/blob/main/LICENSE) | Strong visual/design reference. Source adoption needs a separate review under the project's LGPL2.1 policy. |
| **[Landlab](https://github.com/landlab/landlab)** | Python Earth-surface process framework supporting regular/irregular grids and composable process models. | [MIT core](https://raw.githubusercontent.com/landlab/landlab/master/LICENSE.md) | Offline analysis/prototype reference for drainage, depressions and landscape evolution. Review the selected components and dependencies individually. |
| **[OpenTTD TerraGenesis](https://github.com/OpenTTD/OpenTTD/blob/master/src/tgp.cpp)** | C++ seeded landscape generation with fixed-point heights in a transport-game terrain pipeline. | [GPL-2.0](https://raw.githubusercontent.com/OpenTTD/OpenTTD/master/src/tgp.cpp) | Relevant reference for discrete terrain and final tile appearance. It is game source, not a standalone LGPL drop-in. |

FastNoiseLite's C# source uses framework types rather than Unity. Its
[domain-warp documentation](https://github.com/Auburn/FastNoiseLite/wiki/Documentation)
shows separate noise/warp instances. It is a practical replacement for our custom
noise primitive, but adopting it alone would not establish a better whole-world
terrain design. This integration assessment is an inference from the reviewed
[source](https://raw.githubusercontent.com/Auburn/FastNoiseLite/master/CSharp/FastNoiseLite.cs).

Andrino's network approach samples irregular points, connects them with a Delaunay
mesh, grows upstream drainage relationships and reconstructs heights using flow.
That avoids defining all geography directly on visible tile axes. The code is a
research/demo starting point: its [requirements file](https://raw.githubusercontent.com/dandrino/terrain-erosion-3-ways/master/requirements-pip3.txt)
contains old pins and unrelated machine-learning dependencies. A small modernized
prototype or C# adaptation is preferable. This is a recommendation, not a claim
that its default island example already supports our inland/coast/lake controls.

Lague's [erosion kernel](https://raw.githubusercontent.com/SebLague/Hydraulic-Erosion/master/Assets/Scripts/Erosion.cs)
operates on a height array but inherits MonoBehaviour, uses Unity math/attributes
and builds per-node erosion brushes. Those parts need adaptation, bounded memory
and explicit seeded randomness. Its demonstration does not supply FreeTrain's
lake-count, coast-sector, bank or outlet constraints.

Mapgen4 is deliberately aimed at an appealing illustrated appearance. Its README
notes that rendering/parameters are designed around roughly25,000 cells even
though its underlying structure can support larger meshes. That is not a benchmark
for our1.6-million-cell world. A coarse planning graph and a detailed final tile
field should be evaluated separately. These descriptions come from the
[project README](https://github.com/redblobgames/mapgen4); exact suitability here
is unmeasured.

## Other useful implementations and caveats

- **[flowing-terrain](https://github.com/mrdunk/flowing-terrain)** has a compact
  terrain/drainage approach with TypeScript examples and an
  [MIT license](https://github.com/mrdunk/flowing-terrain/blob/master/LICENSE).
  Its author explicitly discusses a tendency toward cone-shaped islands and uses
  additional noise to vary them. Useful as a smaller prototype/reference; it does
  not remove the need to evaluate landform variety.
- **[SimpleHydrology](https://github.com/weigert/SimpleHydrology)** and its
  [SoilMachine successor](https://github.com/weigert/SoilMachine) demonstrate
  particle-based erosion/hydrology in C++; both READMEs state MIT. They are
  simulation/demo systems with rendering dependencies rather than ready .NET
  libraries. SimpleHydrology's January2023 note says its flooding system was
  removed because of bugs/slowness. Do not assume its screenshots establish a
  production-ready lake system. The selected files and notices still need review.
- **[HighMap](https://github.com/ottolink-dev/HighMap)** and
  **[Fastscapelib](https://github.com/fastscape-lem/fastscapelib)** are broader C++
  terrain/landscape toolkits, but their reviewed READMEs specify GPLv3. Treat them
  as external research/authoring candidates rather than assuming direct inclusion
  preserves the project's LGPL2.1 distribution terms.

Landlab illustrates why the top-level license is insufficient: its
[`PriorityFloodFlowRouter`](https://landlab.csdms.io/generated/api/landlab.components.priority_flood_flow_router.priority_flood_flow_router.html)
is documented as a wrapper around **[GPLv3 RichDEM](https://github.com/r-barnes/richdem)**.
The separate
[`FlowAccumulator`](https://landlab.csdms.io/generated/api/landlab.components.flow_accum.flow_accumulator.html)
has depression-routing options, including examples with DepressionFinderAndRouter.
Review a concrete component/dependency set instead of declaring all Landlab paths
cleared by the core MIT license.

No license change is recommended. MIT candidates offer the simplest initial
integration path with their notices retained. Apache/GPL combinations require
separate analysis of how code is incorporated and distributed; the
[GNU compatibility guidance](https://www.gnu.org/licenses/license-compatibility.en.html)
explains why “open source” alone is insufficient. This shortlist is not a completed
dependency or distribution clearance.

## Recommended prototype

The following is an engineering proposal, not delivered implementation or promised
performance:

1. Establish a coarse terrain/drainage planning domain, initially perhaps256–512
   samples across, in canonical world coordinates or an irregular graph.
2. Generate broad landmass and mountain-range fields using reviewed noise
   primitives, including ridged/multifractal composition and limited domain warp.
3. Build drainage basins and merging tributaries using a river-network-first
   method, then shape valleys and surrounding ridges together. Compare against
   a heightfield-first flow-accumulation approach rather than committing to one
   algorithm solely from screenshots.
4. Select natural basin candidates to satisfy requested lake count/approximate
   area and coast/outlet requirements. Preserve declared constraints and report
   infeasible combinations explicitly.
5. Apply a bounded erosion/deposition or slope-relaxation pass where it improves
   the final tile result. Erosion used during generation does not require a live
   fluid simulation in the game.
6. Upsample/refine at tile scale, convert through the canonical terrain geometry,
   and validate the actual water/land state and resource budget.

A useful three-way comparison would be our current generator, a noise-plus-erosion
pipeline, and a drainage-network-first pipeline. Use the same landmass goals and
seeds, show raw heightmaps plus actual isometric game views, and include plains,
mountain valleys, inland lakes, islands and selected coasts. Judge connected valley
systems, basin placement, range structure, repeated-template appearance, tile-axis
artifacts and railway-friendly lowlands. Do visual review before investing in a
large new acceptance matrix. Then measure the existing1500×1090×45 extent, job
latency/memory, exact save/reopen and platform determinism.

Retain the creation worker, preview/confirmation, persistence and topology-validation
boundaries where suitable. Benchmark a replacement generator behind them. Pin any
adopted source/version and establish reproducible fixtures; seeded floating-point
code is not by itself a demonstrated cross-platform bit-identity guarantee.

## The existing water policy

The owner-directed contract retains one global water surface. That permits much
better landforms, coastlines and lowland basins, but visible mountain rivers cannot
follow arbitrary downhill water elevations while all water is forced to that one
height. Carving every upland watercourse below sea level can produce unnatural
trenches regardless of the chosen noise library. This is a deduction from our
[water contract](web-parity-terrain-contract.md), not a criticism or benchmark of an
external generator.

With the current policy, use drainage to shape dry upland valleys and reserve wet
channels for compatible lowland/coastal geography. Elevated/descending water would
be a separate scoped representation decision; it does not inherently require fluid
simulation. This research does not change the agreed policy or parity checklist.
