using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class RailwayConstructionTests
{
    static RailwayProfile Profile => RailwayProfiles.Bundled;
    static Location P => new(8,8,2);
    static Location End => new(10,8,2);
    static DefinitionReference Ref(string id) => new(PreparedContentCatalog.BundledPackId,id,id);
    static RailComponent Rail(int pattern=4) => new(Ref($"system:pattern/{pattern}"),pattern);
    static TerrainWorld Terrain(int ground=2,int water=0,int levels=8) => new(Profile.Terrain,"Construction",new(32,24,levels),water,
        Enumerable.Repeat(new TerrainCell(ground,TerrainSurface.Cut),768),new(1,[]),[]);
    static MixedWorld World(TerrainWorld? terrain=null,RailwayProfile? profile=null,
        params (Location P,TrafficComponents T)[] traffic)
    {
        terrain ??= Terrain(); profile ??= Profile;
        var next=terrain.Spatial.NextEntityId;
        var entities=terrain.Spatial.Entities.ToBuilder(); var voxels=new List<RailwayVoxel>();
        foreach (var (p,t) in traffic)
        {
            entities.Add(new(next,0,profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
            voxels.Add(new(p,next++,RailwayVoxelKind.Traffic,t));
        }
        return new(profile,terrain,new(next,entities.ToImmutable()),voxels,[],terrain.Revision);
    }
    static TerrainWorld Occupied(TerrainWorld terrain,Location p,TerrainOccupantKind kind,QuarterCorners corners=default,
        ForestDecoration? forest=null,bool owned=false)
    {
        var definition=terrain.Profile.Definitions.First(d=>d.Kind==kind && d.TreeSpriteCount==0).Reference;
        var cells=terrain.Cells.ToBuilder(); var s=terrain.Space.ToStorage(p);
        cells[s.V*terrain.Space.HorizontalSize+s.H]=new(p.Z+(kind==TerrainOccupantKind.Terrace?1:0),
            kind==TerrainOccupantKind.Terrace?TerrainSurface.Terrace:TerrainSurface.Cut);
        long id=terrain.Spatial.NextEntityId;
        var entity=new SpatialEntity(id,0,definition,p,new([new(new(0,0,0),kind==TerrainOccupantKind.Creek || kind==TerrainOccupantKind.Mountain&&corners.Flat)]),
            owned,kind==TerrainOccupantKind.Creek,forest==null?0:terrain.Profile.Resolve(forest.Definition).Price);
        return new(terrain.Profile,terrain.Name,new(32,24,terrain.Space.Levels),terrain.WaterLevel,cells,
            new(id+1,terrain.Spatial.Entities.Add(entity)),terrain.Occupants.Add(new(id,kind,corners,forest)));
    }
    static MixedWorld WithPier(MixedWorld world,Location p,long supportedId)
    {
        var id=world.Spatial.NextEntityId; var definition=Ref("system:pier");
        return new(world.Profile,world.Terrain,new(id+1,world.Spatial.Entities.Add(new(id,0,definition,p,
            new([new(new(0,0,0),true)]),true,false,0))),world.Voxels.Add(new(p,id,RailwayVoxelKind.Pier,
                Definition:definition,SupportedOwnerId:supportedId)),world.Slopes,world.Revision);
    }
    static MixedConstructionState Apply(MixedWorld world,NormalRailwayConstructionPlan plan) =>
        NormalRailwayConstruction.Apply(new(world),plan,new(GameTime.StartMinute));
    static void Rejected(NormalRailwayConstructionPlan plan,string reason)
    {
        Assert.False(plan.Legal); Assert.Equal(reason,plan.Reason); Assert.Empty(plan.Booking.Charges);
        Assert.Empty(plan.Reclaimed); Assert.Empty(plan.RemovedPiers); Assert.Empty(plan.ElectedPiers);
        Assert.Throws<InvalidOperationException>(()=>Apply(plan.Source,plan));
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    [InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)]
    public void Eight_directions_build_and_remove_atomically_with_literal_ground_price(int direction)
    {
        var w=World();var end=P.Move(new(direction)).Move(new(direction));
        var plan=NormalRailwayConstruction.PreviewBuild(w,P,end);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(18_000_000,plan.Booking.Total);Assert.Empty(plan.ElectedPiers);
        var built=Apply(w,plan);Assert.Equal(3,built.World.Voxels.Length);Assert.Equal(1,built.World.Revision);
        Assert.Equal(15_000_000_000-18_000_000,built.Accounts.Cash);Assert.Empty(w.Voxels);Assert.Equal(1,w.Spatial.NextEntityId);
        Assert.Same(w.Terrain,built.World.Terrain);
        var remove=NormalRailwayConstruction.PreviewRemoval(built.World,P,end);
        Assert.True(remove.Legal);Assert.Equal(18_000_000,remove.Booking.Total);
        var removed=NormalRailwayConstruction.Apply(built,remove,new(GameTime.StartMinute));
        Assert.Empty(removed.World.Voxels);Assert.Equal(4,removed.World.Spatial.NextEntityId);
        Assert.Equal(2,removed.Accounts.Transactions.Length);Assert.Equal(2,removed.World.Revision);
    }

    [Theory]
    [InlineData(1,3,0,54_000_000)]
    [InlineData(4,2,0,72_000_000)]
    [InlineData(3,2,4,36_000_000)]
    [InlineData(0,3,2,72_000_000)]
    [InlineData(2,2,2,18_000_000)]
    public void Fixed_elevation_uses_actual_ground_and_source_water_formula(int ground,int z,int water,long price)
    {
        // Elevated routes must begin on an existing rail; it is charged too.
        var from=P with{Z=z};var to=End with{Z=z};
        var w=World(Terrain(ground,water),traffic:[(from,new(Rail()))]);
        var plan=NormalRailwayConstruction.PreviewBuild(w,from,to);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(price,plan.Booking.Total);
        var result=Apply(w,plan);
        Assert.All(result.World.Voxels.Where(v=>v.Traffic!=null),v=>Assert.Equal(z,v.Position.Z));
        Assert.Same(w.Spatial.At(from).Entity,result.World.Spatial.At(from).Entity);
        Assert.Same(w.At(from),result.World.At(from)); // charged unchanged endpoint
        Assert.Equal(ground<z?2*(z-ground):0,plan.ElectedPiers.Length);
    }

    [Theory]
    [InlineData(0,0,2)][InlineData(0,1,2)][InlineData(0,2,2)][InlineData(1,1,2)]
    public void Underwater_or_at_water_rejects_even_existing_endpoints(int ground,int z,int water)
    {
        var p=P with{Z=z};var w=World(Terrain(ground,water),traffic:[(p,new(Rail()))]);
        Rejected(NormalRailwayConstruction.PreviewBuild(w,p,End with{Z=z}),"underwater");
    }

    [Fact]
    public void Starts_require_existing_rail_or_at_or_below_ground_and_never_snap_endpoint_height()
    {
        var w=World(Terrain(1));var raised=P with{Z=3};
        Assert.False(NormalRailwayConstruction.IsUsableStart(w,raised));
        Rejected(NormalRailwayConstruction.PreviewBuild(w,raised,End with{Z=3}),"unusableStart");
        Rejected(NormalRailwayConstruction.PreviewRemoval(w,raised,End with{Z=3}),"unusableStart");
        Rejected(NormalRailwayConstruction.PreviewBuild(w,P,End with{Z=3}),"noncoplanar");
        Assert.True(NormalRailwayConstruction.IsUsableStart(w,P with{Z=0}));
        var road=Ref("test:road");var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(road,RailwayHandler.Road)));
        w=World(Terrain(1),profile,(raised,new(Road:new(road,17))));
        Assert.False(NormalRailwayConstruction.IsUsableStart(w,raised));
    }

    [Theory]
    [InlineData(0)][InlineData(7)]
    public void Floor_and_roof_storage_cells_are_legal_but_outside_z_is_not(int z)
    {
        var w=World(Terrain(z));var a=P with{Z=z};var b=End with{Z=z};
        Assert.True(NormalRailwayConstruction.PreviewBuild(w,a,b).Legal);
        foreach(var outside in new[]{-1,8})
            Rejected(NormalRailwayConstruction.PreviewBuild(w,a with{Z=outside},b with{Z=outside}),"outside");
        Assert.False(NormalRailwayConstruction.IsUsableStart(w,new(int.MinValue,int.MaxValue,z)));
    }

    [Fact]
    public void Stored_border_is_not_the_train_border_and_does_not_shrink_construction_space()
    {
        var w=World();var a=w.Terrain.Space.ToLocation(new(5,0,2));var b=w.Terrain.Space.ToLocation(new(6,0,2));
        Assert.True(w.Terrain.IsTrainBorder(a));Assert.True(w.Terrain.Space.ContainsVoxel(a));
        var plan=NormalRailwayConstruction.PreviewBuild(w,a,b);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(12_000_000,plan.Booking.Total);
        Assert.Equal(2,Apply(w,plan).World.Voxels.Length);
    }

    [Theory]
    [InlineData(false,false)][InlineData(true,false)][InlineData(false,true)]
    public void Flat_mountains_corner_mountains_and_forests_are_obstacles_even_when_transparent(bool sloped,bool forest)
    {
        var definition=Profile.Terrain.Definitions.First(d=>d.TreeSpriteCount>0);
        var decoration=forest?new ForestDecoration(definition.Reference,[new(4,5,0)]):null;
        var terrain=Occupied(Terrain(),End,TerrainOccupantKind.Mountain,sloped?new(1,2,3,4):default,decoration);
        var w=World(terrain);var state=new MixedConstructionState(w);var before=terrain.Read();
        Assert.Equal(!sloped,w.Spatial.At(End).Transparent);
        Rejected(NormalRailwayConstruction.PreviewBuild(w,P,End),"obstacle");
        Assert.Same(terrain,w.Terrain);Assert.Equal(before,terrain.Read());Assert.Empty(state.Accounts.Transactions);
    }

    [Fact]
    public void Terrace_backing_blocks_underground_rail_but_its_empty_top_is_buildable_and_preserved()
    {
        var terrain=Occupied(Terrain(),End,TerrainOccupantKind.Terrace,new(1,2,3,4));
        var w=World(terrain);Rejected(NormalRailwayConstruction.PreviewBuild(w,P,End),"obstacle");
        var from=End with{X=End.X-1,Z=3};var to=End with{Z=3};
        // Begin at ground Z3 on the terrace, heading out into the air.
        var plan=NormalRailwayConstruction.PreviewBuild(w,to,from);
        Assert.True(plan.Legal,plan.Reason);var after=Apply(w,plan).World;
        Assert.Same(terrain,after.Terrain);Assert.Same(w.Spatial.At(End).Entity,after.Spatial.At(End).Entity);
        Assert.Empty(plan.ElectedPiers.Where(p=>p.Position.X==End.X&&p.Position.Y==End.Y));
    }

    [Fact]
    public void Creek_reclamation_removes_the_whole_entity_and_preserves_unrelated_terrain_ids_and_corners()
    {
        var other=new Location(15,10,2);
        var terrain=Occupied(Occupied(Terrain(),End,TerrainOccupantKind.Creek),other,TerrainOccupantKind.Mountain,new(1,2,3,4));
        var w=World(terrain);var plan=NormalRailwayConstruction.PreviewBuild(w,P,End);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(18_000_000,plan.Booking.Total);
        var loss=Assert.Single(plan.Reclaimed);Assert.Equal(TerrainOccupantKind.Creek,loss.Occupant.Kind);
        Assert.Single(loss.Entity.Footprint.Members);Assert.Equal(1,loss.Entity.Id);
        Assert.Equal(2,terrain.Occupants.Length); // preview did not reclaim anything
        var after=Apply(w,plan).World;
        Assert.Equal(1,after.Terrain.Revision);Assert.Equal(1,after.Revision);
        Assert.Equal(terrain.Cells.ToArray(),after.Terrain.Cells.ToArray());
        Assert.Single(after.Terrain.Occupants);Assert.Same(terrain.OccupantAt(other),after.Terrain.OccupantAt(other));
        Assert.Same(w.Spatial.At(other).Entity,after.Spatial.At(other).Entity);
        Assert.Null(after.Terrain.OccupantAt(End));Assert.NotEqual(loss.Entity.Id,after.At(End)!.OwnerId);
    }

    [Fact]
    public void Below_ground_transparent_occupancy_suppresses_piers_without_rejecting_rail()
    {
        var p=P with{Z=4};var middle=new Location(9,8,4);var road=Ref("test:road");
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(road,RailwayHandler.Road)));
        var w=World(Terrain(2),profile,(p,new(Rail())),(middle with{Z=0},new(Road:new(road,17))));
        var plan=NormalRailwayConstruction.PreviewBuild(w,p,End with{Z=4});
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(new[]{2,3},plan.ElectedPiers.Select(p=>p.Position.Z));
        Assert.All(plan.ElectedPiers,p=>Assert.Equal(End.X,p.Position.X));
        var after=Apply(w,plan).World;Assert.NotNull(after.At(middle)!.Traffic!.Rail);
        Assert.Same(w.At(middle with{Z=0}),after.At(middle with{Z=0}));
        Assert.Equal(0,after.Spatial.At(middle with{Z=0}).Entity!.Revision);
    }

    [Fact]
    public void Literal_support_styles_elect_only_ground_through_rail_minus_one()
    {
        using var fixtures=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var styles=new[]{"default","bridge-top","bridge-body","slopeNE"}.Select((s,i)=>
            new RailwayBinding(Ref("test:pier/"+s),RailwayHandler.Pier,i)).ToArray();
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.AddRange(styles));
        int count=0;
        foreach(var row in fixtures.RootElement.GetProperty("groups").GetProperty("supports").GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");
            if(input.GetProperty("action").GetString()!="elect"||input.GetProperty("occupants").GetArrayLength()!=0)continue;
            var world=World(Terrain(input.GetProperty("ground").GetInt32()),profile);
            RailwayBinding Style(string key)=>styles.Single(s=>s.Reference.ContributionId=="test:pier/"+input.GetProperty(key).GetString());
            var piers=RailwaySupports.Elect(world,P with{Z=input.GetProperty("railZ").GetInt32()},Style("top"),Style("body"));
            Assert.Equal(row.GetProperty("expected").GetProperty("elected").EnumerateArray().Select(p=>p.GetProperty("z").GetInt32()),piers.Select(p=>p.Position.Z));
            Assert.Equal(row.GetProperty("expected").GetProperty("elected").EnumerateArray().Select(p=>"test:pier/"+p.GetProperty("style").GetString()),piers.Select(p=>p.Definition.ContributionId));
            Assert.Empty(world.Voxels);Assert.Equal(1,world.Spatial.NextEntityId);count++;
        }
        Assert.Equal(5,count);
    }

    [Fact]
    public void Teardown_removes_foreign_piers_below_noop_car_and_empty_cells_but_preserves_other_supports()
    {
        var car=Ref("test:car");var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(car,RailwayHandler.Car)));
        var a=P with{Z=3};var b=new Location(9,8,3);var c=End with{Z=3};var off=new Location(11,8,6);
        var w=World(Terrain(),profile,(a,new(Rail(10))),(b,new(Rail(),Car:new(car,10,11))),
            (a with{Z=6},new(Rail())),(b with{Z=6},new(Rail())),(c with{Z=6},new(Rail())),(off,new(Rail())));
        w=WithPier(w,a with{Z=0},w.At(a with{Z=6})!.OwnerId);
        w=WithPier(w,b with{Z=1},w.At(b with{Z=6})!.OwnerId);
        w=WithPier(w,c with{Z=2},w.At(c with{Z=6})!.OwnerId);
        w=WithPier(w,c with{Z=4},w.At(c with{Z=6})!.OwnerId);
        w=WithPier(w,off with{Z=1},w.At(off)!.OwnerId);
        var plan=NormalRailwayConstruction.PreviewRemoval(w,a,c);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(8_000_000,plan.Booking.Total);
        Assert.Equal(new[]{0,1,2},plan.RemovedPiers.Select(p=>p.Position.Z));
        Assert.All(plan.RemovedPiers,p=>Assert.NotNull(p.SupportedOwnerId));
        Assert.All(plan.Topology.Cells,c=>Assert.False(c.Changed));
        var after=Apply(w,plan).World;
        foreach(var v in w.Voxels.Except(plan.RemovedPiers))
        { Assert.Same(v,after.At(v.Position));Assert.Same(w.Spatial.At(v.Position).Entity,after.Spatial.At(v.Position).Entity); }
        Assert.All(plan.RemovedPiers,p=>Assert.Null(after.At(p.Position)));
        Assert.Equal(w.Spatial.NextEntityId,after.Spatial.NextEntityId);
    }

    [Fact]
    public void Removed_rails_leave_roads_and_poles_and_clean_up_crossings_with_explicit_losses()
    {
        var road=Ref("test:road");var crossing=Ref("test:crossing");var pole=Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference;
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(road,RailwayHandler.Road)).Add(new(crossing,RailwayHandler.Crossing)));
        var w=World(profile:profile,traffic:[(P,new(Rail(),new(road,17),new(crossing))),(End,new(Rail(),Accessory:new(pole)))]);
        var plan=NormalRailwayConstruction.PreviewRemoval(w,P,End);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(crossing,Assert.Single(plan.ComponentLosses).Accessory.Definition);
        var after=Apply(w,plan).World;
        Assert.Null(after.At(P)!.Traffic!.Rail);Assert.Null(after.At(P)!.Traffic!.Accessory);Assert.Equal(road,after.At(P)!.Traffic!.Road!.Definition);
        Assert.Null(after.At(End)!.Traffic!.Rail);Assert.Equal(pole,after.At(End)!.Traffic!.Accessory!.Definition);
        Assert.Equal(w.At(P)!.OwnerId,after.At(P)!.OwnerId);Assert.Equal(1,after.Spatial.At(P).Entity!.Revision);
    }

    [Fact]
    public void Road_crossing_creation_requires_an_exact_handler_and_existing_poles_are_retained()
    {
        var road=Ref("test:road");var crossing=Ref("test:crossing");
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(road,RailwayHandler.Road)));
        var w=World(profile:profile,traffic:[(P,new(Road:new(road,17)))]);
        Rejected(NormalRailwayConstruction.PreviewBuild(w,P,End),"unavailableCrossingBinding");
        profile=new(Profile.Terrain,profile.Bindings.Add(new(crossing,RailwayHandler.Crossing)));
        w=World(profile:profile,traffic:[(P,new(Road:new(road,17)))]);
        var after=Apply(w,NormalRailwayConstruction.PreviewBuild(w,P,End)).World;
        Assert.Equal(crossing,after.At(P)!.Traffic!.Accessory!.Definition);
        var pole=Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference;
        w=World(profile:profile,traffic:[(P,new(Road:new(road,17),Accessory:new(pole)))]);
        after=Apply(w,NormalRailwayConstruction.PreviewBuild(w,P,End)).World;
        Assert.Equal(pole,after.At(P)!.Traffic!.Accessory!.Definition);
    }

    [Fact]
    public void Platform_dependency_rejects_removal_without_charging_or_leaving_an_orphan()
    {
        var platform=Ref("test:platform");var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Add(new(platform,RailwayHandler.Platform)));
        var w=World(profile:profile,traffic:[(End,new(Rail(),Platform:platform))]);
        Rejected(NormalRailwayConstruction.PreviewRemoval(w,P,End),"platformDependency");
        Assert.Equal(platform,w.At(End)!.Traffic!.Platform);
    }

    [Fact]
    public void Exact_source_world_is_required_even_for_an_equal_revision_and_replay_is_stale()
    {
        var w=World();var plan=NormalRailwayConstruction.PreviewBuild(w,P,End);
        var replacement=World();
        Assert.Equal("staleRevision",Assert.Throws<InvalidOperationException>(()=>Apply(replacement,plan)).Message);
        var built=Apply(w,plan);
        Assert.Equal("staleRevision",Assert.Throws<InvalidOperationException>(()=>NormalRailwayConstruction.Apply(built,plan,new(GameTime.StartMinute))).Message);
        Assert.Single(built.Accounts.Transactions);
    }

    [Fact]
    public void Allocation_and_booking_overflow_publish_neither_geometry_nor_accounts()
    {
        var w=World();w=new(w.Profile,w.Terrain,new(long.MaxValue,[]),[],[]);
        Rejected(NormalRailwayConstruction.PreviewBuild(w,P,End),"constructionOverflow");
        var accounts=new ConstructionAccounts(long.MinValue);w=World();var state=new MixedConstructionState(w,accounts);
        var plan=NormalRailwayConstruction.PreviewBuild(w,P,End);
        Assert.True(plan.Legal);Assert.Throws<OverflowException>(()=>NormalRailwayConstruction.Apply(state,plan,new(GameTime.StartMinute)));
        Assert.Empty(w.Voxels);Assert.Empty(accounts.Transactions);Assert.Equal(1,w.Spatial.NextEntityId);
        Assert.Equal(ProtocolIdentity.MaximumRailwayVoxels,NormalRailwayConstruction.MaximumRailwayVoxels);
        Assert.Equal(ProtocolIdentity.MaximumAccountTransactions,NormalRailwayConstruction.MaximumAccountTransactions);
    }

    [Fact]
    public void Legal_route_preserves_a_real_natural_corner_neighborhood_and_terrace_forest_scene()
    {
        var terrain=TerrainWorld.Empty(new(WorldCreationProfile.Id,"Natural",new(32,24,8),2),Profile.Terrain);
        var shaped=CornerTerrain.Apply(terrain,CornerTerrain.Preview(terrain,new(16,16,2),"raise"));
        var w=World(shaped);var plan=NormalRailwayConstruction.PreviewBuild(w,P,End);
        Assert.True(plan.Legal,plan.Reason);var after=Apply(w,plan).World;
        Assert.Same(shaped,after.Terrain);Assert.Equal(shaped.Spatial.Entities,after.Terrain.Spatial.Entities);
        Assert.Equal(shaped.FineHeight(new(16,16,2),0),after.Terrain.FineHeight(new(16,16,2),0));

        terrain=TerrainTransportScenario.Create().World;w=World(terrain);
        var occupied=terrain.Space.ToLocation(new(0,0,2));var adjacent=occupied.Move(Heading.SouthEast);
        Rejected(NormalRailwayConstruction.PreviewBuild(w,occupied,adjacent),"underwater");
        Assert.Equal(new QuarterCorners(1,2,3,4),terrain.OccupantAt(occupied)!.Corners);
        Assert.NotNull(terrain.OccupantAt(occupied)!.Forest);
        Assert.Equal(TerrainOccupantKind.Terrace,terrain.OccupantAt(occupied with{Z=1})!.Kind);
    }

    [Fact]
    public void Slopes_fillers_and_stored_supports_block_new_rail_but_normal_teardown_keeps_the_whole_ramp()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var row=f.RootElement.GetProperty("groups").GetProperty("slopes").GetProperty("cases")[0];
        var (entity,cells,owner)=MixedWorldTests.Slope(row);
        var w=new MixedWorld(Profile,Terrain(),new(2,[entity]),cells,[owner]);
        var filler=cells.First(v=>v.Kind==RailwayVoxelKind.SlopeFiller).Position;
        // Use an existing rail as the elevated starting anchor.
        var start=filler.Move(Heading.West);
        var id=w.Spatial.NextEntityId;
        var anchor=new SpatialEntity(id,0,Profile.TrafficOwner,start,new([new(new(0,0,0),true)]),true,false,0);
        w=new(Profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(anchor)),w.Voxels.Add(new(start,id,RailwayVoxelKind.Traffic,new(Rail()))),[owner]);
        Rejected(NormalRailwayConstruction.PreviewBuild(w,start,filler),"obstacle");
        var baseRail=cells.First(v=>v.Traffic?.Rail!=null).Position;
        var end=baseRail.Move(Heading.West);
        var plan=NormalRailwayConstruction.PreviewRemoval(w,baseRail,end);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(6_000_000,plan.Booking.Total);
        var after=Apply(w,plan).World;
        foreach(var cell in cells)Assert.Same(cell,after.At(cell.Position));
        Assert.Same(entity,after.Spatial.At(baseRail).Entity);Assert.Equal(w.Slopes.ToArray(),after.Slopes.ToArray());
    }

    [Fact]
    public void Missing_pier_binding_and_late_obstacles_return_no_partial_quote_or_loss_list()
    {
        var profile=new RailwayProfile(Profile.Terrain,Profile.Bindings.Where(b=>b.Handler!=RailwayHandler.Pier));
        var w=World(profile:profile);
        Rejected(NormalRailwayConstruction.PreviewBuild(w,P,End),"unavailablePierBinding");
        var creek=P.Move(Heading.East);
        var terrain=Occupied(Occupied(Terrain(),creek,TerrainOccupantKind.Creek),End,TerrainOccupantKind.Mountain);
        w=World(terrain);Rejected(NormalRailwayConstruction.PreviewBuild(w,P,End),"obstacle");
        Assert.Equal(2,terrain.Occupants.Length);Assert.Equal(3,w.Spatial.NextEntityId);
    }

    [Fact]
    public void Railway_plan_diagnostics_do_not_recurse_through_computed_headings()
    {
        Assert.Equal("Heading { Index = 2 }",Heading.East.ToString());
        Assert.Contains("Index = 2",NormalRailway.PlanBuild(World(),P,End).Cells[0].ToString());
    }

    [Fact]
    public void Empty_removal_keeps_the_source_zero_spend_call_and_foreign_support_at_selected_height()
    {
        var w=World(traffic:[(End with{Z=5},new(Rail()))]);
        w=WithPier(w,End,w.At(End with{Z=5})!.OwnerId);
        var plan=NormalRailwayConstruction.PreviewRemoval(w,P,End);
        Assert.True(plan.Legal);Assert.Equal(0,Assert.Single(plan.Booking.Charges).Amount);Assert.Empty(plan.RemovedPiers);
        var after=Apply(w,plan);Assert.Single(after.Accounts.Transactions);
        Assert.Same(w.At(End),after.World.At(End));Assert.Equal(w.Spatial.NextEntityId,after.World.Spatial.NextEntityId);
    }

    [Fact]
    public void Current_voxel_and_account_budgets_admit_the_boundary_and_reject_before_publication()
    {
        var terrain=new TerrainWorld(Profile.Terrain,"Capacity",new(128,128,8),0,
            Enumerable.Repeat(new TerrainCell(2),128*128),new(1,[]),[]);
        var a=terrain.Space.ToLocation(new(50,64,2));var b=a.Move(Heading.East).Move(Heading.East);
        var existing=Enumerable.Range(0,8190).Select(i=>(terrain.Space.ToLocation(new(i%128,i/128,4)),new TrafficComponents(Rail()))).ToArray();
        var full=World(terrain,traffic:existing);Rejected(NormalRailwayConstruction.PreviewBuild(full,a,b),"railwayCapacityLimit");
        var fits=World(terrain,traffic:existing[..8189]);var plan=NormalRailwayConstruction.PreviewBuild(fits,a,b);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(8192,Apply(fits,plan).World.Voxels.Length);

        var entries=Enumerable.Range(1,8192).Select(i=>new AccountTransaction(i,1,GameTime.StartMinute,
            "capacity",AccountGenre.RailService,0,0,ConstructionAccounts.DefaultInitialCash)).ToImmutableArray();
        var accounts=ConstructionAccounts.Restore(new(ConstructionAccounts.DefaultInitialCash,new(),entries));
        var w=World();w=new(w.Profile,w.Terrain,w.Spatial.Capture(),[],[],1);
        var state=new MixedConstructionState(w,accounts);plan=NormalRailwayConstruction.PreviewBuild(w,P,End);
        Assert.Equal("accountCapacityLimit",Assert.Throws<InvalidOperationException>(()=>NormalRailwayConstruction.Apply(state,plan,new(GameTime.StartMinute))).Message);
        Assert.Empty(w.Voxels);Assert.Equal(8192,state.Accounts.Transactions.Length);Assert.Equal(1,w.Spatial.NextEntityId);
    }

    [Fact]
    public void Preview_and_publication_preserve_clock_random_mode_and_exact_save_reopen()
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Construction",new(32,24,8),0);
        var terrain=Occupied(Terrain(),End,TerrainOccupantKind.Creek);
        var state=new MixedSessionState(new(World(terrain)),TerrainProvenance.From(request),constructionMode:true);
        state.Random["forest"].Next();var before=SaveCodec.Write(state);
        var plan=NormalRailwayConstruction.PreviewBuild(state.World,P,End);
        Assert.True(plan.Legal);Assert.Equal(before,SaveCodec.Write(state));
        var candidate=NormalRailwayConstruction.Apply(state.Construction,plan,state.Clock.Time);
        var after=state.Successor(state.Revision,candidate,state.ConstructionMode);
        Assert.Equal(before,SaveCodec.Write(state));Assert.True(after.ConstructionMode);
        Assert.Equal(state.Clock.Capture(),after.Clock.Capture());
        Assert.Equal(state.Random["forest"].Next(),after.Random["forest"].Next());
        var bytes=SaveCodec.Write(after);var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(bytes,SaveCodec.Write(restored));Assert.Equal(18_000_000,Assert.Single(restored.Construction.Accounts.Transactions).Expenditure);
        var removal=NormalRailwayConstruction.PreviewRemoval(restored.World,P,End);
        var final=restored.Successor(restored.Revision,NormalRailwayConstruction.Apply(restored.Construction,removal,restored.Clock.Time),true);
        Assert.Empty(final.World.Voxels);Assert.Empty(final.World.Terrain.Occupants);
        Assert.Equal(2,final.Construction.Accounts.Transactions.Length);
    }
}
