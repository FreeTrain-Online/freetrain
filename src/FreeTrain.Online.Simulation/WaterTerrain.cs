namespace FreeTrain.Online.Simulation;

// WORLDGEN-WATER-1: enclosed basins and channels in the single global water plane.
// All planning uses canonical face neighbors. Aprons compensate for source corner
// normalization: a low base alone does not imply a submerged surface.
public static class WaterTerrain
{
    private sealed record Survey(byte[] Corners, int[] Component, int[] Sizes, CoastSides[] Sides);
    private sealed record Channel(int[] Path, int Target, int Source, int Width);

    private static int CeilRoot(int value)
    {
        int lo = 0, hi = 2048;
        while (lo < hi) { int mid = (lo + hi) / 2; if (mid * mid < value) lo = mid + 1; else hi = mid; }
        return lo;
    }

    private static uint Hash(int seed, int index)
    {
        unchecked
        {
            uint n = (uint)(seed ^ index) * 0x9e3779b9u;
            n ^= n >> 16; n *= 0x846ca68bu; return n ^ (n >> 16);
        }
    }

    // Share the shoreline's cubic-smoothed integer noise, with isolated water-stage
    // seeds. Three octaves produce broad bends plus a light fringe of smaller bays.
    private static int Fractal(int seed, int x, int y, int scale) =>
        (4 * CoastTerrain.Noise(seed, x, y, scale, 0) +
         2 * CoastTerrain.Noise(seed, x, y, Math.Max(2, scale / 2), 1) +
         CoastTerrain.Noise(seed, x, y, Math.Max(1, scale / 4), 2)) / 7;

    private static Survey Inspect(TerrainWorld world, GenerationWork? work)
    {
        var d = new WorldDimensions(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels);
        var corners = CoastTerrain.UnderwaterCorners(world, work);
        var ids = new int[corners.Length]; Array.Fill(ids, -1);
        var sizes = new List<int>(); var sides = new List<CoastSides>(); var queue = new int[corners.Length];
        Span<int> neighbors = stackalloc int[4];
        for (int start = 0; start < ids.Length; start++)
        {
            work?.Spend(1); if (corners[start] == 0 || ids[start] >= 0) continue;
            int id = sizes.Count, head = 0, tail = 1; queue[0] = start; ids[start] = id; CoastSides touches = 0;
            while (head < tail)
            {
                work?.Spend(8); int i = queue[head++];
                if (CoastTerrain.Boundary(i % d.H, i / d.H, d)) touches |= CoastTerrain.Sector(i % d.H, i / d.H, d);
                int count = CoastTerrain.Neighbors(i, d, neighbors);
                for (int j = 0; j < count; j++)
                {
                    int n = neighbors[j];
                    if (ids[n] >= 0 || corners[n] == 0 || !CoastTerrain.WetFace(i, n, d, corners)) continue;
                    ids[n] = id; queue[tail++] = n;
                }
            }
            sizes.Add(tail); sides.Add(touches);
        }
        return new(corners, ids, sizes.ToArray(), sides.ToArray());
    }

    public static GeographyMetrics Validate(GeneratedWorldRequest request, TerrainWorld world, GenerationWork? work = null)
    {
        if (request.Options.Lakes.Count + request.Options.Rivers.Count == 0) return CoastTerrain.Validate(request, world, work);
        var survey = Inspect(world, work); int sea = 0, lakes = 0, seas = 0, lakeCount = 0; CoastSides sides = 0;
        for (int c = 0; c < survey.Sizes.Length; c++)
        {
            work?.Spend(1);
            if (survey.Sides[c] == 0)
            {
                lakeCount++; lakes += survey.Sizes[c];
                int target = world.Cells.Length * request.Options.Lakes.AreaPercent / 100;
                if (Math.Abs(survey.Sizes[c] - target) > Math.Max(world.Cells.Length / 100, target / 4))
                    throw new GeographyException("lake-area");
            }
            else { sea += survey.Sizes[c]; seas++; sides |= survey.Sides[c]; }
        }
        // A river can open a requested basin into the boundary-connected water.
        // Basins were independently checked before carving; only isolated lakes
        // remain in these final topology metrics.
        if (lakeCount > request.Options.Lakes.Count || request.Options.Rivers.Count == 0 && lakeCount != request.Options.Lakes.Count)
            throw new GeographyException("lake-rim");
        var d = request.Dimensions;
        var expectedSides = CoastTerrain.Selected(request.Options.Coast) | request.Options.Rivers.Sides;
        if (sides != expectedSides) throw new GeographyException("river-boundary");
        for (int i = 0; i < survey.Corners.Length; i++)
        {
            work?.Spend(1); if (!CoastTerrain.Boundary(i % d.H, i / d.H, d)) continue;
            bool expected = (expectedSides & CoastTerrain.Sector(i % d.H, i / d.H, d)) != 0;
            if (survey.Corners[i] != 0 && !expected || request.Options.Coast.Kind == CoastKind.Island && survey.Corners[i] == 0)
                throw new GeographyException("river-boundary");
        }
        if (request.Options.Rivers.Count > 0 && !survey.Sides.Any(contact => (contact & request.Options.Rivers.Sides) == request.Options.Rivers.Sides))
            throw new GeographyException("river-connectivity");
        int wet = sea + lakes;
        if (wet == world.Cells.Length) throw new GeographyException("river-banks");
        // Rivers may divide the main landmass; the pre-carve coast was independently
        // validated. Count final fully dry components for honest preview metrics.
        int landComponents = 0; var seen = new bool[survey.Corners.Length]; var queue = new int[seen.Length];
        Span<int> neighbors = stackalloc int[4];
        for (int start = 0; start < seen.Length; start++)
        {
            work?.Spend(1); if (seen[start] || survey.Corners[start] != 0) continue;
            landComponents++; int head = 0, tail = 1; seen[start] = true; queue[0] = start;
            while (head < tail)
            {
                work?.Spend(8); int count = CoastTerrain.Neighbors(queue[head++], d, neighbors);
                for (int j = 0; j < count; j++) { int n = neighbors[j]; if (!seen[n] && survey.Corners[n] == 0) { seen[n] = true; queue[tail++] = n; } }
            }
        }
        return new(sea, seen.Length - wet, seas, landComponents, sides, 100d * wet / seen.Length,
            lakes, lakeCount, request.Options.Rivers.Count);
    }

    // Canonical square around a cell. A w-cell core is surrounded by a one-cell
    // low apron; all core corners then remain strictly below the global plane.
    private static IEnumerable<int> Square(int index, int width, WorldSpace space, bool apron)
    {
        int a = -width / 2, b = a + width - 1; if (apron) { a--; b++; }
        var p = space.ToLocation(new(index % space.HorizontalSize, index / space.HorizontalSize, 0));
        for (int y = a; y <= b; y++) for (int x = a; x <= b; x++)
        {
            var q = new Location(p.X + x, p.Y + y, 0); if (!space.ContainsHorizontal(q)) continue;
            var s = space.ToStorage(q); yield return s.V * space.HorizontalSize + s.H;
        }
    }

    private static int[] Distances(WorldDimensions d, bool[] seeds, GenerationWork work)
    {
        var distance = new int[seeds.Length]; Array.Fill(distance, -1); var queue = new int[seeds.Length]; int head = 0, tail = 0;
        for (int i = 0; i < seeds.Length; i++) { work.Spend(1); if (seeds[i]) { distance[i] = 0; queue[tail++] = i; } }
        Span<int> neighbors = stackalloc int[4];
        while (head < tail)
        {
            work.Spend(8); int i = queue[head++], count = CoastTerrain.Neighbors(i, d, neighbors);
            for (int j = 0; j < count; j++)
            {
                int n = neighbors[j]; if (distance[n] >= 0) continue;
                distance[n] = distance[i] + 1;
                queue[tail++] = n;
            }
        }
        return distance;
    }

    internal static TerrainWorld Create(GeneratedWorldRequest request, TerrainWorld basis, TerrainProfile profile,
        Func<int, int, int> fine, GenerationWork work, CancellationToken cancellation)
    {
        GeographyException? failure = null;
        for (int attempt = 0; attempt < WorldCreationProfile.MaxAttempts; attempt++)
        {
            try
            {
                var d = request.Dimensions; int size = basis.Cells.Length;
                var ground = basis.Cells.Select(c => c.Ground).ToArray();
                var original = CoastTerrain.SeaMask(basis, work); var reserved = (bool[])original.Clone();
                for (int i = 0; i < size; i++) { work.Spend(1); reserved[i] |= CoastTerrain.Boundary(i % d.H, i / d.H, d); }
                int lakeSeed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.Lakes, attempt);
                Span<int> neighbors = stackalloc int[4];
                for (int lake = 0; lake < request.Options.Lakes.Count; lake++)
                {
                    var distance = Distances(d, reserved, work); int center = -1;
                    for (int i = 0; i < size; i++)
                    {
                        work.Spend(1);
                        if (center < 0 || distance[i] > distance[center] || distance[i] == distance[center] && Hash(lakeSeed + lake, i) < Hash(lakeSeed + lake, center)) center = i;
                    }
                    int area = size * request.Options.Lakes.AreaPercent / 100;
                    int target = Math.Max(1, area - 4 * CeilRoot(2 * area));
                    var seen = new bool[size]; var core = new List<int>(target);
                    var queue = new PriorityQueue<int, (long Score, int Index)>();
                    var origin = basis.Space.ToLocation(new(center % d.H, center / d.H, 0));
                    int scale = Math.Max(4, CeilRoot(area) / 3);
                    void Enqueue(int i)
                    {
                        work.Spend(128); seen[i] = true;
                        var p = basis.Space.ToLocation(new(i % d.H, i / d.H, 0));
                        int dx = p.X - origin.X, dy = p.Y - origin.Y;
                        int noise = Fractal(lakeSeed + lake, p.X, p.Y, scale) - 32768;
                        // Perturb a Euclidean radius instead of growing Manhattan diamonds.
                        // A connected frontier preserves the basin while fixed target size
                        // prevents fractal displacement from silently changing lake area.
                        long score = ((long)dx * dx + (long)dy * dy) * (65536 + noise);
                        queue.Enqueue(i, (score, i));
                    }
                    Enqueue(center);
                    while (queue.TryDequeue(out int i, out _) && core.Count < target)
                    {
                        work.Spend(8); if (distance[i] < 4) continue; core.Add(i);
                        int count = CoastTerrain.Neighbors(i, d, neighbors);
                        for (int j = 0; j < count; j++) { int n = neighbors[j]; if (!seen[n]) Enqueue(n); }
                    }
                    if (core.Count != target) throw new GeographyException("lake-space");
                    foreach (int i in core) foreach (int n in Square(i, 1, basis.Space, true))
                    { work.Spend(1); ground[n] = request.WaterLevel - 1; reserved[n] = true; }
                }
                // Validate the lake basins before overlaying the river on the same water plane.
                var lakeWorld = request.Options.Lakes.Count == 0 ? basis : ShoreTerrain.Normalize(request, profile, ground, fine, work, cancellation);
                var lakesOnly = request with { Options = request.Options with { Rivers = request.Options.Rivers with { Direction = RiverDirection.None } } };
                Validate(lakesOnly, lakeWorld, work);
                var survey = Inspect(lakeWorld, work);
                var wet = survey.Corners.Select(c => c != 0).ToArray();
                var waterDistance = Distances(d, wet, work);
                var boundarySeeds = new bool[size];
                for (int i = 0; i < size; i++) { work.Spend(1); boundarySeeds[i] = CoastTerrain.Boundary(i % d.H, i / d.H, d); }
                var fromEdge = Distances(d, boundarySeeds, work);
                var channels = new List<Channel>();
                if (request.Options.Rivers.Count > 0)
                {
                    var options = request.Options.Rivers;
                    int riverSeed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.Rivers, attempt);
                    var sourceSide = options.Direction == RiverDirection.NorthSouth ? CoastSides.North : CoastSides.East;
                    var targetSide = options.Direction == RiverDirection.NorthSouth ? CoastSides.South : CoastSides.West;
                    bool Gate(int i)
                    {
                        if (!boundarySeeds[i]) return false;
                        Span<int> adjacent = stackalloc int[4]; int count = CoastTerrain.Neighbors(i, d, adjacent);
                        for (int j = 0; j < count; j++) if (!boundarySeeds[adjacent[j]]) return true;
                        return false;
                    }
                    // Only unrelated shores obstruct the route. Lakes overlay the
                    // channel wherever they intersect, without a separating bank.
                    var otherWater = new bool[size];
                    for (int i = 0; i < size; i++)
                    {
                        work.Spend(1);
                        otherWater[i] = wet[i] && survey.Sides[survey.Component[i]] != 0 &&
                            (CoastTerrain.Sector(i % d.H, i / d.H, d) & options.Sides) == 0;
                    }
                    var fromOtherWater = Distances(d, otherWater, work);
                    var blocked = new bool[size];
                    for (int i = 0; i < size; i++)
                    { work.Spend(1); blocked[i] = fromOtherWater[i] >= 0 && fromOtherWater[i] <= options.Width + 3; }
                    // Choose gates near the same perpendicular world coordinate.
                    // H/V rows are staggered: selecting an image edge would make a
                    // diagonal river and can leave a one-cell lip at the map rim.
                    int Perpendicular(int i)
                    {
                        var p = basis.Space.ToLocation(new(i % d.H, i / d.H, 0));
                        return options.Direction == RiverDirection.NorthSouth ? p.X : p.Y;
                    }
                    int middle = options.Direction == RiverDirection.NorthSouth
                        ? (d.H - 1 + (d.V - 1) / 2) / 2 : (d.H - 1 + d.V / 2) / 2;
                    int PickGate(CoastSides side)
                    {
                        int best = -1;
                        for (int i = 0; i < size; i++)
                        {
                            work.Spend(1);
                            if (blocked[i] || !Gate(i) || CoastTerrain.Sector(i % d.H, i / d.H, d) != side) continue;
                            if (best < 0 || Math.Abs(Perpendicular(i) - middle) < Math.Abs(Perpendicular(best) - middle) ||
                                Math.Abs(Perpendicular(i) - middle) == Math.Abs(Perpendicular(best) - middle) && Hash(riverSeed, i) < Hash(riverSeed, best)) best = i;
                        }
                        if (best < 0) throw new GeographyException("river-space");
                        return best;
                    }
                    int source = PickGate(sourceSide), target = PickGate(targetSide);
                    var goals = new bool[size]; goals[target] = true;
                    for (int i = 0; i < size; i++)
                    { work.Spend(1); blocked[i] |= boundarySeeds[i] && i != source && i != target; }
                    var path = Route(source, goals, blocked, d, riverSeed, options.Meander, work);
                    channels.Add(new(path, target, source, options.Width));
                    foreach (int i in path)
                        foreach (int n in Square(i, options.Width, basis.Space, true))
                        { work.Spend(1); ground[n] = request.WaterLevel - 1; }
                }
                var world = ShoreTerrain.Normalize(request, profile, ground, fine, work, cancellation);
                var final = Inspect(world, work);
                foreach (var channel in channels)
                {
                    int id = final.Component[channel.Target];
                    if (id < 0 || final.Component[channel.Source] != id) throw new GeographyException("river-outlet");
                    for (int k = 0; k < channel.Path.Length; k++)
                    {
                        int i = channel.Path[k]; work.Spend(1);
                        if (final.Component[i] != id || k > 0 && !CoastTerrain.WetFace(channel.Path[k - 1], i, d, final.Corners))
                            throw new GeographyException("river-connectivity");
                        foreach (int n in Square(i, channel.Width, basis.Space, false))
                        { work.Spend(1); if (final.Component[n] != id || final.Corners[n] != 15) throw new GeographyException("river-width"); }
                        // Away from seas, lakes and map edges, retain both dry banks.
                        if ((waterDistance[i] < 0 || waterDistance[i] > 2 * (channel.Width + 6)) && fromEdge[i] > 2 * (channel.Width + 6))
                        {
                            var p = basis.Space.ToLocation(new(i % d.H, i / d.H, 0));
                            var next = basis.Space.ToLocation(new(channel.Path[k + 1] % d.H, channel.Path[k + 1] / d.H, 0));
                            var previous = basis.Space.ToLocation(new(channel.Path[Math.Max(0, k - 1)] % d.H, channel.Path[Math.Max(0, k - 1)] / d.H, 0));
                            int dx = Math.Sign(next.X - previous.X), dy = Math.Sign(next.Y - previous.Y);
                            bool left = false, right = false;
                            foreach (int n in Square(i, channel.Width + 8, basis.Space, false))
                            {
                                work.Spend(1); if (final.Corners[n] != 0) continue;
                                var q = basis.Space.ToLocation(new(n % d.H, n / d.H, 0));
                                int side = dx * (q.Y - p.Y) - dy * (q.X - p.X);
                                left |= side < 0; right |= side > 0;
                            }
                            if (!left || !right) throw new GeographyException("river-banks");
                        }
                    }
                }
                Validate(request, world, work); return world;
            }
            catch (GeographyException error) { failure = error; }
        }
        throw failure!;
    }

    private static int[] Route(int source, bool[] goals, bool[] blocked, WorldDimensions d, int seed, int meander, GenerationWork work)
    {
        // Warp a direct source/outlet guide with smooth fractal displacement. Noise
        // in travel costs alone leaves axis-aligned shortest routes visibly straight.
        // The unwarped legal route supplies the outlet, and the final search can still
        // go around protected rims/other channels instead of forcing the guide through.
        var roughness = new int[goals.Length];
        if (meander > 0)
        {
            var guide = Route(source, goals, blocked, d, seed, 0, work);
            var space = new WorldSpace(d.H, d.V, d.Z);
            var start = space.ToLocation(new(source % d.H, source / d.H, 0));
            int end = guide[^1]; var finish = space.ToLocation(new(end % d.H, end / d.H, 0));
            int dx = finish.X - start.X, dy = finish.Y - start.Y;
            long length2 = (long)dx * dx + (long)dy * dy;
            int length = Math.Max(1, Math.Max(Math.Abs(dx), Math.Abs(dy)));
            int scale = Math.Max(6, Math.Min(d.H, d.V) / 5);
            int amplitude = Math.Max(1, Math.Min(d.H, d.V) * meander / 400);
            var offsets = new int[length + 1];
            for (int k = 0; k <= length; k++)
            {
                work.Spend(128); int t = k * 65536 / length;
                int x = start.X + (int)((long)dx * t / 65536), y = start.Y + (int)((long)dy * t / 65536);
                int noise = Fractal(seed, x, y, scale) - 32768;
                // Parabolic fade pins both endpoints. Cache at most 4097 samples;
                // projection interpolates them instead of resampling noise per cell.
                offsets[k] = (int)((long)noise * amplitude * 4 * t * (65536 - t) / 65536 / 65536 / 65536);
            }
            for (int i = 0; i < roughness.Length; i++)
            {
                work.Spend(16); var p = space.ToLocation(new(i % d.H, i / d.H, 0));
                long dot = (long)(p.X - start.X) * dx + (long)(p.Y - start.Y) * dy;
                int along = (int)Math.Clamp(dot * length * 65536 / length2, 0, length * 65536);
                int k = along / 65536;
                int offset = offsets[k] + (int)((long)(offsets[Math.Min(k + 1, length)] - offsets[k]) * (along % 65536) / 65536);
                int cross = (int)(((long)dx * (p.Y - start.Y) - (long)dy * (p.X - start.X)) / length);
                roughness[i] = Math.Min(800, Math.Abs(cross - offset) * 24);
            }
        }
        var costs = new int[goals.Length]; Array.Fill(costs, int.MaxValue);
        var previous = new int[goals.Length]; Array.Fill(previous, -1);
        var queue = new PriorityQueue<int, (int Cost, int Index)>(); costs[source] = 0; queue.Enqueue(source, (0, source));
        Span<int> neighbors = stackalloc int[4];
        while (queue.TryDequeue(out int i, out var priority))
        {
            work.Spend(64); if (priority.Cost != costs[i]) continue;
            if (goals[i])
            {
                var path = new List<int>();
                for (int n = i; n >= 0; n = previous[n]) { work.Spend(1); path.Add(n); }
                path.Reverse(); return path.ToArray();
            }
            int count = CoastTerrain.Neighbors(i, d, neighbors);
            for (int j = 0; j < count; j++)
            {
                int n = neighbors[j]; if (blocked[n] && !goals[n]) continue;
                // Positive smooth costs retain bounded deterministic routing, including
                // the original zero-meander shortest-path behavior.
                int cost = costs[i] + 100 + roughness[n];
                if (cost >= costs[n]) continue;
                costs[n] = cost; previous[n] = i; queue.Enqueue(n, (cost, n));
            }
        }
        throw new GeographyException("river-route");
    }
}
