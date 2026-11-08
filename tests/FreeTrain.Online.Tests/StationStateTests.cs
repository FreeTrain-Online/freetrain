using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class StationStateTests
{
    static RailwayProfile RailProfile => RailwayProfiles.Bundled;
    internal static StationProfile Profile => RailProfile.Stations!;
    internal static StationDefinition Definition => Profile.Definitions.First(d => d.SizeX == 1 && d.SizeY == 1 && d.Height == 1);
    internal static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/station-fixtures.json")));
    internal static IEnumerable<JsonElement> Cases(JsonDocument doc, string group) => doc.RootElement.GetProperty("groups").GetProperty(group).GetProperty("cases").EnumerateArray();
    static Location Point(JsonElement a) => new(a[0].GetInt32(),a[1].GetInt32(),a[2].GetInt32());
    internal static StationBuilding Building(long id = 1, ImmutableArray<long> children = default) => new(id,0,Definition.Reference,new(12,12,2),"Station",
        children.IsDefault ? [] : children,new(),new(),new(),new());
    internal static StationGraph Graph(IEnumerable<StationBuilding>? buildings = null, IEnumerable<StationPlatform>? platforms = null,
        IEnumerable<StationPassage>? passages = null) => new(Profile,buildings ?? [],platforms ?? [],passages);
    internal static StationPlatform Platform(long id = 2, PlatformKind kind = PlatformKind.Thin, int direction = 0, int length = 4,
        bool roof = false, bool lane0 = true, bool lane1 = false, long? host = null, long firstTraffic = 10,
        ImmutableArray<long> children = default, Location? location = null)
    {
        var p = new StationPlatform(id,0,kind,location ?? new(20,20,2),new(direction),length,"Platform",roof,lane0,lane1,
            Profile.Bells[0].Reference,host,children.IsDefault ? [] : children,[]);
        var yards = ImmutableArray.CreateBuilder<PlatformYard>();
        for (var lane=0;lane<2;lane++) if (lane == 0 ? lane0 : lane1)
            for (var i=0;i<length;i++) yards.Add(new(firstTraffic++,StationGraph.YardLocation(p,lane,i),lane,i,
                kind == PlatformKind.Thin && roof && i >= length/4 && i < length-length/4,new()));
        return p with {Yards=yards.ToImmutable()};
    }
    internal static TerrainWorld Terrain() => new(TerrainProfiles.Bundled,"Stations",new(64,24,8),0,
        Enumerable.Repeat(new TerrainCell(2),64*24),new(1,[]),[]);
    internal static MixedWorld World(StationGraph graph, FleetGraph? fleet = null)
    {
        var entities = new List<SpatialEntity>(); var voxels = new List<RailwayVoxel>();
        foreach (var b in graph.Buildings) entities.Add(new(b.Id,b.Revision,b.Definition,b.Location,Profile.Resolve(b.Definition).Footprint,true,false,0));
        foreach (var p in graph.Platforms)
        {
            if (p.Kind == PlatformKind.Wide) entities.Add(new(p.Id,p.Revision,Profile.Recipe(StationArtKind.WideCenter,p.Direction.Index/2%2).Reference,p.Location,
                new(Enumerable.Range(0,p.Length).Select(i => {var q=StationGraph.CenterLocation(p,i);return new VoxelMember(new(q.X-p.Location.X,q.Y-p.Location.Y,0),false);})),true,false,0));
            foreach (var y in p.Yards)
            {
                var art = p.Kind == PlatformKind.Thin ? Profile.ThinRecipe(p.Direction,y.Roof) : Profile.Recipe(StationArtKind.Yard,0);
                Traffic(y.TrafficOwnerId,y.Location,p.Direction,art.Reference);
            }
        }
        foreach (var p in graph.Passages) Traffic(p.TrafficOwnerId,p.Location,p.Direction,null);
        void Traffic(long id, Location location, Heading direction, DefinitionReference? art)
        {
            entities.Add(new(id,0,RailProfile.TrafficOwner,location,new([new(new(0,0,0),true)]),true,false,0));
            var pattern = NormalRailPatterns.Find(direction.Index,direction.Opposite.Index);
            var rail = RailProfile.Bindings.Single(b => b.Handler == RailwayHandler.Normal && b.Variant == pattern).Reference;
            voxels.Add(new(location,id,RailwayVoxelKind.Traffic,new(new(rail,pattern),Platform:art)));
        }
        var next = graph.Identities.Select(i => i.Id).Concat(entities.Select(e => e.Id)).Concat(fleet?.Identities.Select(i => i.Id) ?? []).DefaultIfEmpty().Max()+1;
        return new(RailProfile,Terrain(),new(next,entities.ToImmutableArray()),voxels,[],fleet:fleet,stations:graph);
    }
    static MixedWorld Rebuild(MixedWorld w, StationGraph? graph = null, IEnumerable<SpatialEntity>? entities = null,
        IEnumerable<RailwayVoxel>? voxels = null, long? next = null, FleetGraph? fleet = null) =>
        new(w.Profile,w.Terrain,new(next ?? w.Spatial.NextEntityId,(entities ?? w.Spatial.Entities).ToImmutableArray()),voxels ?? w.Voxels,w.Slopes,
            w.Revision,w.SpeedLimits,fleet ?? w.Fleet,graph ?? w.Stations);

    [Fact]
    public void All_literal_station_definitions_bind_exact_dimensions_prices_and_immediate_owned_footprints()
    {
        using var fixtures = Fixtures(); var rows = Cases(fixtures,"stations").ToArray();
        Assert.Equal(35,rows.Length); Assert.Equal(35,Profile.Definitions.Length);
        foreach (var row in rows)
        {
            var id = row.GetProperty("id").GetString(); var expected = row.GetProperty("expected");
            var d = Profile.Definitions.Single(d => d.Reference.ContributionId == id);
            Assert.Equal(expected.GetProperty("size").EnumerateArray().Select(n => n.GetInt32()),new[] {d.SizeX,d.SizeY,d.Height});
            Assert.Equal(expected.GetProperty("price").GetInt64(),d.Price);
            Assert.Equal(expected.GetProperty("operationCost").GetInt64(),d.OperationCost);
            Assert.Equal("Gray",d.HeightCutColor);
            var b = Building() with { Definition=d.Reference,Location=Point(row.GetProperty("input").GetProperty("base")) };
            var w = World(Graph([b]));
            Assert.Equal(expected.GetProperty("voxelCount").GetInt32(),w.Spatial.Entities.Single().Footprint.Members.Length);
            Assert.Equal(Point(expected.GetProperty("endExclusive")),d.Bounds(b.Location).End);
            Assert.All(d.Bounds(b.Location).Locations(),p => {Assert.Equal(b.Id,w.Spatial.At(p).Entity!.Id);Assert.False(w.Spatial.At(p).Reusable);Assert.False(w.Spatial.At(p).Transparent);});
            Assert.Empty(w.Voxels); Assert.Equal(0,w.Spatial.Entities.Single().Value);
        }
    }
    [Fact]
    public void Exact_platform_passage_stair_and_bell_resources_bind_without_repairing_invalid_crops()
    {
        Assert.Equal(149,Profile.Art.Length); Assert.Equal(6,Profile.Bells.Length);
        Assert.Equal(3,Profile.Art.Count(a => !a.Ready));
        foreach (var m in Enum.GetValues<PassageMaterial>())
        {
            var a=Profile.Recipe(StationArtKind.Stair,23,m);Assert.False(a.Ready);
            Assert.All(a.Frames,f => Assert.Equal((388,4,32,36),(f.X,f.Y,f.Width,f.Height)));
        }
        using var catalog=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"src/FreeTrain.Online.Content/bundled-v2.json")));
        foreach (var a in Profile.Art)
        {
            var recipe=catalog.RootElement.GetProperty("renderRecipes").EnumerateArray().Single(r => r.GetProperty("id").GetString()==a.Reference.RecipeId);
            Assert.Equal(recipe.GetProperty("frames").GetArrayLength(),a.Frames.Length);
            for(var i=0;i<a.Frames.Length;i++)
            {
                var raw=recipe.GetProperty("frames")[i];var f=a.Frames[i];
                Assert.Equal(raw.GetProperty("rectangle").EnumerateArray().Select(n=>n.GetInt32()),new[]{f.X,f.Y,f.Width,f.Height});
                Assert.Equal(raw.GetProperty("anchor").EnumerateArray().Select(n=>n.GetInt32()),new[]{f.AnchorX,f.AnchorY});
                Assert.Equal(raw.GetProperty("pictureId").GetString(),f.PictureId);
            }
        }
        foreach(var bell in Profile.Bells)
        {
            var raw=catalog.RootElement.GetProperty("assets").EnumerateArray().Single(a=>a.GetProperty("path").GetString()==bell.SourcePath);
            Assert.Equal(raw.GetProperty("native").GetProperty("sha256").GetString(),bell.Sha256);
            Assert.Equal(raw.GetProperty("native").GetProperty("url").GetString(),bell.MediaPath);
        }
        Assert.Equal(213,RailProfile.Bindings.Length); // Binding state does not activate tools.
        Assert.Equal("無人駅",Profile.Definitions.Single(d=>d.Reference.ContributionId=="{5CB98D0A-4D2A-4CEF-A5F2-2EBFFD7EE399}").Name);
        using var fixtures=Fixtures();var selectors=Cases(fixtures,"drawing").Where(r=>r.GetProperty("input").GetProperty("kind").GetString()=="thin").ToArray();
        Assert.Equal(8,selectors.Length);
        foreach(var row in selectors)
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");
            var art=Profile.ThinRecipe(new(i.GetProperty("heading").GetInt32()),i.GetProperty("roof").GetBoolean());
            Assert.Equal(e.GetProperty("selector").GetInt32(),art.Index);
            var f=Assert.Single(art.Frames);
            Assert.Equal(e.GetProperty("rectangle").EnumerateArray().Select(v=>v.GetInt32()),new[]{f.X,f.Y,f.Width,f.Height});
        }
        Assert.Throws<ArgumentException>(()=>Profile.ThinRecipe(new(1),false));
    }
    [Fact]
    public void All_literal_thin_and_wide_geometry_cases_validate_actual_yards_and_separate_center_ownership()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var family in new[]{"thin","wide"}) foreach(var row in Cases(fixtures,family))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var wide=family=="wide";
            var p=Platform(kind:wide?PlatformKind.Wide:PlatformKind.Thin,direction:i.GetProperty("heading").GetInt32(),
                length:i.GetProperty("length").GetInt32(),roof:!wide&&i.GetProperty("roof").GetBoolean(),
                lane0:!wide||i.GetProperty("lanes")[0].GetBoolean(),lane1:wide&&i.GetProperty("lanes")[1].GetBoolean(),location:Point(i.GetProperty("base")));
            var w=World(Graph(platforms:[p]));
            if(wide)
            {
                Assert.Equal(e.GetProperty("center").EnumerateArray().Select(Point),w.Spatial.Entities.Single(s=>s.Id==p.Id).Footprint.At(p.Location).Select(c=>c.Position));
                Assert.All(w.Spatial.Entities.Single(s=>s.Id==p.Id).Footprint.At(p.Location),c=>Assert.False(w.Spatial.At(c.Position).Transparent));
                for(var lane=0;lane<2;lane++) Assert.Equal(e.GetProperty("lanes")[lane].EnumerateArray().Select(Point),p.Yards.Where(y=>y.Lane==lane).Select(y=>y.Location));
            }
            else
            {
                Assert.DoesNotContain(w.Spatial.Entities,s=>s.Id==p.Id);
                Assert.Equal(e.GetProperty("cells").EnumerateArray().Select(Point),p.Yards.Select(y=>y.Location));
                Assert.Equal(e.GetProperty("roof").EnumerateArray().Select(b=>b.GetBoolean()),p.Yards.Select(y=>y.Roof));
            }
            Assert.All(p.Yards,y=>{Assert.Equal(y.TrafficOwnerId,w.Spatial.At(y.Location).Entity!.Id);Assert.NotEqual(p.Id,y.TrafficOwnerId);});count++;
        }
        Assert.Equal(48,count);
    }
    [Fact]
    public void Host_resolution_is_reciprocal_acyclic_and_accepts_only_real_service_stations()
    {
        var b=Building(children:[2]);var a=Platform(host:1,children:[3]);
        var c=Platform(3,host:2,firstTraffic:20,location:new(26,20,2));
        var graph=Graph([b],[a,c]);var w=World(graph);
        Assert.Same(b,graph.HostStation(2));Assert.Same(b,graph.HostStation(3));
        Assert.Null(Graph(platforms:[Platform()]).HostStation(2));
        Assert.Throws<ArgumentException>(()=>Graph([b],[a with{HostId=99},c]));
        Assert.Throws<ArgumentException>(()=>Graph([b with{Children=[]}],[a,c]));
        Assert.Throws<ArgumentException>(()=>Graph([b with{Children=[2,2]}],[a,c]));
        Assert.Throws<ArgumentException>(()=>Graph([b],[a with{HostId=2},c]));
        Assert.Throws<ArgumentException>(()=>Graph(platforms:[a with{HostId=3},c with{Children=[2]}]));
        Assert.Throws<ArgumentException>(()=>Graph(platforms:[a with{HostId=null},c]));
        Assert.Throws<ArgumentException>(()=>Graph([b,b],[a,c]));
        Assert.Throws<ArgumentException>(()=>Graph([b with{Definition=RailProfile.Bindings.First(r=>r.Handler==RailwayHandler.Stationary).Reference}],[a,c]));
        Assert.Same(graph,w.Stations);Assert.Equal(1,w.Stations!.HostStation(3)!.Id);
    }
    [Fact]
    public void Literal_host_graph_inputs_and_outputs_validate_without_activating_reparent_operations()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in Cases(fixtures,"hostGraph"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var stations=input.GetProperty("stations").EnumerateArray().Select(s=>s.GetString()!).ToArray();
            var parents=input.GetProperty("parents");
            var ids=stations.Concat(parents.EnumerateObject().Select(p=>p.Name)).Select((s,i)=>(s,Id:(long)i+1)).ToDictionary(p=>p.s,p=>p.Id);
            StationGraph ReadGraph(JsonElement map)
            {
                var edges=map.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.ValueKind==JsonValueKind.Null?null:p.Value.GetString());
                ImmutableArray<long> ChildrenOf(string name)=>edges.Where(p=>p.Value==name).Select(p=>ids[p.Key]).ToImmutableArray();
                var buildings=stations.Select((s,i)=>Building(ids[s],ChildrenOf(s)) with{Location=new(12+i*2,12,2)});
                var platforms=edges.Select((p,i)=>Platform(ids[p.Key],host:p.Value==null?null:ids[p.Value],children:ChildrenOf(p.Key),
                    length:1,firstTraffic:100+i,location:new(20+i*3,20+i*3,2)));
                return Graph(buildings,platforms);
            }
            if(row.GetProperty("id").GetString()=="graph-6") Assert.Throws<ArgumentException>(()=>ReadGraph(parents));
            else Assert.NotNull(World(ReadGraph(parents)));
            if(expected.TryGetProperty("parents",out var result))
            {
                var graph=ReadGraph(result);Assert.NotNull(World(graph));
                foreach(var r in expected.GetProperty("resolved").EnumerateObject())
                    Assert.Equal(r.Value.GetBoolean(),graph.HostStation(ids[r.Name])!=null);
            }
            count++;
        }
        Assert.Equal(8,count);
    }
    [Fact]
    public void Malformed_platform_choices_references_and_outlooks_reject_before_world_publication()
    {
        var p=Platform(roof:true);var y=p.Yards[1];
        foreach(var bad in new[]{p with{Kind=(PlatformKind)99},p with{Direction=new(1)},p with{Length=0},p with{Lane1=true},
            p with{Bell=Definition.Reference},p with{Name="bad\nname"},p with{Yards=default},p with{Yards=p.Yards.RemoveAt(0)},
            p with{Yards=p.Yards.SetItem(1,y with{Index=0})},p with{Yards=p.Yards.SetItem(1,y with{Location=new(0,0,0)})},
            p with{Yards=p.Yards.SetItem(1,y with{Roof=false})},p with{Yards=p.Yards.SetItem(1,y with{Outlook=new((PlatformOutlookKind)99)})},
            p with{Yards=p.Yards.SetItem(1,y with{Outlook=new(PlatformOutlookKind.Passage,Upward:true)})}})
            Assert.Throws<ArgumentException>(()=>Graph(platforms:[bad]));
        Assert.Throws<ArgumentException>(()=>Graph(platforms:[Platform(kind:PlatformKind.Wide) with{Roof=true}]));
        Assert.Throws<ArgumentException>(()=>Graph(platforms:[p,Platform(3,firstTraffic:30)]));
    }
    [Fact]
    public void Spatial_payload_allocator_and_fleet_collisions_reject_atomically()
    {
        var p=Platform();var w=World(Graph([Building()],[p]));var before=w.Spatial.Capture();
        Assert.Throws<ArgumentException>(()=>Rebuild(w,next:13));
        Assert.Throws<ArgumentException>(()=>Rebuild(w,entities:w.Spatial.Entities.Where(e=>e.Id!=1)));
        Assert.Throws<ArgumentException>(()=>Rebuild(w,entities:w.Spatial.Entities.Select(e=>e.Id==1?e with{Owned=false}:e)));
        Assert.Throws<ArgumentException>(()=>Rebuild(w,entities:w.Spatial.Entities.Select(e=>e.Id==1?e with{Footprint=Footprint.Box(new(new(0,0,0),2,1,1))}:e)));
        Assert.Throws<ArgumentException>(()=>Rebuild(w,voxels:w.Voxels.Skip(1)));
        Assert.Throws<ArgumentException>(()=>Rebuild(w,voxels:w.Voxels.Select(v=>v.Position==p.Yards[0].Location?v with{Traffic=v.Traffic! with{Platform=null}}:v)));
        var curve=RailProfile.Bindings.Single(b=>b.Handler==RailwayHandler.Normal&&b.Variant==0).Reference;
        Assert.Throws<ArgumentException>(()=>Rebuild(w,voxels:w.Voxels.Select(v=>v with{Traffic=v.Traffic! with{Rail=new(curve,0)}})));
        Assert.Throws<ArgumentException>(()=>Rebuild(w,graph:Graph([Building()])));
        var fleet=FleetGraph.Empty(RollingStockCatalog.Bundled.FleetProfile,2);
        Assert.Throws<ArgumentException>(()=>Rebuild(w,fleet:fleet)); // Thin ID2 has no SpatialEntity.
        var together=World(w.Stations!,FleetGraph.Empty(RollingStockCatalog.Bundled.FleetProfile,100));
        Assert.NotNull(together.Fleet);Assert.Same(w.Stations,together.Stations);
        Assert.Equal(before,w.Spatial.Capture());Assert.Same(p,w.Stations!.Platforms[0]);
    }
    [Fact]
    public void Detached_successors_preserve_state_and_require_revisions_without_reusing_identities()
    {
        var b=Building();var w=World(Graph([b]));
        Assert.Same(w.Stations,w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes).Stations);
        var renamed=Graph([b with{Revision=1,Name="Central",Passengers=new(23,45,67),Imports=new(10,20,1.25,2.5)}]);
        var entities=w.Spatial.Entities.Select(e=>e.Id==1?e with{Revision=1}:e).ToImmutableArray();
        var next=w.Replace(0,w.Terrain,new(w.Spatial.NextEntityId,entities),w.Voxels,w.Slopes,stations:renamed);
        Assert.Equal("Central",next.Stations!.Building(1).Name);Assert.Equal("Station",w.Stations!.Building(1).Name);
        Assert.Throws<ArgumentException>(()=>w.Replace(0,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,stations:Graph([b with{Name="Changed"}])));
        Assert.Throws<ArgumentException>(()=>w.Replace(0,w.Terrain,new(3,entities),w.Voxels,w.Slopes,stations:Graph([b with{Revision=2}])));
        var empty=w.Replace(0,w.Terrain,new(w.Spatial.NextEntityId,[]),[],[],stations:Graph());
        Assert.Empty(empty.Stations!.Buildings);
        Assert.Throws<ArgumentException>(()=>empty.Replace(1,empty.Terrain,w.Spatial.Capture(),[],[],stations:w.Stations));
        Assert.Throws<InvalidOperationException>(()=>next.Replace(0,next.Terrain,next.Spatial.Capture(),[],[]));
    }
    [Fact]
    public void Logical_platform_and_traffic_cosmetic_changes_require_their_own_revisions()
    {
        var p=Platform();var passage=new StationPassage(30,new(28,20,2),Heading.East,PassageMaterial.Concrete);
        var w=World(Graph(platforms:[p],passages:[passage]));
        var renamed=Graph(platforms:[p with{Name="Named",Revision=1}],passages:[passage]);
        Assert.Equal("Named",w.Replace(0,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,stations:renamed).Stations!.Platform(2).Name);
        Assert.Throws<ArgumentException>(()=>w.Replace(0,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,
            stations:Graph(platforms:[p with{Name="Named"}],passages:[passage])));
        var material=Graph(platforms:[p],passages:[passage with{Material=PassageMaterial.Green}]);
        Assert.Throws<ArgumentException>(()=>w.Replace(0,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,stations:material));
        var spatial=new SpatialState(w.Spatial.NextEntityId,w.Spatial.Entities.Select(e=>e.Id==30?e with{Revision=1}:e).ToImmutableArray());
        Assert.Equal(PassageMaterial.Green,w.Replace(0,w.Terrain,spatial,w.Voxels,w.Slopes,stations:material).Stations!.Passages[0].Material);
        Assert.Throws<ArgumentException>(()=>w.Replace(0,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,
            stations:Graph(platforms:[p with{Revision=1,Length=3,Yards=p.Yards.RemoveAt(3)}],passages:[passage])));
    }
    [Fact]
    public void Graph_inputs_are_detached_and_value_equal_arrays_preserve_revision_identity()
    {
        var b=Building(children:[2]);var p=Platform(host:1);
        var buildingList=new List<StationBuilding>{b};var platformList=new List<StationPlatform>{p};
        var graph=Graph(buildingList,platformList);buildingList.Clear();platformList.Clear();
        var w=World(graph);
        var copy=Graph([b with{Children=b.Children.ToArray().ToImmutableArray()}],
            [p with{Children=p.Children.ToArray().ToImmutableArray(),Yards=p.Yards.ToArray().ToImmutableArray()}]);
        var next=w.Replace(0,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,stations:copy);
        Assert.Equal(0,next.Stations!.Building(1).Revision);Assert.Single(graph.Buildings);Assert.Single(graph.Platforms);
        Assert.Throws<ArgumentException>(()=>new StationGraph(Profile,[b],[p],nextStationName:0));
    }
    [Fact]
    public void Invalid_content_profiles_do_not_create_service_hosts_or_geometry_bindings()
    {
        Assert.Throws<ArgumentException>(()=>new StationProfile(Profile.Packs,[Definition,Definition],Profile.Art,Profile.Bells));
        Assert.Throws<ArgumentException>(()=>new StationProfile(Profile.Packs,[Definition with{Height=0}],Profile.Art,Profile.Bells));
        Assert.Throws<ArgumentException>(()=>new StationProfile(Profile.Packs,[Definition],Profile.Art.Add(Profile.Art[0]),Profile.Bells));
        Assert.Throws<ArgumentException>(()=>new StationProfile(Profile.Packs,[Definition],Profile.Art.SetItem(0,Profile.Art[0] with{Material=PassageMaterial.Brown}),Profile.Bells));
        Assert.Throws<ArgumentException>(()=>new StationProfile(Profile.Packs,[Definition],Profile.Art,Profile.Bells.SetItem(0,Profile.Bells[0] with{Sha256="missing"})));
        Assert.Throws<ArgumentException>(()=>new StationProfile(Profile.Packs,[Definition with{Reference=Definition.Reference with{PackId="missing"}}],Profile.Art,Profile.Bells));
    }
    [Fact]
    public void Counters_logs_and_inactive_or_registered_timer_state_validate_without_dispatching()
    {
        var b=Building() with{HourTimer=new(1,3,3480,"station/1/hour",60),DayTimer=new(2,4,4320,"station/1/day",1440)};
        var p=Platform() with{DayTimer=new(5,6,4900,"platform/2/day",1440)};
        var graph=Graph([b],[p]);Assert.Equal(4900,World(graph).Stations!.Platform(2).DayTimer!.DueMinute);
        foreach(var bad in new[]{b with{Passengers=new(-1)},b with{Passengers=new(0,-1)},b with{Passengers=new(0,0,-1)},
            b with{Imports=new(-1)},b with{Exports=new(ThisWeek:double.NaN)},b with{Trains=new(LastWeekPerDay:double.PositiveInfinity)},
            b with{DayTimer=null},b with{HourTimer=b.HourTimer! with{Interval=1440}},b with{HourTimer=b.HourTimer! with{HandlerId="station/2/hour"}},
            b with{HourTimer=b.HourTimer! with{Registration=2}},b with{HourTimer=b.HourTimer! with{Order=4}}})
            Assert.Throws<ArgumentException>(()=>Graph([bad],[p]));
        Assert.Null(Graph([Building()]).Building(1).HourTimer);
    }
    [Fact]
    public void Cosmetic_parts_remain_distinct_from_service_hosts_and_support_source_material_choices()
    {
        foreach(var material in Enum.GetValues<PassageMaterial>())
        {
            var p=Platform();p=p with{Yards=p.Yards.SetItem(0,p.Yards[0] with{Outlook=new(PlatformOutlookKind.Passage,material,Bridge:true)})};
            var passage=new StationPassage(30,new(28,20,2),Heading.East,material);
            var g=Graph(platforms:[p],passages:[passage]);var w=World(g);
            Assert.Null(g.HostStation(p.Id));Assert.Single(w.Stations!.Passages);Assert.Equal(5,w.Voxels.Length);
            Assert.Throws<ArgumentException>(()=>Graph(platforms:[p with{HostId=30}]));
            Assert.Throws<ArgumentException>(()=>Graph(platforms:[p],passages:[passage,passage]));
        }
    }
    [Fact]
    public void Current_wire_and_save_admission_cannot_silently_drop_detached_station_state()
    {
        var w=World(Graph([Building()]));
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(64,24,8),0);
        var session=new MixedSessionState(new(w,new()),TerrainProvenance.From(request));
        var error=Assert.Throws<InvalidDataException>(()=>SaveCodec.Write(session));
        Assert.Equal("stationStateRequiresP6Transport",error.Message);
        Assert.Throws<InvalidDataException>(()=>MixedWire.ValidateAdmission(session));
        Assert.Throws<InvalidDataException>(()=>MixedWire.Snapshot(session,1));
        Assert.Throws<InvalidDataException>(()=>MixedWire.Read(session.Construction));
        Assert.Throws<InvalidDataException>(()=>MixedWire.RailwayEntities(w));
        var emptyGraph=new StationGraph(Profile,[],[],nextStationName:4,nextPlatformName:5);
        Assert.Throws<InvalidDataException>(()=>MixedWire.RailwayEntities(World(emptyGraph)));
        Assert.Same(w,session.World);
    }
}
