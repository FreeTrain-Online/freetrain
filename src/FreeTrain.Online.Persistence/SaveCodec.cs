using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

// Only the current save format is supported during parity implementation.
public static class SaveCodec
{
    public const int MaximumBytes = ProtocolIdentity.MaximumStateBytes;
    public const int Version = 6;
    public sealed record MapData(int Width, int Height, int Levels, string CoordinateSystem);
    public sealed record RailData(int X, int Y, int Z, int Pattern, long EntityId);
    public sealed record SpatialData(long NextEntityId, SpatialEntityRead[] Entities);
    public sealed record Document(int SaveVersion, string Ruleset, string ContentVersion,
        RequiredPackRead[] RequiredPacks, MapData Map, RailData[] Rails, ClockState Clock, bool Paused, RandomStreamsState Random, SpatialData Spatial,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FreeTrain.Online.Contracts.TerrainRead? Terrain = null,
        long Revision = 0);
    public sealed record LoadedWorld(ReferenceWorld? Reference, TerrainSessionState? Terrain);
    private static readonly JsonSerializerOptions Json = new(WireJson.CreateOptions())
        { PropertyNameCaseInsensitive = false, NumberHandling = JsonNumberHandling.Strict };

    public static byte[] Write(ReferenceWorld world) => JsonSerializer.SerializeToUtf8Bytes(new Document(Version,
        ProtocolIdentity.ReferenceRuleset, ProtocolIdentity.ReferenceContent, ProtocolIdentity.ReferencePacks,
        new(world.Space.HorizontalSize, world.Space.VerticalSize, world.Space.Levels, "hv"),
        world.Rails.OrderBy(r => r.Position.X).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.Z)
            .Select(r => new RailData(r.Position.X,r.Position.Y,r.Position.Z,r.Pattern,r.EntityId)).ToArray(), world.Clock.Capture(), world.Paused, world.Random.Capture(),
        new(world.Spatial.NextEntityId, world.Spatial.Entities.Select(e => new SpatialEntityRead(e.Id,e.Revision,
            new(e.Definition.PackId,e.Definition.ContributionId,e.Definition.RecipeId),new(e.Anchor.X,e.Anchor.Y,e.Anchor.Z),
            e.Footprint.Members.Select(m => new VoxelMemberRead(new(m.Offset.X,m.Offset.Y,m.Offset.Z),m.Transparent)).ToArray(),
            e.Owned,e.SilentlyReclaimable,e.Value)).ToArray())), Json);

    public static byte[] Write(TerrainSessionState state)
    {
        var snapshot = TerrainWire.Snapshot(state, 1);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(Version, snapshot.Ruleset, snapshot.ContentVersion,
            snapshot.RequiredPacks, new(snapshot.Map.Width, snapshot.Map.Height, snapshot.Map.Levels, "hv"), [],
            state.Clock.Capture(), state.Paused, state.Random.Capture(),
            new(state.World.Spatial.NextEntityId, snapshot.Entities!), snapshot.Terrain, state.Revision), Json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("saveTooLarge");
        return bytes;
    }
    // Canonical candidate bytes include actual state and metadata; no generator is run on load/commit.
    public static string Digest(TerrainSessionState state) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Write(state)));
    public static ReferenceWorld Read(ReadOnlySpan<byte> bytes) => ReadWorld(bytes).Reference ?? throw new InvalidDataException("incompatibleSave");
    public static LoadedWorld ReadWorld(ReadOnlySpan<byte> bytes,
        Func<string, string, IEnumerable<TerrainPack>, TerrainProfile?>? terrainResolver = null)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("saveTooLarge");
        try
        {
            using var parsed = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("saveVersion", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number <= 0)
                throw new InvalidDataException("invalidSave");
            if (number != Version || !root.TryGetProperty("ruleset", out var ruleset)
                || ruleset.ValueKind != JsonValueKind.String
                || ruleset.GetString() is not (ProtocolIdentity.ReferenceRuleset or TerrainProfile.Ruleset))
                throw new InvalidDataException("incompatibleSave");
            CheckUniqueProperties(root);
            if (!root.TryGetProperty("revision", out _)) throw new JsonException("Missing revision.");
            var data = JsonSerializer.Deserialize<Document>(bytes, Json) ?? throw new JsonException();
            if (data.Random.Version != RandomStreams.Version || data.Random.Algorithm != GameRandom.Algorithm)
                throw new InvalidDataException("incompatibleSave");
            if (data.Ruleset == TerrainProfile.Ruleset)
            {
                var terrainProfile = terrainResolver?.Invoke(data.Ruleset, data.ContentVersion,
                    data.RequiredPacks.Select(p => new TerrainPack(p.Id, p.Version, p.CatalogHash)))
                    ?? throw new InvalidDataException("incompatibleSave");
                if (!terrainProfile.Matches(data.Ruleset, data.ContentVersion,
                    data.RequiredPacks.Select(p => new TerrainPack(p.Id, p.Version, p.CatalogHash)))) throw new InvalidDataException("incompatibleSave");
                if (data.Map.CoordinateSystem != "hv" || data.Rails.Length != 0 || data.Terrain == null ||
                    data.Spatial.NextEntityId != data.Terrain.NextEntityId) throw new JsonException();
                return new(null, TerrainWire.Restore(data.Terrain, new(data.Map.Width, data.Map.Height, data.Map.Levels, "hv"),
                    terrainProfile, data.Spatial.Entities, data.Clock, data.Paused, data.Random, data.Revision));
            }
            if (data.Terrain != null || data.Revision != 0) throw new JsonException();
            var profile = WorldProfiles.Resolve(data.Ruleset, data.ContentVersion, data.RequiredPacks);
            if (profile != WorldProfiles.Reference || data.Map.CoordinateSystem != profile.CoordinateSystem)
                throw new InvalidDataException("incompatibleSave");
            if (data.Rails.Any(r => r == null) || data.Spatial.Entities.Any(e => e == null || e.Definition == null || e.Anchor == null || e.Footprint == null || e.Footprint.Any(m => m == null || m.Offset == null))) throw new JsonException();
            if (data.Spatial.Entities.Any(e => !profile.Contains(e.Definition))) throw new InvalidDataException("incompatibleSave");
            var spatial = new SpatialState(data.Spatial.NextEntityId, data.Spatial.Entities.Select(e => new SpatialEntity(e.Id,e.Revision,
                new(e.Definition.PackId,e.Definition.ContributionId,e.Definition.RecipeId),new(e.Anchor.X,e.Anchor.Y,e.Anchor.Z),
                new(e.Footprint.Select(m => new VoxelMember(new(m.Offset.X,m.Offset.Y,m.Offset.Z),m.Transparent))),
                e.Owned,e.SilentlyReclaimable,e.Value)).ToImmutableArray());
            var world = new ReferenceWorld(data.Map.Width, data.Map.Height, data.Map.Levels,
                data.Rails.Select(r => new ReferenceRail(new(r.X,r.Y,r.Z),r.Pattern,r.EntityId)), spatial: spatial);
            world.RestoreTime(data.Clock, data.Paused);
            world.RestoreRandom(data.Random);
            return new(world, null);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException or NullReferenceException or InvalidOperationException)
        { throw new InvalidDataException("invalidSave", ex); }
    }
    private static void CheckUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new JsonException("Duplicate property."); CheckUniqueProperties(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckUniqueProperties(item);
    }
}
