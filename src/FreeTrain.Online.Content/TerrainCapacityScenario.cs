using System.Collections.Immutable;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

// Canonical capacity fixture using implemented terrain, terraces and six realized
// trees per decorated cell. No historical save/plugin execution or future gameplay.
public static class TerrainCapacityScenario
{
    public static TerrainSessionState Create(int h, int v, int z, int stride = 1)
    {
        if (stride < 1) throw new ArgumentOutOfRangeException(nameof(stride));
        var p = TerrainProfiles.Bundled;
        var request = new EmptyWorldRequest(WorldCreationProfile.Id, "Capacity / 容量", new(h, v, z), 1);
        if (!WorldCreationProfile.Validate(request).IsEmpty || z < 4) throw new ArgumentException("Invalid capacity fixture.");
        var space = new WorldSpace(h, v, z);
        var cells = new TerrainCell[h * v];
        var entities = ImmutableArray.CreateBuilder<SpatialEntity>(); var occupants = new List<TerrainOccupant>();
        var forest = p.Definitions.First(d => d.TreeSpriteCount > 0);
        var mountain = p.Definitions.Single(d => d.Reference.ContributionId == TerrainProfile.MountainId).Reference;
        var terrace = p.Definitions.Single(d => d.Kind == TerrainOccupantKind.Terrace).Reference;
        var footprint = new Footprint([new(new(0, 0, 0), false)]);
        var trees = Enumerable.Range(0, 6).Select(n => new TerrainTree(n * 6, n * 3, n % forest.TreeSpriteCount)).ToImmutableArray();
        var decoration = new ForestDecoration(forest.Reference, trees);
        var cut = new TerrainCell(2, TerrainSurface.Cut); var terraced = new TerrainCell(2, TerrainSurface.Terrace);
        long id = 1;
        for (var row = 0; row < v; row++) for (var column = 0; column < h; column++)
        {
            var i = row * h + column; cells[i] = i % stride == 0 ? terraced : cut;
            if (i % stride != 0) continue;
            var anchor = space.ToLocation(new(column, row, 1));
            entities.Add(new(id, 0, terrace, anchor, footprint, false, false, 0));
            occupants.Add(new(id++, TerrainOccupantKind.Terrace, new(1, 2, 3, 4)));
            entities.Add(new(id, 0, mountain, anchor with { Z = 2 }, footprint, false, false, forest.Price));
            occupants.Add(new(id++, TerrainOccupantKind.Mountain, new(1, 2, 3, 4), decoration));
        }
        return new(new(p, request.Name, request.Dimensions, request.WaterLevel, cells,
            new(id, entities.ToImmutable()), occupants), TerrainProvenance.From(request));
    }
}
