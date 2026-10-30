using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

[Collection("World creation")]
public class ShoreTerrainTests
{
    [Theory]
    [InlineData(ReliefKind.Hills)]
    [InlineData(ReliefKind.Mountains)]
    public void Every_water_feature_has_two_complete_flat_voxels_before_relief_rises(ReliefKind kind)
    {
        var features = new[]
        {
            CoastTerrainTests.Request(CoastKind.Island),
            CoastTerrainTests.Request(CoastKind.Sides, CoastSides.North | CoastSides.East),
            WaterTerrainTests.Request(),
            WaterTerrainTests.Request(0, RiverDirection.NorthSouth),
            WaterTerrainTests.Request(0, RiverDirection.EastWest),
            WaterTerrainTests.Request(1, RiverDirection.NorthSouth, CoastKind.Sides, CoastSides.North)
        };
        foreach (var feature in features) foreach (int seed in new[] { 0, 42, int.MinValue })
        {
            var request = feature with { Dimensions = new(96, 80, 32), Options = feature.Options with
            { Seed = seed, Relief = WorldCreationProfile.Relief(kind) } };
            var world = ReliefTerrain.Create(request, TerrainProfiles.Bundled);
            WaterTerrain.Validate(request, world);
            var surface = Surface(world);
            var distances = WaterDistances(surface, request.WaterLevel);
            var shelf = surface.Where(p => distances[p.Key] is 1 or 2 && p.Value.Min() >= 4 * request.WaterLevel).ToArray();
            Assert.NotEmpty(shelf);
            Assert.All(shelf, cell => Assert.All(cell.Value, height => Assert.Equal(4 * request.WaterLevel, height)));
            Assert.Contains(surface.Values, heights => heights.Max() > 4 * (request.WaterLevel + 1));
        }
    }

    [Fact]
    public void Shelf_width_varies_along_straight_banks_and_replays_for_the_same_seed()
    {
        var request = WaterTerrainTests.Request(0, RiverDirection.NorthSouth) with { Dimensions = new(128, 128, 16) };
        // Constant relief and a straight channel isolate shoreline width variation.
        request = request with { Options = request.Options with
        { Relief = new(ReliefKind.Hills, 0, 12, 100), Rivers = request.Options.Rivers with { Meander = 0 } } };
        var world = ReliefTerrain.Create(request, TerrainProfiles.Bundled);
        var surface = Surface(world);
        var distances = WaterDistances(surface, request.WaterLevel);
        Assert.Contains(Enumerable.Range(3, 5), distance =>
            surface.Any(p => distances[p.Key] == distance && p.Value.All(height => height == 4 * request.WaterLevel)) &&
            surface.Any(p => distances[p.Key] == distance && p.Value.Max() > 4 * request.WaterLevel));
        var replay = ReliefTerrain.Create(request, TerrainProfiles.Bundled);
        Assert.Equal(world.Cells.ToArray(), replay.Cells.ToArray());
        Assert.Equal(world.Occupants.ToArray(), replay.Occupants.ToArray());
        var changed = ReliefTerrain.Create(request with { Options = request.Options with { Seed = 42 } }, TerrainProfiles.Bundled);
        Assert.NotEqual(world.Cells.ToArray(), changed.Cells.ToArray());
    }

    private static Dictionary<(int X, int Y), int[]> Surface(TerrainWorld world)
    {
        var surface = new Dictionary<(int, int), int[]>();
        for (int v = 0; v < world.Space.VerticalSize; v++) for (int h = 0; h < world.Space.HorizontalSize; h++)
        {
            var p = world.Space.ToLocation(new(h, v, world.Cell(h, v).Ground));
            surface[(p.X, p.Y)] = Enumerable.Range(0, 4).Select(c => world.FineHeight(p, c)).ToArray();
        }
        return surface;
    }

    // Independent cardinal XY flood from actual submerged surfaces, including
    // fractional corners; no production mask, neighbor or shelf helpers are used.
    private static Dictionary<(int X, int Y), int> WaterDistances(Dictionary<(int X, int Y), int[]> surface, int water)
    {
        var distance = surface.Where(p => p.Value.Min() < 4 * water).ToDictionary(p => p.Key, _ => 0);
        var queue = new Queue<(int X, int Y)>(distance.Keys);
        while (queue.TryDequeue(out var p))
            foreach (var q in new[] { (p.X - 1, p.Y), (p.X + 1, p.Y), (p.X, p.Y - 1), (p.X, p.Y + 1) })
                if (surface.ContainsKey(q) && distance.TryAdd(q, distance[p] + 1)) queue.Enqueue(q);
        Assert.Equal(surface.Count, distance.Count);
        return distance;
    }
}
