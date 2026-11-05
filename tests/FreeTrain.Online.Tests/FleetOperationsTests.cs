using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    // Real P4 construction and purchase/deployment; expectations below are
    // handwritten source traces, never outputs of the production route guide.
    static MixedSessionState OperationsLine(int length=1,int quantity=2,bool signal=false)
    {
        var w=Empty();var plan=NormalRailwayConstruction.PreviewBuild(w,new(18,20,2),new(28,20,2));
        Assert.True(plan.Legal,plan.Reason);var construction=NormalRailwayConstruction.Apply(new(w),plan,new(100));
        if(signal)
        {
            var p=SignalRailwayConstruction.Preview(construction.World,new(22,20,2),SignalRailwayConstruction.ThreeAspect,2);
            Assert.True(p.Legal,p.Reason);construction=SignalRailwayConstruction.Apply(construction,p,new(100));
        }
        var s=Session(construction.World,new(100,0,[]),construction.Accounts);
        return FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,length,quantity));
    }
    static MixedSessionState Place(MixedSessionState s,int index,Location tail,bool reverse=false)
        =>FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[index].Id,tail,reverse));
    static void OperationsBoundary(MixedSessionState s)
    {
        AssertOccupancy(s);
        var deployed=s.Fleet!.Trains.Where(t=>t.State!=TrainOperationalState.Unplaced).Select(t=>MixedSessionState.TrainHandler(t.Id));
        Assert.Equal(deployed.Order(),s.Clock.Capture().Pending.Select(a=>a.HandlerId).Order());
        Assert.Equal(s.Clock.Capture().Pending.Length,s.Clock.Capture().Pending.Select(a=>a.Registration).Distinct().Count());
    }
    static void Trace(MixedSessionState s,long minute,(int X,int Y,int Z,int Heading,int Moves,TrainOperationalState State)[] expected)
    {
        var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,minute);AdvanceTo(restored,minute);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));OperationsBoundary(s);
        for(var n=0;n<expected.Length;n++)
        {
            var e=expected[n];var t=s.Fleet!.Trains[n];
            Assert.Equal(new FleetCarState.Inside(new(e.X,e.Y,e.Z),new(e.Heading)),t.Head.State);
            Assert.Equal(e.Moves,t.MoveCount);Assert.Equal(e.State,t.State);
        }
    }
    const TrainOperationalState Moving=TrainOperationalState.Moving,Blocked=TrainOperationalState.EmergencyStopping;

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Opposing_traffic_blocks_at_signal_without_auto_dispatch_then_operator_reversal_recovers(bool signal)
    {
        var s=OperationsLine(signal:signal);s=Place(s,0,new(20,20,2));s=Place(s,1,new(24,20,2),true);
        var accounts=s.Construction.Accounts.Transactions.Length;
        Trace(s,102,[(21,20,2,2,1,Moving),(23,20,2,6,1,Moving)]);
        // First FIFO callback enters the shared cell; the later callback sees it.
        Trace(s,104,[(22,20,2,2,2,Moving),(23,20,2,6,1,Blocked)]);
        Trace(s,106,[(22,20,2,2,2,Blocked),(23,20,2,6,1,Blocked)]);
        Trace(s,108,[(22,20,2,2,2,Blocked),(23,20,2,6,1,Blocked)]);
        Assert.Equal(accounts+2,s.Construction.Accounts.Transactions.Length);
        var clock=s.Clock.Capture();var id=s.Fleet!.Trains[0].Id;
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewReversal(s,id));
        AssertClock(clock,s.Clock.Capture());Assert.Equal(accounts+2,s.Construction.Accounts.Transactions.Length);
        Trace(s,110,[(21,20,2,6,3,Moving),(22,20,2,6,2,Moving)]);
        // The signals are locations, not reservations. Simple ignores the harbor.
        Trace(s,112,[(20,20,2,6,4,Moving),(21,20,2,6,3,Moving)]);
        Assert.Equal(accounts+2,s.Construction.Accounts.Transactions.Length);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Equal_minute_shared_track_observes_registration_order_instead_of_train_identity(bool leaderFirst)
    {
        var s=OperationsLine();s=Place(s,leaderFirst?1:0,new(leaderFirst?21:20,20,2));
        s=Place(s,leaderFirst?0:1,new(leaderFirst?20:21,20,2));
        var output=Environment.GetEnvironmentVariable("FREETRAIN_OPERATIONS_FLEET_OUTPUT");
        if(output!=null&&!leaderFirst)await File.WriteAllBytesAsync(output,SaveCodec.Write(s));
        var accountCount=s.Construction.Accounts.Transactions.Length;
        var first=s.Fleet!.Trains[leaderFirst?1:0].Id;var second=s.Fleet.Trains[leaderFirst?0:1].Id;
        Assert.Equal(new[]{MixedSessionState.TrainHandler(first),MixedSessionState.TrainHandler(second)},s.Clock.Capture().Pending.Select(a=>a.HandlerId));
        Trace(s,102,leaderFirst?[(21,20,2,2,1,Moving),(22,20,2,2,1,Moving)]:[(20,20,2,2,0,Blocked),(22,20,2,2,1,Moving)]);
        Assert.Equal(accountCount+(leaderFirst?2:1),s.Construction.Accounts.Transactions.Length);
        Assert.Equal(new[]{2L,3L},s.Clock.Capture().Pending.Select(a=>a.Order));
        Trace(s,104,leaderFirst?[(22,20,2,2,2,Moving),(23,20,2,2,2,Moving)]:[(21,20,2,2,1,Moving),(23,20,2,2,2,Moving)]);
        Assert.Equal(accountCount+2,s.Construction.Accounts.Transactions.Length);
        Assert.Equal(new[]{MixedSessionState.TrainHandler(first),MixedSessionState.TrainHandler(second)},s.Clock.Capture().Pending.Select(a=>a.HandlerId));
        Assert.Equal(new[]{4L,5L},s.Clock.Capture().Pending.Select(a=>a.Order));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Converging_junction_traffic_keeps_simple_straight_and_forced_branch_routes_in_both_FIFO_orders(bool branchFirst)
    {
        var s=OperationsLine();var w=s.World;
        var branch=NormalRailwayConstruction.PreviewBuild(w,new(22,20,2),new(19,23,2));
        Assert.True(branch.Legal,branch.Reason);var c=NormalRailwayConstruction.Apply(s.Construction,branch,s.Clock.Time);
        s=s.Successor(s.Revision,c,false);Assert.Equal(14,s.World.At(new(22,20,2))!.Traffic!.Rail!.Pattern);
        s=Place(s,branchFirst?1:0,branchFirst?new(21,21,2):new(21,20,2));
        s=Place(s,branchFirst?0:1,branchFirst?new(21,20,2):new(21,21,2));
        var accounts=s.Construction.Accounts.Transactions.Length;
        Trace(s,102,branchFirst?[(21,20,2,2,0,Blocked),(22,20,2,1,1,Moving)]:[(22,20,2,2,1,Moving),(21,21,2,1,0,Blocked)]);
        // The occupant exits before the other train retries, so both move.
        Trace(s,104,branchFirst?[(22,20,2,2,1,Moving),(23,20,2,2,2,Moving)]:[(23,20,2,2,2,Moving),(22,20,2,1,1,Moving)]);
        Trace(s,106,branchFirst?[(23,20,2,2,2,Moving),(24,20,2,2,3,Moving)]:[(24,20,2,2,3,Moving),(23,20,2,2,2,Moving)]);
        Assert.Equal(accounts+2,s.Construction.Accounts.Transactions.Length);
    }

    [Fact]
    public void Closed_ring_own_tail_blocks_while_independent_neighbor_moves_and_withdrawal_releases_exactly_one_timer()
    {
        var s=BuildRing();
        // Fill all 32 distinct ring voxels with a source-legal 32-car formation.
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,s.Fleet!.Trains[0].Id));
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,32,1));
        s=Place(s,1,new(24,20,2));
        var full=s.Fleet!.Trains[1];var initial=full.Cars;var accounts=s.Construction.Accounts.Transactions.Length;
        // Neighbor Outside countdown is independent of the occupied ring.
        var t=s.Fleet.Trains[0];t=t with{Revision=t.Revision+1,State=Moving,Cars=t.Cars.Select(c=>c with{Revision=c.Revision+1,
            State=new FleetCarState.Outside(new(0,0,2),new(0),100)}).ToImmutableArray()};
        var clock=s.Clock.Capture();var order=clock.NextOrder;
        s=Session(Install(s.World,t),clock with {NextOrder=order+1,Pending=clock.Pending.Add(new(order,order,102,MixedSessionState.TrainHandler(t.Id),0))},s.Construction.Accounts);
        for(var n=1;n<=4;n++)
        {
            var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,100+2*n);AdvanceTo(restored,100+2*n);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));OperationsBoundary(s);
            Assert.Equal(initial.ToArray(),s.Fleet!.Train(full.Id).Cars.ToArray());Assert.Equal(0,s.Fleet.Train(full.Id).MoveCount);
            Assert.Equal(Blocked,s.Fleet.Train(full.Id).State);Assert.False(s.Fleet.Train(full.Id).IsReversed);
            Assert.Equal(100-n,((FleetCarState.Outside)s.Fleet.Train(t.Id).Head.State).Countdown);
        }
        Assert.Equal(accounts+1,s.Construction.Accounts.Transactions.Length);
        var timer=s.Clock.Capture().Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(t.Id));
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,full.Id));
        Assert.Equal(timer,Assert.Single(s.Clock.Capture().Pending));OperationsBoundary(s);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Equal_minute_return_waits_or_enters_when_blocking_train_vacates_first_connected_candidate(bool returningFirst)
    {
        var first=new Location(40,19,2);var alternate=new Location(40,21,2);
        var w=Rail(Rail(Rail(Empty(),first,4),new(39,19,2),4),alternate,4);
        var s=Purchase(w,1,2);var a=s.Fleet!.Trains[0];var b=s.Fleet.Trains[1];
        a=a with{Revision=1,State=Moving,Forwarding=true,Cars=[a.Head with{Revision=1,State=new FleetCarState.Outside(new(40,20,2),new(2),0)}]};
        b=b with{Revision=1,State=Moving,Cars=[b.Head with{Revision=1,State=new FleetCarState.Inside(first,new(6))}]};
        s=Session(Install(Install(s.World,a),b),new(199,2,[
            new(0,0,200,MixedSessionState.TrainHandler(returningFirst?a.Id:b.Id),0),
            new(1,1,200,MixedSessionState.TrainHandler(returningFirst?b.Id:a.Id),0)]),new(1_000_000));
        if(returningFirst&&Environment.GetEnvironmentVariable("FREETRAIN_OPERATIONS_FLEET_OUTPUT") is {} output)
        {
            var delayed=Session(s.World,s.Clock.Capture() with{Pending=s.Clock.Capture().Pending.Select(q=>q.HandlerId==MixedSessionState.TrainHandler(b.Id)?q with{DueMinute=250}:q).ToImmutableArray()},s.Construction.Accounts);
            AdvanceTo(delayed,200);OperationsBoundary(delayed);
            Assert.Equal(Blocked,delayed.Fleet!.Train(a.Id).State);Assert.Equal(0,delayed.Fleet.Train(a.Id).MoveCount);
            Assert.Empty(delayed.Construction.Accounts.Transactions);
            File.WriteAllBytes(output+".return",SaveCodec.Write(delayed));
        }
        var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,200);AdvanceTo(restored,200);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));OperationsBoundary(s);
        Assert.Null(s.World.At(alternate)!.Traffic!.Car);
        Assert.Equal(returningFirst?0:1,s.Fleet!.Train(a.Id).MoveCount);
        Assert.Equal(returningFirst?Blocked:Moving,s.Fleet.Train(a.Id).State);
        Assert.Equal(returningFirst?1:2,s.Construction.Accounts.Transactions.Length);
        if(returningFirst)Assert.Equal(a.Head.State,s.Fleet.Train(a.Id).Head.State);
        else Assert.Equal(new FleetCarState.Inside(first,new(6)),s.Fleet.Train(a.Id).Head.State);
        Assert.True(s.Fleet.Train(a.Id).Forwarding);
    }

    [Fact]
    public void Opposing_ramp_traffic_blocks_across_height_transition_then_withdrawal_and_dead_end_continue_in_order()
    {
        var w=Empty(width:64,height:64);var ramp=SlopeRailwayConstruction.PreviewBuild(w,new(24,20,2),new(2));
        Assert.True(ramp.Legal,ramp.Reason);var c=SlopeRailwayConstruction.Apply(new(w),ramp,new(100));
        foreach(var (from,to) in new[]{(new Location(22,20,2),new Location(24,20,2)),(new Location(27,20,3),new Location(29,20,3))})
        {
            var p=NormalRailwayConstruction.PreviewBuild(c.World,from,to);Assert.True(p.Legal,p.Reason);
            c=NormalRailwayConstruction.Apply(c,p,new(100));
        }
        var s=Session(c.World,new(100,0,[]),c.Accounts);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,1,2));
        s=Place(s,0,new(24,20,2));s=Place(s,1,new(27,20,3),true);
        var accounts=s.Construction.Accounts.Transactions.Length;
        Trace(s,102,[(25,20,2,2,1,Moving),(26,20,3,6,1,Moving)]);
        Trace(s,104,[(25,20,2,2,1,Blocked),(26,20,3,6,1,Blocked)]);
        Trace(s,106,[(25,20,2,2,1,Blocked),(26,20,3,6,1,Blocked)]);
        Assert.Equal(accounts+2,s.Construction.Accounts.Transactions.Length);
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,s.Fleet!.Trains[1].Id));
        for(var move=2;move<=6;move++)
        {
            var due=104+move*2;var restored=Restore(SaveCodec.Write(s));AdvanceTo(s,due);AdvanceTo(restored,due);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));OperationsBoundary(s);
            var t=s.Fleet!.Trains[0];Assert.Equal(move,t.MoveCount);Assert.Equal(Moving,t.State);
            Assert.Equal(new FleetCarState.Inside(new(move<=5?24+move:28,20,3),new(move<=5?2:6)),t.Head.State);
        }
        Assert.True(s.Fleet!.Trains[0].IsReversed);Assert.Equal(accounts+3,s.Construction.Accounts.Transactions.Length);
    }

    [Fact]
    public void Equal_minute_multi_train_callbacks_consume_only_their_own_world_random_bank_through_restore()
    {
        var bytes=SaveCodec.Write(BuildRing(quantity:2));
        var calls=new List<(long Id,int Draw)>();
        TrainCallbackResult Dispatch(MixedSessionState minute,long id)
        {
            calls.Add((id,minute.Random["construction"].Next(1000000)));
            return FleetMovement.Tick(minute,id);
        }
        MixedSessionState Load(byte[] data)=>SaveCodec.ReadWorld(data,FreeTrain.Online.Content.TerrainProfiles.Resolve,
            FreeTrain.Online.Content.RailwayProfiles.Resolve,Dispatch).Mixed!;
        var a=Load(bytes);var b=Load(bytes);AdvanceTo(a,116);var aCalls=calls.ToArray();calls.Clear();
        Assert.Equal(bytes,SaveCodec.Write(b));Assert.Equal(16,aCalls.Length);
        var ids=a.Fleet!.Trains.Select(t=>t.Id).ToArray();Assert.Equal(Enumerable.Range(0,8).SelectMany(_=>ids),aCalls.Select(c=>c.Id));
        AdvanceTo(b,108);b=Load(SaveCodec.Write(b));AdvanceTo(b,116);
        Assert.Equal(aCalls,calls.ToArray());Assert.Equal(SaveCodec.Write(a),SaveCodec.Write(b));OperationsBoundary(a);OperationsBoundary(b);
        Assert.NotEqual(JsonSerializer.Serialize(Restore(bytes).Random.Capture()),JsonSerializer.Serialize(a.Random.Capture()));
    }

    [Fact]
    public async Task Two_host_pause_step_resume_preserves_multi_train_FIFO_and_isolates_clock_random_and_accounts()
    {
        var source=BuildRing(quantity:2);var bytes=SaveCodec.Write(source);
        using var a=SessionComposition.CreateSavedHost(bytes);using var b=SessionComposition.CreateSavedHost(bytes);
        async Task Send(FreeTrain.Online.Session.SessionHost host,CommandPayload payload)
        {
            var pending=host.Enqueue(new("operator-1"),new(Guid.NewGuid().ToString(),host.WorldEpoch,payload));host.Pump(0);
            Assert.IsType<AcceptedMessage>(await pending);
        }
        async Task<byte[]> Save(FreeTrain.Online.Session.SessionHost host){var pending=host.CaptureSave();host.Pump(0);return await pending;}
        var untouched=await Save(b);a.Pump(5);Assert.Equal(bytes,await Save(a));
        await Send(a,new StepCommand());Assert.Equal(101,a.Snapshot().Tick);Assert.True(a.Snapshot().Paused);
        await Send(a,new RunCommand());a.Pump(15);await Send(a,new PauseCommand());var stopped=await Save(a);
        a.Pump(5);Assert.Equal(stopped,await Save(a));Assert.Equal(untouched,await Save(b));
        // The second world's same command sequence reproduces the exact save.
        await Send(b,new StepCommand());await Send(b,new RunCommand());b.Pump(15);await Send(b,new PauseCommand());
        Assert.Equal(stopped,await Save(b));
        AdvanceTo(source,116);var read=a.Snapshot();Assert.All(read.Mixed!.Fleet!.Trains,t=>Assert.Equal(8,t.MoveCount));
        var final=Restore(stopped);OperationsBoundary(final);
        Assert.Equal(JsonSerializer.Serialize(source.Random.Capture()),JsonSerializer.Serialize(final.Random.Capture()));
        AssertClock(source.Clock.Capture(),final.Clock.Capture());
        Assert.Equal(JsonSerializer.Serialize(source.Construction.Accounts),JsonSerializer.Serialize(final.Construction.Accounts));
    }
}
