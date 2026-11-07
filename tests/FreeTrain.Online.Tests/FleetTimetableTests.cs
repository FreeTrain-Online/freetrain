using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static MixedSessionState Timetableize(MixedSessionState s, ImmutableArray<FleetHarborSetting> harbors=default,
        ImmutableArray<FleetJunctionSetting> junctions=default)
    {
        var w=s.World;var f=w.Fleet!;var id=w.Spatial.NextEntityId;
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(t=>t with {ControllerId=id,Revision=t.Revision+1}),
            f.Groups,f.Controllers.Add(new(id,0,"Shared TAT",provider.Kind,provider.Binding,harbors,junctions)));
        return s.Successor(s.Revision,new(w.Replace(w.Revision,w.Terrain,new(id+1,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph),s.Construction.Accounts),false);
    }

    [Theory]
    [InlineData(479,1)][InlineData(480,1440)][InlineData(481,1439)]
    public void TAT_SCN008_actual_signal_daily_wait_and_reversal_continue_after_reopen(int arrival,int wait)
    {
        var w=Empty();for(var x=17;x<=24;x++)w=Rail(w,new(x,20,2),4);
        var plan=SignalRailwayConstruction.Preview(w,TimingHead,SignalRailwayConstruction.ThreeAspect,2);
        Assert.True(plan.Legal,plan.Reason);
        var s=Purchase(SignalRailwayConstruction.Apply(new(w),plan,new(100)).World,3);
        var original=s.Fleet!.Trains[0];
        var deployed=original with {Revision=1,State=TrainOperationalState.Moving,
            Cars=original.Cars.Select((c,n)=>c with {Revision=1,State=new FleetCarState.Inside(new(20-n,20,2),new(2))}).ToImmutableArray()};
        s=Session(Install(s.World,deployed),new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(deployed.Id),0)]),s.Construction.Accounts);
        s=Timetableize(s,[new(TimingHead,FleetHarborKind.Daily,480,true)]);
        var id=s.Fleet!.Trains[0].Id;var calls=new List<(long Minute,int Count)>();
        TrainCallback callback=(minute,trainId)=>FleetMovement.Tick(minute,trainId,(world,t)=>
        {
            calls.Add((minute.Clock.Time.AbsoluteMinute,t.StopCallCount));
            return FleetControllers.StopTime(world,t,time:minute.Clock.Time);
        });
        s=Session(s.World,new(arrival-1,1,[new(0,0,arrival,MixedSessionState.TrainHandler(id),0)]),s.Construction.Accounts,callback,s.Revision);
        s.AdvanceOneMinute();var t=s.Fleet!.Train(id);
        Assert.Equal(TrainOperationalState.StoppingAtStation,t.State);Assert.Equal(1,t.StopCallCount);Assert.Equal(0,t.MoveCount);
        Assert.Equal(arrival+wait,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        Assert.Null(s.World.At(TimingHead)!.Traffic!.Rail!.ControllerId);AssertOccupancy(s);
        var restored=RestoreCallback(s,callback);AdvanceTo(s,arrival+wait);AdvanceTo(restored,arrival+wait);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        t=s.Fleet!.Train(id);Assert.True(t.IsReversed);Assert.Equal(1,t.MoveCount);Assert.Equal(0,t.StopCallCount);
        Assert.Equal(new FleetCarState.Inside(new(17,20,2),new(6)),t.Head.State);
        Assert.Equal(new[]{( (long)arrival,0),((long)arrival+wait,1),((long)arrival+wait,1)},calls);AssertOccupancy(s);
    }

    [Theory]
    [InlineData(null,0,false,10,0)][InlineData("pass",0,false,0,0)]
    [InlineData("fixed",7,false,7,0)][InlineData("fixed",7,true,7,-1)]
    [InlineData("fixed",0,true,0,-1)][InlineData("daily",480,false,280,0)]
    public void TAT_basic_handlers_use_harbor_key_and_keep_station_signal_defaults_distinct(string? kind,int minutes,bool reverse,int first,int later)
    {
        // Labeled future yard adapter: a platform rail uses its host Station's
        // location, not the rail position. No passenger or P6 yard credit.
        var host=new Location(23,22,2);var s=Timetableize(TimingSession(),kind==null?[]:
            [new(host,Enum.Parse<FleetHarborKind>(kind,true),minutes,reverse)]);
        var t=s.Fleet!.Trains[0];var c=s.Fleet.EffectiveController(t.Id);
        var station=new FleetHarborReference.Station(host,9000,0);
        foreach(var count in new[]{0,1,7}) {
            var call=new FleetHarborCall(c,station,TimingHead,count);
            Assert.Equal(count==0?first:later,FleetControllers.TimetableStop(t,call,new(200)).Decision.SourceMinutes);
            Assert.Equal(count==0?30:0,FleetControllers.SimpleStop(s.World,t,call with {Controller=s.Fleet.Simple}).Decision.SourceMinutes);
            Assert.Equal(count==0?10:0,FleetControllers.TimetableStop(t,call with {Harbor=new FleetHarborReference.Signal(TimingHead,100,0)},new(200)).Decision.SourceMinutes);
            Assert.Equal(0,FleetControllers.SimpleStop(s.World,t,call with {Controller=s.Fleet.Simple,Harbor=new FleetHarborReference.Signal(host,100,0)}).Decision.SourceMinutes);
        }
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void TAT_junction_defaults_drive_real_head_and_follower_through_save(bool curve)
    {
        var s=OperationsLine(length:2,quantity:1);
        var branch=NormalRailwayConstruction.PreviewBuild(s.World,new(22,20,2),new(19,23,2));
        Assert.True(branch.Legal,branch.Reason);s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,branch,s.Clock.Time),false);
        s=Timetableize(Place(s,0,new(23,20,2),true),junctions:curve?[new(new(22,20,2),FleetJunctionRoute.Curve)]:[]);
        var restored=Restore(SaveCodec.Write(s));
        for(var n=0;n<4;n++){s.AdvanceOneMinute();restored.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));}
        var t=s.Fleet!.Trains[0];
        Assert.Equal(new FleetCarState.Inside(new(21,curve?21:20,2),new(curve?5:6)),t.Head.State);
        Assert.Equal(new FleetCarState.Inside(new(22,20,2),new(6)),t.Cars[1].State);AssertOccupancy(s);
    }

    [Fact]
    public async Task TAT_junction_manager_patches_copy_cleanup_geometry_fences_and_reopen_are_atomic()
    {
        var s=OperationsLine(length:2,quantity:1);
        var branch=NormalRailwayConstruction.PreviewBuild(s.World,new(22,20,2),new(19,23,2));
        s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,branch,s.Clock.Time),false);
        var p=new Location(22,20,2);var missing=new Location(100,100,2);
        s=Timetableize(s,[new(missing,FleetHarborKind.Fixed,17,false)],
            [new(p,FleetJunctionRoute.Straight),new(missing,FleetJunctionRoute.Curve)]);
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var c=ManagerController(h);var geometry=h.Snapshot().Mixed!.GeometryRevision;
        ManageFleetControllerCommand Edit(FleetControllerRead row,string route="curve")=>new("configure",row.Id,row.Revision,"",null,null,new(p.X,p.Y,p.Z),[new(new(p.X,p.Y,p.Z),route)],geometry);
        var before=await FleetSave(h);
        ManagerRejected(await FleetSend(h,Edit(c) with {ExpectedGeometryRevision=geometry+1}),"staleRevision");
        ManagerRejected(await FleetSend(h,Edit(c,"left")),"invalidInput");
        ManagerRejected(await FleetSend(h,Edit(c) with {Harbors=[]}),"invalidInput");
        Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,Edit(c)));c=ManagerController(h);
        Assert.Equal("curve",c.Junctions![0].DefaultRoute);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(c,"copy")));var copy=ManagerController(h);
        Assert.Equal(c.Junctions,copy.Junctions);
        var clean=new ManageFleetControllerCommand("cleanJunctions",c.Id,c.Revision,"",null,null,ExpectedGeometryRevision:h.Snapshot().Mixed!.GeometryRevision);
        var cleanResult=await FleetSend(h,clean);Assert.True(cleanResult is AcceptedMessage,System.Text.Json.JsonSerializer.Serialize(cleanResult));
        var updated=h.Snapshot().Mixed!.Fleet!.Controllers.Single(a=>a.Id==c.Id);
        Assert.Equal(p.X,Assert.Single(updated.Junctions!).Location.X);Assert.Single(updated.Harbors);
        Assert.Equal(2,ManagerController(h).Junctions!.Length);
        before=await FleetSave(h);ManagerRejected(await FleetSend(h,clean),"staleRevision");Assert.Equal(before,await FleetSave(h));
        using var reopened=new SessionHost(SaveCodec.ReadWorld(before,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(before,await FleetSave(reopened));
        if(Environment.GetEnvironmentVariable("FREETRAIN_TAT_FLEET_OUTPUT") is {} output)File.WriteAllBytes(output,before);
    }
    [Fact]
    public void TAT_labeled_yard_adapter_uses_host_station_and_real_first_later_callbacks()
    {
        var host=new FleetHarborReference.Station(new(23,22,2),9000,0);
        var calls=new List<(Location Harbor,Location Rail,int Count)>();
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,t)=>
            FleetControllers.AtHarbor(world,t,host,(_,train,call)=> {
                calls.Add((call.Harbor.Location,call.RailLocation,call.CallCount));
                return FleetControllers.TimetableStop(train,call,minute.Clock.Time);
            },time:minute.Clock.Time));
        var s=Timetableize(TimingSession(),[new(host.Location,FleetHarborKind.Fixed,17,false)]);
        s=Rebind(s,s.World,callback);AdvanceTo(s,200);
        Assert.Equal(217,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        var restored=RestoreCallback(s,callback);AdvanceTo(s,217);AdvanceTo(restored,217);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);
        Assert.Equal(new[]{0,1,1},calls.Select(c=>c.Count));
        Assert.All(calls,c=>{Assert.Equal(host.Location,c.Harbor);Assert.Equal(TimingHead,c.Rail);});
        Assert.Equal(1,s.Fleet!.Trains[0].MoveCount);
    }

    [Fact]
    public async Task TAT_bounded_junction_patch_and_graph_validation_cover_retained_missing_locations()
    {
        var settings=Enumerable.Range(0,8192).Select(n=>new FleetJunctionSetting(new(n,0,0),FleetJunctionRoute.Curve)).ToImmutableArray();
        var s=Timetableize(TimingSession(),junctions:settings);var c=s.Fleet!.Controllers.Last();
        FleetGraph Graph(FleetController replacement)=>new(s.Fleet.Profile,s.Fleet.RootGroupId,s.Fleet.Trains,s.Fleet.Groups,s.Fleet.Controllers.Replace(c,replacement));
        Assert.Throws<ArgumentException>(()=>Graph(c with {Junctions=settings.Add(settings[0])}));
        Assert.Throws<ArgumentException>(()=>Graph(c with {Junctions=[settings[0],settings[0]]}));
        Assert.Throws<ArgumentException>(()=>Graph(c with {Junctions=[settings[0] with {DefaultRoute=(FleetJunctionRoute)99}]}));
        Assert.Throws<ArgumentException>(()=>s.World.Replace(s.World.Revision,s.World.Terrain,s.World.Spatial.Capture(),s.World.Voxels,s.World.Slopes,
            s.World.SpeedLimits,Graph(c with {Junctions=settings.RemoveAt(0)})));
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var command=new ManageFleetControllerCommand("configure",c.Id,c.Revision,"",null,null,new(8191,0,0),[],s.World.Revision);
        Assert.True(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(command).Length<1024);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,command));
        var actual=ManagerController(h);Assert.Equal(8191,actual.Junctions!.Length);
        Assert.Equal(settings.Take(8191).Select(j=>j.Location.X),actual.Junctions.Select(j=>j.Location.X));
        Assert.Equal(8192,c.Junctions.Length);
        var bytes=await FleetSave(h);Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
    }

    [Theory]
    [InlineData("nullEntry")][InlineData("nullLocation")][InlineData("unknownRoute")]
    public void TAT_malformed_saved_junctions_fail_before_world_reveal(string defect)
    {
        var s=Timetableize(TimingSession(),junctions:[new(new(22,20,2),FleetJunctionRoute.Curve)]);
        var node=System.Text.Json.Nodes.JsonNode.Parse(SaveCodec.Write(s))!;
        var list=node["mixed"]!["fleet"]!["controllers"]!.AsArray().Last()!["junctions"]!.AsArray();
        if(defect=="nullEntry")list[0]=null;
        else if(defect=="nullLocation")list[0]!["location"]=null;
        else list[0]!["defaultRoute"]="left";
        Assert.Throws<InvalidDataException>(()=>SaveCodec.ReadWorld(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()),TerrainProfiles.Resolve,RailwayProfiles.Resolve));
    }

}
