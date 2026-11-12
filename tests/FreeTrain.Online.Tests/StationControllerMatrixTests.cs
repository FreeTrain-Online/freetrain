using System.Collections.Immutable;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationBuildingConstructionTests;

namespace FreeTrain.Online.Tests;

public partial class StationYardStopTests
{
    // Literal source branches: SimpleTrainControllerImpl, StationHandler,
    // AdvancedStationHandler and YardRailRoad at ExAV 883cf7d. No test harbor.
    [Theory]
    [InlineData("simple",30,0,true,false)]
    [InlineData("absent",10,0,true,false)]
    [InlineData("fixed",40,-1,true,false)]
    [InlineData("fixed1440",1440,0,true,false)]
    [InlineData("dailyEqual",1440,-1,true,false)]
    [InlineData("advanced",10,0,true,false)]
    [InlineData("reverse",10,-1,true,false)]
    [InlineData("forwarding",10,-999,false,true)]
    [InlineData("reverseforwarding",10,-1000,false,true)]
    [InlineData("reverse2",10,-1,true,false)]
    [InlineData("rforwarding2",10,-1000,false,true)]
    public void Real_station_production_controller_calls_exchange_and_reopen_have_literal_order(
        string mode,int wait,int departure,bool boards,bool forwarding)
    {
        var s=Scene();var location=Station(s).Location;
        var arrival=s.Clock.Capture().Pending.Single(a=>a.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute;
        if(mode!="simple")s=Edit(s,controller:c=>c with {Harbors=mode switch {
            "absent"=>[],
            "fixed"=>[new(location,FleetHarborKind.Fixed,40,true)],
            "fixed1440"=>[new(location,FleetHarborKind.Fixed,1440,false)],
            "dailyEqual"=>[new(location,FleetHarborKind.Daily,(int)(arrival%1440),true)],
            _=>[new(location,FleetHarborKind.Advanced,0,false,[
                // A nonmatching mask must not hide the subsequent stored order.
                new(2,-1,-1,-1,-1,FleetRuleAction.Pass),
                new(-1,-1,-1,-1,-1,mode=="advanced"?FleetRuleAction.Go:Enum.Parse<FleetRuleAction>(mode,true)),
                new(-1,-1,-1,-1,-1,FleetRuleAction.Stop),
                new(-1,-1,-1,-1,-1,FleetRuleAction.Pass)])]
        }});
        var calls=new List<string>();var callbackTime=s.Clock.Time;
        FleetHarborHandler observe=(world,train,call)=>{
            var host=Assert.IsType<FleetHarborReference.Station>(call.Harbor);
            Assert.Equal(location,host.Location);Assert.Equal(Station(s).Id,host.StationId);
            Assert.NotEqual(call.RailLocation,host.Location);
            var result=call.Controller.Kind==FleetControllerKind.Simple?FleetControllers.SimpleStop(world,train,call):
                FleetControllers.TimetableStop(train,call,callbackTime,world);
            calls.Add($"{call.CallCount}:{result.Decision.SourceMinutes}");return result;
        };
        s=new(s.Construction,s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision,
            trainCallback:(minute,id)=>{callbackTime=minute.Clock.Time;return FleetMovement.Tick(minute,id,(w,t)=>FleetControllers.StopTime(w,t,observe,minute.Clock.Time));});
        Call(s);
        Assert.Equal(new[]{$"0:{wait}"},calls);
        Assert.Equal(1,Station(s).Trains.Today);Assert.Equal(1,s.Fleet!.Trains[0].StopCallCount);
        Assert.Equal(arrival+wait,s.Clock.Capture().Pending.Single(a=>a.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute);
        Assert.Empty(s.DepartureBells);Assert.Equal(0,s.Fleet.Trains[0].Passengers);
        var reopen=Open(s);Call(s);Call(reopen);
        Assert.Equal(new[]{$"0:{wait}",$"1:{departure}"},calls);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopen));
        Assert.Equal(0,s.Fleet.Trains[0].StopCallCount);Assert.Equal(1,s.Fleet.Trains[0].MoveCount);
        Assert.Equal(forwarding,s.Fleet.Trains[0].Forwarding);Assert.Equal(boards,s.Fleet.Trains[0].Passengers>0);
        Assert.Equal(boards?2:1,Station(s).Trains.Today+Station(s).Trains.Yesterday);Assert.Single(s.DepartureBells);
        Assert.Single(s.Clock.Capture().Pending.Where(a=>a.HandlerId.StartsWith("train/",StringComparison.Ordinal)));
    }

    [Fact]
    public void Real_station_advanced_Pass_precedes_Stop_and_exchanges_nothing()
    {
        var s=Scene();var location=Station(s).Location;
        s=Edit(s,controller:c=>c with {Harbors=[new(location,FleetHarborKind.Advanced,0,false,
            [new(-1,-1,-1,-1,-1,FleetRuleAction.Pass),new(-1,-1,-1,-1,-1,FleetRuleAction.Stop)])]});
        Call(s);Assert.Equal(1,s.Fleet!.Trains[0].MoveCount);Assert.Equal(0,s.Fleet.Trains[0].StopCallCount);
        Assert.Equal(0,Station(s).Trains.Today);Assert.Empty(s.PassengerEffects);Assert.Empty(s.DepartureBells);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Both_constructed_platforms_dispatch_the_same_production_station_setting(bool second)
    {
        var s=Scene();var host=Station(s);
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,new(25,16,2),Heading.East,4,false));
        s=Edit(s,controller:c=>c with {Harbors=[new(host.Location,FleetHarborKind.Fixed,40,false)]});
        if(second) {
            s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,s.Fleet!.Trains[0].Id));
            s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[0].Id,new(25,16,2),false));
        }
        Call(s);Assert.Equal(1,s.Fleet!.Trains[0].StopCallCount);
        Assert.Equal(s.Clock.Time.AbsoluteMinute+40,s.Clock.Capture().Pending.Single(a=>a.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute);
        var reopen=Open(s);Call(s);Call(reopen);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopen));Assert.True(s.Fleet.Trains[0].Passengers>0);
        Assert.Equal(s.World.Stations!.Platforms[second?1:0].Id,Assert.Single(s.DepartureBells).PlatformId);
    }

    [Fact]
    public void Multiple_real_platforms_share_host_location_and_rebuilt_station_reuses_retained_TAT_setting()
    {
        var s=Scene();var first=Station(s);var platform=s.World.Stations!.Platforms[0];
        // Second actual platform next to the same host, plus an independent host.
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,new(25,16,2),Heading.East,4,false));
        var second=s.World.Stations!.Platforms[1];
        Assert.Equal(first.Id,s.World.Stations.HostStation(second.Id)!.Id);
        s=Build(s,at:new(36,13,2));var other=s.World.Stations!.Buildings[1];
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,new(37,15,2),Heading.East,4,false));
        s=Edit(s,controller:c=>c with {Harbors=[new(first.Location,FleetHarborKind.Fixed,40,true),new(other.Location,FleetHarborKind.Pass,0,false)]});
        var retained=s.Fleet!.EffectiveController(s.Fleet.Trains[0].Id);
        Assert.Equal(40,FleetControllers.StopTime(s.World,s.Fleet.Trains[0],time:s.Clock.Time).Decision.SourceMinutes);
        first=s.World.Stations!.Building(first.Id);
        var removal=StationBuildingConstruction.Preview(s.World,first.Location,null,true,first.Id,first.Revision);
        Assert.True(removal.Legal,removal.Reason);s=StationBuildingConstruction.Commit(s,removal);
        Assert.Null(s.World.Stations!.HostStation(platform.Id));Assert.Null(s.World.Stations.HostStation(second.Id));
        Assert.Equal(retained.Harbors,s.Fleet!.EffectiveController(s.Fleet.Trains[0].Id).Harbors);
        s=Build(s,at:first.Location);var rebuilt=s.World.Stations!.Buildings.Single(b=>b.Location==first.Location);
        Assert.NotEqual(first.Id,rebuilt.Id);
        foreach(var id in new[]{platform.Id,second.Id}) {
            var p=s.World.Stations!.Platform(id);rebuilt=s.World.Stations.Building(rebuilt.Id);
            s=StationHostConnections.Commit(s,StationHostConnections.Preview(s.World,p.Id,p.Revision,rebuilt.Id,rebuilt.Revision));
        }
        Assert.Equal(40,FleetControllers.StopTime(s.World,s.Fleet!.Trains[0],time:s.Clock.Time).Decision.SourceMinutes);
        Assert.Equal(retained.Harbors,s.Fleet.EffectiveController(s.Fleet.Trains[0].Id).Harbors);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(s)));
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"PassengerService","station-controller-v11.json")),SaveCodec.Write(s));
        if(Environment.GetEnvironmentVariable("FREETRAIN_STATION_CONTROLLER_OUTPUT") is {} path)File.WriteAllBytes(path,SaveCodec.Write(s));
    }
}
