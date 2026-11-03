using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Server;

public enum StartupWater { Random, Dry, Lake, River, Coast }

// Only used when there is no usable checkpoint. The realized terrain and request
// (including the chosen seed) are saved by the normal Save 7 checkpoint path.
public sealed class StartupWorldOptions
{
    public int Width { get; init; } = 400;
    public int Height { get; init; } = 600;
    public int? Seed { get; init; }
    public int Hills { get; init; } = 35;
    public int TreesPercent { get; init; } = 15;
    public StartupWater Water { get; init; } = StartupWater.Random;

    public GeneratedWorldRequest Request(int seed)
    {
        if (!Enum.IsDefined(Water)) throw new ArgumentException("Invalid startup water setting.");
        // Domain-separated choices keep a fixed seed reproducible without using
        // the world's gameplay random streams or depending on System.Random.
        int choice = WorldCreationProfile.StageSeed(seed, GenerationStage.Cover);
        var water = Water == StartupWater.Random ? (StartupWater)(1 + choice % 4) : Water;
        var coast = water == StartupWater.Coast
            ? new CoastOptions(CoastKind.Sides, (CoastSides)(1 << ((choice / 4) % 4)), 20)
            : new CoastOptions(CoastKind.Inland, CoastSides.None, 0);
        int forestSeed = WorldCreationProfile.StageSeed(seed, GenerationStage.ForestMask);
        var palette = TreesPercent == 0 ? [] : WorldCreationCapabilities.ForestChoices()
            .Where(c => c.Variant == "natural")
            .OrderBy(c => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"freetrain.startup-forest\n{forestSeed}\n{c.Binding.ContributionId}"))), StringComparer.Ordinal)
            .Take(1).Select(c => c.Binding).ToImmutableArray();
        var request = new GeneratedWorldRequest(WorldCreationProfile.Id, "New world", new(Width, Height, 24), 4,
            new(seed, new(ReliefKind.Hills, Hills, 64, 75), coast,
                water == StartupWater.Lake ? new(1, 3) : new(0, 0),
                new(water == StartupWater.River
                    ? ((choice / 4) % 2 == 0 ? RiverDirection.NorthSouth : RiverDirection.EastWest)
                    : RiverDirection.None, 3, 30),
                new(TreesPercent, 35, palette), (int)Math.Clamp((long)Width * Height / 4, 0, 256)));
        if (!WorldCreationProfile.Validate(request).IsEmpty)
            throw new ArgumentException("Invalid FreeTrainOnline:StartupWorld generation settings.");
        return request;
    }

    public TerrainSessionState Create()
    {
        var request = Request(Seed ?? RandomNumberGenerator.GetInt32(int.MaxValue));
        return new(ReliefTerrain.Create(request, TerrainProfiles.Bundled), TerrainProvenance.From(request));
    }
}
