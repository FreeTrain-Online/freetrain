using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static void AssertClock(ClockState expected,ClockState actual)
    {
        Assert.Equal(expected.AbsoluteMinute,actual.AbsoluteMinute);Assert.Equal(expected.NextOrder,actual.NextOrder);
        Assert.Equal(expected.Pending.ToArray(),actual.Pending.ToArray());
    }
    [Fact]
    public void Twenty_four_literal_reversals_keep_original_identity_types_indices_and_exact_callback_through_save()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"reversals"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var w=Empty();
            foreach(var rail in input.GetProperty("rails").EnumerateArray())
                w=Rail(w,Point(rail.GetProperty("location")),Int(rail,"pattern"));
            var s=Purchase(w,2);var before=s.Fleet!.Trains[0];
            before=before with {Revision=1,State=TrainOperationalState.Moving,MoveCount=4,Forwarding=true,StopCallCount=3,
                Cars=before.Cars.Select((c,n)=>c with {Revision=1,State=Pose(input.GetProperty("cars")[n])}).ToImmutableArray()};
            s=Session(Install(s.World,before),new(200,4,[new(3,3,210,MixedSessionState.TrainHandler(before.Id),0)]),new(999));
            var bytes=SaveCodec.Write(s);var plan=FleetDeployment.PreviewReversal(s,before.Id);
            Assert.Equal(bytes,SaveCodec.Write(s));AssertPose(plan.Train,expected.GetProperty("cars"));
            var after=FleetDeployment.Commit(s,plan);var t=after.Fleet!.Train(before.Id);
            Assert.True(t.IsReversed);Assert.Equal(before.Cars.Reverse().Select(c=>(c.Id,c.Definition,c.OriginalIndex)),t.Cars.Select(c=>(c.Id,c.Definition,c.OriginalIndex)));
            Assert.All(t.Cars,c=>Assert.Equal(2,c.Revision));Assert.Equal(2,t.Revision);
            Assert.Equal(before.MoveCount,t.MoveCount);Assert.Equal(before.Forwarding,t.Forwarding);Assert.Equal(before.StopCallCount,t.StopCallCount);
            AssertClock(s.Clock.Capture(),after.Clock.Capture());Assert.Same(s.Construction.Accounts,after.Construction.Accounts);AssertOccupancy(after);
            var saved=SaveCodec.Write(after);Assert.Equal(saved,SaveCodec.Write(Restore(saved)));count++;
        }
        Assert.Equal(24,count); // includes literal SCN-004 reverse-2-0
    }

    [Fact]
    public void Outside_reversal_uses_all_sixteen_literal_found_missing_references_and_complemented_counters()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"outside").Where(r=>r.GetProperty("input").GetProperty("operation").GetString()=="reverse"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var w=Empty();
            foreach(var index in input.GetProperty("connected").EnumerateArray())
                w=Rail(w,Point(expected.GetProperty("candidates")[index.GetInt32()]),
                    NormalRailPatterns.Find(Int(input.GetProperty("car"),"heading"),(Int(input.GetProperty("car"),"heading")+4)%8));
            var s=Purchase(w,1);var train=s.Fleet!.Trains[0];
            train=train with {State=TrainOperationalState.Moving,Cars=[train.Head with {State=Pose(input.GetProperty("car"))}]};
            // P5-02's abstract Outside positions are intentionally interior. Test
            // the pure source visitor here; legal border/save integration below.
            var reversed=FleetReversal.Reverse(s.World,train);
            Assert.Equal(Pose(expected.GetProperty("car")),reversed.Head.State);Assert.True(reversed.IsReversed);
            Assert.Equal(train.Head.Id,reversed.Head.Id);Assert.Equal(train.Head.Definition,reversed.Head.Definition);count++;
        }
        Assert.Equal(16,count);
    }

    [Fact]
    public void Legal_mixed_inside_outside_reversal_saves_exactly_and_inside_head_continues_with_one_callback()
    {
        var w=Rail(Rail(Empty(),new(39,20,2),10),new(38,19,2),10);var s=Purchase(w,2);var train=s.Fleet!.Trains[0];
        train=train with {Revision=1,State=TrainOperationalState.Moving,Passengers=39,Forwarding=true,MoveCount=4,StopCallCount=2,
            Cars=[train.Cars[0] with {Revision=1,State=new FleetCarState.Inside(new(39,20,2),new(0))},
                  train.Cars[1] with {Revision=1,State=new FleetCarState.Outside(new(39,19,2),new(0),37)}]};
        s=Session(Install(s.World,train),new(100,1,[new(0,0,102,MixedSessionState.TrainHandler(train.Id),0)]));
        var queue=s.Clock.Capture();var after=FleetDeployment.Commit(s,FleetDeployment.PreviewReversal(s,train.Id));
        var reversed=after.Fleet!.Train(train.Id);
        Assert.Equal(new FleetCarState.Outside(new(38,18,2),new(0),63),reversed.Head.State);
        Assert.Equal(new FleetCarState.Inside(new(39,20,2),new(4)),reversed.Cars[1].State);
        AssertClock(queue,after.Clock.Capture());AssertOccupancy(after);
        var restored=Restore(SaveCodec.Write(after));Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(restored));
        foreach(var state in new[]{after,restored})
        {
            var result=FleetDeployment.Commit(state,FleetDeployment.PreviewReversal(state,train.Id));
            Assert.Equal(new FleetCarState.Inside(new(39,20,2),new(0)),result.Fleet!.Train(train.Id).Head.State);
            AssertClock(queue,result.Clock.Capture());AdvanceTo(result,102);AssertOccupancy(result);
            Assert.Single(result.Clock.Capture().Pending);Assert.Equal(5,result.Fleet.Train(train.Id).MoveCount);
            if(ReferenceEquals(state,after))s=result;else Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(result));
        }
    }

    [Theory]
    [InlineData(1)][InlineData(3)]
    public void Both_null_retries_reverse_once_without_charge_and_recover_after_track_connection_through_reopen(int length)
    {
        var w=Empty();for(var x=20;x<20+length;x++)w=Rail(w,new(x,20,2),4);
        var s=Purchase(w,length);var id=s.Fleet!.Trains[0].Id;
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,id,new(20,20,2)));
        var original=s.Fleet!.Train(id);var accounts=s.Construction.Accounts;var restored=Restore(SaveCodec.Write(s));
        for(var n=1;n<=4;n++)
        {
            AdvanceTo(s,100+n*2);AdvanceTo(restored,100+n*2);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
            var t=s.Fleet!.Train(id);Assert.Equal(TrainOperationalState.EmergencyStopping,t.State);
            Assert.Equal(n%2==1,t.IsReversed);Assert.Equal(0,t.MoveCount);Assert.Equal(accounts.Transactions,s.Construction.Accounts.Transactions);
            Assert.Equal(original.Cars.OrderBy(c=>c.Id).Select(c=>((FleetCarState.Inside)c.State).Location),
                t.Cars.OrderBy(c=>c.Id).Select(c=>((FleetCarState.Inside)c.State).Location));
            Assert.Equal(100+(n+1)*2,Assert.Single(s.Clock.Capture().Pending).DueMinute);AssertOccupancy(s);
            restored=Restore(SaveCodec.Write(s));
        }
        // Build a genuine reciprocal approach, retaining the stopped train/queue.
        var route=NormalRailwayConstruction.PreviewBuild(s.World,new(20+length,20,2),new(23+length,20,2));
        Assert.True(route.Legal,route.Reason);
        var built=NormalRailwayConstruction.Apply(s.Construction,route,s.Clock.Time);
        s=s.Successor(s.Revision,built,s.ConstructionMode);restored=Restore(SaveCodec.Write(s));
        AdvanceTo(s,110);AdvanceTo(restored,110);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var recovered=s.Fleet!.Train(id);Assert.Equal(TrainOperationalState.Moving,recovered.State);Assert.False(recovered.IsReversed);
        Assert.Equal(1,recovered.MoveCount);Assert.Equal(new FleetCarState.Inside(new(20+length,20,2),new(2)),recovered.Head.State);
        Assert.Single(s.Clock.Capture().Pending);AssertOccupancy(s);
    }

    [Fact]
    public void Reversal_of_mixed_stock_head_body_tail_preserves_each_original_binding_through_movement_and_reopen()
    {
        var w=Empty();for(var x=18;x<=26;x++)w=Rail(w,new(x,20,2),4);
        var s=Session(w,new(100,0,[]));var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.PricePerCar==50_000_000);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,3,1));var id=s.Fleet!.Trains[0].Id;
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,id,new(20,20,2)));
        var before=s.Fleet!.Train(id);Assert.Equal(3,before.Cars.Select(c=>c.Definition).Distinct().Count());
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewReversal(s,id));var restored=Restore(SaveCodec.Write(s));
        var due=Assert.Single(s.Clock.Capture().Pending).DueMinute;AdvanceTo(s,due);AdvanceTo(restored,due);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);
        var after=s.Fleet!.Train(id);Assert.True(after.IsReversed);Assert.Equal(1,after.MoveCount);
        Assert.Equal(before.Cars.Reverse().Select(c=>(c.Id,c.OriginalIndex,c.Definition)),after.Cars.Select(c=>(c.Id,c.OriginalIndex,c.Definition)));
        Assert.Equal(new FleetCarState.Inside(new(19,20,2),new(6)),after.Head.State);
    }

    [Fact]
    public void Reversal_plan_rejects_stale_clock_and_overflow_without_partial_car_or_occupancy_changes()
    {
        var s=Purchase(Rail(Rail(Empty(),new(20,20,2),4),new(21,20,2),4),2);var id=s.Fleet!.Trains[0].Id;
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,id,new(20,20,2)));
        var plan=FleetDeployment.PreviewReversal(s,id);s.AdvanceOneMinute();var bytes=SaveCodec.Write(s);
        Assert.Equal("staleRevision",Assert.Throws<InvalidOperationException>(()=>FleetDeployment.Commit(s,plan)).Message);
        Assert.Equal(bytes,SaveCodec.Write(s));var t=s.Fleet!.Train(id);
        var prior=s.World;var fleet=prior.Fleet!;
        var graph=new FleetGraph(fleet.Profile,fleet.RootGroupId,fleet.Trains.Select(a=>a.Id==id
            ?t with {Cars=t.Cars.Select(c=>c with {Revision=long.MaxValue}).ToImmutableArray()}:a),fleet.Groups,fleet.Controllers);
        var w=new MixedWorld(prior.Profile,prior.Terrain,prior.Spatial.Capture(),prior.Voxels,prior.Slopes,prior.Revision,prior.SpeedLimits,graph);
        s=Session(w,s.Clock.Capture(),s.Construction.Accounts);bytes=SaveCodec.Write(s);
        Assert.Throws<OverflowException>(()=>FleetDeployment.PreviewReversal(s,id));Assert.Equal(bytes,SaveCodec.Write(s));
    }
}
