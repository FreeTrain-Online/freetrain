using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class CityPassengerFeedbackTests
{
    static MixedSessionState Open(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static MixedSessionState Rebind(MixedSessionState s,TrainCallback? train=null,StationCallback? station=null)=>
        new(s.Construction,s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision,trainCallback:train,stationCallback:station);
    static float Q(MixedSessionState s,Location at)
    {
        var p=s.World.Terrain.Space.ToStorage(at);var heat=s.World.City!.State.Heat;
        return heat.Q[heat.Index(p.H,p.V)];
    }
    static MixedSessionState Ready()
    {
        var s=StationYardStopTests.Scene();var id=s.Fleet!.Trains[0].Id;
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,id));
        var world=StationCatchment.Detach(s.World,s.World.Stations!.Listeners.Single().ProviderId);
        s=s.Successor(s.Revision,new(world,s.Construction.Accounts),false);
        s=CityHeat.Enable(s);
        var apartment=RailwayProfiles.Bundled.City!.Structures.Single(d=>d.Reference.ContributionId=="{C7536844-7CD0-49e0-8982-2819F5792B32}");
        s=FixedCityConstruction.Commit(s,FixedCityConstruction.Preview(new(s.Construction,s.Clock.Capture(),s.Random.Capture()),
            new(22,11,2),apartment.Reference,selectedHeight:5));
        for(var n=0;n<15000 && s.World.City!.State.Producers.IsEmpty;n++)s.AdvanceOneMinute();
        Assert.Single(s.World.City!.State.Producers);
        Assert.Equal("city-variable",s.World.Stations!.Listeners.Single().Provider!.Input);
        Assert.Equal(10,StationCatchment.Demand(s.World.Stations,s.World.Stations.Buildings[0].Id,s.Clock.Time));
        var d=s.World.Stations.Profile.Definitions.First(d=>d.SizeX==1&&d.SizeY==1&&d.Height==1);
        s=StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,new(36,13,2),d.Reference));
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,new(37,15,2),Heading.East,4,false));
        // Literal source unload operand: Single(25/24) capped by three passengers.
        var w=s.World;var g=w.Stations!;
        var stations=new StationGraph(g.Profile,g.Buildings.Select((b,i)=>i==1?b with {Imports=b.Imports with {LastWeekPerDay=25}}:b),
            g.Platforms,g.Passages,g.NextStationName,g.NextPlatformName,g.Listeners,g.ListenerMemberships);
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,stations,w.City);
        s=new(new(w,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,id,new(25,15,2)));
        s=StationYardStopTests.Edit(s,t=>t with {StopCallCount=2});
        var due=s.Clock.Capture().Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(id)).DueMinute;
        while(s.Clock.Time.AbsoluteMinute<due-1)s.AdvanceOneMinute();
        return s;
    }
    static void Emit(string suffix,MixedSessionState before,MixedSessionState after)
    {
        if(Environment.GetEnvironmentVariable("FREETRAIN_PASSENGER_FEEDBACK_OUTPUT") is not {} path)return;
        var options=WireJson.CreateOptions();var snapshot=MixedWire.Snapshot(before,1);
        File.WriteAllBytes(path+suffix,SaveCodec.Write(before));
        File.WriteAllBytes(path+suffix+".after.json",SaveCodec.Write(after));
        File.WriteAllText(path+suffix+".before.json",JsonSerializer.Serialize<WireMessage>(snapshot,options));
        File.WriteAllText(path+suffix+".snapshot.json",JsonSerializer.Serialize<WireMessage>(MixedWire.Snapshot(after,1),options));
        File.WriteAllText(path+suffix+".delta.json",JsonSerializer.Serialize<WireMessage>(StateDifference.Between(snapshot,MixedWire.Snapshot(after,1)),options));
    }
    [Fact]
    public void Real_city_yards_and_train_deposit_literal_board_and_unload_heat_once_with_exact_reopen()
    {
        var s=Ready();var before=s.Freeze();var reopen=Open(SaveCodec.Write(s));
        s.AdvanceOneMinute();reopen.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopen));
        Assert.Equal(3,s.Fleet!.Trains[0].Passengers);Assert.Equal(7.5f,Q(s,new(23,13,2)));
        Assert.Equal(10,s.World.Stations!.Listeners.Single().Provider!.LandValue);
        Assert.Equal(20,StationCatchment.Demand(s.World.Stations,s.World.Stations.Buildings[0].Id,s.Clock.Time));
        Assert.Single(s.DepartureBells);Assert.Single(s.PassengerEffects.OfType<StationPassengerEffect.LandValue>());
        Emit("",before,s);
        var after=SaveCodec.Write(s);var reopenedAfter=Open(after);
        Assert.Empty(reopenedAfter.PassengerEffects);Assert.Empty(reopenedAfter.DepartureBells);
        s.AdvanceOneMinute();reopenedAfter.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopenedAfter));Assert.Equal(7.5f,Q(s,new(23,13,2)));
        // Reach the next actual destination yard without bypassing movement.
        while(((FleetCarState.Inside)s.Fleet.Trains[0].Head.State).Location.X<39)StationYardStopTests.Call(s);
        var due=s.Clock.Capture().Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(s.Fleet.Trains[0].Id)).DueMinute;
        while(s.Clock.Time.AbsoluteMinute<due-1)s.AdvanceOneMinute();
        before=s.Freeze();reopen=Open(SaveCodec.Write(s));s.AdvanceOneMinute();reopen.AdvanceOneMinute();
        Assert.Equal(0,s.Fleet.Trains[0].Passengers);Assert.Equal(3,s.World.Stations!.Buildings[1].Imports.Today);
        Assert.Equal(2.6041665f,Q(s,new(36,13,2)));
        Assert.Equal(3,Assert.Single(s.PassengerEffects.OfType<StationPassengerEffect.Transport>()).Passengers);
        Assert.Empty(s.DepartureBells);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopen));
        Emit(".unload",before,s);
        StationYardStopTests.Call(s);Assert.Equal(2.6041665f,Q(s,new(36,13,2)));
        Assert.Empty(s.PassengerEffects.OfType<StationPassengerEffect.LandValue>()); // zero-supply departure
    }
    [Fact]
    public void Controlled_P6_supply_stays_controlled_while_first_positive_feedback_activates_heat_and_one_timer()
    {
        var s=StationYardStopTests.Scene(count:2);Assert.Null(s.World.City);
        StationYardStopTests.Call(s);
        Assert.Equal("controlled",s.World.Stations!.Listeners.Single().Provider!.Input);
        Assert.Equal(1485f,Q(s,new(23,13,2))); // packed 594 * 10 / 4
        Assert.Single(s.Clock.Capture().Pending.Where(a=>a.HandlerId==CityHandlers.Heat));
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
    }
    [Theory]
    [InlineData("heat")][InlineData("revision")][InlineData("queue")][InlineData("late")]
    public void Failed_feedback_minute_rolls_back_all_state_and_previous_transients(string failure)
    {
        var s=Ready();var callbacks=0;
        if(failure=="heat")
        {
            var w=s.World;var c=w.City!;
            c=new(c.Profile,c.State with {Settings=c.State.Settings with {LandValuePower=1000}});
            w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,w.Stations,c);
            s=new(new(w,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
        }
        if(failure=="revision")s=new(s.Construction,s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:long.MaxValue);
        if(failure=="queue")s=new(s.Construction,s.Provenance,clock:s.Clock.Capture() with {NextOrder=long.MaxValue},random:s.Random.Capture(),revision:s.Revision);
        s=Rebind(s,(next,id)=>{
            callbacks++;next.Random["construction"].Next();
            var result=FleetMovement.Tick(next,id);
            if(failure=="late")next.Clock.AtEndOfTurn(()=>{
                Assert.Equal(7.5f,Q(next,new(23,13,2)));
                throw new OverflowException("after consumed feedback");
            });
            return result;
        });
        var before=SaveCodec.Write(s);var effects=s.PassengerEffects;var bells=s.DepartureBells;
        if(failure=="heat" && Environment.GetEnvironmentVariable("FREETRAIN_PASSENGER_FEEDBACK_OUTPUT") is {} path)File.WriteAllBytes(path+".rejected",before);
        if(failure=="heat")Assert.Throws<ArgumentException>(()=>s.AdvanceOneMinute());
        else Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());
        Assert.Equal(1,callbacks);
        Assert.Equal(before,SaveCodec.Write(s));Assert.Equal(effects,s.PassengerEffects);Assert.Equal(bells,s.DepartureBells);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Two_actual_yard_callbacks_consume_in_FIFO_order_before_later_callbacks_and_roll_back_together(bool fail)
    {
        var s=Ready();var first=s.Fleet!.Trains[0];
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,first.Definition,3,1));
        var second=s.Fleet!.Trains[^1];
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,second.Id,new(37,15,2)));
        s=StationYardStopTests.Edit(s,t=>t.Id==second.Id?t with {Passengers=3,BoardingOrigin=new FleetCarState.Inside(new(27,15,2),Heading.East)}:t);
        var clock=s.Clock.Capture();var due=clock.Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(second.Id)).DueMinute;
        clock=clock with {Pending=clock.Pending.Select(a=>a.HandlerId==MixedSessionState.TrainHandler(first.Id)?a with {DueMinute=due}:a)
            .OrderBy(a=>a.DueMinute).ThenBy(a=>a.Order).ToImmutableArray()};
        s=new(s.Construction,s.Provenance,clock:clock,random:s.Random.Capture(),revision:s.Revision);
        while(s.Clock.Time.AbsoluteMinute<due-1)s.AdvanceOneMinute();
        var order=new List<long>();
        s=Rebind(s,(next,id)=>{
            order.Add(id);
            if(id==second.Id)
            {
                Assert.Equal(7.5f,Q(next,new(23,13,2)));
                Assert.Equal(10,next.World.Stations!.Listeners.Single().Provider!.LandValue);
                if(fail)throw new OverflowException("second train after first deposit");
            }
            return FleetMovement.Tick(next,id);
        });
        var before=SaveCodec.Write(s);
        if(fail){Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());Assert.Equal(before,SaveCodec.Write(s));}
        else
        {
            s.AdvanceOneMinute();
            Assert.Equal(7.5f,Q(s,new(23,13,2)));Assert.Equal(2.6041665f,Q(s,new(36,13,2)));
            Assert.Equal(new[]{3f,1.0416666f},s.PassengerEffects.OfType<StationPassengerEffect.LandValue>().Select(e=>e.Amount));
            Assert.Equal(3,Assert.Single(s.PassengerEffects.OfType<StationPassengerEffect.Transport>()).Passengers);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
        }
        Assert.Equal(new[]{first.Id,second.Id},order);
    }
    [Fact]
    public void Single_deposits_retain_effect_order_instead_of_coalescing()
    {
        var s=Ready();var b=s.World.Stations!.Buildings[0];
        s=Rebind(s,(next,id)=>FleetMovement.Tick(next,id) with {PassengerEffects=[
            new StationPassengerEffect.LandValue(b.Id,b.Location,16777216f),
            new StationPassengerEffect.LandValue(b.Id,b.Location,1),
            new StationPassengerEffect.LandValue(b.Id,b.Location,1)]});
        s.AdvanceOneMinute();Assert.Equal(41943048f,Q(s,b.Location)); // each 2.5 rounds up by four
    }
    [Fact]
    public void A_rejected_minute_retains_the_previous_successful_effects_and_bells_without_reconsumption()
    {
        var fail=false;
        var s=Rebind(Ready(),(next,id)=>{
            if(fail)throw new OverflowException("next minute");
            return FleetMovement.Tick(next,id) with {NextDelay=1};
        });
        s.AdvanceOneMinute();var before=SaveCodec.Write(s);var effects=s.PassengerEffects;var bells=s.DepartureBells;
        Assert.NotEmpty(effects);Assert.Single(bells);fail=true;
        Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());
        Assert.Equal(before,SaveCodec.Write(s));Assert.Equal(effects,s.PassengerEffects);Assert.Equal(bells,s.DepartureBells);
        Assert.Equal(7.5f,Q(s,new(23,13,2)));
    }
}
