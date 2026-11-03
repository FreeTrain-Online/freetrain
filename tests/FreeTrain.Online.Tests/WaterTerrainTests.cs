using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using Xunit.Abstractions;

[Collection("World creation")]
public class WaterTerrainTests(ITestOutputHelper output)
{
    internal static GeneratedWorldRequest Request(int lakes = 1, RiverDirection direction = RiverDirection.None,
        CoastKind coast = CoastKind.Inland, CoastSides sides = CoastSides.None, int width = 1, int seed = 0)
    {
        var r = WorldCreationProfile.Generated(seed, "Lakes and rivers / 湖と川");
        return r with { Dimensions = new(64, 64, 32), Options = r.Options with { StartingArea = 0,
            Coast = new(coast, sides, coast == CoastKind.Inland ? 0 : 30), Lakes = new(lakes, lakes == 0 ? 0 : 3),
            Rivers = new(direction, width, 70) } };
    }

    public static IEnumerable<object[]> Combinations()
    {
        yield return [1, RiverDirection.None, CoastKind.Inland, CoastSides.None, 1];
        yield return [3, RiverDirection.None, CoastKind.Inland, CoastSides.None, 1];
        yield return [0, RiverDirection.NorthSouth, CoastKind.Inland, CoastSides.None, 5];
        yield return [1, RiverDirection.EastWest, CoastKind.Inland, CoastSides.None, 7];
        yield return [1, RiverDirection.NorthSouth, CoastKind.Island, CoastSides.None, 8];
        yield return [0, RiverDirection.EastWest, CoastKind.Sides, CoastSides.West, 8];
        foreach (var direction in new[] { RiverDirection.NorthSouth, RiverDirection.EastWest })
        {
            yield return [0, direction, CoastKind.Inland, CoastSides.None, 1];
            yield return [1, direction, CoastKind.Inland, CoastSides.None, 4];
            yield return [1, direction, CoastKind.Island, CoastSides.None, 1];
            foreach (var sides in new[] { CoastSides.North, CoastSides.East, CoastSides.South, CoastSides.West,
                         CoastSides.North | CoastSides.South, CoastSides.East | CoastSides.West })
                yield return [1, direction, CoastKind.Sides, sides, 2];
        }
    }

    [Theory, MemberData(nameof(Combinations))]
    public void Actual_normalized_water_has_lake_basins_and_connected_channels(int lakes, RiverDirection direction, CoastKind coast, CoastSides sides, int width)
    {
        var r = Request(lakes, direction, coast, sides, width);
        var world = ReliefTerrain.Create(r, TerrainProfiles.Bundled);
        var m = WaterTerrain.Validate(r, world); output.WriteLine(JsonSerializer.Serialize(m));
        Assert.InRange(m.LakeComponents, direction == RiverDirection.None ? lakes : 0, lakes); Assert.Equal(direction == RiverDirection.None ? 0 : 1, m.RiverCount);
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
            var r = Request(N("lakes"),(RiverDirection)N("direction"),row.GetProperty("island").GetBoolean() ? CoastKind.Island : N("sides") != 0 ? CoastKind.Sides : CoastKind.Inland,(CoastSides)N("sides"),N("width"),N("seed"));
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
            Assert.Equal(row.GetProperty("enclosedSizes").GetArrayLength(),metrics.LakeComponents);
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
        Assert.InRange(enclosed.Count, r.Options.Rivers.Count == 0 ? r.Options.Lakes.Count : 0, r.Options.Lakes.Count);
        foreach (int size in enclosed)
        {
            int target = surface.Count * r.Options.Lakes.AreaPercent / 100;
            Assert.InRange(Math.Abs(size - target), 0, Math.Max(surface.Count / 100, target / 4));
        }
        if (r.Options.Coast.Kind == CoastKind.Inland) Assert.Equal(r.Options.Rivers.Count, open);
        if (r.Options.Rivers.Direction != RiverDirection.None) AssertRiverEdges(r, surface, boundary);
        // Shared corner equality is essential for a legitimate constant water plane.
        foreach (var (p, heights) in surface)
        {
            if (surface.TryGetValue((p.X+1,p.Y),out var east)) { Assert.Equal(heights[0],east[3]); Assert.Equal(heights[1],east[2]); }
            if (surface.TryGetValue((p.X,p.Y+1),out var south)) { Assert.Equal(heights[1],south[0]); Assert.Equal(heights[2],south[3]); }
        }
    }

    private static void AssertRiverEdges(GeneratedWorldRequest r,
        Dictionary<(int X, int Y), int[]> surface, HashSet<(int, int)> boundary)
    {
        int xmin = surface.Keys.Min(p=>p.X), xmax = surface.Keys.Max(p=>p.X);
        int ymin = surface.Keys.Min(p=>p.Y), ymax = surface.Keys.Max(p=>p.Y);
        int Sector((int X,int Y) p)
        {
            int x=2*p.X-xmin-xmax, y=2*p.Y-ymin-ymax;
            return Math.Abs(y)*(xmax-xmin)>=Math.Abs(x)*(ymax-ymin) ? y<0?1:4 : x<0?8:2;
        }
        int startSide=r.Options.Rivers.Direction==RiverDirection.NorthSouth?1:2;
        int endSide=r.Options.Rivers.Direction==RiverDirection.NorthSouth?4:8;
        var fullyWet=surface.Where(p=>p.Value.Max()<4*r.WaterLevel).Select(p=>p.Key).ToHashSet();
        var starts=boundary.Where(p=>Sector(p)==startSide && fullyWet.Contains(p)).ToArray();
        Assert.NotEmpty(starts);
        var seen=starts.ToHashSet(); var queue=new Queue<(int X,int Y)>(starts); bool reaches=false;
        while(queue.TryDequeue(out var p))
        {
            if(boundary.Contains(p) && Sector(p)==endSide) reaches=true;
            foreach(var n in new[]{(p.X+1,p.Y),(p.X-1,p.Y),(p.X,p.Y+1),(p.X,p.Y-1)})
                if(fullyWet.Contains(n) && seen.Add(n))queue.Enqueue(n);
        }
        Assert.True(reaches,"The river core must join fully submerged gates on opposite compass boundaries.");
        // On inland maps every wet boundary patch belongs to the chosen axis.
        if(r.Options.Coast.Kind==CoastKind.Inland)
            Assert.All(boundary.Where(p=>surface[p].Min()<4*r.WaterLevel),p=>Assert.Contains(Sector(p),new[]{startSide,endSide}));
    }

    [Theory]
    [InlineData(0,0,CoastKind.Inland,CoastSides.None,0)]
    [InlineData(1,1,CoastKind.Inland,CoastSides.None,0)]
    [InlineData(0,4,CoastKind.Inland,CoastSides.None,5)]
    [InlineData(1,1,CoastKind.Sides,CoastSides.North,1)]
    public void Existing_count_and_outlet_provenance_still_reopens_exactly(int lakes,int count,CoastKind coast,CoastSides sides,int outlet)
    {
        var r=Request(lakes,RiverDirection.None,coast,sides);
        var world=ReliefTerrain.Create(r,TerrainProfiles.Bundled);
        var node=JsonSerializer.SerializeToNode<WorldCreationRequest>(r)!;
        node["Options"]!["Rivers"]=JsonSerializer.SerializeToNode(new {Count=count,Width=1,Meander=70,Outlet=outlet});
        string json=node.ToJsonString();
        var provenance=new TerrainProvenance(r.Profile,"generated",Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),json);
        var saved=SaveCodec.Write(new TerrainSessionState(world,provenance));
        var restored=SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve).Terrain!;
        Assert.Equal(provenance,restored.Provenance);Assert.Equal(saved,SaveCodec.Write(restored));
        Assert.Throws<ArgumentException>(()=>(provenance with {RequestHash=new string('0',64)}).Validate(world));
        var invalid=node.DeepClone();invalid["Options"]!["Rivers"]!["Count"]=5;
        string invalidJson=invalid.ToJsonString();
        Assert.Throws<ArgumentException>(()=>new TerrainProvenance(r.Profile,"generated",
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(invalidJson))),invalidJson).Validate(world));
    }

    [Theory]
    [InlineData(16,16,1,0)] [InlineData(17,16,4,42)] [InlineData(64,65,2,int.MinValue)]
    [InlineData(128,127,4,int.MaxValue)]
    [InlineData(64,65,8,int.MinValue)]
    public void Both_axes_reach_map_edges_without_a_dry_seam(int h,int v,int width,int seed)
    {
        foreach(var direction in new[]{RiverDirection.NorthSouth,RiverDirection.EastWest})
        {
            var r=Request(0,direction,width:width,seed:seed) with { Dimensions=new(h,v,32) };
            Oracle(r,ReliefTerrain.Create(r,TerrainProfiles.Bundled));
        }
    }

    [Theory]
    [InlineData(RiverDirection.NorthSouth,1)] [InlineData(RiverDirection.EastWest,1)]
    [InlineData(RiverDirection.NorthSouth,4)] [InlineData(RiverDirection.EastWest,4)]
    [InlineData(RiverDirection.NorthSouth,8)] [InlineData(RiverDirection.EastWest,8)]
    public void River_and_lake_overlay_preserves_both_water_surfaces_without_a_separating_bank(RiverDirection direction,int width)
    {
        // This seed places the randomized basin across both river axes.
        var r=Request(1,direction,width:width,seed:4);
        var lake=ReliefTerrain.Create(r with {Options=r.Options with {Rivers=r.Options.Rivers with {Direction=RiverDirection.None}}},TerrainProfiles.Bundled);
        var river=ReliefTerrain.Create(r with {Options=r.Options with {Lakes=new(0,0)}},TerrainProfiles.Bundled);
        var combined=ReliefTerrain.Create(r,TerrainProfiles.Bundled);
        int overlap=0, lakeOnly=0, riverOnly=0;
        for(int v=0;v<r.Dimensions.V;v++)for(int h=0;h<r.Dimensions.H;h++)
        {
            var p=combined.Space.ToLocation(new(h,v,0));
            bool lakeWet=false,riverWet=false;
            for(int corner=0;corner<4;corner++)
            {
                bool a=lake.FineHeight(p,corner)<4*r.WaterLevel, b=river.FineHeight(p,corner)<4*r.WaterLevel;
                lakeWet|=a;riverWet|=b;
                if(a || b)Assert.True(combined.FineHeight(p,corner)<4*r.WaterLevel,"Overlay must retain every wet corner of the independent lake and river.");
            }
            if(lakeWet && riverWet)overlap++;
            else if(lakeWet)lakeOnly++;
            else if(riverWet)riverOnly++;
        }
        Assert.True(overlap>0);Assert.True(lakeOnly>0);Assert.True(riverOnly>0);
        var metrics=WaterTerrain.Validate(r,combined);
        Assert.Equal(0,metrics.LakeComponents);Assert.Equal(1,metrics.SeaComponents);Assert.Equal(1,metrics.RiverCount);
        Oracle(r,combined);
    }

    [Fact]
    public void Controls_are_effective_and_replay_is_exact_without_gameplay_randomness()
    {
        var r = Request(0, RiverDirection.NorthSouth);
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
            var r=Request(lake?1:0,lake?RiverDirection.None:RiverDirection.NorthSouth,seed:seed) with { Dimensions=new(128,127,32) };
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
    [InlineData(8,RiverDirection.None,CoastKind.Inland,CoastSides.None,1)]
    [InlineData(0,RiverDirection.NorthSouth,CoastKind.Sides,CoastSides.North,1)]
    [InlineData(0,RiverDirection.EastWest,CoastKind.Inland,CoastSides.None,1)]
    [InlineData(2,RiverDirection.NorthSouth,CoastKind.Inland,CoastSides.None,4)]
    [InlineData(2,RiverDirection.NorthSouth,CoastKind.Inland,CoastSides.None,8)]
    public void Maximum_feature_counts_and_rough_relief_are_bounded(int lakes,RiverDirection direction,CoastKind coast,CoastSides sides,int width)
    {
        var r=Request(lakes,direction,coast,sides,width,int.MinValue) with { Dimensions=new(128,127,64), WaterLevel=1 };
        r=r with { Options=r.Options with { Lakes=new(lakes,lakes==0?0:1),Relief=new(ReliefKind.Mountains,100,4,0) } };
        var world=ReliefTerrain.Create(r,TerrainProfiles.Bundled); Oracle(r,world);
        Assert.InRange(WaterTerrain.Validate(r,world).LakeComponents, direction == RiverDirection.None ? lakes : 0, lakes);
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
        using var accepted=await Preview(Request(1,RiverDirection.NorthSouth));Assert.True(accepted.IsSuccessStatusCode,await accepted.Content.ReadAsStringAsync());
        Assert.Equal(saved,await server.Http.GetByteArrayAsync("/api/saves/download"));
    }

    [Fact]
    public void Impossible_rims_cancel_and_invalid_directions_are_explicit()
    {
        var r = Request(8) with { Dimensions = new(16,16,8), WaterLevel = 1 };
        Assert.StartsWith("lake-", Assert.Throws<GeographyException>(()=>ReliefTerrain.Create(r,TerrainProfiles.Bundled)).Constraint);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>ReliefTerrain.Create(Request(),TerrainProfiles.Bundled,cancellation.Token));
        var invalid = Request(0, (RiverDirection)999);
        Assert.Contains(WorldCreationProfile.Validate(invalid), i=>i.Code=="rivers");
    }
}
