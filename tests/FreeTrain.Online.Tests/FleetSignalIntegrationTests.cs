using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static readonly Location SharedSignal=new(22,20,2);
    // Gameplay construction, purchase and deployment. Three independent trains
    // pass the SAME harbor: two differently configured TAT instances and Simple.
    static MixedSessionState SignalJourney(string type)
    {
        var w=Empty(width:64,height:64);
        var track=NormalRailwayConstruction.PreviewBuild(w,new(18,20,2),new(45,20,2));
        Assert.True(track.Legal,track.Reason);
        var c=NormalRailwayConstruction.Apply(new(w),track,new(100));
        var signal=SignalRailwayConstruction.Preview(c.World,SharedSignal,type,2);
        Assert.True(signal.Legal,signal.Reason);c=SignalRailwayConstruction.Apply(c,signal,new(100));
        var s=Session(c.World,new(100,0,[]),c.Accounts);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,1,3));
        s=Place(Place(Place(s,0,SharedSignal),1,new(20,20,2)),2,new(18,20,2));
        w=s.World;var f=w.Fleet!;var id=w.Spatial.NextEntityId;
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        FleetController[] controllers=[new(id,0,"Signal ten",provider.Kind,provider.Binding,
            [new(SharedSignal,FleetHarborKind.Fixed,10,false)]),
            new(id+1,0,"Signal twenty",provider.Kind,provider.Binding,
            [new(SharedSignal,FleetHarborKind.Fixed,20,false),new(new(30,20,2),FleetHarborKind.Pass,0,false)])];
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select((t,n)=>n<2?t with {ControllerId=id+n,Revision=t.Revision+1}:t),
            f.Groups,f.Controllers.Concat(controllers));
        w=w.Replace(w.Revision,w.Terrain,new(id+2,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        return s.Successor(s.Revision,new(w,s.Construction.Accounts),false);
    }
    static void SignalTrain(MixedSessionState s,int index,int x,int moves,int calls,TrainOperationalState state)
    {
        var t=s.Fleet!.Trains[index];
        Assert.Equal(new FleetCarState.Inside(new(x,20,2),new(2)),t.Head.State);
        Assert.Equal(moves,t.MoveCount);Assert.Equal(calls,t.StopCallCount);Assert.Equal(state,t.State);
        Assert.Equal(0,t.Passengers);Assert.Null(t.BoardingOrigin);
    }

    [Theory]
    [InlineData(SignalRailwayConstruction.ThreeAspect,"three")]
    [InlineData(SignalRailwayConstruction.Shunting,"shunting")]
    public void Signal_real_shared_harbor_has_distinct_waits_simple_pass_and_exact_reopen(string type,string suffix)
    {
        var s=SignalJourney(type);OperationsBoundary(s);
        if(Environment.GetEnvironmentVariable("FREETRAIN_SIGNAL_FLEET_OUTPUT") is {} output)
            File.WriteAllBytes(output+"-"+suffix+".json",SaveCodec.Write(s));
        foreach(var minute in new[]{102,104,112,114,134,136}) {
            var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,minute);AdvanceTo(restored,minute);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));OperationsBoundary(s);
            Assert.Null(s.World.At(SharedSignal)!.Traffic!.Rail!.ControllerId);
            if(minute==102) {
                SignalTrain(s,0,22,0,1,TrainOperationalState.StoppingAtStation);
                SignalTrain(s,1,21,1,0,Moving);SignalTrain(s,2,19,1,0,Moving);
                Assert.Equal(112,s.Clock.Capture().Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(s.Fleet!.Trains[0].Id)).DueMinute);
            } else if(minute==104) {
                SignalTrain(s,1,21,1,0,Blocked);SignalTrain(s,2,20,2,0,Moving);
            } else if(minute==112) {
                SignalTrain(s,0,23,1,0,Moving);SignalTrain(s,1,22,2,0,Moving);SignalTrain(s,2,21,3,0,Moving);
            } else if(minute==114) {
                SignalTrain(s,1,22,2,1,TrainOperationalState.StoppingAtStation);SignalTrain(s,2,21,3,0,Blocked);
                Assert.Equal(134,s.Clock.Capture().Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(s.Fleet!.Trains[1].Id)).DueMinute);
            } else if(minute==134) {
                SignalTrain(s,1,23,3,0,Moving);SignalTrain(s,2,22,4,0,Moving);
            } else {
                SignalTrain(s,1,24,4,0,Moving);SignalTrain(s,2,23,5,0,Moving);
            }
        }
    }

    [Theory]
    [InlineData(SignalRailwayConstruction.ThreeAspect)]
    [InlineData(SignalRailwayConstruction.Shunting)]
    public async Task Signal_waiting_edits_removal_and_same_location_reuse_are_safe(string type)
    {
        var s=SignalJourney(type);AdvanceTo(s,102);
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var f=h.Snapshot().Mixed!.Fleet!;var c=f.Controllers.Single(c=>c.Name=="Signal ten");
        var q=new RailwayQuery(h.WorldEpoch,h.Snapshot().Mixed!.GeometryRevision,h.Snapshot().ContentVersion,
            new(22,20,2),new(22,20,2),"remove","source","signal",SignalType:type,SignalDirection:2);
        Assert.Equal("carOccupied",h.PreviewRailway(q).Reason);
        var bytes=await FleetSave(h);
        ManagerRejected(await FleetSend(h,new EditRailwayCommand(q)),"invalidPlacement");
        ManagerRejected(await FleetSend(h,ManagerEdit(c,"delete")),"controllerReferenced");Assert.Equal(bytes,await FleetSave(h));
        // Removing a setting leaves the pending callback untouched. The default
        // later-call result is Proceed, rather than another initial ten-minute wait.
        Assert.IsType<AcceptedMessage>(await FleetSend(h,ManagerEdit(c,"configure",harbors:[]) with {Location=new(22,20,2)}));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(f.Trains),System.Text.Json.JsonSerializer.Serialize(h.Snapshot().Mixed!.Fleet!.Trains));Assert.Equal(s.Clock.Capture().Pending.Select(a=>a.DueMinute),h.Snapshot().Mixed!.Queue!.Pending.Select(a=>a.DueMinute));
        await FleetStep(h,10);Assert.Equal("moving",h.Snapshot().Mixed!.Fleet!.Trains[0].State);
        // Withdraw the following trains so infrastructure removal is independent
        // of their collision retries. Location-keyed settings survive removal.
        foreach(var train in h.Snapshot().Mixed!.Fleet!.Trains.Skip(1)) {
            var read=h.Snapshot();var query=new FleetDeploymentQuery(read.WorldEpoch,read.Revision,read.Tick,read.ContentVersion,train.Id,"withdraw",null,false);
            Assert.IsType<AcceptedMessage>(await FleetSend(h,new DeployFleetCommand(query)));
        }
        q=q with {ExpectedGeometryRevision=h.Snapshot().Mixed!.GeometryRevision};
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(q)));
        var retained=h.Snapshot().Mixed!.Fleet!.Controllers.Single(c=>c.Name=="Signal twenty");Assert.Equal(2,retained.Harbors.Length);
        q=q with {ExpectedGeometryRevision=h.Snapshot().Mixed!.GeometryRevision,Action="lay"};
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(q)));
        Assert.Equal(retained.Harbors,h.Snapshot().Mixed!.Fleet!.Controllers.Single(c=>c.Id==retained.Id).Harbors);
        var readAfterBuild=h.Snapshot();var second=readAfterBuild.Mixed!.Fleet!.Trains[1];
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new DeployFleetCommand(new(readAfterBuild.WorldEpoch,readAfterBuild.Revision,
            readAfterBuild.Tick,readAfterBuild.ContentVersion,second.Id,"place",new(22,20,2),false))));
        bytes=await FleetSave(h);using var reopened=new SessionHost(Restore(bytes),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(bytes,await FleetSave(reopened));await FleetStep(h,6);await FleetStep(reopened,6);Assert.Equal(await FleetSave(h),await FleetSave(reopened));
        var reused=h.Snapshot();Assert.Equal("stoppingatstation",reused.Mixed!.Fleet!.Trains[1].State);
        Assert.Equal(1,reused.Mixed.Fleet.Trains[1].StopCallCount);
        Assert.Equal(134,reused.Mixed.Queue!.Pending.Single(a=>a.HandlerId=="train/"+second.Id).DueMinute);
    }

    [Theory]
    [InlineData(SignalRailwayConstruction.ThreeAspect)]
    [InlineData(SignalRailwayConstruction.Shunting)]
    public void Signal_dispatch_all_headings_is_separate_from_cardinal_placement(string type)
    {
        foreach(var direction in new[]{0,2,4,6}) {
            var plan=SignalRailwayConstruction.Preview(Empty(),SharedSignal,type,direction);Assert.True(plan.Legal,plan.Reason);
            var s=Purchase(SignalRailwayConstruction.Apply(new(plan.Source),plan,new(100)).World,1);
            for(var heading=0;heading<8;heading++) {
                var t=s.Fleet!.Trains[0];t=t with {Cars=[t.Head with {State=new FleetCarState.Inside(SharedSignal,new(heading))}]};
                var calls=0;var result=FleetControllers.StopTime(s.World,t,(_,train,call)=>{calls++;Assert.Equal(0,call.CallCount);return new(train,FleetStopDecision.Wait(7));});
                // Literal accepted headings: same, clockwise one, counterclockwise one.
                var accepted=heading==direction||heading==(direction+1)%8||heading==(direction+7)%8;
                Assert.Equal(accepted?1:0,calls);Assert.Equal(accepted?7:0,result.Decision.SourceMinutes);
            }
        }
        foreach(var diagonal in new[]{1,3,5,7})Assert.Equal("invalidInput",SignalRailwayConstruction.Preview(Empty(),SharedSignal,type,diagonal).Reason);
    }
}
