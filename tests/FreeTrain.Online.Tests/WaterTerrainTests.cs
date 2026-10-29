using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using Xunit.Abstractions;

[Collection("World creation")]
public class WaterTerrainTests(ITestOutputHelper output)
{
    internal static GeneratedWorldRequest Request(int lakes = 1, int rivers = 0, RiverOutlet outlet = RiverOutlet.Automatic,
        CoastKind coast = CoastKind.Inland, CoastSides sides = CoastSides.None, int width = 1, int seed = 0)
    {
        var r = WorldCreationProfile.Generated(seed, "Lakes and rivers / 湖と川");
        return r with { Dimensions = new(64, 64, 32), Options = r.Options with { StartingArea = 0,
            Coast = new(coast, sides, coast == CoastKind.Inland ? 0 : 30), Lakes = new(lakes, lakes == 0 ? 0 : 3),
            Rivers = new(rivers, width, 70, outlet) } };
    }

    public static IEnumerable<object[]> Combinations()
    {
        yield return [1, 0, RiverOutlet.Automatic, CoastKind.Inland, CoastSides.None, 1];
        yield return [3, 0, RiverOutlet.Automatic, CoastKind.Inland, CoastSides.None, 1];
        yield return [1, 1, RiverOutlet.Automatic, CoastKind.Inland, CoastSides.None, 1];
        yield return [0, 1, RiverOutlet.EdgeToEdge, CoastKind.Inland, CoastSides.None, 1];
        yield return [1, 0, RiverOutlet.Automatic, CoastKind.Island, CoastSides.None, 1];
        yield return [1, 1, RiverOutlet.Automatic, CoastKind.Island, CoastSides.None, 1];
        foreach (var (side, outlet) in new[] { (CoastSides.North, RiverOutlet.North), (CoastSides.East, RiverOutlet.East),
                     (CoastSides.South, RiverOutlet.South), (CoastSides.West, RiverOutlet.West) })
            yield return [1, 1, outlet, CoastKind.Sides, side, 1];
        yield return [0, 1, RiverOutlet.Automatic, CoastKind.Sides, CoastSides.North | CoastSides.South, 4];
        yield return [0, 2, RiverOutlet.Automatic, CoastKind.Sides, CoastSides.North, 1];
    }

    [Theory, MemberData(nameof(Combinations))]
    public void Actual_normalized_water_has_requested_rims_channels_and_outlets(int lakes, int rivers, RiverOutlet outlet, CoastKind coast, CoastSides sides, int width)
    {
        var r = Request(lakes, rivers, outlet, coast, sides, width);
        var world = ReliefTerrain.Create(r, TerrainProfiles.Bundled);
        var m = WaterTerrain.Validate(r, world); output.WriteLine(JsonSerializer.Serialize(m));
        Assert.Equal(lakes, m.LakeComponents); Assert.Equal(rivers, m.RiverCount);
        Assert.True(m.LandCells > 0); Assert.True(m.WaterPercent > 0);
        Oracle(r, world);
        var saved = SaveCodec.Write(new TerrainSessionState(world, TerrainProvenance.From(r)));
        var restored = SaveCodec.ReadWorld(saved, TerrainProfiles.Resolve).Terrain!;
        Assert.Equal(saved, SaveCodec.Write(restored)); Assert.Equal(m, WaterTerrain.Validate(r, restored.World));
    }

    [Fact]
    public void Independent_literal_geometry_and_river_path_witnesses_match()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Spatial/water-fixtures.json")));
        string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
        foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            int N(string key) => row.GetProperty(key).GetInt32();
            var r = Request(N("lakes"),N("rivers"),(RiverOutlet)N("outlet"),row.GetProperty("island").GetBoolean() ? CoastKind.Island : N("sides") != 0 ? CoastKind.Sides : CoastKind.Inland,(CoastSides)N("sides"),N("width"),N("seed"));
            r = r with { Dimensions = new(N("h"),N("v"),N("z")), WaterLevel = N("water"), Options = r.Options with {
                Lakes = new(N("lakes"),N("area")), Rivers = r.Options.Rivers with { Meander = N("meander") } } };
            if (row.TryGetProperty("error",out var error))
            { Assert.Equal(error.GetString(),Assert.Throws<GeographyException>(()=>ReliefTerrain.Create(r,TerrainProfiles.Bundled)).Constraint); continue; }
            var world = ReliefTerrain.Create(r,TerrainProfiles.Bundled); Oracle(r,world);
            Assert.Equal(row.GetProperty("groundHash").GetString(),Hash(world.Cells.Select(c=>(byte)c.Ground).ToArray()));
            var corners = new byte[world.Cells.Length*2];
            for (int k=0;k<world.Occupants.Length;k++)
            {
                var p=world.Space.ToStorage(world.Spatial.Entities[k].Anchor);
                BinaryPrimitives.WriteUInt16LittleEndian(corners.AsSpan((p.V*r.Dimensions.H+p.H)*2),(ushort)world.Occupants[k].Corners.Packed);
            }
            Assert.Equal(row.GetProperty("cornerHash").GetString(),Hash(corners));
            var metrics=WaterTerrain.Validate(r,world); Assert.Equal(N("wetCells"),metrics.SeaCells+metrics.LakeCells);
            foreach (var path in row.GetProperty("paths").EnumerateArray())
            {
                var sites=path.EnumerateArray().Select(i=>world.Space.ToLocation(new(i.GetInt32()%r.Dimensions.H,i.GetInt32()/r.Dimensions.H,0))).ToArray();
                for (int k=0;k<sites.Length;k++)
                {
                    var p=sites[k]; if(k>0)Assert.Equal(1,Math.Abs(p.X-sites[k-1].X)+Math.Abs(p.Y-sites[k-1].Y));
                    int a=-N("width")/2;
                    for(int y=a;y<a+N("width");y++)for(int x=a;x<a+N("width");x++)
                    {
                        var q=new Location(p.X+x,p.Y+y,0);if(!world.Space.ContainsHorizontal(q))continue;
                        Assert.All(Enumerable.Range(0,4),c=>Assert.True(world.FineHeight(q,c)<4*r.WaterLevel));
                    }
                }
            }
        }
    }

    // Independent XY face flood over actual quarter heights. No runtime mask,
    // component traversal or storage-neighbor helper is used here.
    internal static void Oracle(GeneratedWorldRequest r, TerrainWorld world)
    {
        var surface = new Dictionary<(int X, int Y), int[]>(); var boundary = new HashSet<(int, int)>();
        for (int v = 0; v < r.Dimensions.V; v++) for (int h = 0; h < r.Dimensions.H; h++)
        {
            var p = world.Space.ToLocation(new(h, v, world.Cell(h,v).Ground));
            surface[(p.X,p.Y)] = Enumerable.Range(0,4).Select(c=>world.FineHeight(p,c)).ToArray();
            if (h == 0 || v == 0 || h == r.Dimensions.H - 1 || v == r.Dimensions.V - 1) boundary.Add((p.X,p.Y));
        }
        var wet = surface.Where(p=>p.Value.Min()<4*r.WaterLevel).Select(p=>p.Key).ToHashSet();
        var remaining = new HashSet<(int,int)>(wet); var enclosed = new List<int>(); int open = 0;
        while (remaining.Count > 0)
        {
            var start = remaining.First(); remaining.Remove(start); var queue = new Queue<(int X,int Y)>(); queue.Enqueue(start);
            int size = 0; bool touches = false;
            while (queue.TryDequeue(out var p))
            {
                size++; touches |= boundary.Contains(p);
                foreach (var (x,y,a,b) in new[] { (p.X+1,p.Y,0,1), (p.X-1,p.Y,2,3), (p.X,p.Y+1,1,2), (p.X,p.Y-1,0,3) })
                    if (Math.Min(surface[p][a],surface[p][b]) < 4*r.WaterLevel && remaining.Remove((x,y))) queue.Enqueue((x,y));
            }
            if (touches) open++; else enclosed.Add(size);
        }
        Assert.Equal(r.Options.Lakes.Count, enclosed.Count);
        foreach (int size in r.Options.Rivers.Count == 0 ? enclosed : new List<int>())
        {
            int target = surface.Count * r.Options.Lakes.AreaPercent / 100;
            Assert.InRange(Math.Abs(size - target), 0, Math.Max(surface.Count / 100, target / 4));
        }
        if (r.Options.Rivers.Outlet == RiverOutlet.EdgeToEdge) Assert.Equal(r.Options.Rivers.Count, open);
        if (r.Options.Coast.Kind == CoastKind.Inland && r.Options.Rivers.Outlet != RiverOutlet.EdgeToEdge) Assert.Equal(0, open);
        // Shared corner equality is essential for a legitimate constant water plane.
        foreach (var (p, heights) in surface)
        {
            if (surface.TryGetValue((p.X+1,p.Y),out var east)) { Assert.Equal(heights[0],east[3]); Assert.Equal(heights[1],east[2]); }
            if (surface.TryGetValue((p.X,p.Y+1),out var south)) { Assert.Equal(heights[1],south[0]); Assert.Equal(heights[2],south[3]); }
        }
    }

    [Fact]
    public void Controls_are_effective_and_replay_is_exact_without_gameplay_randomness()
    {
        var r = Request(0, 1, RiverOutlet.EdgeToEdge);
        byte[] Build(GeneratedWorldRequest input) => SaveCodec.Write(new TerrainSessionState(ReliefTerrain.Create(input, TerrainProfiles.Bundled), TerrainProvenance.From(input)));
        var first = Build(r); Assert.Equal(first, Build(r));
        var geometry = ReliefTerrain.Create(r, TerrainProfiles.Bundled).Cells.Select(c=>c.Ground).ToArray();
        foreach (var change in new[] { r.Options with { Seed = 42 }, r.Options with { Rivers = r.Options.Rivers with { Width = 4 } },
                     r.Options with { Rivers = r.Options.Rivers with { Meander = 0 } } })
            Assert.NotEqual(geometry, ReliefTerrain.Create(r with { Options = change }, TerrainProfiles.Bundled).Cells.Select(c=>c.Ground).ToArray());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Fractal_water_silhouettes_vary_with_seed_on_flat_terrain_and_replay_exactly(bool lake)
    {
        var shapes=new HashSet<string>();
        foreach(int seed in new[]{0,42,int.MinValue})
        {
            var r=Request(lake?1:0,lake?0:1,lake?RiverOutlet.Automatic:RiverOutlet.EdgeToEdge,seed:seed) with { Dimensions=new(128,127,32) };
            r=r with { Options=r.Options with { Relief=r.Options.Relief with { Amount=0 } } };
            var world=ReliefTerrain.Create(r,TerrainProfiles.Bundled);Oracle(r,world);
            var mask=CoastTerrain.SeaMask(world);
            Assert.Equal(mask,CoastTerrain.SeaMask(ReliefTerrain.Create(r,TerrainProfiles.Bundled)));
            var cells=Enumerable.Range(0,mask.Length).Where(i=>mask[i]).Select(i=>(H:i%r.Dimensions.H,V:i/r.Dimensions.H)).ToArray();
            int minH=cells.Min(p=>p.H),minV=cells.Min(p=>p.V);
            // Normalize translation so moving an otherwise identical basin/channel
            // cannot satisfy the seeded-silhouette regression.
            shapes.Add(string.Join(";",cells.Select(p=>$"{p.H-minH},{p.V-minV}")));
            if(lake)
            {
                int bounds=(cells.Max(p=>p.H)-minH+1)*(cells.Max(p=>p.V)-minV+1);
                Assert.True(cells.Length*100<bounds*90,"Lake should have a rounded, perturbed outline rather than fill a stored rectangle.");
            }
            else
            {
                var straight=r with { Options=r.Options with { Rivers=r.Options.Rivers with { Meander=0 } } };
                Assert.NotEqual(mask,CoastTerrain.SeaMask(ReliefTerrain.Create(straight,TerrainProfiles.Bundled)));
            }
        }
        Assert.Equal(3,shapes.Count);
    }

    [Theory]
    [InlineData(8,0,RiverOutlet.Automatic,CoastKind.Inland,CoastSides.None,1)]
    [InlineData(0,4,RiverOutlet.Automatic,CoastKind.Sides,CoastSides.North,1)]
    [InlineData(0,4,RiverOutlet.EdgeToEdge,CoastKind.Inland,CoastSides.None,1)]
    [InlineData(2,2,RiverOutlet.Automatic,CoastKind.Inland,CoastSides.None,4)]
    public void Maximum_feature_counts_and_rough_relief_are_bounded(int lakes,int rivers,RiverOutlet outlet,CoastKind coast,CoastSides sides,int width)
    {
        var r=Request(lakes,rivers,outlet,coast,sides,width,int.MinValue) with { Dimensions=new(128,127,64), WaterLevel=1 };
        r=r with { Options=r.Options with { Lakes=new(lakes,lakes==0?0:1),Relief=new(ReliefKind.Mountains,100,4,0) } };
        var world=ReliefTerrain.Create(r,TerrainProfiles.Bundled); Oracle(r,world);
        Assert.Equal(lakes,WaterTerrain.Validate(r,world).LakeComponents);
    }

    [Fact]
    public async Task Impossible_http_lakes_release_reservations_and_preserve_live_world()
    {
        await using var server=await NetworkTests.Harness.Start(canonical:true);
        var before=await server.Bootstrap();var saved=await server.Http.GetByteArrayAsync("/api/saves/download");
        using var socket=await server.Connect();await NetworkTests.Read(socket);
        async Task<HttpResponseMessage> Preview(GeneratedWorldRequest r)=>await server.Http.PostAsync("/api/worlds/generated/preview",
            new StringContent(JsonSerializer.Serialize(new FreeTrain.Online.Server.GeneratedPreviewRequest(r.Name,r.Dimensions,r.WaterLevel,r.Options,before.WorldEpoch,before.Snapshot.Revision),FreeTrain.Online.Server.NetworkSession.Json),System.Text.Encoding.UTF8,"application/json"));
        for(int attempt=0;attempt<5;attempt++)
        {
            using var response=await Preview(Request(8) with { Dimensions=new(16,16,8),WaterLevel=1 });
            Assert.Equal(System.Net.HttpStatusCode.BadRequest,response.StatusCode);
            using var body=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("geographyImpossible",body.RootElement.GetProperty("code").GetString());
            Assert.Equal("lake-space",body.RootElement.GetProperty("constraint").GetString());
        }
        Assert.Equal(saved,await server.Http.GetByteArrayAsync("/api/saves/download"));
        using var accepted=await Preview(Request(1,1));Assert.True(accepted.IsSuccessStatusCode,await accepted.Content.ReadAsStringAsync());
        Assert.Equal(saved,await server.Http.GetByteArrayAsync("/api/saves/download"));
    }

    [Fact]
    public void Impossible_rims_cancel_and_invalid_outlets_are_explicit()
    {
        var r = Request(8) with { Dimensions = new(16,16,8), WaterLevel = 1 };
        Assert.StartsWith("lake-", Assert.Throws<GeographyException>(()=>ReliefTerrain.Create(r,TerrainProfiles.Bundled)).Constraint);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>ReliefTerrain.Create(Request(),TerrainProfiles.Bundled,cancellation.Token));
        Assert.Contains(WorldCreationProfile.Validate(Request(0,1)),i=>i.Code=="river-needs-outlet");
        Assert.Contains(WorldCreationProfile.Validate(Request(1,1,RiverOutlet.North)),i=>i.Code=="river-outlet");
    }
}
