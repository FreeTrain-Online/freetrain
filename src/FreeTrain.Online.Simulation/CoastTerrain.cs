namespace FreeTrain.Online.Simulation;

public sealed class GeographyException(string constraint) : ArgumentException(constraint)
{
    public string Constraint { get; } = constraint;
}

public sealed record GeographyMetrics(int SeaCells, int LandCells, int SeaComponents, int LandComponents,
    CoastSides SeaSides, double WaterPercent);

// WORLDGEN-COAST-1. New procedural policy; compass sectors follow WORLDGEN-D001.
// Connectivity uses shared XYZ cell faces, never H/V image neighbors or diagonal contact.
public static class CoastTerrain
{
    private const int One = 65536;
    public const int CoverageTolerance = 5; // percentage points, measured after normalization
    public static CoastSides Selected(CoastOptions coast) => coast.Kind == CoastKind.Island ? (CoastSides)15 : coast.Sides;
    public static bool Boundary(int h, int v, WorldDimensions d) => h == 0 || v == 0 || h == d.H - 1 || v == d.V - 1;

    public static CoastSides Sector(int h, int v, WorldDimensions d)
    {
        // Bounds of the staggered footprint in canonical XY. Use integer cross products.
        int xmin = 0, xmax = d.H - 1 + (d.V - 1) / 2;
        int ymin = 0, ymax = d.H - 1 + d.V / 2;
        int x = h - v / 2 + (d.V - 1) / 2, y = h + (v + 1) / 2;
        int qx = 2 * x - xmin - xmax, qy = 2 * y - ymin - ymax;
        return (long)Math.Abs(qy) * (xmax - xmin) >= (long)Math.Abs(qx) * (ymax - ymin)
            ? qy < 0 ? CoastSides.North : CoastSides.South
            : qx < 0 ? CoastSides.West : CoastSides.East;
    }

    private static int Neighbors(int index, WorldDimensions d, Span<int> result)
    {
        int h = index % d.H, v = index / d.H, count = 0, right = h + v % 2;
        for (int row = v - 1; row <= v + 1; row += 2)
        {
            if (row < 0 || row >= d.V) continue;
            if (right > 0) result[count++] = row * d.H + right - 1;
            if (right < d.H) result[count++] = row * d.H + right;
        }
        return count;
    }

    // Smooth value noise in canonical XY; three octaves add broad bays and smaller
    // headlands. Fixed-point interpolation and uint overflow are part of v1.
    private static int Noise(int seed, int x, int y, int scale, int octave)
    {
        static int Lattice(int seed, int x, int y, int octave)
        {
            unchecked
            {
                uint n = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu ^ (uint)(octave + 1) * 0xc2b2ae35u;
                n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; n ^= n >> 16;
                return (int)(n >> 16);
            }
        }
        static int Fade(int t) => (int)((long)t * t / One * (3L * One - 2L * t) / One);
        static int Mix(int a, int b, int t) => a + (int)((long)(b - a) * t / One);
        int u = x * One / scale, v = y * One / scale, ix = u / One, iy = v / One;
        int fx = Fade(u % One), fy = Fade(v % One);
        return Mix(Mix(Lattice(seed, ix, iy, octave), Lattice(seed, ix + 1, iy, octave), fx),
            Mix(Lattice(seed, ix, iy + 1, octave), Lattice(seed, ix + 1, iy + 1, octave), fx), fy);
    }

    // Opposing sea/dry distance fields taper the coast toward retained boundaries.
    // Fractal displacement fades to zero at both sea and dry anchors.
    internal static int[] Shape(GeneratedWorldRequest request, int[] relief, int attempt, GenerationWork work)
    {
        var d = request.Dimensions; var coast = request.Options.Coast; var sides = Selected(coast);
        var distances = new int[relief.Length]; Array.Fill(distances, -1);
        var queue = new int[relief.Length]; int head = 0, tail = 0;
        CoastSides present = CoastSides.None;
        for (int v = 0; v < d.V; v++) for (int h = 0; h < d.H; h++)
        {
            work.Spend(1); if (!Boundary(h, v, d)) continue;
            int i = v * d.H + h; var sector = Sector(h, v, d); present |= sector;
            if ((sides & sector) == 0) distances[i] = -2; // protected dry boundary
            else { distances[i] = 0; queue[tail++] = i; }
        }
        if ((present & sides) != sides) throw new GeographyException("coast-sector");
        Span<int> neighbors = stackalloc int[4];
        while (head < tail)
        {
            work.Spend(8); int i = queue[head++];
            int count = Neighbors(i, d, neighbors);
            for (int j = 0; j < count; j++)
            {
                int n = neighbors[j]; if (distances[n] != -1) continue;
                distances[n] = distances[i] + 1; queue[tail++] = n;
            }
        }
        // Distance to the retained dry boundary (or central island seed) tapers the
        // carve before it can strand an unselected boundary behind a strip of sea.
        var dryDistance = new int[relief.Length]; Array.Fill(dryDistance, -1);
        head = 0; int dryTail = 0;
        for (int i = 0; i < distances.Length; i++)
        { work.Spend(1); if (distances[i] == -2) { dryDistance[i] = 0; queue[dryTail++] = i; } }
        int center = (d.V / 2) * d.H + d.H / 2;
        if (dryDistance[center] == -1) { dryDistance[center] = 0; queue[dryTail++] = center; }
        while (head < dryTail)
        {
            work.Spend(8); int i = queue[head++], count = Neighbors(i, d, neighbors);
            for (int j = 0; j < count; j++)
            { int n = neighbors[j]; if (dryDistance[n] >= 0) continue; dryDistance[n] = dryDistance[i] + 1; queue[dryTail++] = n; }
        }
        int seed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.Coast, attempt);
        int scale = Math.Max(4, Math.Min(d.H, d.V) / 3);
        var order = new long[tail];
        int position = 0;
        for (int i = 0; i < distances.Length; i++)
        {
            work.Spend(128); if (distances[i] < 0) continue; // noise and bounded sort comparisons
            uint noise;
            unchecked { noise = (uint)(i ^ seed) * 0x9e3779b9u; noise ^= noise >> 16; noise *= 0x846ca68bu; noise ^= noise >> 16; }
            int h = i % d.H, v = i / d.H;
            int x = h - v / 2 + (d.V - 1) / 2, y = h + (v + 1) / 2;
            int fractal = (4 * Noise(seed, x, y, scale, 0) + 2 * Noise(seed, x, y, Math.Max(2, scale / 2), 1)
                + Noise(seed, x, y, Math.Max(1, scale / 4), 2)) / 7 - One / 2;
            int score = distances[i] * One / Math.Max(1, distances[i] + dryDistance[i]);
            score += (int)((long)fractal * score * (One - score) / One / One);
            order[position++] = ((long)(score * 256 + (int)(noise & 255)) << 32) | (uint)i;
        }
        // Sorting at most MaxCells fixed-width keys; cancellation before/after the bounded sort.
        work.Spend(1); Array.Sort(order); work.Spend(1);
        int target = (int)((long)relief.Length * (coast.WaterPercent + attempt) / 100);
        if (target >= tail || target == 0) throw new GeographyException("coast-coverage");
        var ground = (int[])relief.Clone();
        for (int j = 0; j < target; j++)
        { work.Spend(1); ground[(int)(uint)order[j]] = request.WaterLevel - 1; }
        return ground;
    }

    // Quarter-corner lowering can pinch off a tiny sea patch. Fill only enclosed
    // patches, then normalize once more and run all predicates again. No lake is
    // silently admitted as coast, and no unbounded repair loop is used.
    internal static int[]? FillEnclosedSea(TerrainWorld world, GenerationWork work)
    {
        var d = new WorldDimensions(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels);
        var corners = UnderwaterCorners(world, work); var wet = corners.Select(c => c != 0).ToArray(); var reached = new bool[wet.Length]; var queue = new int[wet.Length];
        int head = 0, tail = 0;
        for (int i = 0; i < wet.Length; i++)
        { work.Spend(1); if (wet[i] && Boundary(i % d.H, i / d.H, d)) { reached[i] = true; queue[tail++] = i; } }
        Span<int> neighbors = stackalloc int[4];
        while (head < tail)
        {
            work.Spend(8); int i = queue[head++], count = Neighbors(i, d, neighbors);
            for (int j = 0; j < count; j++)
            { int n = neighbors[j]; if (wet[n] && !reached[n] && WetFace(i, n, d, corners)) { reached[n] = true; queue[tail++] = n; } }
        }
        int[]? ground = null;
        for (int i = 0; i < wet.Length; i++)
        {
            work.Spend(1); if (!wet[i] || reached[i]) continue;
            ground ??= world.Cells.Select(c => c.Ground).ToArray(); ground[i] = world.WaterLevel + 1;
        }
        return ground;
    }

    // A wet cell has a positive-area patch below the global plane. A slope whose
    // four corners meet/exceed the plane is dry even if its buried base is lower.
    public static bool[] SeaMask(TerrainWorld world, GenerationWork? work = null)
        => UnderwaterCorners(world, work).Select(c => c != 0).ToArray();

    private static byte[] UnderwaterCorners(TerrainWorld world, GenerationWork? work)
    {
        int water = 4 * world.WaterLevel;
        var wet = new byte[world.Cells.Length];
        for (int i = 0; i < wet.Length; i++) { work?.Spend(1); wet[i] = (byte)(world.Cells[i].Ground * 4 < water ? 15 : 0); }
        for (int k = 0; k < world.Occupants.Length; k++)
        {
            var occupant = world.Occupants[k];
            work?.Spend(1); if (occupant.Kind != TerrainOccupantKind.Mountain) continue;
            // Both canonical collections are sorted by the same validated entity IDs.
            var entity = world.Spatial.Entities[k];
            var p = world.Space.ToStorage(entity.Anchor); int i = p.V * world.Space.HorizontalSize + p.H;
            wet[i] = 0;
            for (int c = 0; c < 4; c++) if (4 * world.Cells[i].Ground + occupant.Corners[c] < water) wet[i] |= (byte)(1 << c);
        }
        return wet;
    }

    private static bool WetFace(int i, int n, WorldDimensions d, byte[] corners)
    {
        int h = i % d.H, v = i / d.H;
        // North: NE/NW; east: NE/SE; south: SE/SW; west: SW/NW.
        int face = n / d.H < v ? n % d.H == h + v % 2 ? 3 : 9 : n % d.H == h + v % 2 ? 6 : 12;
        return (corners[i] & face) != 0;
    }

    public static GeographyMetrics Measure(TerrainWorld world, GenerationWork? work = null)
        => Measure(world, UnderwaterCorners(world, work), work);

    private static GeographyMetrics Measure(TerrainWorld world, byte[] corners, GenerationWork? work)
    {
        var wet = corners.Select(c => c != 0).ToArray();
        var d = new WorldDimensions(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels);
        if (wet.Length != world.Cells.Length) throw new ArgumentException("Invalid sea mask.");
        var seen = new bool[wet.Length]; var queue = new int[wet.Length];
        int sea = 0, seaComponents = 0, landComponents = 0; CoastSides seaSides = 0;
        Span<int> neighbors = stackalloc int[4];
        for (int start = 0; start < wet.Length; start++)
        {
            work?.Spend(1); if (seen[start]) continue;
            bool isSea = wet[start]; int head = 0, tail = 1; queue[0] = start; seen[start] = true;
            CoastSides touches = 0;
            while (head < tail)
            {
                work?.Spend(8); int i = queue[head++], h = i % d.H, v = i / d.H;
                if (Boundary(h, v, d)) touches |= Sector(h, v, d);
                int count = Neighbors(i, d, neighbors);
                for (int j = 0; j < count; j++)
                { int n = neighbors[j]; if (!seen[n] && wet[n] == isSea && (!isSea || WetFace(i, n, d, corners))) { seen[n] = true; queue[tail++] = n; } }
            }
            if (isSea)
            {
                if (touches == 0) throw new GeographyException("coast-enclosed-sea");
                sea += tail; seaComponents++; seaSides |= touches;
            }
            else landComponents++;
        }
        return new(sea, wet.Length - sea, seaComponents, landComponents, seaSides, 100d * sea / wet.Length);
    }

    public static GeographyMetrics Validate(GeneratedWorldRequest request, TerrainWorld world, GenerationWork? work = null)
    {
        var corners = UnderwaterCorners(world, work); var metrics = Measure(world, corners, work); var coast = request.Options.Coast;
        if (metrics.LandComponents != 1 || metrics.LandCells == 0) throw new GeographyException("coast-land-connectivity");
        if (metrics.SeaSides != Selected(coast)) throw new GeographyException("coast-boundary");
        if (coast.Kind == CoastKind.Island)
        {
            if (metrics.SeaComponents != 1) throw new GeographyException("coast-sea-connectivity");
            var d = request.Dimensions;
            for (int v = 0; v < d.V; v++) for (int h = 0; h < d.H; h++)
            { work?.Spend(1); if (Boundary(h, v, d) && corners[v * d.H + h] == 0) throw new GeographyException("coast-island-rim"); }
        }
        if (Math.Abs(metrics.WaterPercent - coast.WaterPercent) > CoverageTolerance)
            throw new GeographyException("coast-coverage");
        return metrics;
    }
}
