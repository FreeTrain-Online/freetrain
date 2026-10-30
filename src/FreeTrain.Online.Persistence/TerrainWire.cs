using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

public static class TerrainWire
{
    private sealed record Geometry(SpatialEntityRead[] Entities, TerrainCellRead[] Cells, TerrainOccupantRead[] Occupants);
    private sealed record Publication(object Identity, object? Previous, TerrainCellChangeRead[] Cells,
        SpatialEntityRead[] Entities, TerrainOccupantRead[] Occupants, long[] Removed);
    private static readonly ConditionalWeakTable<SnapshotMessage, Publication> publications = new();
    private static readonly ConditionalWeakTable<object, Geometry> geometry = new();
    private sealed class Published { public SnapshotMessage? Snapshot; }
    private static readonly ConditionalWeakTable<TerrainSessionState, Published> published = new();
    private static Geometry GeometryFor(TerrainWorld w) => geometry.GetValue(w.GeometryIdentity, _ =>
    {
        var world = w;
        if (w.PreviousGeometryIdentity is {} predecessor && geometry.TryGetValue(predecessor, out var before))
        {
            var editedCells = before.Cells;
            if (!w.ChangedCells.IsEmpty)
            {
                editedCells = (TerrainCellRead[])editedCells.Clone();
                foreach (var i in w.ChangedCells) editedCells[i] = new(w.Cells[i].Ground,Name(w.Cells[i].Surface));
            }
            if (w.ChangedEntities.IsEmpty) return before with { Cells = editedCells };
            var changed = w.ChangedEntities.ToHashSet();
            var entities = new SpatialEntityRead[w.Spatial.Entities.Length];
            var occupants = new TerrainOccupantRead[w.Occupants.Length];
            int j = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var e = w.Spatial.Entities[i]; var o = w.Occupants[i];
                while (j < before.Entities.Length && before.Entities[j].Id < e.Id) j++;
                if (!changed.Contains(e.Id) && j < before.Entities.Length && before.Entities[j].Id == e.Id)
                { entities[i] = before.Entities[j]; occupants[i] = before.Occupants[j]; }
                else
                {
                    entities[i] = Entity(e);
                    occupants[i] = new(o.EntityId,Name(o.Kind),o.Corners.NE,o.Corners.SE,o.Corners.SW,o.Corners.NW,
                        o.Forest is {} f ? new(Definition(f.Definition),f.Trees.Select(t => new TerrainTreeRead(t.X,t.Y,t.Sprite)).ToArray()) : null,o.CreekConnections);
                }
            }
            return new(entities,editedCells,occupants);
        }
        var values = new Dictionary<TerrainCell, TerrainCellRead>();
        var cells = world.Cells.Select(c =>
        {
            if (!values.TryGetValue(c, out var read)) values.Add(c, read = new(c.Ground, Name(c.Surface)));
            return read;
        }).ToArray();
        // Forest-heavy worlds contain millions of repeated immutable triples and
        // exact definitions. Share values within this geometry, with no global value cache.
        var definitions = new Dictionary<DefinitionReference, DefinitionRead>();
        DefinitionRead DefinitionFor(DefinitionReference reference)
        {
            if (!definitions.TryGetValue(reference, out var read)) definitions.Add(reference, read = Definition(reference));
            return read;
        }
        var trees = new Dictionary<TerrainTree, TerrainTreeRead>();
        TerrainTreeRead TreeFor(TerrainTree tree)
        {
            if (!trees.TryGetValue(tree, out var read)) trees.Add(tree, read = new(tree.X, tree.Y, tree.Sprite));
            return read;
        }
        return new(Entities(world.Spatial), cells,
            world.Occupants.Select(o => new TerrainOccupantRead(o.EntityId, Name(o.Kind), o.Corners.NE, o.Corners.SE, o.Corners.SW, o.Corners.NW,
                o.Forest is {} f ? new(DefinitionFor(f.Definition), f.Trees.Select(TreeFor).ToArray()) : null,
                o.CreekConnections)).ToArray());
    });
    private static SpatialEntityRead Entity(SpatialEntity e) => new(e.Id,e.Revision,Definition(e.Definition),
        new(e.Anchor.X,e.Anchor.Y,e.Anchor.Z),e.Footprint.Members.Select(m => new VoxelMemberRead(new(m.Offset.X,m.Offset.Y,m.Offset.Z),m.Transparent)).ToArray(),
        e.Owned,e.SilentlyReclaimable,e.Value);
    public static bool TryDifference(SnapshotMessage before, SnapshotMessage after, out DeltaMessage delta)
    {
        delta = null!;
        if (before.WorldEpoch != after.WorldEpoch || !publications.TryGetValue(before,out var a) ||
            !publications.TryGetValue(after,out var b) || (a.Identity != b.Identity && a.Identity != b.Previous)) return false;
        var same = a.Identity == b.Identity; var t = after.Terrain!;
        delta = new(after.WorldEpoch,before.Revision,after.Revision,after.Tick,after.Paused,after.Cash,[],[],[],[],
            new([],[],[],[],same ? [] : b.Removed),after.Ruleset,after.ContentVersion,after.RequiredPacks,
            same ? [] : b.Entities,new(t.WaterLevel,t.ConstructionMode,t.GeometryRevision,t.NextEntityId,
                same ? [] : b.Cells,same ? [] : b.Occupants));
        return true;
    }
    public static SpatialEntityRead[] Entities(SpatialIndex spatial)
    {
        var definitions = new Dictionary<DefinitionReference, DefinitionRead>();
        var footprints = new Dictionary<Footprint, VoxelMemberRead[]>();
        return spatial.Entities.Select(e =>
        {
            if (!definitions.TryGetValue(e.Definition, out var definition)) definitions.Add(e.Definition, definition = Definition(e.Definition));
            if (!footprints.TryGetValue(e.Footprint, out var footprint)) footprints.Add(e.Footprint, footprint = e.Footprint.Members
                .Select(m => new VoxelMemberRead(new(m.Offset.X, m.Offset.Y, m.Offset.Z), m.Transparent)).ToArray());
            return new SpatialEntityRead(e.Id, e.Revision, definition, new(e.Anchor.X, e.Anchor.Y, e.Anchor.Z), footprint,
                e.Owned, e.SilentlyReclaimable, e.Value);
        }).ToArray();
    }
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
            var g = GeometryFor(w);
            var snapshot = Header(state, epoch) with { Entities = g.Entities, Terrain = Read(state) };
            if (cache.Snapshot is {} prior && publications.TryGetValue(prior,out var publication) && publication.Identity == w.GeometryIdentity)
            { publications.Add(snapshot,publication); return cache.Snapshot = snapshot; }
            var ids = w.ChangedEntities.ToHashSet();
            var changedEntities = g.Entities.Where(e => ids.Contains(e.Id)).ToArray();
            var changedOccupants = g.Occupants.Where(o => ids.Contains(o.EntityId)).ToArray();
            publications.Add(snapshot,new(w.GeometryIdentity,w.PreviousGeometryIdentity,
                w.ChangedCells.Select(i => new TerrainCellChangeRead(i,g.Cells[i].Ground,g.Cells[i].Surface)).ToArray(),
                changedEntities,changedOccupants,w.ChangedEntities.Except(changedEntities.Select(e => e.Id)).ToArray()));
            return cache.Snapshot = snapshot;
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
