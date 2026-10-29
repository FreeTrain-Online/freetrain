using System.Text.Json;
using System.Buffers.Binary;
using System.Security.Cryptography;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using FreeTrain.Online.Server;
using Xunit;
using Xunit.Abstractions;

[Collection("World creation")]
public class CoastTerrainTests(ITestOutputHelper output)
{
    internal static GeneratedWorldRequest Request(CoastKind kind, CoastSides sides = CoastSides.None, int percent = 40, int seed = 0)
    {
        var r = WorldCreationProfile.Generated(seed, "Coast / 海岸");
        return r with { Dimensions = new(32, 27, 32), Options = r.Options with { StartingArea = 0, Coast = new(kind, sides, percent) } };
    }
    public static IEnumerable<object[]> Coasts() => Enumerable.Range(1, 15).Select(mask => new object[] { CoastKind.Sides, (CoastSides)mask })
        .Append([CoastKind.Island, CoastSides.None]);

    [Fact]
    public void Partially_submerged_neighbors_separated_by_a_dry_shared_edge_are_different_seas()
    {
        var d = new WorldDimensions(1, 4, 8); var space = new WorldSpace(d.H, d.V, d.Z);
        var definition = TerrainProfiles.Bundled.Definitions.Single(x => x.Reference.ContributionId == TerrainProfile.MountainId).Reference;
        var entities = new List<SpatialEntity>(); var occupants = new List<TerrainOccupant>();
        int Height(int x, int y) => x == 0 && y is 0 or 1 ? 4 : 0;
        for (int v = 0; v < 4; v++)
        {
            var p = space.ToLocation(new(0,v,0)); long id = v+1;
            entities.Add(new(id,0,definition,p,new([new(new(0,0,0),false)]),false,false,0));
            occupants.Add(new(id,TerrainOccupantKind.Mountain,new(Height(p.X,p.Y-1),Height(p.X,p.Y),Height(p.X-1,p.Y),Height(p.X-1,p.Y-1))));
        }
        var world = new TerrainWorld(TerrainProfiles.Bundled,"Shared dry edge",d,1,Enumerable.Repeat(new TerrainCell(0),4),new(5,[..entities]),occupants);
        Assert.All(CoastTerrain.SeaMask(world), wet => Assert.True(wet));
        Assert.Equal(2,CoastTerrain.Measure(world).SeaComponents);
    }

    [Fact]
    public void Independent_Python_literal_geometry_and_metrics_match()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Spatial/coast-fixtures.json")));
        string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            int N(string key) => row.GetProperty(key).GetInt32();
            var r = Request(row.GetProperty("island").GetBoolean() ? CoastKind.Island : CoastKind.Sides, (CoastSides)N("sides"), N("percent"), N("seed"))
                with { Dimensions = new(N("h"), N("v"), N("z")), WaterLevel = N("water") };
            if (row.TryGetProperty("error", out var error))
            { Assert.Equal(error.GetString(), Assert.Throws<GeographyException>(() => ReliefTerrain.Create(r, TerrainProfiles.Bundled)).Constraint); continue; }
            var world = ReliefTerrain.Create(r, TerrainProfiles.Bundled); Oracle(r, world);
            Assert.Equal(row.GetProperty("groundHash").GetString(), Hash(world.Cells.Select(c => (byte)c.Ground).ToArray()));
            var corners = new byte[world.Cells.Length * 2];
            for (int k = 0; k < world.Occupants.Length; k++)
            {
                var p = world.Space.ToStorage(world.Spatial.Entities[k].Anchor);
                BinaryPrimitives.WriteUInt16LittleEndian(corners.AsSpan((p.V * r.Dimensions.H + p.H) * 2), (ushort)world.Occupants[k].Corners.Packed);
            }
            Assert.Equal(row.GetProperty("cornerHash").GetString(), Hash(corners));
            var actual = CoastTerrain.Validate(r, world);
            Assert.Equal(N("seaCells"), actual.SeaCells); Assert.Equal(N("landCells"), actual.LandCells);
            Assert.Equal(N("seaComponents"), actual.SeaComponents); Assert.Equal(N("landComponents"), actual.LandComponents);
            Assert.Equal((CoastSides)N("seaSides"), actual.SeaSides);
        }
    }

    // Independent oracle: construct the XY domain, inspect the four actual surface
    // heights and flood via cardinal XYZ offsets. Do not reuse production traversal/sectors.
    private static void Oracle(GeneratedWorldRequest r, TerrainWorld world)
    {
        var sites = (from v in Enumerable.Range(0, r.Dimensions.V) from h in Enumerable.Range(0, r.Dimensions.H)
                     let p = world.Space.ToLocation(new(h, v, world.Cell(h, v).Ground)) select p).ToArray();
        var heights = sites.ToDictionary(p => (p.X, p.Y), p => Enumerable.Range(0,4).Select(c => world.FineHeight(p,c)).ToArray());
        var wet = sites.ToDictionary(p => (p.X, p.Y), p => Enumerable.Range(0, 4).Min(c => world.FineHeight(p, c)) < r.WaterLevel * 4);
        var seen = new HashSet<(int, int)>(); int land = 0, sea = 0, count = 0; var actualSides = new HashSet<CoastSides>();
        int xmin = sites.Min(p => p.X), xmax = sites.Max(p => p.X), ymin = sites.Min(p => p.Y), ymax = sites.Max(p => p.Y);
        CoastSides Sector(Location p)
        {
            double x = (2d * p.X - xmin - xmax) / (xmax - xmin), y = (2d * p.Y - ymin - ymax) / (ymax - ymin);
            return Math.Abs(y) >= Math.Abs(x) ? y < 0 ? CoastSides.North : CoastSides.South : x < 0 ? CoastSides.West : CoastSides.East;
        }
        var boundary = sites.Where(p => { var s = world.Space.ToStorage(p); return s.H == 0 || s.V == 0 || s.H == r.Dimensions.H - 1 || s.V == r.Dimensions.V - 1; })
            .ToDictionary(p => (p.X, p.Y), Sector);
        foreach (var start in wet.Keys)
        {
            if (!seen.Add(start)) continue;
            bool isSea = wet[start], outlet = false; if (isSea) sea++; else land++;
            var queue = new Queue<(int X, int Y)>(); queue.Enqueue(start);
            while (queue.TryDequeue(out var p))
            {
                if (isSea) { count++; if (boundary.TryGetValue(p, out var side)) { outlet = true; actualSides.Add(side); } }
                foreach (var q in new[] { (p.X + 1, p.Y), (p.X - 1, p.Y), (p.X, p.Y + 1), (p.X, p.Y - 1) })
                {
                    var (a,b) = q.Item1 > p.X ? (0,1) : q.Item1 < p.X ? (2,3) : q.Item2 > p.Y ? (1,2) : (0,3);
                    if (isSea && Math.Min(heights[p][a],heights[p][b]) >= 4*r.WaterLevel) continue;
                    if (wet.TryGetValue(q, out bool value) && value == isSea && seen.Add(q)) queue.Enqueue(q);
                }
            }
            if (isSea) Assert.True(outlet, "Enclosed sea component");
        }
        Assert.Equal(1, land);
        CoastSides expected = r.Options.Coast.Kind == CoastKind.Island ? (CoastSides)15 : r.Options.Coast.Sides;
        Assert.Equal(expected, actualSides.Aggregate(CoastSides.None, (a, b) => a | b));
        Assert.InRange(100d * count / wet.Count, r.Options.Coast.WaterPercent - 5, r.Options.Coast.WaterPercent + 5);
        if (r.Options.Coast.Kind == CoastKind.Island) { Assert.Equal(1, sea); Assert.All(boundary.Keys, p => Assert.True(wet[p])); }
        Assert.All(boundary, pair => { if ((expected & pair.Value) == 0) Assert.False(wet[pair.Key]); });
    }

    [Theory, MemberData(nameof(Coasts))]
    public void All_asymmetric_sectors_and_combinations_have_real_surface_connectivity(CoastKind kind, CoastSides sides)
    {
        foreach (int seed in new[] { 0, 42, int.MinValue })
        {
            var r = Request(kind, sides, seed: seed);
            var world = ReliefTerrain.Create(r, TerrainProfiles.Bundled); Oracle(r, world);
            var metrics = CoastTerrain.Validate(r, world);
            output.WriteLine($"{kind}/{sides} seed={seed}: {JsonSerializer.Serialize(metrics)}");
        }
    }

    [Fact]
    public void Fractal_coasts_change_the_surface_between_seeds_beyond_single_cell_ties()
    {
        foreach (var (kind, sides) in new[] { (CoastKind.Island, CoastSides.None), (CoastKind.Sides, CoastSides.North),
            (CoastKind.Sides, CoastSides.North | CoastSides.East), (CoastKind.Sides, CoastSides.North | CoastSides.South) })
        {
            var r = Request(kind, sides) with { Dimensions = new(128, 96, 16) };
            // Flat relief isolates coastline variation from seeded mountain heights.
            r = r with { Options = r.Options with { Relief = new(ReliefKind.Plains, 0, 24, 100) } };
            var first = ReliefTerrain.Create(r, TerrainProfiles.Bundled); Oracle(r, first);
            var mask = CoastTerrain.SeaMask(first);
            Assert.Equal(mask, CoastTerrain.SeaMask(ReliefTerrain.Create(r, TerrainProfiles.Bundled)));
            var changed = r with { Options = r.Options with { Seed = 42 } };
            var second = ReliefTerrain.Create(changed, TerrainProfiles.Bundled); Oracle(changed, second);
            int difference = mask.Zip(CoastTerrain.SeaMask(second)).Count(pair => pair.First != pair.Second);
            Assert.True(difference > mask.Length / 50, $"{kind}/{sides}: only {difference} sea cells changed between seeds");
        }
    }

    [Fact]
    public void Realized_coasts_replay_save_and_water_coverage_controls()
    {
        var r = Request(CoastKind.Island);
        byte[] Save(GeneratedWorldRequest request) => SaveCodec.Write(new TerrainSessionState(ReliefTerrain.Create(request, TerrainProfiles.Bundled), TerrainProvenance.From(request)));
        var bytes = Save(r); Assert.Equal(bytes, Save(r)); Assert.Equal(bytes, SaveCodec.Write(SaveCodec.ReadWorld(bytes, TerrainProfiles.Resolve).Terrain!));
        Assert.NotEqual(bytes, Save(r with { Options = r.Options with { Seed = 42 } }));
        foreach (int water in new[] { 1, 4, 28 }) foreach (int coverage in new[] { 25, 50, 75 })
        {
            var changed = r with { WaterLevel = water, Options = r.Options with { Coast = r.Options.Coast with { WaterPercent = coverage } } };
            Oracle(changed, ReliefTerrain.Create(changed, TerrainProfiles.Bundled));
        }
    }

    [Fact]
    public void Impossible_small_island_reports_unsatisfied_constraint_and_cancellation_is_bounded()
    {
        var r = Request(CoastKind.Island, percent: 5) with { Dimensions = new(16, 16, 8), WaterLevel = 1 };
        var error = Assert.Throws<GeographyException>(() => ReliefTerrain.Create(r, TerrainProfiles.Bundled));
        Assert.StartsWith("coast-", error.Constraint);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => ReliefTerrain.Create(Request(CoastKind.Island), TerrainProfiles.Bundled, cancel.Token));
    }

    [Fact]
    public async Task Impossible_http_request_preserves_world_and_releases_its_worker_and_candidate()
    {
        await using var server = await NetworkTests.Harness.Start(canonical: true);
        var before = await server.Bootstrap(); var saved = await server.Http.GetByteArrayAsync("/api/saves/download");
        using var socket = await server.Connect(); await NetworkTests.Read(socket);
        async Task<HttpResponseMessage> Preview(GeneratedWorldRequest r) => await server.Http.PostAsync("/api/worlds/generated/preview",
            new StringContent(JsonSerializer.Serialize(new GeneratedPreviewRequest(r.Name, r.Dimensions, r.WaterLevel, r.Options,
                before.WorldEpoch, before.Snapshot.Revision), NetworkSession.Json), System.Text.Encoding.UTF8, "application/json"));
        var impossible = Request(CoastKind.Island, percent: 5) with { Dimensions = new(16, 16, 8), WaterLevel = 1 };
        for (int attempt = 0; attempt < 5; attempt++)
        {
            using var response = await Preview(impossible); Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("geographyImpossible", body.RootElement.GetProperty("code").GetString());
            Assert.StartsWith("coast-", body.RootElement.GetProperty("constraint").GetString());
        }
        Assert.Equal(saved, await server.Http.GetByteArrayAsync("/api/saves/download"));
        using var accepted = await Preview(Request(CoastKind.Island));
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
        Assert.Equal(saved, await server.Http.GetByteArrayAsync("/api/saves/download"));
    }
}
