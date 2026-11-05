using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static readonly Location TimingHead=new(20,20,2);
    static SpeedLimits Limits(Location p,IEnumerable<string> limits)
        => new(limits.Contains("low")?[p]:[],limits.Contains("medium")?[p]:[],limits.Contains("fast")?[p]:[]);
    static SpeedLimits LimitsAt(MixedWorld w,Location p,params string[] limits)
    {
        var old=w.SpeedLimits;var add=Limits(p,limits);
        return new([..old.Low,..add.Low],[..old.Medium,..add.Medium],[..old.Fast,..add.Fast]);
    }
    static MixedWorld SetLimits(MixedWorld w,SpeedLimits limits)
        => w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,limits,w.Fleet);
    // Numeric boundary speeds 1/5/Int32.MaxValue are source-legal registrations,
    // separate from the bundled stock catalog and its immutable pack identity.
    static StockTrain NativeStock(int native)=>new(Stock.Binding,Stock.Text,Stock.PricePerCar,native,
        Stock.Fare,Stock.Amenity,Stock.TripRange,null,null,null,
        Stock.PermittedLengths.Select(n=>Stock.Create(n).Cars));
    static MixedWorld NativeWorld(MixedWorld w,int native)
    {
        var f=w.Fleet!;var profile=new FleetProfile(f.Profile.Packs,
            f.Profile.Trains.Select(t=>t.Binding==Stock.Binding?NativeStock(native):t),f.Profile.Providers);
        return new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,
            new FleetGraph(profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers));
    }
    static MixedSessionState TimingSession(int native=2,TrainCallback? callback=null)
    {
        var w=Empty();for(var x=17;x<=24;x++)w=Rail(w,new(x,20,2),4);
        if(native!=2)w=NativeWorld(w,native);
        var s=Purchase(w,3);var t=s.Fleet!.Trains[0];
        t=t with {Revision=1,State=TrainOperationalState.Moving,Passengers=39,
            Cars=t.Cars.Select((c,n)=>c with {Revision=1,State=new FleetCarState.Inside(new(20-n,20,2),new(2))}).ToImmutableArray()};
        return Session(Install(s.World,t),new(199,1,[new(0,0,200,MixedSessionState.TrainHandler(t.Id),0)]),new(10_000_000),callback);
    }
    static MixedSessionState Rebind(MixedSessionState s,MixedWorld w,TrainCallback? callback=null)
        =>Session(w,s.Clock.Capture(),s.Construction.Accounts,callback,Math.Max(s.Revision,w.Revision));
    static MixedSessionState RestoreCallback(MixedSessionState s,TrainCallback callback)
        =>SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve,callback).Mixed!;

    [Fact]
    public void All_32_speed_handoff_rows_plus_overlap_match_source_arithmetic_and_admission_with_live_positive_callbacks()
    {
        using var f=Fixtures();var count=0;var live=0;
        foreach(var row in Cases(f,"numericSpeed"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var native=Int(i,"native");
            var limits=Limits(TimingHead,i.GetProperty("limits").EnumerateArray().Select(v=>v.GetString()!));
            Assert.Equal(Int(e,"minutes"),FleetMovement.LimitedDelay(limits,TimingHead,native));
            if(!e.GetProperty("admitted").GetBoolean())Assert.Throws<ArgumentException>(()=>NativeStock(native));
            else
            {
                var s=TimingSession(native);s=Rebind(s,SetLimits(s.World,limits));s.AdvanceOneMinute();
                Assert.Equal(200L+Int(e,"minutes"),Assert.Single(s.Clock.Capture().Pending).DueMinute);
                Assert.Equal(1,s.Fleet!.Trains[0].MoveCount);Assert.Equal(9_878_000,s.Construction.Accounts.Cash);
                AssertOccupancy(s);live++;
            }
            count++;
        }
        Assert.Equal(33,count);Assert.Equal(25,live);
        // SCN-006 also names all-overlap for every native speed 1 through 5.
        foreach(var native in new[]{2,3,4,5})
        {
            var s=TimingSession(native);s=Rebind(s,SetLimits(s.World,Limits(TimingHead,["fast","medium","low"])));
            s.AdvanceOneMinute();Assert.Equal(204,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        }
        // Also exercise Medium-over-Fast without Low masking that branch.
        var medium=TimingSession(5);medium=Rebind(medium,SetLimits(medium.World,Limits(TimingHead,["medium","fast"])));
        medium.AdvanceOneMinute();Assert.Equal(203,Assert.Single(medium.Clock.Capture().Pending).DueMinute);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Placement_uses_native_timer_under_all_limits_then_actual_callback_uses_original_head_limit(bool reverse)
    {
        var s=BuildRing();var t=s.Fleet!.Trains[0];
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,t.Id));
        var locations=s.World.Voxels.Where(v=>v.Traffic!=null).Select(v=>v.Position).ToImmutableArray();
        s=Rebind(s,SetLimits(s.World,new(locations,locations,locations)));
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,t.Id,new(24,20,2),reverse));
        Assert.Equal(102,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,102);AdvanceTo(restored,102);
        Assert.Equal(106,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);
    }

    [Fact]
    public void All_seven_timing_handoff_rows_register_exactly_one_timer_from_original_head_or_explicit_wait()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"timing"))
        {
            var i=row.GetProperty("input");var destination=TimingHead.Move(new(2));
            TrainCallback? callback=null;
            if(i.TryGetProperty("stopMinutes",out var wait))
                callback=(minute,id)=>FleetMovement.Tick(minute,id,(_,t)=>new(t,wait.GetInt32()));
            var s=TimingSession(Int(i,"native"));var w=s.World;
            if(i.TryGetProperty("priorLimit",out var prior))w=SetLimits(w,LimitsAt(w,TimingHead,prior.GetString()!));
            if(i.TryGetProperty("nextLimit",out var nextLimit))w=SetLimits(w,LimitsAt(w,destination,nextLimit.GetString()!));
            // Outside route selection remains P5-14. Supply a controlled route
            // candidate to the SAME shift/cost/timing continuation used by Tick.
            if(i.GetProperty("beforeHead").GetString()=="outside")
            {
                var t=s.Fleet!.Trains[0];t=t with {Revision=t.Revision+1,Cars=t.Cars.Select(c=>c with {
                    Revision=c.Revision+1,State=new FleetCarState.Outside(new(0,0,2),new(2),0)}).ToImmutableArray()};
                w=Install(w,t);callback=(minute,id)=>FleetMovement.CompleteMove(minute,minute.Fleet!.Train(id),new FleetCarState.Inside(destination,new(2)));
            }
            else if(i.TryGetProperty("afterHead",out var after)&&after.GetString()=="outside")
                callback=(minute,id)=>FleetMovement.CompleteMove(minute,minute.Fleet!.Train(id),new FleetCarState.Outside(new(0,0,2),new(2),100));
            s=Rebind(s,w,callback);s.AdvanceOneMinute();
            Assert.Equal(200L+Int(row.GetProperty("expected"),"minutes"),Assert.Single(s.Clock.Capture().Pending).DueMinute);
            var train=s.Fleet!.Trains[0];AssertOccupancy(s);
            if(i.TryGetProperty("stopMinutes",out _))
            {
                Assert.Equal(TrainOperationalState.StoppingAtStation,train.State);Assert.Equal(1,train.StopCallCount);
                Assert.Equal(0,train.MoveCount);Assert.Empty(s.Construction.Accounts.Transactions);
                Assert.Equal(new FleetCarState.Inside(TimingHead,new(2)),train.Head.State);
            }
            else {Assert.Equal(1,train.MoveCount);Assert.Equal(122_000,Assert.Single(s.Construction.Accounts.Transactions).Expenditure);}
            count++;
        }
        Assert.Equal(7,count);
    }

    [Theory]
    [InlineData(false,0)][InlineData(false,-1)][InlineData(false,-999)][InlineData(false,-1000)]
    [InlineData(true,0)][InlineData(true,-1)][InlineData(true,-999)][InlineData(true,-1000)]
    public void Handler_side_and_central_reversal_keep_original_head_limit_and_post_handler_passenger_cost(bool handlerReverse,int stop)
    {
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,t)=>
            new(handlerReverse?FleetReversal.Reverse(world,t with {Passengers=40}):t with {Passengers=40},stop));
        var s=TimingSession(callback:callback);
        var w=SetLimits(s.World,new([TimingHead],[],[new(18,20,2),new(17,20,2),new(21,20,2)]));
        s=Rebind(s,w,callback);var restored=RestoreCallback(s,callback);
        s.AdvanceOneMinute();restored.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var train=s.Fleet!.Trains[0];Assert.Equal(204,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        Assert.Equal(handlerReverse^(stop is -1 or -1000),train.IsReversed);
        Assert.Equal(stop is -999 or -1000,train.Forwarding);Assert.Equal(0,train.StopCallCount);
        Assert.Equal(124_000,Assert.Single(s.Construction.Accounts.Transactions).Expenditure);AssertOccupancy(s);
    }

    [Fact]
    public void Repeated_positive_waits_preserve_handler_effects_then_depart_without_counting_or_charging_waits_through_reopen()
    {
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,t)=>t.StopCallCount<2
            ?new(t.StopCallCount==0?FleetReversal.Reverse(world,t with {Forwarding=true}):t,7):new(t,0));
        var s=TimingSession(callback:callback);s=Rebind(s,SetLimits(s.World,Limits(TimingHead,["low"])),callback);
        var original=s.Fleet!.Trains[0];var restored=RestoreCallback(s,callback);
        foreach(var (now,due,stops) in new[]{(200,207,1),(207,214,2),(214,216,0)})
        {
            AdvanceTo(s,now);AdvanceTo(restored,now);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
            var t=s.Fleet!.Trains[0];Assert.Equal(due,Assert.Single(s.Clock.Capture().Pending).DueMinute);
            Assert.Equal(stops,t.StopCallCount);Assert.True(t.IsReversed);AssertOccupancy(s);
            if(stops>0)
            {
                Assert.Equal(TrainOperationalState.StoppingAtStation,t.State);Assert.Equal(0,t.MoveCount);Assert.True(t.Forwarding);
                Assert.Empty(s.Construction.Accounts.Transactions);Assert.Equal(original.Cars.Reverse().Select(c=>c.Id),t.Cars.Select(c=>c.Id));
            }
            else {Assert.Equal(1,t.MoveCount);Assert.False(t.Forwarding);Assert.Equal(9_878_000,s.Construction.Accounts.Cash);}
            restored=RestoreCallback(restored,callback);
        }
    }

    [Fact]
    public void SCN014_actual_ring_moves_charge_1_5_9_13_17_with_exact_limit_times_and_controlled_receipt()
    {
        using var f=Fixtures();var row=Assert.Single(Cases(f,"costs"));var e=row.GetProperty("expected");
        var s=BuildRing();var t=s.Fleet!.Trains[0];var w=Install(s.World,t with {Revision=t.Revision+1,Passengers=39});
        w=SetLimits(w,new([new(24,18,2)],[new(24,17,2)],[new(24,16,2)]));
        s=Session(w,s.Clock.Capture(),new(10_000_000));
        var output=Environment.GetEnvironmentVariable("FREETRAIN_TIMING_FLEET_OUTPUT");
        if(output!=null)File.WriteAllBytes(output,SaveCodec.Write(s));
        var restored=Restore(SaveCodec.Write(s));
        int[] times=[102,106,109,111,113,115,117,119,121,123,125,127,129,131,133,135,137];
        var charged=new List<int>();
        for(var move=1;move<=17;move++)
        {
            Assert.Equal(times[move-1],Assert.Single(s.Clock.Capture().Pending).DueMinute);
            AdvanceTo(s,times[move-1]);AdvanceTo(restored,times[move-1]);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);Assert.Equal(move,s.Fleet!.Trains[0].MoveCount);
            if(s.Construction.Accounts.Transactions.Length>charged.Count)charged.Add(move);
            Assert.Equal(10_000_000-charged.Count*122_000,s.Construction.Accounts.Cash);
            if(move is 4 or 8 or 12 or 16)restored=Restore(SaveCodec.Write(restored));
        }
        Assert.Equal(e.GetProperty("chargedMoveNumbers").EnumerateArray().Select(v=>v.GetInt32()),charged);
        Assert.All(s.Construction.Accounts.Transactions,a=>{Assert.Equal(Int(e,"costPerCharge"),a.Expenditure);Assert.Equal(AccountGenre.RailService,a.Genre);});
        Assert.Equal(Int(e,"operatingExpenditure"),s.Construction.Accounts.Transactions.Sum(a=>a.Expenditure));
        Assert.Equal(new long[]{102,113,121,129,137},s.Construction.Accounts.Transactions.Select(a=>a.AbsoluteMinute));
        // Controlled accounting companion only: P6 owns fare/passenger gameplay.
        w=s.World.Replace(s.World.Revision,s.World.Terrain,s.World.Spatial.Capture(),s.World.Voxels,s.World.Slopes,s.World.SpeedLimits,s.Fleet);
        var accounts=s.Construction.Accounts.Earn(500_000,w.Revision,s.Clock.Time,"controlledReceipt");
        s=s.Successor(s.Revision,new(w,accounts),false);
        Assert.Equal(Int(e,"cashAfter"),s.Construction.Accounts.Cash);
        Assert.Equal(500_000,s.Construction.Accounts.Transactions.Sum(a=>a.Revenue));
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Restore(SaveCodec.Write(s))));
    }

    [Fact]
    public void Controlled_Outside_countdown_moves_share_cost_cadence_native_timing_and_exact_save_continuation()
    {
        // Legal saved border consist; selection/exchange/return remain P5-14.
        TrainCallback callback=(minute,id)=>{
            var train=minute.Fleet!.Train(id);var old=(FleetCarState.Outside)train.Head.State;
            return FleetMovement.CompleteMove(minute,train,old with {Countdown=old.Countdown-1});
        };
        var s=TimingSession();var t=s.Fleet!.Trains[0];
        t=t with {Revision=t.Revision+1,Forwarding=true,Cars=t.Cars.Select((c,n)=>c with {
            Revision=c.Revision+1,State=new FleetCarState.Outside(new(0,0,2),new(0),100)}).ToImmutableArray()};
        s=Rebind(s,Install(s.World,t),callback);var restored=RestoreCallback(s,callback);
        for(var move=1;move<=17;move++)
        {
            var prior=s.Fleet!.Trains[0];
            AdvanceTo(s,198+move*2);AdvanceTo(restored,198+move*2);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);
            var train=s.Fleet!.Trains[0];Assert.Equal(move,train.MoveCount);Assert.True(train.Forwarding);
            Assert.Equal(100-move,((FleetCarState.Outside)train.Head.State).Countdown);
            for(var n=1;n<train.Cars.Length;n++)Assert.Equal(prior.Cars[n-1].State,train.Cars[n].State);
            Assert.Equal(200+move*2,Assert.Single(s.Clock.Capture().Pending).DueMinute);
            Assert.Equal(10_000_000-((move+3)/4)*122_000,s.Construction.Accounts.Cash);
            if(move==8)restored=RestoreCallback(restored,callback);
        }
        Assert.Equal(5,s.Construction.Accounts.Transactions.Length);Assert.Equal(9_390_000,s.Construction.Accounts.Cash);
    }

    [Fact]
    public void Controlled_occupied_return_candidate_keeps_native_timer_and_no_move_or_charge()
    {
        var destination=TimingHead.Move(new(2));
        TrainCallback callback=(minute,id)=>FleetMovement.CompleteMove(minute,minute.Fleet!.Train(id),new FleetCarState.Inside(destination,new(2)));
        var s=TimingSession();var t=s.Fleet!.Trains[0];
        t=t with {Revision=t.Revision+1,Forwarding=true,StopCallCount=2,Cars=t.Cars.Select((c,n)=>c with {
            Revision=c.Revision+1,State=n==1?new FleetCarState.Inside(destination,new(2)):
                new FleetCarState.Outside(new(0,0,2),new(0),0)}).ToImmutableArray()};
        s=Rebind(s,SetLimits(Install(s.World,t),Limits(destination,["low"])),callback);
        var restored=RestoreCallback(s,callback);s.AdvanceOneMinute();restored.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var after=s.Fleet!.Trains[0];Assert.Equal(t.Cars.ToArray(),after.Cars.ToArray());
        Assert.Equal(TrainOperationalState.EmergencyStopping,after.State);Assert.Equal(0,after.MoveCount);
        Assert.Equal(0,after.StopCallCount);Assert.True(after.Forwarding);Assert.Empty(s.Construction.Accounts.Transactions);
        Assert.Equal(202,Assert.Single(s.Clock.Capture().Pending).DueMinute);AssertOccupancy(s);
    }

    [Theory]
    [InlineData("stops")][InlineData("late-handler")]
    public void Waiting_handler_overflow_or_failure_rolls_back_mutations_random_accounts_and_timer(string fault)
    {
        TrainCallback callback=(minute,id)=>FleetMovement.Tick(minute,id,(world,t)=>{
            minute.Random["construction"].Next(100);
            var changed=FleetReversal.Reverse(world,t with {Passengers=40,Forwarding=true});
            if(fault=="late-handler")throw new InvalidOperationException("Injected handler fault");
            return new(changed,7);
        });
        var s=TimingSession(callback:callback);var train=s.Fleet!.Trains[0];
        if(fault=="stops")s=Rebind(s,Install(s.World,train with {Revision=train.Revision+1,StopCallCount=int.MaxValue}),callback);
        var before=SaveCodec.Write(s);var world=s.World;
        if(fault=="stops")Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());
        else Assert.Throws<InvalidOperationException>(()=>s.AdvanceOneMinute());
        Assert.Same(world,s.World);Assert.Equal(before,SaveCodec.Write(s));AssertOccupancy(s);
    }
}
