using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

public static class TerrainWire
{
    private sealed record Geometry(SpatialEntityRead[] Entities, TerrainCellRead[] Cells, TerrainOccupantRead[] Occupants);
    private static readonly ConditionalWeakTable<TerrainWorld, Geometry> geometry = new();
    private sealed class Published { public SnapshotMessage? Snapshot; }
    private static readonly ConditionalWeakTable<TerrainSessionState, Published> published = new();
    private static Geometry GeometryFor(TerrainWorld w) => geometry.GetValue(w, world =>
    {
        var values = new Dictionary<TerrainCell, TerrainCellRead>();
        var cells = world.Cells.Select(c =>
        {
            if (!values.TryGetValue(c, out var read)) values.Add(c, read = new(c.Ground, Name(c.Surface)));
            return read;
        }).ToArray();
        return new(Entities(world.Spatial), cells,
            world.Occupants.Select(o => new TerrainOccupantRead(o.EntityId, Name(o.Kind), o.Corners.NE, o.Corners.SE, o.Corners.SW, o.Corners.NW,
                o.Forest is {} f ? new(Definition(f.Definition), f.Trees.Select(t => new TerrainTreeRead(t.X, t.Y, t.Sprite)).ToArray()) : null,
                o.CreekConnections)).ToArray());
    });
    public static SpatialEntityRead[] Entities(SpatialIndex spatial) => spatial.Entities.Select(e => new SpatialEntityRead(e.Id, e.Revision,
        Definition(e.Definition), new(e.Anchor.X, e.Anchor.Y, e.Anchor.Z),
        e.Footprint.Members.Select(m => new VoxelMemberRead(new(m.Offset.X, m.Offset.Y, m.Offset.Z), m.Transparent)).ToArray(),
        e.Owned, e.SilentlyReclaimable, e.Value)).ToArray();
    public static SpatialState Spatial(long nextEntityId, SpatialEntityRead[] entities) => new(nextEntityId, entities.Select(e =>
        new SpatialEntity(e.Id, e.Revision, Definition(e.Definition), new(e.Anchor.X, e.Anchor.Y, e.Anchor.Z),
            new(e.Footprint.Select(m => new VoxelMember(new(m.Offset.X, m.Offset.Y, m.Offset.Z), m.Transparent))),
            e.Owned, e.SilentlyReclaimable, e.Value)).ToImmutableArray());
    private static DefinitionRead Definition(DefinitionReference d) => new(d.PackId, d.ContributionId, d.RecipeId);
    private static DefinitionReference Definition(DefinitionRead d) => new(d.PackId, d.ContributionId, d.RecipeId);
    private static string Name<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
    private static T Parse<T>(string value) where T : struct, Enum => Enum.GetValues<T>().SingleOrDefault(v => Name(v) == value) is var result && Name(result) == value
        ? result : throw new ArgumentException("Invalid terrain enum.");
    public static FreeTrain.Online.Contracts.TerrainRead Read(TerrainSessionState state)
    {
        var w = state.World; var p = state.Provenance;
        return new(w.Name, w.WaterLevel, state.ConstructionMode, w.Revision, w.Spatial.NextEntityId,
            new(p.Profile, p.Source, p.RequestHash, p.RequestJson),
            GeometryFor(w).Cells, GeometryFor(w).Occupants);
    }
    public static TerrainSessionState Restore(FreeTrain.Online.Contracts.TerrainRead data, MapRead map, TerrainProfile profile,
        SpatialEntityRead[] entities, ClockState clock, bool paused, RandomStreamsState random, long revision)
    {
        var world = new TerrainWorld(profile, data.Name, new(map.Width, map.Height, map.Levels), data.WaterLevel,
            data.Cells.Select(c => new TerrainCell(c.Ground, Parse<TerrainSurface>(c.Surface))), Spatial(data.NextEntityId, entities),
            data.Occupants.Select(o => new TerrainOccupant(o.EntityId, Parse<TerrainOccupantKind>(o.Kind), new(o.NE, o.SE, o.SW, o.NW),
                o.Forest is {} f ? new(Definition(f.Definition), f.Trees.Select(t => new TerrainTree(t.X, t.Y, t.Sprite)).ToImmutableArray()) : null,
                o.CreekConnections)), data.GeometryRevision);
        var p = data.Provenance;
        return new(world, new(p.Profile, p.Source, p.RequestHash, p.RequestJson), data.ConstructionMode, clock, paused, random, revision);
    }
    public static SnapshotMessage Snapshot(TerrainSessionState state, long epoch)
    {
        var cache = published.GetOrCreateValue(state);
        lock (cache)
        {
            if (cache.Snapshot is { } previous && previous.WorldEpoch == epoch && previous.Revision == state.Revision) return previous;
            var w = state.World;
            return cache.Snapshot = Header(state, epoch) with { Entities = GeometryFor(w).Entities, Terrain = Read(state) };
        }
    }
    public static SnapshotMessage Header(TerrainSessionState state, long epoch)
    {
        var w = state.World;
        return new(epoch, state.Revision, state.Clock.Time.AbsoluteMinute, state.Paused, 0,
            new(w.Space.HorizontalSize, w.Space.VerticalSize, w.Space.Levels, "hv"), [], [], [], [],
            TerrainProfile.Ruleset, TerrainProfile.ContentVersion,
            w.Profile.Packs.Select(p => new RequiredPackRead(p.Id, p.Version, p.CatalogHash)).ToArray(), []);
    }
}
