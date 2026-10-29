using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record ForestMetrics(int EligibleCells, int MaskCells, int ForestCells, int Trees,
    int ReservedCells, int LargestFlatArea);
public sealed record ForestLayout(bool[] Eligible, bool[] Reserved, int LargestFlatArea);

// WORLDGEN-FOREST-1. Distribution/reservation are new policy. Scatter follows
// ForestBuilder.createRandomTrees (ExAV 883cf7d, Kohsuke Kawaguchi et al., LGPL-2.1-or-later).
public static class ForestTerrain
{
    private static uint Hash(int seed, int index)
    {
        unchecked
        {
            uint n = (uint)seed ^ (uint)index * 0x9e3779b9u;
            n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; n ^= n >> 16;
            return n;
        }
    }

    // Source position-only selection sort: sprite indices remain in draw order.
    // Zero trees is a valid binomial outcome; never reroll to force coverage.
    public static ImmutableArray<TerrainTree> Scatter(int density, int sprites, Func<int, int> next)
    {
        if (density is < 1 or > 3 || sprites < 1) throw new ArgumentException("Invalid forest scatter.");
        int Draw(int bound)
        {
            int n = next(bound);
            return n >= 0 && n < bound ? n : throw new ArgumentException("Invalid scatter draw.");
        }
        int count = 0;
        for (int i = 0; i < 2 * density; i++) if (Draw(2) == 0) count++;
        var trees = new TerrainTree[count];
        for (int i = 0; i < count; i++)
        {
            int x = Draw(16), y = Draw(16);
            trees[i] = new(x + y, ((-x + y) >> 1) + 8, Draw(sprites));
        }
        for (int i = 0; i < count - 1; i++) for (int j = i + 1; j < count; j++)
            if (trees[i].Y > trees[j].Y)
            {
                var a = trees[i]; var b = trees[j];
                trees[i] = new(b.X, b.Y, a.Sprite); trees[j] = new(a.X, a.Y, b.Sprite);
            }
        return trees.ToImmutableArray();
    }

    public static ForestLayout Layout(GeneratedWorldRequest request, TerrainWorld world, GenerationWork? work = null)
    {
        var d = request.Dimensions; int length = world.Cells.Length;
        if (d != new WorldDimensions(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels))
            throw new ArgumentException("Mismatched forest dimensions.");
        var eligible = new bool[length]; var flat = new bool[length];
        for (int i = 0; i < length; i++)
        {
            work?.Spend(1);
            eligible[i] = world.Cells[i].Surface == TerrainSurface.Natural && world.Cells[i].Ground >= world.WaterLevel;
            flat[i] = eligible[i];
        }
        for (int k = 0; k < world.Occupants.Length; k++)
        {
            work?.Spend(1); var occupant = world.Occupants[k]; var p = world.Space.ToStorage(world.Spatial.Entities[k].Anchor);
            int i = p.V * d.H + p.H;
            if (occupant.Kind != TerrainOccupantKind.Mountain) eligible[i] = false;
            flat[i] = eligible[i] && occupant.Corners.Flat;
        }
        // Same-height, flat, fully dry cells joined by canonical XY faces. Reserve
        // existing terrain only: geography is never reshaped to make room.
        var seen = new bool[length]; var queue = new int[length];
        int largest = 0, chosen = -1, seed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.StartingArea);
        uint tie = uint.MaxValue; Span<int> neighbors = stackalloc int[4];
        for (int start = 0; start < length; start++)
        {
            work?.Spend(1); if (!flat[start] || seen[start]) continue;
            int head = 0, tail = 1; queue[0] = start; seen[start] = true;
            int root = start; uint rootTie = Hash(seed, start);
            while (head < tail)
            {
                work?.Spend(8); int i = queue[head++], count = CoastTerrain.Neighbors(i, d, neighbors);
                uint candidate = Hash(seed, i);
                if (candidate < rootTie) { root = i; rootTie = candidate; }
                for (int j = 0; j < count; j++)
                {
                    int n = neighbors[j];
                    if (!seen[n] && flat[n] && world.Cells[n].Ground == world.Cells[i].Ground)
                    { seen[n] = true; queue[tail++] = n; }
                }
            }
            if (tail > largest || tail == largest && rootTie < tie)
            { largest = tail; chosen = root; tie = rootTie; }
        }
        if (largest < request.Options.StartingArea) throw new GeographyException("starting-area");
        var reserved = new bool[length];
        if (request.Options.StartingArea > 0)
        {
            int head = 0, tail = 1; queue[0] = chosen; reserved[chosen] = true;
            while (tail < request.Options.StartingArea)
            {
                work?.Spend(8); int i = queue[head++], count = CoastTerrain.Neighbors(i, d, neighbors);
                for (int j = 0; j < count && tail < request.Options.StartingArea; j++)
                {
                    int n = neighbors[j];
                    if (!reserved[n] && flat[n] && world.Cells[n].Ground == world.Cells[i].Ground)
                    { reserved[n] = true; queue[tail++] = n; }
                }
            }
        }
        for (int i = 0; i < length; i++) { work?.Spend(1); if (reserved[i]) eligible[i] = false; }
        return new(eligible, reserved, largest);
    }

    public static ForestMetrics Measure(GeneratedWorldRequest request, TerrainWorld world, ForestLayout layout, GenerationWork? work = null)
    {
        int eligible = 0, reserved = 0, forests = 0, trees = 0;
        for (int i = 0; i < layout.Eligible.Length; i++)
        { work?.Spend(1); if (layout.Eligible[i]) eligible++; if (layout.Reserved[i]) reserved++; }
        for (int k = 0; k < world.Occupants.Length; k++)
        {
            work?.Spend(1); if (world.Occupants[k].Forest is not { } forest) continue;
            var p = world.Space.ToStorage(world.Spatial.Entities[k].Anchor);
            if (!layout.Eligible[p.V * request.Dimensions.H + p.H] || forest.Trees.IsEmpty)
                throw new GeographyException("forest-surface");
            forests++; trees += forest.Trees.Length;
        }
        return new(eligible, (int)((long)eligible * request.Options.Forests.CoveragePercent / 100), forests, trees, reserved, layout.LargestFlatArea);
    }

    internal static TerrainWorld Create(GeneratedWorldRequest request, TerrainWorld world, GenerationWork work, CancellationToken cancellation)
    {
        var options = request.Options.Forests;
        if (options.CoveragePercent == 0 && request.Options.StartingArea == 0) return world;
        var layout = Layout(request, world, work);
        if (options.CoveragePercent == 0) return world;
        var palette = options.Palette.OrderBy(b => b.PackId, StringComparer.Ordinal).ThenBy(b => b.ContributionId, StringComparer.Ordinal).Select(b =>
        {
            if (!world.Profile.Packs.Any(p => p.Id == b.PackId && p.Version == b.Version && p.CatalogHash == b.CatalogHash))
                throw new ArgumentException("Unavailable forest pack.");
            var definition = world.Profile.Resolve(new(b.PackId, b.ContributionId, b.ContributionId));
            return definition.Kind == TerrainOccupantKind.Mountain && definition.TreeSpriteCount > 0 && definition.TreeDensity > 0
                ? definition : throw new ArgumentException("Unavailable forest handler.");
        }).ToArray();
        int maskSeed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.ForestMask);
        int scatterSeed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.ForestScatter);
        int scale = Math.Max(4, Math.Min(request.Dimensions.H, request.Dimensions.V) / 8);
        var order = new long[layout.Eligible.Count(x => x)]; int position = 0;
        for (int i = 0; i < layout.Eligible.Length; i++)
        {
            work.Spend(64); if (!layout.Eligible[i]) continue;
            int h = i % request.Dimensions.H, v = i / request.Dimensions.H;
            int x = h - v / 2 + (request.Dimensions.V - 1) / 2, y = h + (v + 1) / 2;
            int coherent = CoastTerrain.Noise(maskSeed, x, y, scale, 0), independent = (int)(Hash(maskSeed, i) >> 16);
            int score = (coherent * options.Clustering + independent * (100 - options.Clustering)) / 100;
            order[position++] = ((long)score << 32) | (uint)i;
        }
        work.Spend(1); Array.Sort(order); work.Spend(1);
        int target = (int)((long)order.Length * options.CoveragePercent / 100);
        // Reuse the grid for selected cells; do not build a dictionary per cell.
        Array.Clear(layout.Eligible);
        for (int i = 0; i < target; i++) { work.Spend(1); layout.Eligible[(int)(uint)order[i]] = true; }
        var entities = world.Spatial.Entities.ToBuilder(); var occupants = world.Occupants.ToBuilder();
        var index = new int[world.Cells.Length]; Array.Fill(index, -1);
        for (int k = 0; k < occupants.Count; k++)
        { work.Spend(1); var p = world.Space.ToStorage(entities[k].Anchor); index[p.V * request.Dimensions.H + p.H] = k; }
        var mountain = world.Profile.Definitions.Single(d => d.Reference.ContributionId == TerrainProfile.MountainId).Reference;
        var footprint = new Footprint([new(new(0, 0, 0), true)]); long nextId = world.Spatial.NextEntityId;
        for (int i = 0; i < index.Length; i++)
        {
            work.Spend(64); if (!layout.Eligible[i]) continue;
            uint state = Hash(scatterSeed, i) | 1u;
            int Next(int bound)
            {
                // Cell-local xorshift32 and multiply-high bounded draws. No live RNG.
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (int)((ulong)state * (uint)bound >> 32);
            }
            var definition = palette[Next(palette.Length)];
            var trees = Scatter(definition.TreeDensity, definition.TreeSpriteCount, Next);
            if (trees.IsEmpty) continue;
            var forest = new ForestDecoration(definition.Reference, trees); int k = index[i];
            if (k >= 0)
            {
                occupants[k] = occupants[k] with { Forest = forest };
                entities[k] = entities[k] with { Value = definition.Price };
            }
            else
            {
                var anchor = world.Space.ToLocation(new(i % request.Dimensions.H, i / request.Dimensions.H, world.Cells[i].Ground));
                entities.Add(new(nextId, 0, mountain, anchor, footprint, false, false, definition.Price));
                occupants.Add(new(nextId++, TerrainOccupantKind.Mountain, new(), forest));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        return new(world.Profile, world.Name, request.Dimensions, world.WaterLevel, world.Cells,
            new(nextId, entities.ToImmutable()), occupants, world.Revision);
    }
}
