using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

[Collection("World creation")]
public class ForestTerrainTests
{
    internal static GeneratedWorldRequest Request(int coverage = 70, int starting = 64, string variant = "natural")
    {
        var r = WorldCreationProfile.Generated(0, "Forest / 森林");
        return r with { Dimensions = new(32, 27, 32), Options = r.Options with { StartingArea = starting,
            Forests = new(coverage, 50, coverage == 0 ? [] : WorldCreationCapabilities.ForestChoices()
                .Where(c => c.Variant == variant).Select(c => c.Binding).ToImmutableArray()) } };
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static IEnumerable<object[]> Fixtures()
    {
        using var f = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Spatial/forest-fixtures.json")));
        return f.RootElement.GetProperty("cases").EnumerateArray().Select(r => new object[] { r.Clone() }).ToArray();
    }
    [Theory, MemberData(nameof(Fixtures))]
    public void Independent_XYZ_forest_reservation_and_realized_scatter_literals_match(JsonElement row)
    {
        int N(string key) => row.GetProperty(key).GetInt32();
        var r = Request(N("coverage"), N("starting"), row.TryGetProperty("variant", out var variant)
            ? new[] { "natural", "mountain", "grass" }[variant.GetInt32()] : "natural");
        r = r with { Dimensions = new(N("h"), N("v"), N("z")), WaterLevel = N("water"), Options = r.Options with { Seed = N("seed"),
            Forests = r.Options.Forests with { Clustering = N("clustering") } } };
        if (row.TryGetProperty("tree", out var tree))
            r = r with { Options = r.Options with { Relief = new(ReliefKind.Plains, 0, 4, 0), Forests = r.Options.Forests with {
                Palette = [WorldCreationCapabilities.ForestChoices().Single(c => c.Binding.ContributionId == tree.GetString()).Binding] } } };
        if (row.TryGetProperty("island", out var island) && island.GetBoolean())
            r = r with { Options = r.Options with { Coast = new(CoastKind.Island, CoastSides.None, 40) } };
        if (row.TryGetProperty("error", out var error))
        {
            Assert.Empty(WorldCreationProfile.Validate(r));
            Assert.Equal(error.GetString(), Assert.Throws<GeographyException>(() => ReliefTerrain.Create(r, TerrainProfiles.Bundled)).Constraint);
            return;
        }
        var world = ReliefTerrain.Create(r, TerrainProfiles.Bundled); var layout = ForestTerrain.Layout(r, world);
        var metrics = ForestTerrain.Measure(r, world, layout);
        Assert.Equal(new ForestMetrics(N("eligibleCells"), N("maskCells"), N("forestCells"), N("trees"), N("reservedCells"), N("largestFlatArea")), metrics);
        var records = world.Occupants.Zip(world.Spatial.Entities).Where(pair => pair.First.Forest is not null).Select(pair =>
        {
            var p = world.Space.ToStorage(pair.Second.Anchor); var f = pair.First.Forest!;
            return (Index: p.V * r.Dimensions.H + p.H, Forest: f);
        }).OrderBy(pair => pair.Index);
        var literal = string.Join("\n", records.Select(pair => $"{pair.Index}:{pair.Forest.Definition.ContributionId}:" +
            string.Join(";", pair.Forest.Trees.Select(t => $"{t.X},{t.Y},{t.Sprite}"))));
        Assert.Equal(row.GetProperty("forestHash").GetString(), Hash(Encoding.UTF8.GetBytes(literal)));
        Assert.Equal(row.GetProperty("startingHash").GetString(), Hash(layout.Reserved.Select(x => x ? (byte)1 : (byte)0).ToArray()));
        Assert.All(world.Occupants.Where(o => o.Forest != null), o => Assert.Equal(o.Forest!.Trees.OrderBy(t => t.Y), o.Forest.Trees));
        var save = SaveCodec.Write(new TerrainSessionState(world, TerrainProvenance.From(r)));
        Assert.Equal(save, SaveCodec.Write(SaveCodec.ReadWorld(save, TerrainProfiles.Resolve).Terrain!));
    }
    [Fact]
    public void Source_binomial_scatter_preserves_position_only_sort_and_zero_outcomes()
    {
        var values = new Queue<int>([0, 1, 0, 0, 0, 15, 2, 15, 0, 1, 2, 2, 0]);
        Assert.Equal(new[] { new TerrainTree(15, 0, 2), new TerrainTree(4, 8, 1), new TerrainTree(15, 15, 0) },
            ForestTerrain.Scatter(2, 3, _ => values.Dequeue()));
        Assert.Empty(values); Assert.Empty(ForestTerrain.Scatter(3, 9, _ => 1));
        Assert.Throws<ArgumentException>(() => ForestTerrain.Scatter(2, 3, _ => 2));
    }
    [Theory]
    [InlineData(0, 0)][InlineData(15, 0)][InlineData(35, 50)][InlineData(70, 100)][InlineData(90, 100)]
    public void Forest_controls_preserve_actual_water_ground_corners_and_connected_level_reservation(int coverage, int clustering)
    {
        var r = WaterTerrainTests.Request(1, RiverDirection.NorthSouth, CoastKind.Sides, CoastSides.North);
        var baseline = ReliefTerrain.Create(r, TerrainProfiles.Bundled);
        r = r with { Options = r.Options with { StartingArea = 16, Forests = Request(coverage).Options.Forests with { Clustering = clustering } } };
        var world = ReliefTerrain.Create(r, TerrainProfiles.Bundled);
        Assert.Equal(baseline.Cells.ToArray(), world.Cells.ToArray()); Assert.Equal(CoastTerrain.SeaMask(baseline), CoastTerrain.SeaMask(world));
        for (int v = 0; v < r.Dimensions.V; v++) for (int h = 0; h < r.Dimensions.H; h++)
        {
            var p = world.Space.ToLocation(new(h, v, world.Cell(h, v).Ground));
            for (int c = 0; c < 4; c++) Assert.Equal(baseline.FineHeight(p, c), world.FineHeight(p, c));
        }
        Assert.Equal(WaterTerrain.Validate(r, baseline), WaterTerrain.Validate(r, world));
        var layout = ForestTerrain.Layout(r, world); Assert.Equal(16, layout.Reserved.Count(x => x));
        // Independent canonical face walk, without the generator's neighbor helper.
        var points = Enumerable.Range(0, layout.Reserved.Length).Where(i => layout.Reserved[i])
            .Select(i => world.Space.ToLocation(new(i % r.Dimensions.H, i / r.Dimensions.H, world.Cells[i].Ground))).ToHashSet();
        Assert.Single(points.Select(p => p.Z).Distinct()); var seen = new HashSet<Location>(); var queue = new Queue<Location>();queue.Enqueue(points.First());
        while (queue.TryDequeue(out var p)) if (seen.Add(p)) foreach (var heading in new[] { 0, 2, 4, 6 })
        { var n = p.Move(new(heading)); if (points.Contains(n) && !seen.Contains(n)) queue.Enqueue(n); }
        Assert.Equal(points.Count, seen.Count);
        foreach (var p in points) for (int c = 0; c < 4; c++) Assert.Equal(4 * p.Z, world.FineHeight(p, c));
        var metrics = ForestTerrain.Measure(r, world, layout); Assert.True(metrics.ForestCells <= metrics.MaskCells);
        Assert.Equal(metrics, ForestTerrain.Measure(r, ReliefTerrain.Create(r, TerrainProfiles.Bundled), layout));
    }
    [Fact]
    public void High_clustering_forms_larger_groves_on_identical_flat_land()
    {
        var r = Request(35, 0); r = r with { Dimensions = new(128, 127, 32), Options = r.Options with {
            Relief = new(ReliefKind.Plains, 0, 4, 0), Forests = r.Options.Forests with { Palette = [r.Options.Forests.Palette.First(b => b.ContributionId.StartsWith("{FBD0"))] } } };
        double Adjacent(int clustering)
        {
            var world = ReliefTerrain.Create(r with { Options = r.Options with { Forests = r.Options.Forests with { Clustering = clustering } } }, TerrainProfiles.Bundled);
            var trees = world.Occupants.Zip(world.Spatial.Entities).Where(p => p.First.Forest != null).Select(p => p.Second.Anchor).ToHashSet();
            return trees.Sum(p => new[] { 0, 2, 4, 6 }.Count(h => trees.Contains(p.Move(new(h))))) / (double)trees.Count;
        }
        Assert.True(Adjacent(100) > Adjacent(0) * 1.5);
    }
    [Theory]
    [InlineData(8, 1)][InlineData(64, 1)][InlineData(64, 60)]
    public void Forests_respect_water_and_headroom_extremes(int z, int water)
    {
        var r = Request(90, 0); r = r with { Dimensions = new(16, 17, z), WaterLevel = water };
        var world = ReliefTerrain.Create(r, TerrainProfiles.Bundled);
        Assert.True(ForestTerrain.Measure(r, world, ForestTerrain.Layout(r, world)).ForestCells > 0);
        Assert.All(world.Spatial.Entities, e => Assert.InRange(e.Anchor.Z, water, z - 1));
    }
    [Fact]
    public void Clustering_palette_and_coverage_change_forests_without_touching_live_randomness()
    {
        var r = Request(35); var state = new TerrainSessionState(ReliefTerrain.Create(r, TerrainProfiles.Bundled), TerrainProvenance.From(r));
        var before = JsonSerializer.Serialize(state.Random.Capture());
        byte[] Forests(GeneratedWorldRequest q) => PackedTerrain.Capture(new TerrainSessionState(ReliefTerrain.Create(q, TerrainProfiles.Bundled), TerrainProvenance.From(q))).Occupants;
        var original = Forests(r);
        Assert.NotEqual(original, Forests(r with { Options = r.Options with { Forests = r.Options.Forests with { Clustering = 100 } } }));
        Assert.NotEqual(original, Forests(Request(70)));
        Assert.NotEqual(original, Forests(r with { Options = r.Options with { Forests = r.Options.Forests with { Palette = [r.Options.Forests.Palette[0]] } } }));
        Assert.Equal(original, Forests(r with { Options = r.Options with { Forests = r.Options.Forests with { Palette = r.Options.Forests.Palette.Reverse().ToImmutableArray() } } }));
        Assert.Equal(before, JsonSerializer.Serialize(state.Random.Capture()));
        var choices = WorldCreationCapabilities.ForestChoices(); Assert.Equal(18, choices.Length); Assert.Equal(6, choices.Select(c => c.Species).Distinct().Count());
        Assert.Empty(WorldCreationCapabilities.GeneratedReady().Validate(r));
        Assert.NotEmpty(WorldCreationCapabilities.EmptyReady().Validate(r));
        var bad = r with { Options = r.Options with { Forests = r.Options.Forests with { Palette = [r.Options.Forests.Palette[0] with { CatalogHash = new('b', 64) }] } } };
        Assert.Contains(WorldCreationCapabilities.GeneratedReady().Validate(bad), i => i.Code == "content-missing");
        Assert.Throws<ArgumentException>(() => ReliefTerrain.Create(bad, TerrainProfiles.Bundled));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => ReliefTerrain.Create(r, TerrainProfiles.Bundled, cancel.Token));
    }
}
