namespace FreeTrain.Online.Simulation;

// WORLDGEN-RELIEF-1, portable procedural addition, not an ExAV algorithm.
// All noise, interpolation and amplitude arithmetic is integer/fixed point.
public static class ReliefTerrain
{
    private const int One = 65536;

    // Later geography stages are explicitly unavailable until their own slices ship.
    public static bool Supports(GeneratedWorldRequest request) => WorldCreationProfile.Validate(request).IsEmpty &&
        request.Options is { Coast: { Kind: CoastKind.Inland, Sides: CoastSides.None, WaterPercent: 0 },
            Lakes: { Count: 0 }, Rivers: { Count: 0 }, Forests: { CoveragePercent: 0 }, StartingArea: 0 };

    // Public literal-sample boundary for profile compatibility fixtures. h2 is twice
    // stored H; odd V rows have an additional half-cell stagger, matching the loader.
    public static int SampleQuarterHeight(GeneratedWorldRequest request, int h2, int v)
    {
        if (!Supports(request) || h2 < 0 || h2 > 2 * request.Dimensions.H || v < 0 || v >= request.Dimensions.V)
            throw new ArgumentException("invalidInput");
        return Sample(request, WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.Relief), h2, v);
    }

    private static int Sample(GeneratedWorldRequest request, int seed, int h2, int v)
    {
        var relief = request.Options.Relief;
        int x2 = h2 + v % 2, scale = relief.FeatureScale;
        // Three octaves; smoothness attenuates the shorter wavelengths.
        int detail = (100 - relief.Smoothness) * 256 / 100;
        int a = Noise(seed, x2, v, scale, 0), b = Noise(seed, x2, v, Math.Max(2, scale / 2), 1),
            c = Noise(seed, x2, v, Math.Max(1, scale / 4), 2);
        int value = (int)(((long)a * 256 + (long)b * detail + (long)c * detail / 2) / (256 + detail + detail / 2));
        // Mountains use broad ridges, hills unshaped noise, plains reduced amplitude.
        if (relief.Kind == ReliefKind.Mountains) value = One - Math.Abs(2 * value - One);
        int strength = relief.Kind switch { ReliefKind.Plains => 25, ReliefKind.Hills => 65, _ => 100 };
        int bottom = 4 * (request.WaterLevel + 1);
        int headroom = 4 * (request.Dimensions.Z - 1) - bottom;
        int amplitude = headroom * relief.Amount * strength / 10000;
        return bottom + (int)((long)amplitude * value / One);
    }

    // uint overflow is part of v1. Domain-separated octaves never advance a random bank.
    private static int Lattice(int seed, int x, int y, int octave)
    {
        unchecked
        {
            uint n = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu ^ (uint)(octave + 1) * 0xc2b2ae35u;
            n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15; n *= 0x846ca68bu; n ^= n >> 16;
            return (int)(n >> 16);
        }
    }
    private static int Noise(int seed, int x2, int y, int scale, int octave)
    {
        int x = x2 * One / (2 * scale), v = y * One / scale;
        int ix = x / One, iy = v / One;
        static int Fade(int t) => (int)((long)t * t / One * (3L * One - 2L * t) / One);
        static int Mix(int a, int b, int t) => a + (int)((long)(b - a) * t / One);
        int fx = Fade(x % One), fy = Fade(v % One);
        return Mix(Mix(Lattice(seed, ix, iy, octave), Lattice(seed, ix + 1, iy, octave), fx),
            Mix(Lattice(seed, ix, iy + 1, octave), Lattice(seed, ix + 1, iy + 1, octave), fx), fy);
    }

    public static TerrainWorld Create(GeneratedWorldRequest request, TerrainProfile profile, CancellationToken cancellation = default)
    {
        if (!Supports(request)) throw new ArgumentException("invalidInput");
        var work = new GenerationWork(request, cancellation);
        int seed = WorldCreationProfile.StageSeed(request.Options.Seed, GenerationStage.Relief);
        var d = request.Dimensions;
        var ground = new int[d.H * d.V];
        for (int v = 0; v < d.V; v++) for (int h = 0; h < d.H; h++)
        { work.Spend(64); ground[v * d.H + h] = Sample(request, seed, 2 * h, v) / 4; }
        return NaturalTerrain.Normalize(request, profile, ground, (h, v) => Sample(request, seed, 2 * h + 1, v), work, cancellation);
    }
}
