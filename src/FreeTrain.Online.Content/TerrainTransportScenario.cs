using System.Collections.Immutable;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

// Deterministic transport fixture; never selected at server startup.
public static class TerrainTransportScenario
{
    // Deliberately edited away from its Empty request: underwater cut, terrace, quarter geometry,
    // realized forest and creek. Loading the request instead of the state would lose all of it.
    public static TerrainSessionState Create(int side = 3)
    {
        var p = TerrainProfiles.Bundled;
        var request = new EmptyWorldRequest(WorldCreationProfile.Id, "保存 / Terrain", new(side, side, 6), 1);
        var cells = Enumerable.Repeat(new TerrainCell(2, TerrainSurface.Cut), side * side).ToArray();
        cells[0] = new(2, TerrainSurface.Terrace);
        var space = new WorldSpace(side, side, 6);
        var forest = p.Definitions.First(d => d.TreeSpriteCount > 0);
        DefinitionReference Binding(TerrainOccupantKind kind) => p.Definitions.First(d => d.Kind == kind && d.TreeSpriteCount == 0).Reference;
        SpatialEntity Entity(long id, TerrainOccupantKind kind, int h, int v, int z, long value = 0) => new(id, 0, Binding(kind),
            space.ToLocation(new(h, v, z)), new([new(new(0, 0, 0), kind == TerrainOccupantKind.Creek)]), false, kind == TerrainOccupantKind.Creek, value);
        var entities = new[] { Entity(1, TerrainOccupantKind.Terrace, 0, 0, 1),
            Entity(2, TerrainOccupantKind.Mountain, 0, 0, 2, forest.Price), Entity(3, TerrainOccupantKind.Creek, 1, 0, 2) };
        var world = new TerrainWorld(p, request.Name, request.Dimensions, 3, cells, new(7, entities.ToImmutableArray()),
            [new(1, TerrainOccupantKind.Terrace, new(1, 2, 3, 4)),
             new(2, TerrainOccupantKind.Mountain, new(1, 2, 3, 4), new(forest.Reference, [new(0, 0, 0), new(30, 15, forest.TreeSpriteCount - 1)])),
             new(3, TerrainOccupantKind.Creek, default, CreekConnections: 13)], 4);
        var state = new TerrainSessionState(world, TerrainProvenance.From(request), true, revision: 5);
        state.Random["forest"].Next(); state.AdvanceOneMinute();
        return state;
    }
    public static TerrainWorld Edit(TerrainSessionState state)
    {
        var w = state.World;
        var entities = w.Spatial.Entities.Where(e => e.Id != 3).Select(e => e.Id == 2 ? e with { Revision = e.Revision + 1 } : e)
            .Append(w.Spatial.Entities.Single(e => e.Id == 3) with { Id = w.Spatial.NextEntityId }).ToImmutableArray();
        var occupants = w.Occupants.Where(o => o.EntityId != 3).Select(o => o.EntityId == 2
            ? o with { Forest = o.Forest! with { Trees = [new(17, 9, 1)] } } : o)
            .Append(new(w.Spatial.NextEntityId, TerrainOccupantKind.Creek, default, CreekConnections: 5));
        return w.Replace(w.Revision, w.Cells, w.WaterLevel, new(w.Spatial.NextEntityId + 1, entities), occupants);
    }

}
