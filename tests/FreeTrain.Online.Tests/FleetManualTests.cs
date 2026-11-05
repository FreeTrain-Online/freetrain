using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static MixedSessionState Manualize(MixedSessionState s)
    {
        var w=s.World;var f=w.Fleet!;var id=w.Spatial.NextEntityId;
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Manual);
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(t=>t with {ControllerId=id,Revision=t.Revision+1}),
            f.Groups,f.Controllers.Add(new(id,0,"Shared manual",provider.Kind,provider.Binding)));
        var world=w.Replace(w.Revision,w.Terrain,new(id+1,w.Spatial.Entities),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        return s.Successor(s.Revision,new(world,s.Construction.Accounts),s.ConstructionMode);
    }
    static readonly DateTimeOffset ManualNow=new(2026,11,5,12,0,0,TimeSpan.Zero);
    static readonly ParticipantContext ManualOwner=new("owner"),ManualOperator=new("operator",ParticipantRole.Operator,"operator-connection");
    static SessionHost ManualHost()
    {
        var s=ManualLine();
        return new(Manualize(s),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    }
    static MixedSessionState ManualLine()
    {
        var s=OperationsLine(quantity:2,signal:true);
        var p=SignalRailwayConstruction.Preview(s.World,new(26,20,2),SignalRailwayConstruction.ThreeAspect,2);
        Assert.True(p.Legal,p.Reason);s=s.Successor(s.Revision,SignalRailwayConstruction.Apply(s.Construction,p,s.Clock.Time),false);
        s=Place(s,0,new(22,20,2));return Place(s,1,new(26,20,2));
    }
    [Fact]
    public void Manual_browser_input_is_gameplay_built_purchased_and_deployed()
    {
        var s=ManualLine();OperationsBoundary(s);
        if(Environment.GetEnvironmentVariable("FREETRAIN_MANUAL_FLEET_OUTPUT") is {} output)
            File.WriteAllBytes(output,SaveCodec.Write(s));
    }
    static async Task<WireMessage> ManualSend(SessionHost h,CommandPayload payload,ParticipantContext? who=null,int ms=0,long? epoch=null)
    {
        var task=h.Enqueue(who??ManualOwner,new(Guid.NewGuid().ToString(),epoch??h.WorldEpoch,payload));
        h.Pump(0,ManualNow.AddMilliseconds(ms));return await task;
    }
    static FleetControlRead ManualLease(SessionHost h,long? id=null)=>h.Snapshot().FleetControl!.Single(l=>id==null||l.TrainId==id);
    static ControllerInputCommand ManualKeys(FleetControlRead lease,long sequence=0,bool pass=false,bool depart=false,bool reverse=false,bool left=false,bool right=false,bool reset=false)
        =>new(lease.TrainId.ToString(),sequence,pass,depart,reverse,left,right,reset,lease.Lease.LeaseId);
    static async Task ManualStep(SessionHost h,int minutes,int ms=0)
    { for(var n=0;n<minutes;n++)Assert.IsType<AcceptedMessage>(await ManualSend(h,new StepCommand(),ms:ms)); }
    static async Task<byte[]> ManualSave(SessionHost h)
    {var save=h.CaptureSave();h.Pump(0,ManualNow);return await save;}

    [Fact]
    public void Manual_source_key_tables_cover_all_32_masks_and_station_signal_calls()
    {
        var s=Manualize(TimingSession());var train=s.Fleet!.Trains[0];
        int[] initial=[5,0,5,0,5,0,5,0],later=[5,5,0,0,-1,-1,0,0];
        for(var mask=0;mask<32;mask++)foreach(var count in new[]{0,1,19})
        {
            var input=new FleetManualInput((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0);
            foreach(var harbor in new FleetHarborReference[]{new FleetHarborReference.Signal(TimingHead,1,0),new FleetHarborReference.Station(new(12,12,2),99,0)})
            {
                var result=FleetControllers.AtHarbor(s.World,train with {StopCallCount=count},harbor,input:input);
                Assert.Equal((count==0?initial:later)[mask%8],result.Decision.SourceMinutes);
                Assert.Equal(train with {StopCallCount=count},result.Train);
            }
        }
    }

    [Theory]
    [InlineData(12,5,4)][InlineData(13,5,6)][InlineData(14,6,5)][InlineData(15,6,7)]
    [InlineData(16,7,6)][InlineData(17,7,0)][InlineData(18,0,7)][InlineData(19,0,1)]
    [InlineData(20,1,0)][InlineData(21,1,2)][InlineData(22,2,1)][InlineData(23,2,3)]
    [InlineData(24,3,2)][InlineData(25,3,4)][InlineData(26,4,3)][InlineData(27,4,5)]
    public void Manual_junction_routes_keep_valid_left_then_right_and_neutral_straight(int pattern,int heading,int curve)
    {
        var s=Manualize(TimingSession());var t=s.Fleet!.Trains[0];
        var call=new FleetJunctionCall(s.Fleet.EffectiveController(t.Id),TimingHead,pattern,new(heading));
        foreach(var (left,right,expected) in new[]{(false,false,heading),(true,false,curve==(heading+7)%8?curve:heading),
            (false,true,curve==(heading+1)%8?curve:heading),(true,true,curve)})
        {
            var route=FleetControllers.OnJunction(t,call,new(false,false,false,left,right));
            Assert.Equal(new Heading(expected),FleetRailGuidance.Guide(RailwayHandler.Junction,pattern,new(heading),route));
        }
    }

    [Fact]
    public async Task Real_signal_waits_poll_depart_reverse_and_shared_manual_inputs_are_train_local()
    {
        using var h=ManualHost();var ids=h.Snapshot().Mixed!.Fleet!.Trains.Select(t=>t.Id).ToArray();
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new AcquireControlCommand(ids[0].ToString())));
        var lease=ManualLease(h);await ManualStep(h,2);var s=h.Snapshot();var train=s.Mixed!.Fleet!.Trains[0];
        Assert.Equal(102,s.Tick);Assert.Equal(1,train.StopCallCount);Assert.Equal(0,train.MoveCount);
        Assert.Equal(107,s.Mixed.Queue!.Pending.Single(a=>a.HandlerId=="train/"+ids[0]).DueMinute);
        Assert.Equal(22,train.Cars[0].State.Location!.X);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManualKeys(lease,depart:true,reverse:true)));
        await ManualStep(h,5);train=h.Snapshot().Mixed!.Fleet!.Trains[0];
        Assert.Equal(23,train.Cars[0].State.Location!.X);Assert.False(train.IsReversed);Assert.Equal(0,train.StopCallCount);
        Assert.Equal(1,train.MoveCount);
        // The other train shares the controller but never inherits departure/pass.
        Assert.Equal(h.Snapshot().Mixed!.Fleet!.Trains[0].ControllerId,h.Snapshot().Mixed!.Fleet!.Trains[1].ControllerId);
        Assert.Equal(2,h.Snapshot().Mixed!.Fleet!.Trains[1].StopCallCount);
        Assert.Equal(0,h.Snapshot().Mixed!.Fleet!.Trains[1].MoveCount);
        using var reverse=ManualHost();var id=reverse.Snapshot().Mixed!.Fleet!.Trains[0].Id;
        await ManualSend(reverse,new AcquireControlCommand(id.ToString()));await ManualStep(reverse,2);
        await ManualSend(reverse,ManualKeys(ManualLease(reverse),reverse:true));await ManualStep(reverse,5);
        train=reverse.Snapshot().Mixed!.Fleet!.Trains[0];Assert.True(train.IsReversed);Assert.Equal(21,train.Cars[0].State.Location!.X);
    }

    [Fact]
    public async Task Initial_shift_passes_only_its_train_and_global_reset_neutralizes_queued_input()
    {
        using var h=ManualHost();var id=h.Snapshot().Mixed!.Fleet!.Trains[0].Id.ToString();
        await ManualSend(h,new AcquireControlCommand(id));var lease=ManualLease(h);
        await ManualSend(h,ManualKeys(lease,pass:true,depart:true,reverse:true));await ManualStep(h,2);
        var train=h.Snapshot().Mixed!.Fleet!.Trains[0];Assert.Equal(23,train.Cars[0].State.Location!.X);
        Assert.False(train.IsReversed);Assert.Equal(1,train.MoveCount);Assert.Equal(0,train.StopCallCount);
        Assert.Equal(1,h.Snapshot().Mixed!.Fleet!.Trains[1].StopCallCount);
        using var reset=ManualHost();await ManualSend(reset,new AcquireControlCommand(id));lease=ManualLease(reset);
        var queued=reset.Enqueue(ManualOwner,new("before-global-reset",reset.WorldEpoch,ManualKeys(lease,pass:true)));
        reset.RequestInputReset(false);reset.Pump(0,ManualNow);
        Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await queued).Code);
        await ManualStep(reset,2);Assert.Equal(1,reset.Snapshot().Mixed!.Fleet!.Trains[0].StopCallCount);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Manual_input_reaches_real_junction_movement_and_followers_use_the_predecessor(bool left)
    {
        var s=OperationsLine(length:2,quantity:1);
        var branch=NormalRailwayConstruction.PreviewBuild(s.World,new(22,20,2),new(19,23,2));
        Assert.True(branch.Legal,branch.Reason);s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,branch,s.Clock.Time),false);
        s=Manualize(Place(s,0,new(23,20,2),true));var id=s.Fleet!.Trains[0].Id;
        var inputs=new Dictionary<long,FleetManualInput>{{id,new(false,false,false,left,!left)}};
        for(var n=0;n<4;n++)s.AdvanceOneMinute(inputs);
        var t=s.Fleet!.Train(id);
        Assert.Equal(new FleetCarState.Inside(new(21,left?21:20,2),new(left?5:6)),t.Head.State);
        Assert.Equal(new FleetCarState.Inside(new(22,20,2),new(6)),t.Cars[1].State);AssertOccupancy(s);
        // A successor/save has no retained keys; only supplied callback input applies.
        var restored=Restore(SaveCodec.Write(s));s.AdvanceOneMinute();restored.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
    }

    [Fact]
    public async Task Canonical_leases_enforce_roles_sequences_generations_wall_expiry_and_disconnect()
    {
        using var h=ManualHost();h.RegisterParticipant(ManualOperator);var observer=new ParticipantContext("viewer",ParticipantRole.Observer);
        h.RegisterParticipant(observer);var id=h.Snapshot().Mixed!.Fleet!.Trains[0].Id.ToString();
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await ManualSend(h,new AcquireControlCommand(id),observer)).Code);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new AcquireControlCommand(id)));
        var lease=ManualLease(h);
        Assert.Equal("controlBusy",Assert.IsType<RejectedMessage>(await ManualSend(h,new AcquireControlCommand(id),ManualOperator)).Code);
        Assert.Equal("leaseRequired",Assert.IsType<RejectedMessage>(await ManualSend(h,ManualKeys(lease,pass:true),ManualOperator)).Code);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,ManualKeys(lease,pass:true)));
        Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await ManualSend(h,ManualKeys(lease,pass:true))).Code);
        // Input expires while paused and at a slow playback rate, independently of game time.
        await ManualSend(h,new SetPlaybackRateCommand(.125));h.Pump(0,ManualNow.AddMilliseconds(501));
        await ManualStep(h,2,501);Assert.Equal(1,h.Snapshot().Mixed!.Fleet!.Trains[0].StopCallCount);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new AcquireControlCommand(id),ms:502));
        var next=ManualLease(h);Assert.NotEqual(lease.Lease.LeaseId,next.Lease.LeaseId);
        Assert.Equal("leaseRequired",Assert.IsType<RejectedMessage>(await ManualSend(h,ManualKeys(lease,1,depart:true),ms:502)).Code);
        h.Pump(0,ManualNow.AddSeconds(6));Assert.Empty(h.Snapshot().FleetControl!);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new AcquireControlCommand(id),ManualOperator,6000));
        h.DisconnectParticipant(ManualOperator);h.Pump(0,ManualNow.AddSeconds(6));Assert.Empty(h.Snapshot().FleetControl!);
    }

    [Fact]
    public async Task Reset_reconnect_replacement_and_reopen_cannot_restore_held_manual_input()
    {
        using var h=ManualHost();var id=h.Snapshot().Mixed!.Fleet!.Trains[0].Id.ToString();
        await ManualSend(h,new AcquireControlCommand(id));var lease=ManualLease(h);
        var pending=h.Enqueue(ManualOwner,new("queued-old-input",h.WorldEpoch,ManualKeys(lease,pass:true)));
        h.ResetParticipantInput(ManualOwner);h.Pump(0,ManualNow);
        Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await pending).Code);
        await ManualSend(h,ManualKeys(lease,1,pass:true,depart:true,reverse:true));
        var bytes=await ManualSave(h);var json=System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("leaseId",json);Assert.DoesNotContain("inputSequence",json);Assert.DoesNotContain("fleetControl",json);
        using var reopened=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Empty(reopened.Snapshot().FleetControl!);await ManualStep(reopened,2);
        Assert.Equal(1,reopened.Snapshot().Mixed!.Fleet!.Trains[0].StopCallCount);
        var replacement=h.LoadSave(bytes,h.WorldEpoch,ManualOwner);h.Pump(0,ManualNow);await replacement;
        Assert.Empty(h.Snapshot().FleetControl!);
        Assert.Equal("leaseRequired",Assert.IsType<RejectedMessage>(await ManualSend(h,ManualKeys(lease,2,pass:true))).Code);
        await ManualSend(h,new AcquireControlCommand(id));lease=ManualLease(h);await ManualSend(h,ManualKeys(lease,pass:true));
        var replacementOwner=ManualOwner with {ConnectionId="reconnected"};h.RegisterParticipant(replacementOwner);h.Pump(0,ManualNow);
        Assert.Empty(h.Snapshot().FleetControl!);
        for(var n=0;n<2;n++)Assert.IsType<AcceptedMessage>(await ManualSend(h,new StepCommand(),replacementOwner));
        Assert.Equal(1,h.Snapshot().Mixed!.Fleet!.Trains[0].StopCallCount);
    }

    [Fact]
    public async Task Controller_assignment_is_durable_receives_a_real_provider_and_preserves_callback()
    {
        var s=OperationsLine(quantity:1,signal:true);s=Place(s,0,new(22,20,2));
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var before=h.Snapshot();var train=before.Mixed!.Fleet!.Trains[0];
        await ManualSend(h,new AcquireControlCommand(train.Id.ToString()));var lease=ManualLease(h);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new SetControllerCommand(train.Id.ToString(),train.Revision,"manual",lease.Lease.LeaseId)));
        var configured=h.Snapshot();var c=configured.Mixed!.Fleet!.Controllers.Single(c=>c.Kind=="manual");
        Assert.Equal(RollingStockCatalog.Bundled.FleetProfile.Providers.Single(p=>p.Kind==FleetControllerKind.Manual).Binding.ContributionId,c.Provider!.ContributionId);
        Assert.Equal(before.Mixed.Queue!.Pending,configured.Mixed.Queue!.Pending);Assert.Equal(before.Cash,configured.Cash);
        var delta=StateDifference.Between(before,configured);Assert.Single(delta.FleetControl!);
        Assert.Equal(configured.FleetControl![0],delta.FleetControl![0]);
        var bytes=await ManualSave(h);using var reopen=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(c.Id,reopen.Snapshot().Mixed!.Fleet!.Trains[0].ControllerId);Assert.Empty(reopen.Snapshot().FleetControl!);
    }

    [Fact]
    public async Task Reopen_during_a_wait_does_not_replay_held_departure_or_reverse()
    {
        using var h=ManualHost();var id=h.Snapshot().Mixed!.Fleet!.Trains[0].Id.ToString();
        await ManualSend(h,new AcquireControlCommand(id));await ManualStep(h,2);
        await ManualSend(h,ManualKeys(ManualLease(h),depart:true,reverse:true));var bytes=await ManualSave(h);
        using var restored=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        await ManualStep(h,5);await ManualStep(restored,5);
        var moved=h.Snapshot().Mixed!.Fleet!.Trains[0];var waiting=restored.Snapshot().Mixed!.Fleet!.Trains[0];
        Assert.Equal(1,moved.MoveCount);Assert.Equal(0,waiting.MoveCount);Assert.Equal(2,waiting.StopCallCount);
        Assert.False(waiting.IsReversed);Assert.Equal(22,waiting.Cars[0].State.Location!.X);
        Assert.Equal(112,restored.Snapshot().Mixed!.Queue!.Pending.Single(a=>a.HandlerId=="train/"+id).DueMinute);
    }

    [Fact]
    public async Task Withdrawal_clears_lease_input_and_only_the_withdrawn_callback()
    {
        using var h=ManualHost();var id=h.Snapshot().Mixed!.Fleet!.Trains[0].Id;
        await ManualSend(h,new AcquireControlCommand(id.ToString()));var lease=ManualLease(h);
        await ManualSend(h,ManualKeys(lease,pass:true));var read=h.Snapshot();
        var query=new FleetDeploymentQuery(read.WorldEpoch,read.Revision,read.Tick,read.ContentVersion,id,"withdraw",null,false);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new DeployFleetCommand(query)));
        Assert.Empty(h.Snapshot().FleetControl!);Assert.Single(h.Snapshot().Mixed!.Queue!.Pending);
        Assert.Equal("unplaced",h.Snapshot().Mixed!.Fleet!.Trains[0].State);
        Assert.Equal("leaseRequired",Assert.IsType<RejectedMessage>(await ManualSend(h,ManualKeys(lease,1,depart:true))).Code);
        Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await ManualSend(h,ManualKeys(lease,2,reverse:true),epoch:h.WorldEpoch+1)).Code);
    }

    [Fact]
    public async Task Shared_controller_change_fences_an_old_lease_even_inside_one_FIFO_batch()
    {
        var s=Manualize(ManualLine());using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var id=s.Fleet!.Trains[0].Id;await ManualSend(h,new AcquireControlCommand(id.ToString()));var lease=ManualLease(h);
        var w=s.World;var f=w.Fleet!;
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers.Select(c=>c.Kind==FleetControllerKind.Manual
            ?c with {Name="Renamed shared manual",Revision=c.Revision+1}:c));
        var world=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        var change=h.ApplyMixed(h.WorldEpoch,h.Snapshot().Revision,new(world,s.Construction.Accounts),false,ManualOwner);
        var input=h.Enqueue(ManualOwner,new("input-after-shared-edit",h.WorldEpoch,ManualKeys(lease,pass:true)));
        h.Pump(0,ManualNow);await change;
        Assert.Equal("leaseRequired",Assert.IsType<RejectedMessage>(await input).Code);Assert.Empty(h.Snapshot().FleetControl!);
    }

    [Fact]
    public async Task Switching_to_manual_near_expiry_renews_the_existing_lease_for_a_full_duration()
    {
        var s=ManualLine();using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var train=s.Fleet!.Trains[0];await ManualSend(h,new AcquireControlCommand(train.Id.ToString()));var lease=ManualLease(h);
        Assert.IsType<AcceptedMessage>(await ManualSend(h,new SetControllerCommand(train.Id.ToString(),train.Revision,"manual",lease.Lease.LeaseId),ms:4500));
        var renewed=ManualLease(h);Assert.Equal(lease.Lease.LeaseId,renewed.Lease.LeaseId);
        Assert.Equal(ManualNow.AddMilliseconds(9500).ToUnixTimeMilliseconds(),renewed.Lease.ExpiresAt);
        h.Pump(0,ManualNow.AddMilliseconds(6000));Assert.Single(h.Snapshot().FleetControl!);
        h.Pump(0,ManualNow.AddMilliseconds(9500));Assert.Empty(h.Snapshot().FleetControl!);
    }

    static MixedSessionState ManualJunction(bool right)
    {
        var s=OperationsLine(length:2,quantity:1);
        var branch=NormalRailwayConstruction.PreviewBuild(s.World,new(22,20,2),new(19,right?17:23,2));
        Assert.True(branch.Legal,branch.Reason);
        s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,branch,s.Clock.Time),false);
        return Manualize(Place(s,0,new(23,20,2),true));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Manual_junction_motion_publishes_the_committed_exit_for_every_car(bool right)
    {
        var s=ManualJunction(right);
        if(Environment.GetEnvironmentVariable("FREETRAIN_MANUAL_JUNCTION_OUTPUT") is {} output)
            File.WriteAllBytes(output+(right?".right":""),SaveCodec.Write(s));
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var id=s.Fleet!.Trains[0].Id;
        await ManualSend(h,new AcquireControlCommand(id.ToString()));
        await ManualSend(h,ManualKeys(ManualLease(h),left:!right,right:right));
        await ManualStep(h,2);var approach=h.Snapshot();
        Assert.Equal(new PositionRead(22,20,2),approach.Mixed!.Fleet!.Trains[0].Cars[0].State.Location);
        await ManualStep(h,2);var turn=h.Snapshot();
        Assert.Equal(new PositionRead(21,right?19:21,2),turn.Mixed!.Fleet!.Trains[0].Cars[0].State.Location);
        var delta=StateDifference.Between(approach,turn);
        Assert.Equal(right?7:5,Assert.Single(delta.TrainMotion!.Steps).Cars[0].From.Outgoing);
        await ManualStep(h,2);
        foreach(var step in h.Snapshot().TrainMotion!.Steps)
        {
            Assert.Equal(2,step.Cars.Length);
            foreach(var car in step.Cars)
            {
                Assert.Equal(car.To.Heading,car.From.Outgoing);
                var (x,y)=new Heading(car.To.Heading).Offset;
                Assert.Equal(x,car.To.Position.X-car.From.Position.X);
                Assert.Equal(y,car.To.Position.Y-car.From.Position.Y);
                Assert.Contains((car.From.Outgoing-car.From.Heading+8)%8,new[]{0,1,7});
            }
        }
    }
}
