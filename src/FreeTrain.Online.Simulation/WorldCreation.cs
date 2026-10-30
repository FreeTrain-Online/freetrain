using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeTrain.Online.Simulation;

public sealed record WorldDimensions(int H, int V, int Z);
public sealed record ContentBinding(string PackId, string Version, string CatalogHash, string ContributionId);
public enum WorldSizePreset { Compact, Standard }
public enum ReliefKind { Plains, Hills, Mountains }
public enum CoastKind { Inland, Island, Sides }
[Flags] public enum CoastSides { None = 0, North = 1, East = 2, South = 4, West = 8 }
public enum RiverDirection { None, NorthSouth, EastWest }
public sealed record ReliefOptions(ReliefKind Kind, int Amount, int FeatureScale, int Smoothness);
public sealed record CoastOptions(CoastKind Kind, CoastSides Sides, int WaterPercent);
public sealed record LakeOptions(int Count, int AreaPercent);
public sealed record RiverOptions(RiverDirection Direction, int Width, int Meander)
{
    [JsonIgnore] public int Count => Direction == RiverDirection.None ? 0 : 1;
    [JsonIgnore] public CoastSides Sides => Direction switch
    {
        RiverDirection.NorthSouth => CoastSides.North | CoastSides.South,
        RiverDirection.EastWest => CoastSides.East | CoastSides.West,
        _ => CoastSides.None
    };
}
public sealed record ForestOptions(int CoveragePercent, int Clustering, ImmutableArray<ContentBinding> Palette);
public sealed record GeneratedOptions(int Seed, ReliefOptions Relief, CoastOptions Coast, LakeOptions Lakes,
    RiverOptions Rivers, ForestOptions Forests, int StartingArea);
// A digest identifies uploaded bytes, never a server path/URL. Decoding belongs outside Simulation.
public sealed record HeightmapInput(string Sha256, string MediaType, long EncodedBytes, int Width, int Height,
    int Frames, bool HasTransparency = false, ContentBinding? Provider = null);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "source")]
[JsonDerivedType(typeof(EmptyWorldRequest), "empty")]
[JsonDerivedType(typeof(HeightmapWorldRequest), "heightmap")]
[JsonDerivedType(typeof(GeneratedWorldRequest), "generated")]
public abstract record WorldCreationRequest(string Profile, string Name, WorldDimensions Dimensions, int WaterLevel);
public sealed record EmptyWorldRequest(string Profile, string Name, WorldDimensions Dimensions, int WaterLevel)
    : WorldCreationRequest(Profile, Name, Dimensions, WaterLevel);
public sealed record HeightmapWorldRequest(string Profile, string Name, WorldDimensions Dimensions, int WaterLevel, HeightmapInput Image)
    : WorldCreationRequest(Profile, Name, Dimensions, WaterLevel);
public sealed record GeneratedWorldRequest(string Profile, string Name, WorldDimensions Dimensions, int WaterLevel, GeneratedOptions Options)
    : WorldCreationRequest(Profile, Name, Dimensions, WaterLevel);

public sealed record CreationIssue(string Code, string Field);
public sealed record GenerationBudget(long Cells, long Voxels, long WorkingBytes, long WorkUnits, long TransferBytes);
public enum GenerationStage { Relief, Coast, Lakes, Rivers, StartingArea, ForestMask, ForestScatter, Cover }

// WORLDGEN policy, not desktop source behavior. Service admissions are narrower than these structural contracts.
public static class WorldCreationProfile
{
    public const string Id = "terrain-generation-1";
    public const int MaxSide = 2048, MaxLevels = 64, MaxCells = 1_700_000;
    public const long MaxVoxels = 108_800_000, MaxWorkingBytes = 4096L * 1024 * 1024;
    public const long MaxWorkUnits = 35_000_000_000, MaxTransferBytes = 256L * 1024 * 1024;
    public const long MaxImageBytes = 8L * 1024 * 1024, MaxImagePixels = 4_194_304;
    public const int MaxImageSide = 4096, MaxAttempts = 4;
    public const long MaximumRealizedTerrainBytes = 2048L * 1024 * 1024;
    public static long RealizedTerrainBytes(long cells, long occupants, long trees)
        => checked(cells * 24 + occupants * 1110 + trees * 6);
    public static EmptyWorldRequest Empty(string name = "") => new(Id, name, new(30, 42, 6), 1);
    public static HeightmapWorldRequest Heightmap(HeightmapInput image, string name = "") => new(Id, name, new(64, 64, 16), 4, image);
    public static GeneratedWorldRequest Generated(int seed = 0, string name = "") => new(Id, name, new(64, 64, 16), 4,
        new(seed, Relief(ReliefKind.Hills), new(CoastKind.Inland, CoastSides.None, 0), new(0, 0),
            new(RiverDirection.None, 1, 30), new(0, 50, []), 64));
    public static WorldDimensions Size(WorldSizePreset preset) => preset switch
    {
        WorldSizePreset.Compact => new(64, 64, 16),
        WorldSizePreset.Standard => new(128, 128, 32),
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };
    public static ReliefOptions Relief(ReliefKind kind) => kind switch
    {
        ReliefKind.Plains => new(kind, 20, 32, 75),
        ReliefKind.Hills => new(kind, 50, 24, 50),
        ReliefKind.Mountains => new(kind, 85, 32, 25),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    // SHA256 UTF-8 framing, invariant signed Int32 root, named stage, attempt, no trailing newline.
    // No GameRandom/RandomStreams instance from a live world is accepted or touched.
    public static int StageSeed(int seed, GenerationStage stage, int attempt = 0)
    {
        if (!Enum.IsDefined(stage) || attempt is < 0 or >= MaxAttempts) throw new ArgumentOutOfRangeException();
        var stageId = stage switch
        {
            GenerationStage.StartingArea => "starting-area", GenerationStage.ForestMask => "forest-mask",
            GenerationStage.ForestScatter => "forest-scatter", _ => stage.ToString().ToLowerInvariant()
        };
        var input = $"freetrain.world-generation.v1\n{Id}\n{seed.ToString(CultureInfo.InvariantCulture)}\n{stageId}\n{attempt.ToString(CultureInfo.InvariantCulture)}";
        return BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(input))) & int.MaxValue;
    }

    public static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static ImmutableArray<CreationIssue> Validate(WorldCreationRequest? request)
    {
        var issues = ImmutableArray.CreateBuilder<CreationIssue>();
        void Require(bool valid, string code, string field) { if (!valid) issues.Add(new(code, field)); }
        if (request is null) return [new("required", "request")];
        Require(request.Profile == Id, "profile", "profile");
        Require(request.Name is not null && request.Name.Length <= 128 && !request.Name.Any(char.IsControl), "name", "name");
        if (request.Dimensions is not { } d) return [new("required", "dimensions")];
        Require(d.H is >= 1 and <= MaxSide && d.V is >= 1 and <= MaxSide && d.Z is >= 1 and <= MaxLevels, "dimensions", "dimensions");
        Require(request.WaterLevel >= 0 && request.WaterLevel < d.Z, "water", "waterLevel");
        if (issues.Count > 0) return issues.ToImmutable(); // Never multiply/allocate untrusted dimensions.
        if (request is HeightmapWorldRequest map)
        {
            if (map.Image is not { } image) return [new("required", "image")];
            Require(IsHash(image.Sha256), "digest", "image.sha256");
            Require(image.MediaType is "image/png" or "image/jpeg" or "image/bmp" or "image/gif", "image-format", "image.mediaType");
            Require(image.EncodedBytes is > 0 and <= MaxImageBytes, "image-bytes", "image.encodedBytes");
            Require(image.Width is > 0 and <= MaxImageSide && image.Height is > 0 and <= MaxImageSide &&
                (long)image.Width * image.Height <= MaxImagePixels, "image-pixels", "image.dimensions");
            Require(image.Frames == 1, "image-animation", "image.frames");
            Require(!image.HasTransparency, "image-alpha", "image.hasTransparency");
            if (image.Provider is { } provider) Require(ValidBinding(provider), "content-binding", "image.provider");
        }
        else if (request is GeneratedWorldRequest generated) ValidateGenerated(generated, issues);
        else Require(request is EmptyWorldRequest, "source", "source");
        if (issues.Count > 0) return issues.ToImmutable();
        var budget = EstimateValidated(request);
        Require(budget.Cells <= MaxCells && budget.Voxels <= MaxVoxels && budget.WorkingBytes <= MaxWorkingBytes &&
            budget.WorkUnits <= MaxWorkUnits && budget.TransferBytes <= MaxTransferBytes, "budget", "request");
        return issues.ToImmutable();
    }

    private static void ValidateGenerated(GeneratedWorldRequest r, ImmutableArray<CreationIssue>.Builder issues)
    {
        void Require(bool valid, string code, string field) { if (!valid) issues.Add(new(code, field)); }
        var d = r.Dimensions;
        Require(d.H >= 16 && d.V >= 16 && d.Z >= 8, "generated-minimum", "dimensions");
        Require(r.WaterLevel >= 1 && r.WaterLevel <= d.Z - 4, "headroom", "waterLevel");
        if (r.Options is not { Relief: { } relief, Coast: { } coast, Lakes: { } lakes, Rivers: { } rivers, Forests: { } forest } o)
        { issues.Add(new("required", "options")); return; }
        Require(Enum.IsDefined(relief.Kind) && relief.Amount is >= 0 and <= 100 && relief.FeatureScale is >= 4 and <= 128 &&
            relief.Smoothness is >= 0 and <= 100, "relief", "options.relief");
        Require(Enum.IsDefined(coast.Kind) && (coast.Sides & ~((CoastSides)15)) == 0 &&
            (coast.Kind == CoastKind.Sides ? coast.Sides != CoastSides.None : coast.Sides == CoastSides.None), "coast-sides", "options.coast");
        Require(coast.Kind == CoastKind.Inland ? coast.WaterPercent == 0 : coast.WaterPercent is >= 5 and <= 75,
            "coast-coverage", "options.coast.waterPercent");
        Require(lakes.Count is >= 0 and <= 8 && (lakes.Count == 0 ? lakes.AreaPercent == 0 : lakes.AreaPercent is >= 1 and <= 20), "lakes", "options.lakes");
        Require(Enum.IsDefined(rivers.Direction) && rivers.Width is >= 1 and <= 4 && rivers.Meander is >= 0 and <= 100,
            "rivers", "options.rivers");
        Require(forest.CoveragePercent is >= 0 and <= 90 && forest.Clustering is >= 0 and <= 100 && !forest.Palette.IsDefault &&
            forest.Palette.Length <= 18 && (forest.CoveragePercent == 0 ? forest.Palette.IsEmpty : !forest.Palette.IsEmpty) &&
            forest.Palette.All(ValidBinding) && forest.Palette.Distinct().Count() == forest.Palette.Length,
            "forest", "options.forests");
        var cells = (long)d.H * d.V;
        Require(o.StartingArea >= 0 && o.StartingArea <= cells / 4, "starting-area", "options.startingArea");
        if (issues.Count > 0) return; // Do not calculate areas from rejected unbounded feature values.
        // Necessary space bound only. Final connectivity/representability is checked after normalization.
        var reserved = (long)o.StartingArea + cells * (coast.WaterPercent + lakes.Count * (long)lakes.AreaPercent) / 100 +
            rivers.Count * (long)rivers.Width * (d.H + d.V) / 2;
        Require(reserved <= cells * 3 / 4, "feature-space", "options");
    }

    public static bool ValidBinding(ContentBinding? b) => b is not null && !string.IsNullOrWhiteSpace(b.PackId) &&
        !string.IsNullOrWhiteSpace(b.Version) && IsHash(b.CatalogHash) && !string.IsNullOrWhiteSpace(b.ContributionId) &&
        b.PackId.Length <= 128 && b.Version.Length <= 64 && b.ContributionId.Length <= 256;

    public static GenerationBudget Estimate(WorldCreationRequest request)
    {
        if (!Validate(request).IsEmpty) throw new ArgumentException("Invalid world creation request.", nameof(request));
        return EstimateValidated(request);
    }
    private static GenerationBudget EstimateValidated(WorldCreationRequest r)
    {
        var cells = (long)r.Dimensions.H * r.Dimensions.V;
        var pixels = r is HeightmapWorldRequest m ? (long)m.Image.Width * m.Image.Height : 0;
        var encoded = r is HeightmapWorldRequest image ? image.Image.EncodedBytes : 0;
        // P3-16a: packed transfer plus metadata, and conservative measured working
        // reservations for canonical geometry, restoration and immutable wire reads.
        // No H*V*Z allocation. Four generation attempts remain sequential.
        var cellBytes = r is HeightmapWorldRequest or GeneratedWorldRequest ? 96 : 4;
        // Decode scratch is released before terrain generation. Reserve the larger phase,
        // The hard 80MiB arena is trimmed to the single RGBA output before the managed
        // raster is allocated, so it does not overlap a full arena.
        long working = r is HeightmapWorldRequest
            ? Math.Max(80L * 1024 * 1024, cells * 2048 + pixels * 8)
            : r is GeneratedWorldRequest ? cells * 2048 : cells * 256;
        return new(cells, cells * r.Dimensions.Z, 2_228_224 + working + encoded,
            cells * (64L * r.Dimensions.Z + 256) * MaxAttempts + pixels, 65_536 + cells * cellBytes);
    }
    public static string Fingerprint(WorldCreationRequest request)
    {
        if (!Validate(request).IsEmpty) throw new ArgumentException("Invalid world creation request.", nameof(request));
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
    }
}
