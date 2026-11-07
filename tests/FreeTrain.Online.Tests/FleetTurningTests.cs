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
    public static IEnumerable<object[]> TurningCases()
    {
        using var fixtures=Fixtures();
        foreach(var row in Cases(fixtures,"turning"))
            foreach(var reversed in new[]{false,true})yield return [row.GetProperty("id").GetString()!,reversed];
    }
    // The P5-02 Outside-only poses are abstract locations. Move their external
    // references to a real border, away from all return candidates/Inside slots.
    static FleetCarState TurningPose(System.Text.Json.JsonElement pose) => Pose(pose) is FleetCarState.Outside o
        ? o with {Location=new(0,0,2)} : Pose(pose);
    static void AssertTurningPose(FleetTrain train,System.Text.Json.JsonElement poses,bool originallyReversed)
    {
        for(var n=0;n<train.Cars.Length;n++) {
            var index=Int(poses[n],"index");
            Assert.Equal(originallyReversed?train.Cars.Length-index-1:index,train.Cars[n].OriginalIndex);
            Assert.Equal(TurningPose(poses[n]),train.Cars[n].State);
        }
    }
    static FleetTrain TurningRevision(FleetTrain before,FleetTrain after) => after with {Revision=before.Revision+1,
        Cars=after.Cars.Select(c=>c.State==before.Cars.Single(b=>b.Id==c.Id).State?before.Cars.Single(b=>b.Id==c.Id):
            c with {Revision=before.Cars.Single(b=>b.Id==c.Id).Revision+1}).ToImmutableArray()};

    [Theory]
    [MemberData(nameof(TurningCases))]
    public void Turning_all_36_independent_handler_and_central_poses_preserve_ids_and_timers_in_both_original_orders(string id,bool originallyReversed)
    {
        using var fixtures=Fixtures();var row=Cases(fixtures,"turning").Single(r=>r.GetProperty("id").GetString()==id);
        var i=row.GetProperty("input");var e=row.GetProperty("expected");var w=Empty();
        foreach(var rail in i.GetProperty("rails").EnumerateArray())w=Rail(w,Point(rail.GetProperty("location")),Int(rail,"pattern"));
        var s=Purchase(w,i.GetProperty("cars").GetArrayLength());var t=s.Fleet!.Trains[0];
        var cars=originallyReversed?t.Cars.Reverse().ToImmutableArray():t.Cars;
        t=t with {Revision=1,IsReversed=originallyReversed,State=TrainOperationalState.Moving,StopCallCount=1,
            Cars=cars.Select((c,n)=>c with {Revision=1,State=TurningPose(i.GetProperty("cars")[n])}).ToImmutableArray()};
        s=Session(Install(s.World,t),new(200,6,[new(4,4,210,MixedSessionState.TrainHandler(t.Id),0)]));
        var bytes=SaveCodec.Write(s);var reopened=Restore(bytes);var queue=s.Clock.Capture();
        var result=FleetTurning.CheckTurn(s.World,t,Int(i,"requested"),time:s.Clock.Time);
        var repeat=FleetTurning.CheckTurn(reopened.World,reopened.Fleet!.Train(t.Id),Int(i,"requested"),time:reopened.Clock.Time);
        Assert.Equal(Int(e,"result"),result.Decision.SourceMinutes);
        AssertTurningPose(result.Train,e.GetProperty("cars"),originallyReversed);
        Assert.Equal(e.GetProperty("isReversed").GetBoolean()^originallyReversed,result.Train.IsReversed);
        Assert.Equal(result.Train.Cars.ToArray(),repeat.Train.Cars.ToArray());
        Assert.Equal(t.Cars.Select(c=>c.Id).Order(),result.Train.Cars.Select(c=>c.Id).Order());
        Assert.Equal(bytes,SaveCodec.Write(s));AssertClock(queue,s.Clock.Capture());
        var central=FleetReversal.Reverse(s.World,result.Train,time:s.Clock.Time);
        AssertTurningPose(central,e.GetProperty("afterCentral"),originallyReversed);
        Assert.Equal(e.GetProperty("afterCentralReversed").GetBoolean()^originallyReversed,central.IsReversed);
        foreach(var pose in new[]{result.Train,central}) {
            var saved=Session(Install(s.World,TurningRevision(t,pose)),queue);
            AssertOccupancy(saved);Assert.Equal(SaveCodec.Write(saved),SaveCodec.Write(Restore(SaveCodec.Write(saved))));
            AssertClock(queue,saved.Clock.Capture());
        }
    }

    [Theory]
    [InlineData(1,2,false)][InlineData(1,2,true)][InlineData(1,6,false)][InlineData(1,6,true)]
    [InlineData(3,2,false)][InlineData(3,2,true)][InlineData(3,6,false)][InlineData(3,6,true)]
    public void Turning_real_signal_composes_replacement_central_reverse_old_head_delay_and_one_callback(int length,int heading,bool forwarding)
    {
        var w=Empty();for(var x=10;x<=30;x++)w=Rail(w,new(x,20,2),4);
        var signal=SignalRailwayConstruction.Preview(w,TimingHead,SignalRailwayConstruction.ThreeAspect,heading);
        Assert.True(signal.Legal,signal.Reason);var s=Purchase(SignalRailwayConstruction.Apply(new(w),signal,new(100)).World,length,2);
        var t=s.Fleet!.Trains[0];var neighbor=s.Fleet.Trains[1];
        var dx=heading==2?1:-1;
        t=t with {Revision=1,State=TrainOperationalState.Moving,Cars=t.Cars.Select((c,n)=>c with {Revision=1,
            State=new FleetCarState.Inside(new(20-dx*n,20,2),new(heading))}).ToImmutableArray()};
        neighbor=neighbor with {Revision=1,State=TrainOperationalState.Moving,Cars=neighbor.Cars.Select((c,n)=>c with {Revision=1,
            State=new FleetCarState.Inside(new(27+n,20,2),new(6))}).ToImmutableArray()};
        w=Install(Install(s.World,t),neighbor);w=SetLimits(w,new([TimingHead],[],[]));
        s=Session(w,new(199,6,[new(4,4,200,MixedSessionState.TrainHandler(t.Id),0),new(5,5,210,MixedSessionState.TrainHandler(neighbor.Id),0)]));
        s=Timetableize(s,[new(TimingHead,FleetHarborKind.Advanced,0,false,[Rule(forwarding?FleetRuleAction.RForwarding2:FleetRuleAction.Reverse2)])]);
        AdvanceTo(s,200);Assert.Equal(1,s.Fleet!.Train(t.Id).StopCallCount);
        var bytes=SaveCodec.Write(s);
        if(length==3 && heading==2 && !forwarding && Environment.GetEnvironmentVariable("FREETRAIN_TURNING_FLEET_OUTPUT") is {} output)
            File.WriteAllBytes(output,bytes);
        var restored=Restore(bytes);AdvanceTo(s,210);AdvanceTo(restored,210);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);
        var after=s.Fleet!.Train(t.Id);Assert.False(after.IsReversed);Assert.Equal(forwarding,after.Forwarding);
        Assert.Equal(1,after.MoveCount);Assert.Equal(0,after.StopCallCount);
        Assert.Equal(new FleetCarState.Inside(new(20-dx*length,20,2),new(heading==2?6:2)),after.Head.State);
        Assert.Equal(t.Cars.Select(c=>c.Id),after.Cars.Select(c=>c.Id));
        var pending=s.Clock.Capture().Pending;Assert.Equal(2,pending.Length);
        Assert.Equal(214,pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(t.Id)).DueMinute);
        Assert.Equal(1,s.Fleet.Train(neighbor.Id).MoveCount);
        AdvanceTo(s,214);AdvanceTo(restored,214);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.False(s.Fleet.Train(t.Id).Forwarding);Assert.Equal(2,s.Clock.Capture().Pending.Length);
    }

    [Theory]
    [InlineData(FleetRuleAction.Reverse2)][InlineData(FleetRuleAction.RForwarding2)]
    public void Turning_actions_keep_first_call_mask_order_and_stop_barriers(FleetRuleAction action)
    {
        var s=TimingSession();var t=s.Fleet!.Trains[0];
        FleetStopResult Call(ImmutableArray<FleetTimetableRule> rules,int count)=>FleetTimetable.Stop(s.World,t,rules,count,new(203));
        Assert.Equal(10,Call([Rule(action)],0).Decision.SourceMinutes);
        Assert.Equal(0,Call([Rule(action),Rule(FleetRuleAction.Pass)],0).Decision.SourceMinutes);
        Assert.Equal(t,Call([Rule(FleetRuleAction.Stop),Rule(action)],1).Train);
        Assert.Equal(7,Call([Rule(FleetRuleAction.Stop),Rule(action)],1).Decision.SourceMinutes);
        Assert.Equal(t,Call([Rule(action,month:2)],1).Train);
        Assert.NotEqual(t.IsReversed,Call([Rule(action)],1).Train.IsReversed);
    }

    [Fact]
    public void Turning_one_diagonal_car_enables_forwarding_for_reverse2_through_the_real_callback()
    {
        var w=Empty();foreach(var p in new[]{new Location(20,20,2),new(21,19,2),new(22,18,2)})w=Rail(w,p,1);
        var s=Purchase(w,1);var t=s.Fleet!.Trains[0];
        t=t with {Revision=1,State=TrainOperationalState.Moving,StopCallCount=1,Cars=[t.Cars[0] with {Revision=1,State=new FleetCarState.Inside(new(20,20,2),new(1))}]};
        s=Timetableize(Session(Install(s.World,t),new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0)])),
            [new(TimingHead,FleetHarborKind.Advanced,0,false,[Rule(FleetRuleAction.Reverse2)])]);
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,train)=>FleetControllers.AtHarbor(world,train,
            new FleetHarborReference.Station(TimingHead,1000,0),time:minute.Clock.Time));
        var restored=RestoreCallback(s,callback);s=Rebind(s,s.World,callback);
        AdvanceTo(s,200);AdvanceTo(restored,200);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var after=s.Fleet!.Train(t.Id);Assert.True(after.Forwarding);Assert.False(after.IsReversed);
        Assert.Equal(new FleetCarState.Inside(new(21,19,2),new(1)),after.Head.State);
        Assert.Equal(1,after.MoveCount);Assert.Equal(0,after.StopCallCount);Assert.Single(s.Clock.Capture().Pending);AssertOccupancy(s);
    }

    [Fact]
    public void Turning_helper_early_returns_preserve_their_ordered_source_effects()
    {
        var s=Purchase(Empty(),1);var t=s.Fleet!.Trains[0];
        foreach(var value in new[]{0,5,-999,-2000})Assert.Equal(new FleetStopResult(t,-1),FleetTurning.CheckTurn(s.World,t,value));
        foreach(var value in new[]{-1,-1000})Assert.Equal(new FleetStopResult(t,value),FleetTurning.CheckTurn(s.World,t,value));
        // Ports 0,4,5 reach the source's THIRD-port return; the independent
        // junction fixture's 0,1,4 returns earlier at its non-opposite pair.
        var world=Rail(s.World,TimingHead,27);
        t=t with {State=TrainOperationalState.Moving,Cars=[t.Head with {State=new FleetCarState.Inside(TimingHead,new(0))}]};
        var result=FleetTurning.CheckTurn(world,t,-1);
        Assert.True(result.Train.IsReversed);Assert.Equal(-1,result.Decision.SourceMinutes);
        Assert.Equal(new FleetCarState.Inside(TimingHead,new(4)),result.Train.Head.State);
    }

    [Fact]
    public void Turning_callback_uses_replaced_occupancy_when_next_slot_was_vacated_by_the_handler()
    {
        var w=Empty();for(var x=16;x<=21;x++)w=Rail(w,new(x,20,2),4);
        var s=Purchase(w,3);var t=s.Fleet!.Trains[0];int[] positions=[16,19,20];
        // A trusted, noncontiguous but admitted pose deliberately makes source
        // re-placement vacate x19, which is the subsequent movement destination.
        t=t with {Revision=1,State=TrainOperationalState.Moving,StopCallCount=1,Cars=t.Cars.Select((c,n)=>c with {Revision=1,
            State=new FleetCarState.Inside(new(positions[n],20,2),new(6))}).ToImmutableArray()};
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,train)=>FleetTurning.CheckTurn(world,train,-1,time:minute.Clock.Time));
        s=Session(Install(s.World,t),new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0)]),callback:callback);
        var restored=RestoreCallback(s,callback);AdvanceTo(s,200);AdvanceTo(restored,200);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));var after=s.Fleet!.Train(t.Id);
        Assert.Equal(1,after.MoveCount);Assert.Equal(TrainOperationalState.Moving,after.State);
        Assert.Equal(t.Cars.Select(c=>c.Id),after.Cars.Select(c=>c.Id));
        Assert.Equal(new[]{19,18,17},after.Cars.Select(c=>((FleetCarState.Inside)c.State).Location.X));AssertOccupancy(s);
    }

    [Theory]
    [InlineData("missing")][InlineData("occupied")][InlineData("bounds")]
    public void Turning_invalid_replacement_aborts_complete_detached_callback_and_preserves_neighbor(string defect)
    {
        // The admitted graph allows separate straight segments with different
        // heights. Source re-placement omits zdiff and runs out of usable slots.
        var w=Empty();var head=defect=="bounds"?w.Terrain.Space.ToLocation(new(16,0,2)):new Location(20,20,2);
        var tail=defect=="bounds"?head with {X=head.X+1,Y=head.Y+2}:head with {X=head.X-1,Z=3};
        w=Rail(Rail(w,head,4),tail,4);
        if(defect=="occupied")w=Rail(w,tail with {Z=2},4);
        var s=Purchase(w,2,2);var t=s.Fleet!.Trains[0];
        t=t with {Revision=1,State=TrainOperationalState.Moving,StopCallCount=1,Cars=[
            t.Cars[0] with {Revision=1,State=new FleetCarState.Inside(head,new(2))},
            t.Cars[1] with {Revision=1,State=new FleetCarState.Inside(tail,new(2))}]};
        w=Install(s.World,t);
        if(defect=="occupied") {
            var other=s.Fleet.Trains[1];other=other with {Revision=1,State=TrainOperationalState.Moving,Cars=[
                other.Cars[0] with {Revision=1,State=new FleetCarState.Inside(new(19,20,2),new(2))},
                other.Cars[1] with {Revision=1,State=new FleetCarState.Outside(new(0,0,2),new(2),100)}]};w=Install(w,other);
        }
        var actions=w.Fleet!.Trains.Where(a=>a.State!=TrainOperationalState.Unplaced).Select((a,n)=>new ScheduledAction(n,n,200,MixedSessionState.TrainHandler(a.Id),0)).ToImmutableArray();
        s=Session(w,new(199,actions.Length,actions),callback:(minute,id)=>FleetMovement.Tick(minute,id,(world,train)=>FleetTurning.CheckTurn(world,train,-1,time:minute.Clock.Time)));
        var before=SaveCodec.Write(s);Assert.Throws<ArgumentException>(()=>s.AdvanceOneMinute());
        Assert.Equal(before,SaveCodec.Write(s));AssertOccupancy(s);
    }

    [Theory]
    [InlineData("reverse2")][InlineData("rforwarding2")]
    public async Task Turning_rules_use_existing_atomic_commands_copy_and_save_roundtrip(string action)
    {
        using var h=new SessionHost(Timetableize(ManualLine(),[new(new(22,20,2),FleetHarborKind.Advanced,0,false)]),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var c=ManagerController(h);var p=new PositionRead(22,20,2);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new EditFleetRuleCommand(c.Id,c.Revision,p,"harbor","add",-1,0,new(-1,-1,-1,-1,-1,action))));
        c=ManagerController(h);Assert.Equal(action,c.Harbors[0].Rules![0].Action);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManagerEdit(c,"copy")));
        Assert.Equal(action,ManagerController(h).Harbors[0].Rules![0].Action);
        var saved=await ManualSave(h);Assert.Equal(saved,SaveCodec.Write(Restore(saved)));
    }
}
