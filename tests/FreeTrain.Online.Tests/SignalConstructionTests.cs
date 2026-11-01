using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;
public class SignalConstructionTests
{
    static RailwayProfile Profile=>RailwayProfiles.Bundled;
    static Location P=>GarageConstructionTests.Start;
    static MixedWorld World()=>GarageConstructionTests.World();
    static MixedWorld Add(MixedWorld w,RailwayHandler? handler,int pattern=4,bool car=false,long? controller=null)
    {
        var id=w.Spatial.NextEntityId;
        var carDef=new DefinitionReference(Profile.Terrain.Packs[0].Id,"test:car","test:car");
        var profile=car?new RailwayProfile(w.Profile.Terrain,w.Profile.Bindings.Add(new(carDef,RailwayHandler.Car))):w.Profile;
        var b=handler==null?null:profile.Bindings.First(b=>b.Handler==handler&&(handler is not (RailwayHandler.Normal or RailwayHandler.Junction)||b.Variant==pattern));
        var rail=b==null?null:new RailComponent(b.Reference,pattern,Direction:handler==RailwayHandler.Signal?2:0,ControllerId:controller);
        return new(profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(new(id,0,profile.TrafficOwner,P,new([new(new(0,0,0),true)]),true,false,0))),
            w.Voxels.Add(new(P,id,RailwayVoxelKind.Traffic,new(rail,Accessory:handler==null&&!car?new(profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference):null,Car:car?new(carDef,1,1):null))),[],w.Revision);
    }
    static MixedWorld Apply(MixedWorld w,RailwayConstructionPlan p)=>SignalRailwayConstruction.Apply(new(w),p,new(0)).World;
    [Theory]
    [InlineData(SignalRailwayConstruction.ThreeAspect)][InlineData(SignalRailwayConstruction.Shunting)]
    public void Every_direction_replaces_any_ordinary_pattern_and_restores_exactly_without_booking(string type)
    {
        foreach(var d in new[]{0,2,4,6})foreach(var pattern in Enumerable.Range(0,12))
        {
            var original=Add(World(),RailwayHandler.Normal,pattern);var id=original.At(P)!.OwnerId;
            var plan=SignalRailwayConstruction.Preview(original,P,type,d);Assert.True(plan.Legal,plan.Reason);Assert.Empty(plan.Booking.Charges);
            var w=Apply(original,plan);var rail=w.At(P)!.Traffic!.Rail!;
            Assert.Equal(d%4==0?10:4,rail.Pattern);Assert.Equal(d,rail.Direction);Assert.Equal(type,rail.Definition.ContributionId);
            Assert.Equal(id,w.At(P)!.OwnerId);Assert.Equal(original.Spatial.NextEntityId,w.Spatial.NextEntityId);
            Assert.Equal(new SignalControllerReference(id,null),SignalRailwayConstruction.ControllerReference(w,P));
            var remove=SignalRailwayConstruction.Preview(w,P,type,(d+2)%8,true);Assert.True(remove.Legal);Assert.Empty(remove.Booking.Charges);
            var next=SignalRailwayConstruction.Apply(new(w),remove,new(0));Assert.Empty(next.Accounts.Transactions);
            Assert.Equal(rail.Pattern,next.World.At(P)!.Traffic!.Rail!.Pattern);Assert.Null(SignalRailwayConstruction.ControllerReference(next.World,P));
            Assert.Equal(RailwayHandler.Normal,Profile.Resolve(next.World.At(P)!.Traffic!.Rail!.Definition).Handler);
        }
    }
    [Fact]
    public void Independent_signal_vectors_and_empty_traffic_cover_eligibility_and_cars()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var count=0;
        foreach(var row in f.RootElement.GetProperty("groups").EnumerateObject().SelectMany(g=>g.Value.GetProperty("cases").EnumerateArray()).Where(c=>c.GetProperty("input").TryGetProperty("kind",out var k)&&k.GetString()=="signal"&&c.GetProperty("input").TryGetProperty("railKind",out _)))
        {
            var i=row.GetProperty("input");var kind=i.GetProperty("railKind").GetString();var car=i.GetProperty("car").GetBoolean();
            var handler=kind switch{"empty"=>(RailwayHandler?)null,"normal"=>RailwayHandler.Normal,"junction"=>RailwayHandler.Junction,"slope"=>RailwayHandler.Slope,_=>RailwayHandler.Signal};
            // Slope footprints have separate tests; reject their real candidate below.
            if(handler==RailwayHandler.Slope)continue;
            var w=Add(World(),handler,handler==RailwayHandler.Junction?12:4,car);
            var plan=SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,i.GetProperty("direction").GetInt32());
            Assert.Equal(row.GetProperty("expected").GetProperty("allowed").GetBoolean(),plan.Legal);Assert.Equal(0,plan.Booking.Total);count++;
        }
        Assert.True(count>0);
        foreach(var d in new[]{0,2,4,6}){
            var source=World();var w=Apply(source,SignalRailwayConstruction.Preview(source,P,SignalRailwayConstruction.Shunting,d));Assert.Equal(d,w.At(P)!.Traffic!.Rail!.Direction);
        }
    }
    [Fact]
    public void Rejection_and_controller_boundary_preserve_shared_identity_and_heading_gate()
    {
        foreach(var handler in new[]{RailwayHandler.Bridge,RailwayHandler.Girder,RailwayHandler.Garage,RailwayHandler.Signal}){
            var w=Add(World(),handler);Assert.Equal("signalTarget",SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,0).Reason);
        }
        var occupied=Add(World(),RailwayHandler.Signal,4,true,42);
        Assert.Equal("carOccupied",SignalRailwayConstruction.Preview(occupied,P,SignalRailwayConstruction.Shunting,0,true).Reason);
        Assert.Equal(new SignalControllerReference(occupied.At(P)!.OwnerId,42),SignalRailwayConstruction.ControllerReference(occupied,P));
        Assert.Equal("signalNotFound",SignalRailwayConstruction.Preview(World(),P,SignalRailwayConstruction.Shunting,0,true).Reason);
        Assert.Equal("outside",SignalRailwayConstruction.Preview(World(),new(-100,0,0),SignalRailwayConstruction.ThreeAspect,0).Reason);
        Assert.False(SignalRailwayConstruction.Preview(World(),P,"unknown",0).Legal);
        Assert.False(SignalRailwayConstruction.Preview(World(),P,SignalRailwayConstruction.ThreeAspect,1).Legal);
        foreach(var d in new[]{0,2,4,6})foreach(var heading in Enumerable.Range(0,8))Assert.Equal(new[]{d,(d+1)%8,(d+7)%8}.Contains(heading),SignalRailwayConstruction.DelegatesStopTime(d,new(heading)));
    }
    [Fact]
    public void Empty_placement_allocates_once_and_prepared_plans_reject_substitution()
    {
        var w=World();var plan=SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,2);Assert.True(plan.Legal);
        var next=SignalRailwayConstruction.Apply(new(w),plan,new(0));Assert.Equal(w.Spatial.NextEntityId+1,next.World.Spatial.NextEntityId);
        Assert.Throws<InvalidOperationException>(()=>SignalRailwayConstruction.Apply(next,plan,new(0)));
        Assert.Throws<InvalidOperationException>(()=>SignalRailwayConstruction.Apply(new(w),NormalRailwayConstruction.PreviewBuild(w,P,P with{X=P.X+1}),new(0)));
    }
    [Fact]
    public void Obstacles_tunnels_and_slope_owners_reject_without_losing_dependencies()
    {
        var mountain=TunnelConstructionTests.World();
        Assert.Equal("obstacle",SignalRailwayConstruction.Preview(mountain,P,SignalRailwayConstruction.ThreeAspect,0).Reason);
        var tunnel=TunnelConstructionTests.Add(World(),P,tunnel:true);
        Assert.Equal("signalTarget",SignalRailwayConstruction.Preview(tunnel,P,SignalRailwayConstruction.ThreeAspect,0).Reason);
        var slope=SlopeRailwayConstruction.PreviewBuild(World(),P,new(2));Assert.True(slope.Legal,slope.Reason);
        var w=SlopeRailwayConstruction.Apply(new(slope.Source),slope,new(0)).World;
        foreach(var v in w.Voxels.Where(v=>v.Traffic?.Rail!=null))Assert.Equal("signalTarget",SignalRailwayConstruction.Preview(w,v.Position,SignalRailwayConstruction.ThreeAspect,0).Reason);
    }
    [Fact]
    public void Shared_controller_reference_survives_exact_save_and_zero_booking_needs_no_journal_slot()
    {
        var w=Add(World(),RailwayHandler.Signal,4,controller:42);
        var provenance=TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0));
        var state=new MixedSessionState(new(w),provenance);var bytes=SaveCodec.Write(state);
        var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(bytes,SaveCodec.Write(restored));Assert.Equal(SignalRailwayConstruction.ControllerReference(w,P),SignalRailwayConstruction.ControllerReference(restored.World,P));
        w=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes);
        var accounts=new ConstructionAccounts().Book(new ConstructionBooking(Enumerable.Repeat(new AccountCharge(AccountGenre.RailService,0),8192)),w.Revision,new(0),"fixture");
        var removal=SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,0,true);
        var next=SignalRailwayConstruction.Apply(new(w,accounts),removal,new(0));Assert.Equal(8192,next.Accounts.Transactions.Length);
    }

}
