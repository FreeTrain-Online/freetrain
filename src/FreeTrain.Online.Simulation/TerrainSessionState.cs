using System.Text.Json;

namespace FreeTrain.Online.Simulation;

// Actual state is authoritative. Provenance describes the original request, never a load recipe.
public sealed record TerrainProvenance(string Profile, string Source, string RequestHash, string RequestJson)
{
    public static TerrainProvenance From(WorldCreationRequest request) => new(request.Profile,
        request switch { EmptyWorldRequest => "empty", HeightmapWorldRequest => "heightmap", GeneratedWorldRequest => "generated", _ => throw new ArgumentException() },
        WorldCreationProfile.Fingerprint(request), JsonSerializer.Serialize(request));
    public void Validate(TerrainWorld world)
    {
        var request = JsonSerializer.Deserialize<WorldCreationRequest>(RequestJson) ?? throw new ArgumentException("Missing provenance.");
        if (!WorldCreationProfile.Validate(request).IsEmpty || From(request) != this ||
            request.Name != world.Name || request.Dimensions != new WorldDimensions(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels))
            throw new ArgumentException("Invalid terrain provenance.");
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
