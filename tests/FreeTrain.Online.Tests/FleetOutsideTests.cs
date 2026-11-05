using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    // Translate the independent abstract fixture into an admitted visible border:
    // H15/V7/Z4. Every matrix candidate still has a real stored voxel.
    static Location OutsidePoint(JsonElement a)=>new(a[0].GetInt32()+15,a[1].GetInt32()-1,a[2].GetInt32()+2);
    static FleetCarState OutsidePose(JsonElement e)=>e.GetProperty("state").GetString()=="Outside"
        ?new FleetCarState.Outside(OutsidePoint(e.GetProperty("location")),new(Int(e,"heading")),Int(e,"countdown"))
        :new FleetCarState.Inside(OutsidePoint(e.GetProperty("location")),new(Int(e,"heading")));

    [Fact]
    public void All_67_advance_literals_run_real_callbacks_with_ordered_returns_blocking_countdown_and_exchange()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"outside").Where(r=>r.GetProperty("input").GetProperty("operation").GetString()=="advance"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var car=i.GetProperty("car");
            var heading=new Heading(Int(car,"heading"));var w=Empty();
            foreach(var index in i.GetProperty("connected").EnumerateArray())
                w=Rail(w,OutsidePoint(e.GetProperty("candidates")[index.GetInt32()]),NormalRailPatterns.Find(heading.Index,heading.Opposite.Index));
            var exchange=Int(car,"countdown")==51;
            if(exchange)
            {
                // Isolated source-legal registrations cover normal capacities
                // 0/40/160 and fare3 without changing bundled catalog identity.
                var stock=new StockTrain(Stock.Binding,Stock.Text,Stock.PricePerCar,2,Int(i,"fare"),Stock.Amenity,Stock.TripRange,
                    null,null,null,[Stock.Create(2).Cars.Select(c=>c with {NormalCapacity=Int(i,"capacity")/2,SeatedCapacity=0}).ToImmutableArray()]);
                var old=w.Fleet!;var profile=new FleetProfile(old.Profile.Packs,old.Profile.Trains.Select(t=>t.Binding==Stock.Binding?stock:t),old.Profile.Providers);
                w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,
                    new(profile,old.RootGroupId,old.Trains,old.Groups,old.Controllers));
            }
            var s=Purchase(w,2);var t=s.Fleet!.Trains[0];
            var blocked=i.GetProperty("occupied").GetArrayLength()>0;
            var follower=blocked?new FleetCarState.Inside(OutsidePoint(e.GetProperty("candidates")[0]),heading.Opposite):(FleetCarState)OutsidePose(car);
            var passengers=exchange&&Int(i,"capacity")==0?0:Int(i,"passengers");
            t=t with {Revision=1,State=TrainOperationalState.Moving,Passengers=passengers,Forwarding=i.GetProperty("forwarding").GetBoolean(),
                BoardingOrigin=i.GetProperty("boardingSource").ValueKind==JsonValueKind.Null?null:(FleetCarState.Placed)OutsidePose(i.GetProperty("boardingSource")),
                MoveCount=Int(i,"moveCount"),StopCallCount=2,
                Cars=[t.Cars[0] with {Revision=1,State=OutsidePose(car)},t.Cars[1] with {Revision=1,State=follower}]};
            // The abstract zero-capacity literal starts with39, which admission
            // correctly rejects. Its legal zero-passenger companion has identical
            // exchange/output arithmetic; other literals preserve every input.
            if(exchange&&Int(i,"capacity")==0)
                Assert.Throws<ArgumentException>(()=>Install(s.World,t with {Passengers=39}));
            s=Session(Install(s.World,t),new(199,10,[new(8,8,200,MixedSessionState.TrainHandler(t.Id),0)]),new(Int(i,"cash")));
            s.AdvanceOneMinute();var after=s.Fleet!.Train(t.Id);
            Assert.Equal(OutsidePose(e.GetProperty("car")),after.Head.State);
            Assert.Equal(Int(e,"passengers"),after.Passengers);Assert.Equal(e.GetProperty("forwarding").GetBoolean(),after.Forwarding);
            Assert.Equal(Int(e,"moveCount"),after.MoveCount);Assert.Equal(0,after.StopCallCount);
            Assert.Equal(Enum.Parse<TrainOperationalState>(e.GetProperty("state").GetString()!),after.State);
            Assert.Equal(Int(e,"cash"),s.Construction.Accounts.Cash);
            Assert.Equal(Int(e,"spend"),s.Construction.Accounts.Transactions.Sum(a=>a.Expenditure));
            Assert.Equal(Int(e,"revenue"),s.Construction.Accounts.Transactions.Sum(a=>a.Revenue));
            Assert.Equal(202,Assert.Single(s.Clock.Capture().Pending).DueMinute);
            Assert.Equal(t.Cars.Select(c=>(c.Id,c.OriginalIndex,c.Definition)),after.Cars.Select(c=>(c.Id,c.OriginalIndex,c.Definition)));
            Assert.Equal(blocked?follower:t.Head.State,after.Cars[1].State);
            Assert.Equal(e.GetProperty("boardingSource").ValueKind==JsonValueKind.Null?null:OutsidePose(e.GetProperty("boardingSource")),after.BoardingOrigin);
            AssertOccupancy(s);count++;
        }
        Assert.Equal(67,count);
    }

    static MixedSessionState BorderRun()
    {
        var state=new MixedConstructionState(Empty());
        var plan=NormalRailwayConstruction.PreviewBuild(state.World,new(34,20,2),new(39,20,2));Assert.True(plan.Legal,plan.Reason);
        state=NormalRailwayConstruction.Apply(state,plan,new(100));
        var s=Session(state.World,new(100,0,[]),state.Accounts);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,3,1));
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[0].Id,new(37,20,2)));
        var t=s.Fleet!.Trains[0];Assert.Equal(new FleetCarState.Inside(new(39,20,2),new(2)),t.Head.State);
        t=t with {Revision=t.Revision+1,Passengers=39,BoardingOrigin=new FleetCarState.Inside(new(36,16,0),new(2))};
        return Session(Install(s.World,t),s.Clock.Capture(),new(10_000_000));
    }

    [Fact]
    public void Gameplay_built_three_car_border_trip_exchanges_once_at50_returns_after0_and_reopens_at_every_boundary()
    {
        var s=BorderRun();var t=s.Fleet!.Trains[0];var original=t.Cars;var restored=Restore(SaveCodec.Write(s));
        var output=Environment.GetEnvironmentVariable("FREETRAIN_OUTSIDE_FLEET_OUTPUT");
        if(!string.IsNullOrWhiteSpace(output))File.WriteAllBytes(output,SaveCodec.Write(s));
        var expectedCash=10_000_000L;var charges=0;
        // Exit at102, countdown99 at104,50 at202,0 at302, return at304.
        for(var move=1;move<=105;move++)
        {
            var prior=s.Fleet!.Trains[0];var due=100+move*2;
            AdvanceTo(s,due);AdvanceTo(restored,due);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
            t=s.Fleet!.Trains[0];Assert.Equal(move,t.MoveCount);Assert.False(t.Forwarding);
            if(move==51)expectedCash+=39*Stock.Fare*6*2; // sqrt(4²+4²+2²)=6, before shift
            if(move%4==1){expectedCash-=(60+(move<51?39:100)/20)*2000;charges++;}
            Assert.Equal(expectedCash,s.Construction.Accounts.Cash);
            Assert.Equal(move<51?39:100,t.Passengers);
            Assert.Equal(move<51?new FleetCarState.Inside(new(36,16,0),new(2)):new FleetCarState.Outside(new(40,20,2),new(2),51),t.BoardingOrigin);
            Assert.Equal(original.Select(c=>(c.Id,c.OriginalIndex,c.Definition)),t.Cars.Select(c=>(c.Id,c.OriginalIndex,c.Definition)));
            for(var n=1;n<3;n++)Assert.Equal(prior.Cars[n-1].State,t.Cars[n].State);
            if(move<=101)Assert.Equal(new FleetCarState.Outside(new(40,20,2),new(2),101-move),t.Head.State);
            else Assert.Equal(new FleetCarState.Inside(new(141-move,20,2),new(6)),t.Head.State);
            Assert.Equal(due+2,Assert.Single(s.Clock.Capture().Pending).DueMinute);AssertOccupancy(s);
            if(new[]{1,50,51,100,101,102,104}.Contains(move))restored=Restore(SaveCodec.Write(s));
            if(!string.IsNullOrWhiteSpace(output)&&new[]{50,101}.Contains(move))File.WriteAllBytes(output+"."+move,SaveCodec.Write(s));
        }
        Assert.Equal(27,charges);Assert.Single(s.Construction.Accounts.Transactions.Where(a=>a.Revenue>0));
    }

    static MixedSessionState OutsideSession(int countdown=51,bool forwarding=false,bool connected=true)
    {
        var s=connected?BorderRun():Purchase(Empty(),3);var t=s.Fleet!.Trains[0];
        t=t with {Revision=t.Revision+1,State=TrainOperationalState.Moving,Passengers=39,Forwarding=forwarding,
            BoardingOrigin=new FleetCarState.Inside(new(36,16,0),new(2)),StopCallCount=2,
            Cars=t.Cars.Select(c=>c with {Revision=c.Revision+1,State=new FleetCarState.Outside(new(40,20,2),new(2),countdown)}).ToImmutableArray()};
        return Session(Install(s.World,t),new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0)]),new(10_000_000));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Exchange_earns_before_same_move_charge_and_preserves51_origin_or_suppresses_reload(bool forwarding)
    {
        var s=OutsideSession(forwarding:forwarding);var t=s.Fleet!.Trains[0];var restored=Restore(SaveCodec.Write(s));
        var output=Environment.GetEnvironmentVariable("FREETRAIN_OUTSIDE_FLEET_OUTPUT");
        if(!forwarding&&!string.IsNullOrWhiteSpace(output))File.WriteAllBytes(output+".charged",SaveCodec.Write(s));
        // Outside callbacks never consult an Inside rail/stop handler.
        s=Rebind(s,s.World,(minute,id)=>FleetMovement.Tick(minute,id,(_,_)=>throw new Exception("Outside called stop handler")));
        s.AdvanceOneMinute();restored.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var after=s.Fleet!.Train(t.Id);var transactions=s.Construction.Accounts.Transactions;
        Assert.Equal(2,transactions.Length);Assert.Equal(39*Stock.Fare*6*2,transactions[0].Revenue);
        Assert.Equal(0,transactions[0].Expenditure);Assert.Equal(forwarding?120_000:130_000,transactions[1].Expenditure);
        Assert.Equal(transactions[0].WorldRevision,transactions[1].WorldRevision);
        Assert.Equal(transactions[0].CashAfter-transactions[1].Expenditure,transactions[1].CashAfter);
        Assert.Equal(forwarding?0:100,after.Passengers);Assert.Equal(forwarding,after.Forwarding);
        Assert.Equal(forwarding?null:t.Head.State,after.BoardingOrigin);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Restore(SaveCodec.Write(s))));
        s=Restore(SaveCodec.Write(s)); // resume production handler for the later Inside callback
        AdvanceTo(s,302);Assert.Equal(forwarding,s.Fleet!.Train(t.Id).Forwarding);
        Assert.Equal(new FleetCarState.Inside(new(39,20,2),new(6)),s.Fleet.Train(t.Id).Head.State);
        AdvanceTo(s,304);Assert.False(s.Fleet.Train(t.Id).Forwarding); // ordinary Inside zero resets it
        Assert.Single(s.Construction.Accounts.Transactions.Where(a=>a.Revenue>0));
    }

    [Fact]
    public void Missing_return_retries10_every_eleven_callbacks_and_recovers_after_real_track_build_through_reopen()
    {
        var s=OutsideSession(0,true,false);var restored=Restore(SaveCodec.Write(s));
        for(var callback=0;callback<=22;callback++)
        {
            var due=200+callback*2;AdvanceTo(s,due);AdvanceTo(restored,due);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));var t=s.Fleet!.Trains[0];
            Assert.Equal(new FleetCarState.Outside(new(40,20,2),new(2),10-callback%11),t.Head.State);
            Assert.Equal(callback+1,t.MoveCount);Assert.True(t.Forwarding);AssertOccupancy(s);
            if(callback%11==0)restored=Restore(SaveCodec.Write(s));
        }
        var plan=NormalRailwayConstruction.PreviewBuild(s.World,new(37,20,2),new(39,20,2));Assert.True(plan.Legal,plan.Reason);
        s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,plan,s.Clock.Time),false);
        restored=Restore(SaveCodec.Write(s));AdvanceTo(s,266);AdvanceTo(restored,266);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.Equal(new FleetCarState.Inside(new(39,20,2),new(6)),s.Fleet!.Trains[0].Head.State);
        Assert.Equal(34,s.Fleet.Trains[0].MoveCount);Assert.True(s.Fleet.Trains[0].Forwarding);
        AdvanceTo(s,268);Assert.False(s.Fleet.Trains[0].Forwarding);AssertOccupancy(s);
        Assert.Empty(s.Construction.Accounts.Transactions.Where(a=>a.Revenue!=0));
    }

    [Theory]
    [InlineData("distance")][InlineData("cash")][InlineData("move")][InlineData("car")]
    public void Exchange_failure_rolls_back_passengers_fares_cars_costs_and_callback(string failure)
    {
        var s=OutsideSession();var t=s.Fleet!.Trains[0];
        if(failure!="cash")
        {
            t=t with {Revision=t.Revision+1,BoardingOrigin=failure=="distance"?new FleetCarState.Inside(new(int.MaxValue,16,0),new(2)):t.BoardingOrigin,
                MoveCount=failure=="move"?int.MaxValue:t.MoveCount,
                Cars=failure=="car"?t.Cars.Select(c=>c with{Revision=long.MaxValue}).ToImmutableArray():t.Cars};
            // Exhausted saved car revisions are legal baseline state; no edit is
            // attempted until the actual callback normalizes the moved cars.
            if(failure=="car")
            {
                var w=s.World;var f=w.Fleet!;s=Rebind(s,new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,
                    new(f.Profile,f.RootGroupId,[t],f.Groups,f.Controllers)));
            }
            else s=Rebind(s,Install(s.World,t));
        }
        else s=Session(s.World,s.Clock.Capture(),new(long.MaxValue));
        var bytes=SaveCodec.Write(s);var world=s.World;
        Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());Assert.Same(world,s.World);Assert.Equal(bytes,SaveCodec.Write(s));AssertOccupancy(s);
    }

    [Fact]
    public void All_forty_return_choices_prefer_first_connected_over_later_rails_and_ignore_wrong_ports()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"outside").Where(r=>r.GetProperty("id").GetString()!.StartsWith("return-")&&
            r.GetProperty("input").GetProperty("connected").GetArrayLength()==1))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var h=new Heading(Int(i.GetProperty("car"),"heading"));
            var selected=i.GetProperty("connected")[0].GetInt32();var w=Empty();
            for(var n=0;n<5;n++)
                w=Rail(w,OutsidePoint(e.GetProperty("candidates")[n]),n<selected
                    ?NormalRailPatterns.Find((h.Index+2)%8,(h.Index+6)%8):NormalRailPatterns.Find(h.Index,h.Opposite.Index));
            var s=Purchase(w,1);var t=s.Fleet!.Trains[0];
            t=t with {Revision=1,State=TrainOperationalState.Moving,Cars=[t.Head with {Revision=1,State=OutsidePose(i.GetProperty("car"))}]};
            s=Session(Install(s.World,t),new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0)]));
            var restored=Restore(SaveCodec.Write(s));s.AdvanceOneMinute();restored.AdvanceOneMinute();
            Assert.Equal(OutsidePose(e.GetProperty("car")),s.Fleet!.Train(t.Id).Head.State);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);count++;
        }
        Assert.Equal(40,count);
    }

    [Fact]
    public void Occupied_first_return_waits_without_trying_free_alternate_then_recovers_after_other_train_withdraws()
    {
        var first=new Location(40,19,2);var alternate=new Location(40,21,2);
        var s=Purchase(Rail(Rail(Empty(),first,4),alternate,4),1,2);var t=s.Fleet!.Trains[0];var other=s.Fleet.Trains[1];
        t=t with {Revision=1,State=TrainOperationalState.Moving,Forwarding=true,Cars=[t.Head with {Revision=1,State=new FleetCarState.Outside(new(40,20,2),new(2),0)}]};
        other=other with {Revision=1,State=TrainOperationalState.Moving,Cars=[other.Head with {Revision=1,State=new FleetCarState.Inside(first,new(6))}]};
        var w=Install(Install(s.World,t),other);w=SetLimits(w,Limits(first,["low"]));
        s=Session(w,new(199,2,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0),new(1,1,250,MixedSessionState.TrainHandler(other.Id),0)]),new(1_000_000));
        var restored=Restore(SaveCodec.Write(s));
        for(var due=200;due<=204;due+=2)
        {
            AdvanceTo(s,due);AdvanceTo(restored,due);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
            Assert.Equal(t.Head.State,s.Fleet!.Train(t.Id).Head.State);Assert.Equal(0,s.Fleet.Train(t.Id).MoveCount);
            Assert.Equal(TrainOperationalState.EmergencyStopping,s.Fleet.Train(t.Id).State);
            Assert.Null(s.World.At(alternate)!.Traffic!.Car);Assert.Empty(s.Construction.Accounts.Transactions);AssertOccupancy(s);
        }
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,other.Id));restored=Restore(SaveCodec.Write(s));
        AdvanceTo(s,206);AdvanceTo(restored,206);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.Equal(new FleetCarState.Inside(first,new(6)),s.Fleet!.Train(t.Id).Head.State);
        Assert.True(s.Fleet.Train(t.Id).Forwarding);Assert.Equal(208,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        Assert.Single(s.Construction.Accounts.Transactions);AssertOccupancy(s);
    }

    [Fact]
    public void Mixed_reversal_complements_countdown_then_moves_Outside_and_reentry_keeps_exact_car_identity()
    {
        var w=Rail(Rail(Empty(),new(39,20,2),10),new(38,19,2),10);var s=Purchase(w,2);var t=s.Fleet!.Trains[0];
        t=t with {Revision=1,State=TrainOperationalState.Moving,Forwarding=true,MoveCount=4,StopCallCount=2,
            Cars=[t.Cars[0] with {Revision=1,State=new FleetCarState.Inside(new(39,20,2),new(0))},
                  t.Cars[1] with {Revision=1,State=new FleetCarState.Outside(new(39,19,2),new(0),99)}]};
        s=Session(Install(s.World,t),new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0)]));
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewReversal(s,t.Id));var reversed=s.Fleet!.Train(t.Id);
        Assert.Equal(new FleetCarState.Outside(new(38,18,2),new(0),1),reversed.Head.State);
        var restored=Restore(SaveCodec.Write(s));
        AdvanceTo(s,200);AdvanceTo(restored,200);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.Equal(new FleetCarState.Outside(new(38,18,2),new(0),0),s.Fleet!.Train(t.Id).Head.State);
        Assert.Equal(reversed.Head.State,s.Fleet.Train(t.Id).Cars[1].State);AssertOccupancy(s);
        restored=Restore(SaveCodec.Write(s));AdvanceTo(s,202);AdvanceTo(restored,202);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        // The now-vacated former Inside head is matrix candidate3, before the
        // reversal reference at candidate4. Reentry searches the order anew.
        Assert.Equal(new FleetCarState.Inside(new(39,20,2),new(4)),s.Fleet!.Train(t.Id).Head.State);
        Assert.Equal(t.Cars.Reverse().Select(c=>(c.Id,c.OriginalIndex,c.Definition)),s.Fleet.Train(t.Id).Cars.Select(c=>(c.Id,c.OriginalIndex,c.Definition)));
        Assert.True(s.Fleet.Train(t.Id).Forwarding);AssertOccupancy(s);
    }

    [Theory]
    [InlineData(0)][InlineData(-1)][InlineData(-1000)]
    public void Inside_handler_or_missing_track_reversal_can_continue_from_Outside_head_with_native_timing(int stop)
    {
        var p=new Location(39,20,2);var s=Purchase(Rail(Rail(Empty(),p,10),new(38,19,2),10),2);var t=s.Fleet!.Trains[0];
        t=t with {Revision=1,State=TrainOperationalState.Moving,Passengers=39,Forwarding=true,MoveCount=4,StopCallCount=2,
            Cars=[t.Cars[0] with {Revision=1,State=new FleetCarState.Inside(p,new(4))},
                  t.Cars[1] with {Revision=1,State=new FleetCarState.Outside(new(39,19,2),new(0),37)}]};
        var w=SetLimits(Install(s.World,t),Limits(p,["low"]));
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(_,train)=>new(train,stop));
        s=Session(w,new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0)]),new(1_000_000),callback);
        var restored=RestoreCallback(s,callback);s.AdvanceOneMinute();restored.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));var after=s.Fleet!.Train(t.Id);
        Assert.True(after.IsReversed);Assert.Equal(stop==-1000,after.Forwarding);Assert.Equal(0,after.StopCallCount);
        Assert.Equal(new FleetCarState.Outside(new(38,18,2),new(0),62),after.Head.State);
        Assert.Equal(new FleetCarState.Outside(new(38,18,2),new(0),63),after.Cars[1].State);
        Assert.Equal(t.Cars.Reverse().Select(c=>c.Id),after.Cars.Select(c=>c.Id));Assert.Equal(5,after.MoveCount);
        Assert.Equal(918_000,s.Construction.Accounts.Cash);Assert.Equal(202,Assert.Single(s.Clock.Capture().Pending).DueMinute);AssertOccupancy(s);
    }
}
