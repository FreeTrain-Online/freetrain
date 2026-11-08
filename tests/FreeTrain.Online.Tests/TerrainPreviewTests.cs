using System.Buffers.Binary;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class TerrainPreviewTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 1, 1)] // H neighbors share a corner, not a canonical XY face.
    [InlineData(3, 2, 6)]
    [InlineData(4, 8, 32)]
    public void Flat_dry_availability_uses_world_faces_not_storage_image_neighbors(int h, int v, int largest)
    {
        var world = TerrainWorld.Empty(new(WorldCreationProfile.Id, "Preview", new(h, v, 6), 1), TerrainProfiles.Bundled);
        var overview = TerrainPreview.Capture(world);
        Assert.Equal(new(h * v, 0, 0, h * v, largest, 4, 4), overview.Metrics);
        Assert.Equal(1, overview.Step);
        var samples = Convert.FromBase64String(overview.Samples);
        for (int i = 0; i < h * v; i++) Assert.Equal(new byte[] { 1, 0, 0, 8 }, samples[(4 * i)..(4 * i + 4)]);
    }

    [Fact]
    public void Retained_save_overview_matches_actual_quarters_water_and_occupied_land()
    {
        var world = SaveCodec.ReadWorld(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Saves", "terrain-v10.json")), TerrainProfiles.Resolve).Terrain!.World;
        var overview = TerrainPreview.Capture(world);
        var samples = Convert.FromBase64String(overview.Samples);
        int wet = 0, forests = 0, minimum = int.MaxValue, maximum = 0;
        var available = new HashSet<Location>();
        for (int v = 0; v < world.Space.VerticalSize; v++) for (int h = 0; h < world.Space.HorizontalSize; h++)
        {
            var cell = world.Cell(h, v); var p = world.Space.ToLocation(new(h, v, cell.Ground));
            var heights = Enumerable.Range(0, 4).Select(c => world.FineHeight(p, c)).ToArray();
            minimum = Math.Min(minimum, heights.Min()); maximum = Math.Max(maximum, heights.Max());
            bool water = heights.Any(x => x < 4 * world.WaterLevel);
            var occupant = world.Spatial.At(p).Entity;
            var decoration = occupant == null ? null : world.Occupants.Single(o => o.EntityId == occupant.Id);
            bool forest = decoration?.Forest != null;
            bool clear = !water && heights.All(x => x == 4 * cell.Ground) && cell.Surface == TerrainSurface.Natural &&
                (decoration == null || decoration.Kind == TerrainOccupantKind.Mountain && !forest);
            if (clear) available.Add(p);
            if (water) wet++; if (forest) forests++;
            int i = 4 * (v * world.Space.HorizontalSize + h);
            Assert.Equal(cell.Ground, samples[i]);
            int packed = BinaryPrimitives.ReadUInt16LittleEndian(samples.AsSpan(i + 1, 2));
            Assert.Equal(heights.Select(x => x - 4 * cell.Ground), new[] { packed >> 12, packed >> 8 & 15, packed >> 4 & 15, packed & 15 });
            Assert.Equal((water ? 1 : 0) | (forest ? 2 : 0) | (clear ? 8 : 0), samples[i + 3]);
        }
        int count = available.Count, largest = 0;
        while (available.Count > 0)
        {
            var queue = new Queue<Location>(); var first = available.First(); queue.Enqueue(first); available.Remove(first); int size = 0;
            while (queue.TryDequeue(out var p))
            {
                size++;
                foreach (var direction in new[] { 0, 2, 4, 6 })
                {
                    var next = p.Move(new Heading(direction));
                    if (available.Remove(next)) queue.Enqueue(next);
                }
            }
            largest = Math.Max(largest, size);
        }
        Assert.Equal(new(world.Cells.Length, wet, forests, count, largest, minimum, maximum), overview.Metrics);
    }

    [Fact]
    public void Full_size_overview_is_bounded_metrics_are_full_grid_and_cancellation_is_observed()
    {
        var world = TerrainWorld.Empty(new(WorldCreationProfile.Id, "Large", new(1500, 1090, 45), 10), TerrainProfiles.Bundled);
        var overview = TerrainPreview.Capture(world);
        Assert.Equal((125, 91, 12), (overview.Width, overview.Height, overview.Step));
        Assert.Equal(1_635_000, overview.Metrics.Cells);
        Assert.Equal(1_635_000, overview.Metrics.LargestBuildableArea);
        Assert.True(Convert.FromBase64String(overview.Samples).Length <= 128 * 128 * 4);
        Assert.Equal(40, overview.Metrics.MinimumQuarterHeight);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => TerrainPreview.Capture(world, cancellation: cancellation.Token));
    }
}
