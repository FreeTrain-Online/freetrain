namespace FreeTrain.Online.Simulation;

// A water-level shelf separates generated hills/mountains from every carved shore.
internal static class ShoreTerrain
{
    internal static TerrainWorld Normalize(GeneratedWorldRequest request, TerrainProfile profile, int[] ground,
        Func<int, int, int> fineHeight, GenerationWork work, CancellationToken cancellation)
    {
        if (request.Options.Relief.Kind == ReliefKind.Plains || request.Options.Coast.Kind == CoastKind.Inland &&
            request.Options.Lakes.Count + request.Options.Rivers.Count == 0)
            return NaturalTerrain.Normalize(request, profile, ground, fineHeight, work, cancellation);

        var d = request.Dimensions;
        var distance = new byte[ground.Length]; Array.Fill(distance, byte.MaxValue);
        var queue = new int[ground.Length]; int head = 0, tail = 0;
        for (int i = 0; i < ground.Length; i++)
        {
            work.Spend(1);
            if (ground[i] < request.WaterLevel) { distance[i] = 0; queue[tail++] = i; }
        }
        if (tail == 0) return NaturalTerrain.Normalize(request, profile, ground, fineHeight, work, cancellation);

        // Corners take the maximum of a 2x2 XY patch. Four face steps of low
        // bases leave at least two complete, flat dry voxels after normalization.
        // Smooth seeded noise extends that band by up to two more face steps.
        Span<int> neighbors = stackalloc int[4];
        while (head < tail)
        {
            work.Spend(8); int i = queue[head++];
            if (distance[i] == 6) continue;
            int count = CoastTerrain.Neighbors(i, d, neighbors);
            for (int j = 0; j < count; j++)
            {
                int n = neighbors[j]; if (distance[n] != byte.MaxValue) continue;
                distance[n] = (byte)(distance[i] + 1); queue[tail++] = n;
            }
        }
        int seed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.Relief);
        int scale = Math.Max(4, request.Options.Relief.FeatureScale / 2);
        for (int i = 0; i < ground.Length; i++)
        {
            work.Spend(1); if (distance[i] is 0 or > 6) continue;
            work.Spend(64);
            int h = i % d.H, v = i / d.H;
            int x = h - v / 2 + (d.V - 1) / 2, y = h + (v + 1) / 2;
            int width = 4 + CoastTerrain.Noise(seed, x, y, scale, 3) * 3 / 65536;
            if (distance[i] <= width) ground[i] = request.WaterLevel;
        }
        // Suppress fractional relief bumps as well as whole-height relief on the shelf.
        return NaturalTerrain.Normalize(request, profile, ground,
            (h, v) => ground[v * d.H + h] == request.WaterLevel ? 4 * request.WaterLevel : fineHeight(h, v), work, cancellation);
    }
}
