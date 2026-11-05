using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    [Theory]
    [InlineData(0,FleetStopAction.Proceed,0)]
    [InlineData(1,FleetStopAction.Wait,1)]
    [InlineData(int.MaxValue,FleetStopAction.Wait,int.MaxValue)]
    [InlineData(-1,FleetStopAction.Reverse,0)]
    [InlineData(-2,FleetStopAction.Reverse,0)]
    [InlineData(int.MinValue,FleetStopAction.Reverse,0)]
    [InlineData(-999,FleetStopAction.Forwarding,0)]
    [InlineData(-1000,FleetStopAction.ReverseForwarding,0)]
    public void Typed_controller_decisions_preserve_source_sentinels_and_positive_waits(int source,FleetStopAction action,int wait)
    {
        var result=FleetStopDecision.FromSourceMinutes(source);
        Assert.Equal(action,result.Action);Assert.Equal(wait,result.WaitMinutes);
        Assert.Equal(source<0&&source is not (-999 or -1000)?-1:source,result.SourceMinutes);
        Assert.Equal(result,FleetStopDecision.FromSourceMinutes(result.SourceMinutes));
        Assert.Equal(FleetStopDecision.Proceed,default);
        Assert.Throws<ArgumentOutOfRangeException>(()=>FleetStopDecision.Wait(0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>FleetStopDecision.Wait(-999));
    }

    [Fact]
    public void Simple_test_station_waits_thirty_then_departs_and_resets_before_next_harbor_through_reopen()
    {
        // No platform/boarding implementation: trusted Station adapters at two
        // literal rail positions exercise only the P5 controller/callback contract.
        var calls=new List<(long Id,long Revision,Location Harbor,Location Rail,int Count)>();
        var station=new FleetHarborReference.Station(new(23,22,2),9000,4);
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,train)=>
        {
            var p=((FleetCarState.Inside)train.Head.State).Location;
            if(p!=TimingHead&&p!=new Location(22,20,2))return FleetControllers.StopTime(world,train);
            return FleetControllers.AtHarbor(world,train,station,(w,t,call)=>
            {
                calls.Add((call.Controller.Id,call.Controller.Revision,call.Harbor.Location,call.RailLocation,call.CallCount));
                return FleetControllers.SimpleStop(w,t,call);
            });
        });
        var s=TimingSession(callback:callback);var original=s.Fleet!.Trains[0];
        s=Rebind(s,Install(s.World,original with {Revision=original.Revision+1,Forwarding=true}),callback);
        s=Rebind(s,SetLimits(s.World,new([TimingHead],[],[])),callback);
        var initialCars=s.Fleet!.Trains[0].Cars;var accounts=s.Construction.Accounts;
        AdvanceTo(s,200);
        var stopped=s.Fleet!.Trains[0];Assert.Equal(TrainOperationalState.StoppingAtStation,stopped.State);
        Assert.Equal(1,stopped.StopCallCount);Assert.True(stopped.Forwarding);Assert.Equal(0,stopped.MoveCount);
        Assert.Equal(initialCars.ToArray(),stopped.Cars.ToArray());Assert.Same(accounts,s.Construction.Accounts);
        Assert.Equal(230,Assert.Single(s.Clock.Capture().Pending).DueMinute);AssertOccupancy(s);
        var restored=RestoreCallback(s,callback);
        foreach(var minute in new[]{230,234,236})
        {
            AdvanceTo(s,minute);AdvanceTo(restored,minute);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);
        }
        var t=s.Fleet!.Trains[0];Assert.Equal(2,t.MoveCount);Assert.Equal(1,t.StopCallCount);
        Assert.False(t.Forwarding);Assert.Equal(39,t.Passengers);Assert.Null(t.BoardingOrigin);
        Assert.Equal(new FleetCarState.Inside(new(22,20,2),new(2)),t.Head.State);
        Assert.Equal(266,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        Assert.Equal(122_000,Assert.Single(s.Construction.Accounts.Transactions).Expenditure);
        Assert.Equal(new[]{0,1,1,0,0},calls.Select(c=>c.Count));
        Assert.All(calls,c=>{Assert.Equal(s.Fleet.Simple.Id,c.Id);Assert.Equal(0,c.Revision);Assert.Equal(station.Location,c.Harbor);});
        Assert.Equal(new[]{TimingHead,TimingHead,TimingHead,new(22,20,2),new(22,20,2)},calls.Select(c=>c.Rail));
    }

    [Theory]
    [InlineData(false,0)][InlineData(false,-1)][InlineData(false,-999)][InlineData(false,-1000)]
    [InlineData(true,0)][InlineData(true,-1)][InlineData(true,-999)][InlineData(true,-1000)]
    public void Typed_harbor_mutations_precede_central_actions_and_keep_captured_head_timing(bool handlerReverse,int source)
    {
        // Independently authored trace: initial head20/tail18; one handler
        // reversal plus one central reversal cancels. Low belongs to old head20.
        var harbor=new FleetHarborReference.Station(new(23,22,2),9000,0);
        var calls=new List<int>();
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,train)=>
            FleetControllers.AtHarbor(world,train,harbor,(w,t,call)=>
            {
                calls.Add(call.CallCount);
                t=t with {Passengers=40,Forwarding=true};
                if(handlerReverse)t=FleetReversal.Reverse(w,t);
                return new(t,FleetStopDecision.FromSourceMinutes(source));
            }));
        var s=TimingSession(callback:callback);
        s=Rebind(s,SetLimits(s.World,new([TimingHead],[],[])),callback);
        var original=s.Fleet!.Trains[0];s.AdvanceOneMinute();var t=s.Fleet!.Trains[0];
        var reversed=handlerReverse^(source is -1 or -1000);
        Assert.Equal(reversed,t.IsReversed);Assert.Equal(source is -999 or -1000,t.Forwarding);
        Assert.Equal(new FleetCarState.Inside(new(reversed?17:21,20,2),new(reversed?6:2)),t.Head.State);
        Assert.Equal(reversed?original.Cars.Reverse().Select(c=>c.Id):original.Cars.Select(c=>c.Id),t.Cars.Select(c=>c.Id));
        Assert.All(t.Cars,c=>Assert.Equal(2,c.Revision));Assert.Equal(0,t.StopCallCount);
        Assert.Equal(1,t.MoveCount);Assert.Equal(TrainOperationalState.Moving,t.State);Assert.Equal(new[]{0},calls);
        Assert.Equal(204,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        Assert.Equal(124_000,Assert.Single(s.Construction.Accounts.Transactions).Expenditure);AssertOccupancy(s);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(RestoreCallback(s,callback)));
    }

    [Fact]
    public void Typed_positive_wait_retains_handler_reversal_and_forwarding_until_later_departure()
    {
        var harbor=new FleetHarborReference.Station(new(23,22,2),9000,0);var counts=new List<int>();
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,train)=>
            FleetControllers.AtHarbor(world,train,harbor,(w,t,call)=>
            {
                counts.Add(call.CallCount);
                return call.CallCount==0
                    ?new(FleetReversal.Reverse(w,t with {Forwarding=true}),FleetStopDecision.Wait(7))
                    :new(t,FleetStopDecision.Proceed);
            }));
        var s=TimingSession(callback:callback);var before=s.Fleet!.Trains[0];s.AdvanceOneMinute();
        var wait=s.Fleet!.Trains[0];Assert.True(wait.Forwarding);Assert.True(wait.IsReversed);
        Assert.Equal(before.Cars.Reverse().Select(c=>c.Id),wait.Cars.Select(c=>c.Id));
        Assert.Equal(1,wait.StopCallCount);Assert.Equal(0,wait.MoveCount);
        Assert.Equal(TrainOperationalState.StoppingAtStation,wait.State);Assert.Empty(s.Construction.Accounts.Transactions);
        Assert.Equal(207,Assert.Single(s.Clock.Capture().Pending).DueMinute);AssertOccupancy(s);
        s=RestoreCallback(s,callback);AdvanceTo(s,207);var moved=s.Fleet!.Trains[0];
        Assert.Equal(new[]{0,1},counts);Assert.False(moved.Forwarding);Assert.True(moved.IsReversed);
        Assert.Equal(0,moved.StopCallCount);Assert.Equal(1,moved.MoveCount);
        Assert.Equal(new FleetCarState.Inside(new(17,20,2),new(6)),moved.Head.State);AssertOccupancy(s);
    }

    [Fact]
    public void Signal_fixture_calls_resolve_passing_train_instance_and_simple_ignores_nonstation_harbors()
    {
        using var f=Fixtures();var count=0;
        var plan=SignalRailwayConstruction.Preview(Empty(),TimingHead,SignalRailwayConstruction.ThreeAspect,0);
        Assert.True(plan.Legal,plan.Reason);
        var s=Purchase(SignalRailwayConstruction.Apply(new(plan.Source),plan,new(100)).World,1,2);
        var w=s.World;var graph=w.Fleet!;
        var provider=graph.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        var first=w.Spatial.NextEntityId;
        var controllers=new[]{new FleetController(first,0,"C1",provider.Kind,provider.Binding),
            new FleetController(first+1,0,"C2",provider.Kind,provider.Binding)};
        graph=new(graph.Profile,graph.RootGroupId,graph.Trains.Select((t,n)=>t with {ControllerId=controllers[n].Id}),
            graph.Groups,graph.Controllers.Concat(controllers));
        w=new(w.Profile,w.Terrain,new(first+2,w.Spatial.Entities),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,graph);
        foreach(var row in Cases(f,"signals"))
        {
            var calls=new List<(long Controller,int Count,Location Harbor)>();var waits=new List<int>();
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var n=0;
            foreach(var input in i.GetProperty("trains").EnumerateArray())
            {
                // Directional callback probes include diagonal arrivals from the
                // independent source fixtures; they do not claim saved poses.
                var t=graph.Trains[n++];
                t=t with {StopCallCount=Int(input,"callCount"),
                    Cars=[t.Head with {State=new FleetCarState.Inside(TimingHead,new(Int(input,"heading")))}]};
                var stop=FleetControllers.StopTime(w,t,(_,train,call)=>
                {
                    var signal=Assert.IsType<FleetHarborReference.Signal>(call.Harbor);
                    Assert.Equal(w.At(TimingHead)!.OwnerId,signal.OwnerId);Assert.Equal(TimingHead,call.RailLocation);
                    Assert.Same(graph.EffectiveController(t.Id),call.Controller);
                    calls.Add((call.Controller.Id,call.CallCount,call.Harbor.Location));
                    return new(train,FleetStopDecision.FromSourceMinutes(Int(input,"wait")));
                });
                waits.Add(stop.Decision.SourceMinutes);
                var simpleCall=new FleetHarborCall(graph.Simple,new FleetHarborReference.Signal(TimingHead,1,0),TimingHead,t.StopCallCount);
                Assert.Equal(FleetStopDecision.Proceed,FleetControllers.SimpleStop(w,t,simpleCall).Decision);
            }
            Assert.Equal(e.GetProperty("waits").EnumerateArray().Select(v=>v.GetInt32()),waits);
            var expected=e.GetProperty("calls").EnumerateArray().ToArray();Assert.Equal(expected.Length,calls.Count);
            for(var j=0;j<expected.Length;j++)
            {
                Assert.Equal(expected[j].GetProperty("controller").GetString()=="C2"?first+1:first,calls[j].Controller);
                Assert.Equal(Int(expected[j],"callCount"),calls[j].Count);Assert.Equal(TimingHead,calls[j].Harbor);
            }
            Assert.Null(w.At(TimingHead)!.Traffic!.Rail!.ControllerId);count++;
        }
        Assert.Equal(9,count);
        // Ordinary rails bypass the controller completely, including providers
        // not bound yet; they have no harbor identity or controller callback.
        var ordinary=TimingSession();var train=ordinary.Fleet!.Trains[0];
        Assert.Equal(FleetStopDecision.Proceed,FleetControllers.StopTime(ordinary.World,train,(_,_,_)=>throw new Exception()).Decision);
        Assert.Equal(FleetJunctionRoute.Straight,FleetControllers.OnJunction(train,new(ordinary.Fleet.Simple,TimingHead,23,new(2))));
    }

    [Fact]
    public void Shared_controller_revision_and_station_location_survive_repeated_callbacks_and_save_reopen()
    {
        var station=new FleetHarborReference.Station(new(23,22,2),9000,3);
        var calls=new List<(long Train,long Controller,long Revision,int Count,Location Harbor,Location Rail)>();
        // A labeled test provider consumes existing inert fixed settings. This
        // tests shared dispatch/configuration, not P5-19 TAT implementation.
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,train)=>
            FleetControllers.AtHarbor(world,train,station,(_,t,call)=>
            {
                calls.Add((t.Id,call.Controller.Id,call.Controller.Revision,call.CallCount,call.Harbor.Location,call.RailLocation));
                Assert.Same(world.Fleet!.EffectiveController(t.Id),call.Controller);
                return new(t,call.CallCount<2?FleetStopDecision.Wait(call.Controller.Harbors.Single(h=>h.Location==call.Harbor.Location).Minutes)
                    :FleetStopDecision.Proceed);
            }));
        var s=Place(Place(OperationsLine(),0,new(20,20,2)),1,new(24,20,2),true);
        var w=s.World;var f=w.Fleet!;var id=w.Spatial.NextEntityId;
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        var controller=new FleetController(id,0,"Shared test provider",provider.Kind,provider.Binding,
            [new(station.Location,FleetHarborKind.Fixed,7,false)]);
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(t=>t with {ControllerId=id,Revision=t.Revision+1}),
            f.Groups,f.Controllers.Add(controller));
        w=w.Replace(w.Revision,w.Terrain,new(id+1,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        s=s.Successor(s.Revision,new(w,s.Construction.Accounts),false);s=Rebind(s,s.World,callback);
        var accounts=s.Construction.Accounts;AdvanceTo(s,102);
        Assert.All(s.Fleet!.Trains,t=>{Assert.Equal(1,t.StopCallCount);Assert.Equal(0,t.MoveCount);Assert.Equal(TrainOperationalState.StoppingAtStation,t.State);});
        Assert.All(s.Clock.Capture().Pending,a=>Assert.Equal(109,a.DueMinute));Assert.Same(accounts,s.Construction.Accounts);
        w=s.World;f=w.Fleet!;
        var changed=controller with {Revision=1,Harbors=[new(station.Location,FleetHarborKind.Fixed,11,false)]};
        FleetGraph Configuration(FleetController c)=>new(f.Profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers.Select(a=>a.Id==id?c:a));
        Assert.Throws<ArgumentException>(()=>w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.SpeedLimits,
            Configuration(changed with {Revision=0})));
        w=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.SpeedLimits,Configuration(changed));
        s=s.Successor(s.Revision,new(w,s.Construction.Accounts),false);
        Assert.Equal(7,controller.Harbors[0].Minutes);var restored=RestoreCallback(s,callback);
        AdvanceTo(s,109);AdvanceTo(restored,109);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.All(s.Clock.Capture().Pending,a=>Assert.Equal(120,a.DueMinute));
        Assert.All(s.Fleet!.Trains,t=>Assert.Equal(2,t.StopCallCount));Assert.Same(accounts,s.Construction.Accounts);
        AdvanceTo(s,120);AdvanceTo(restored,120);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.All(s.Fleet!.Trains,t=>{Assert.Equal(0,t.StopCallCount);Assert.Equal(1,t.MoveCount);Assert.Equal(TrainOperationalState.Moving,t.State);});
        Assert.Equal(2,s.Clock.Capture().Pending.Length);AssertOccupancy(s);
        Assert.Equal(new[]{0,0,1,1,1,1,2,2,2,2},calls.Select(c=>c.Count));
        Assert.All(calls,c=>{Assert.Equal(id,c.Controller);Assert.Equal(station.Location,c.Harbor);});
        Assert.All(calls.Take(2),c=>Assert.Equal(0,c.Revision));Assert.All(calls.Skip(2),c=>Assert.Equal(1,c.Revision));
        Assert.Equal(2,calls.Select(c=>c.Train).Distinct().Count());Assert.Equal(2,calls.Select(c=>c.Rail).Distinct().Count());
    }
}
