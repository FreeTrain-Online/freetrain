using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Terrain algorithms keep their source ordering/skips. They read the combined
// occupancy graph, allocate above its high-water mark and publish with it once.
public static class MixedTerrain
{
    public static CornerTerrainDraft Draft(MixedWorld world) =>
        new(world.Terrain,world.Spatial.NextEntityId,world.Spatial);

    public static MixedConstructionState Replace(MixedConstructionState state, TerrainWorld terrain, GameTime time)
    {
        var world=state.World;
        var terrainIds=world.Terrain.Spatial.Entities.Select(e=>e.Id).ToHashSet();
        var spatial=new SpatialState(Math.Max(world.Spatial.NextEntityId,terrain.Spatial.NextEntityId),terrain.Spatial.Entities
            .AddRange(world.Spatial.Entities.Where(e=>!terrainIds.Contains(e.Id))).OrderBy(e=>e.Id).ToImmutableArray());
        return state.Replace(world.Revision,terrain,spatial,world.Voxels,world.Slopes,
            ConstructionBooking.None,time,"terrainEdit");
    }
}
