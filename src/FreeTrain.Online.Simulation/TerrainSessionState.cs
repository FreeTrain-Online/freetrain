using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FreeTrain.Online.Simulation;

// Actual state is authoritative. Provenance describes the original request, never a load recipe.
public sealed record TerrainProvenance(string Profile, string Source, string RequestHash, string RequestJson)
{
    public static TerrainProvenance From(WorldCreationRequest request) => new(request.Profile,
        request switch { EmptyWorldRequest => "empty", HeightmapWorldRequest => "heightmap", GeneratedWorldRequest => "generated", _ => throw new ArgumentException() },
        WorldCreationProfile.Fingerprint(request), JsonSerializer.Serialize(request));
    public void Validate(TerrainWorld world)
    {
        if (RequestJson is null || RequestJson.Length > 65536) throw new ArgumentException("Invalid terrain provenance size.");
        var request = JsonSerializer.Deserialize<WorldCreationRequest>(RequestJson) ?? throw new ArgumentException("Missing provenance.");
        if (!WorldCreationProfile.Validate(request).IsEmpty || (From(request) != this && !MatchesLegacyRivers(request)) ||
            request.Name != world.Name || request.Dimensions != new WorldDimensions(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels))
            throw new ArgumentException("Invalid terrain provenance.");
    }

    // Save7 provenance is a description, never a regeneration recipe. Preserve
    // canonical requests written before directional rivers so existing worlds
    // still reopen byte-for-byte. New previews only use RiverDirection.
    private sealed record LegacyRivers(int Count, int Width, int Meander, int Outlet);
    private bool MatchesLegacyRivers(WorldCreationRequest request)
    {
        if (request is not GeneratedWorldRequest r || Source != "generated" || Profile != request.Profile) return false;
        var original = JsonNode.Parse(RequestJson);
        var node = original?["Options"]?["Rivers"];
        if (node is null || node["Direction"] is not null || node["Count"] is null || node["Outlet"] is null) return false;
        var rivers = node.Deserialize<LegacyRivers>()!;
        if (rivers.Count is < 0 or > 4 || rivers.Width is < 1 or > 4 || rivers.Meander is < 0 or > 100 || rivers.Outlet is < 0 or > 5) return false;
        var coast = r.Options.Coast; var lakes = r.Options.Lakes;
        if (rivers.Count > 0)
        {
            var side = rivers.Outlet is >= 1 and <= 4 ? (CoastSides)(1 << (rivers.Outlet - 1)) : CoastSides.None;
            if (side != CoastSides.None && coast.Kind != CoastKind.Island && (coast.Kind != CoastKind.Sides || (coast.Sides & side) == 0) ||
                rivers.Outlet == 0 && coast.Kind == CoastKind.Inland && lakes.Count == 0 ||
                rivers.Outlet == 5 && (coast.Kind != CoastKind.Inland || lakes.Count != 0)) return false;
        }
        long cells = (long)r.Dimensions.H * r.Dimensions.V;
        long reserved = r.Options.StartingArea + cells * (coast.WaterPercent + lakes.Count * (long)lakes.AreaPercent) / 100 +
            rivers.Count * (long)rivers.Width * Math.Min(r.Dimensions.H, r.Dimensions.V);
        if (reserved > cells * 3 / 4) return false;
        var canonical = JsonSerializer.SerializeToNode(request)!;
        canonical["Options"]!["Rivers"] = JsonSerializer.SerializeToNode(rivers);
        return canonical.ToJsonString() == RequestJson &&
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(RequestJson))) == RequestHash;
    }
}

public sealed class TerrainSessionState
{
    public TerrainWorld World { get; private set; }
    public TerrainProvenance Provenance { get; }
    public bool ConstructionMode { get; private set; }
    public Clock Clock { get; private set; }
    public RandomStreams Random { get; }
    public bool Paused { get; private set; }
    public long Revision { get; private set; }
    public bool CanAdvance => Clock.Time.AbsoluteMinute < GameTime.MaximumMinute && Revision < long.MaxValue;
    public TerrainSessionState(TerrainWorld world, TerrainProvenance provenance, bool constructionMode = false,
        ClockState? clock = null, bool paused = true, RandomStreamsState? random = null, long revision = 0)
    {
        provenance.Validate(world);
        if (revision < world.Revision) throw new ArgumentException("Invalid state revision.");
        World = world; Provenance = provenance; ConstructionMode = constructionMode;
        Clock = clock == null ? new() : Clock.Restore(clock);
        Random = random == null ? new(RandomStreams.ReferenceSeed) : RandomStreams.Restore(random);
        Paused = paused; Revision = revision;
    }
    public void SetPaused(bool paused)
    { if (Paused == paused) return; var next = checked(Revision + 1); Paused = paused; Revision = next; }
    public void AdvanceOneMinute()
    { var next = checked(Revision + 1); Clock.Advance(); Revision = next; }
    // Trusted session-boundary integration. Gameplay legality/booking belongs to the tool slice.
    public void Replace(long expectedRevision, TerrainWorld replacement, bool constructionMode)
    {
        if (expectedRevision != Revision) throw new InvalidOperationException("staleRevision");
        if (replacement.Profile != World.Profile || replacement.Name != World.Name ||
            replacement.Space.HorizontalSize != World.Space.HorizontalSize || replacement.Space.VerticalSize != World.Space.VerticalSize ||
            replacement.Space.Levels != World.Space.Levels || replacement.Revision != World.Revision + 1)
            throw new ArgumentException("Invalid terrain replacement.");
        var next = checked(Revision + 1);
        var validated = World.Replace(World.Revision, replacement.Cells, replacement.WaterLevel,
            new(replacement.Spatial.NextEntityId, replacement.Spatial.Entities), replacement.Occupants);
        World = validated; ConstructionMode = constructionMode; Revision = next;
    }
}
