using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;
using static FreeTrain.Online.Tests.StationBuildingConstructionTests;

namespace FreeTrain.Online.Tests;

public class StationYardStopTests
{
    static int Int(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    public static IEnumerable<object[]> Positions()=>StationPassengerExchangeTests.Rows("stopIndices");
    public static IEnumerable<object[]> Yards()=>StationPassengerExchangeTests.Rows("yards");

    // Every yard and host is constructed by the same domain operations as the UI.
    // Fixture callCount/passengers are explicit callback-boundary operands.
    static MixedSessionState Scene(int length=3,int index=2,bool same=true,bool host=true,int count=0,bool wide=false,int lane=0)
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Constructed yards",new(64,64,8),0,
            Enumerable.Repeat(new TerrainCell(2),64*64),new(1,[]),[]);
        var initial=MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain).EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var s=new MixedSessionState(new(initial),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(64,64,8),0)),clock:new(517,0,[]),revision:initial.Revision);
        s=Build(s,at:new(23,13,2));
        var route=NormalRailwayConstruction.PreviewBuild(s.World,new(16,15,2),new(44,15,2));Assert.True(route.Legal,route.Reason);
        s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,route,s.Clock.Time),false);
        if(wide)
        {
            var plan=WidePlatformConstruction.Preview(s.World,new(25,lane==0?14:16,2),Heading.East,4,false);
            Assert.True(plan.Legal,plan.Reason);s=WidePlatformConstruction.Commit(s,plan);
            if(lane==1)
            {
                var p=s.World.Stations!.Platforms[0];
                s=WidePlatformConstruction.Commit(s,WidePlatformConstruction.PreviewLane(s.World,p.Location,p.Id,p.Revision,1,false));
                p=s.World.Stations!.Platforms[0];
                s=WidePlatformConstruction.Commit(s,WidePlatformConstruction.PreviewLane(s.World,p.Location,p.Id,p.Revision,0,true));
            }
        }
        else s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,new(25,15,2),Heading.East,4,true));
        var platform=s.World.Stations!.Platforms[0];Assert.NotNull(platform.HostId);
        if(!host)s=StationHostConnections.Commit(s,StationHostConnections.Preview(s.World,platform.Id,platform.Revision,null,0));
        var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.PermittedLengths.Contains(length));
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,length,1));
        var train=s.Fleet!.Trains[0];
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,train.Id,new(25+index-(same?length-1:0),15,2),!same));
        Assert.Equal(new Location(25+index,15,2),((FleetCarState.Inside)s.Fleet!.Trains[0].Head.State).Location);
        var world=StationCatchment.Attach(s.World,s.World.At(new(20,15,2))!.OwnerId,
            new(new("ConstantPopulation",10000),0,Special:true));
        s=s.Successor(s.Revision,new(world,s.Construction.Accounts),false);
        return Edit(s,t=>t with {StopCallCount=count});
    }
    static MixedSessionState Edit(MixedSessionState s,Func<FleetTrain,FleetTrain>? train=null,
        Func<FleetController,FleetController>? controller=null,SpeedLimits? limits=null)
    {
        var f=s.Fleet!;var w=s.World;var spatial=w.Spatial.Capture();var controllers=f.Controllers;var groups=f.Groups;
        if(controller!=null)
        {
            var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
            var existing=controllers.SingleOrDefault(c=>c.Kind==FleetControllerKind.Timetable);
            var c=existing ?? new FleetController(spatial.NextEntityId,0,"Shared TAT",provider.Kind,provider.Binding);
            c=controller(c) with {Revision=existing==null?0:c.Revision+1};
            controllers=existing==null?controllers.Add(c):controllers.Select(old=>old.Id==c.Id?c:old).ToImmutableArray();
            if(existing==null)spatial=spatial with {NextEntityId=spatial.NextEntityId+1};
            groups=groups.Select(g=>g.Id==f.RootGroupId?g with {ControllerId=c.Id,Revision=g.Revision+1}:g).ToImmutableArray();
        }
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(t=>train==null && controller==null?t:
            (train==null?t:train(t)) with {ControllerId=controller==null?t.ControllerId:f.Controllers.Single(c=>c.Kind==FleetControllerKind.Delegation).Id,Revision=t.Revision+1}),groups,controllers,f.ColorTest);
        return s.Successor(s.Revision,new(w.Replace(w.Revision,w.Terrain,spatial,w.Voxels,w.Slopes,
            limits ?? w.SpeedLimits,graph),s.Construction.Accounts),false);
    }
    static MixedSessionState Bind(MixedSessionState s,FleetHarborHandler handler,StationCallback? station=null)=>
        new(s.Construction,s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision,
            trainCallback:(session,id)=>FleetMovement.Tick(session,id,(w,t)=>FleetControllers.StopTime(w,t,handler,session.Clock.Time)),stationCallback:station);
    static void Call(MixedSessionState s)
    {
        var due=s.Clock.Capture().Pending.Single(t=>t.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute;
        while(s.Clock.Time.AbsoluteMinute<due)s.AdvanceOneMinute();
    }
    static MixedSessionState Open(MixedSessionState s)=>SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static StationBuilding Station(MixedSessionState s)=>Assert.Single(s.World.Stations!.Buildings);

    [Theory,MemberData(nameof(Positions))]
    public void All_eight_literal_length_direction_gates_execute_at_constructed_yards(JsonElement row)=>Witness(row);
    [Theory,MemberData(nameof(Yards))]
    public void All_fourteen_literal_yard_call_orders_execute_with_real_hosts(JsonElement row)=>Witness(row);
    static void Witness(JsonElement row)
    {
        var i=row.GetProperty("input");var e=row.GetProperty("expected");var trace=new List<string>();
        var s=Scene(Int(i,"trainLength"),Int(i,"index"),i.GetProperty("sameDirection").GetBoolean(),i.GetProperty("host").GetBoolean(),Int(i,"callCount"));
        FleetHarborHandler handler=(world,train,call)=>{
            trace.Add("controller");var station=Assert.IsType<FleetHarborReference.Station>(call.Harbor);
            Assert.Equal(Station(s).Location,station.Location);Assert.Equal(Station(s).Id,station.StationId);Assert.Equal(Station(s).Revision,station.Revision);
            Assert.Equal(((FleetCarState.Inside)train.Head.State).Location,call.RailLocation);
            Assert.NotEqual(call.Harbor.Location,call.RailLocation);Assert.Equal(Int(i,"callCount"),call.CallCount);
            if(i.GetProperty("handlerEffects").GetArrayLength()>0){trace.AddRange(["reverse","re-place"]);return FleetTurning.CheckTurn(world,train,Int(i,"result"),s.Clock.Time);}
            return new(train,Int(i,"result"));
        };
        var before=SaveCodec.Write(s);var stop=FleetControllers.StopTime(s.World,s.Fleet!.Trains[0],handler,s.Clock.Time);
        if(stop.Exchange!=null)trace.Add(stop.Decision.Action==FleetStopAction.Wait?"unload":"load");
        if(stop.Bell!=null)trace.Add("bell");
        Assert.Equal(e.GetProperty("calls").EnumerateArray().Select(v=>v.GetString()),trace);
        Assert.Equal(Int(e,"result"),stop.Decision.SourceMinutes);Assert.Equal(before,SaveCodec.Write(s));
        trace.Clear();var worldRevision=s.World.Revision;var trainBefore=s.Fleet.Trains[0];var stationBefore=Station(s);var accounts=s.Construction.Accounts;
        s=Bind(s,handler);Call(s);var after=s.Fleet!.Trains[0];
        Assert.Equal(worldRevision+1,s.World.Revision);Assert.Equal(trainBefore.Revision+1,after.Revision);
        Assert.Equal(stationBefore.Revision+(stop.Exchange==null?0:1),Station(s).Revision);
        Assert.Equal(Station(s).Revision,s.World.Spatial.Entities.Single(v=>v.Id==Station(s).Id).Revision);
        Assert.Equal<StationPassengerEffect>(stop.Exchange?.Effects ?? [],s.PassengerEffects);Assert.Equal(stop.Bell==null?0:1,s.DepartureBells.Length);
        if(stop.Decision.Action==FleetStopAction.Wait)
        {
            Assert.Equal(trainBefore.StopCallCount+1,after.StopCallCount);Assert.Equal<FleetCar>(trainBefore.Cars,after.Cars);
            Assert.Equal(trainBefore.MoveCount,after.MoveCount);Assert.Equal(accounts.Cash,s.Construction.Accounts.Cash);
            Assert.Equal(TrainOperationalState.StoppingAtStation,after.State);
        }
        else {Assert.Equal(0,after.StopCallCount);Assert.Equal(trainBefore.MoveCount+1,after.MoveCount);}
        Assert.Single(s.Clock.Capture().Pending.Where(t=>t.HandlerId==MixedSessionState.TrainHandler(after.Id)));
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(s)));
    }

    [Theory]
    [InlineData(-1000)][InlineData(-999)][InlineData(-2)][InlineData(-1)][InlineData(0)]
    public void First_nonpositive_calls_never_exchange_or_ring(int result)
    {
        var s=Bind(Scene(),(_,t,_)=>new(t,result));Call(s);
        Assert.Equal(0,Station(s).Trains.Today);Assert.Empty(s.PassengerEffects);Assert.Empty(s.DepartureBells);
        Assert.Equal(0,s.Fleet!.Trains[0].StopCallCount);Assert.Equal(1,s.Fleet.Trains[0].MoveCount);
        Assert.Equal(result is -999 or -1000,s.Fleet.Trains[0].Forwarding);
    }

    [Theory]
    [InlineData(false,0)][InlineData(true,0)][InlineData(true,1)]
    public void Production_Simple_unloads_waits_boards_and_moves_with_one_publication_and_exact_reopen(bool wide,int lane)
    {
        var s=Scene(wide:wide,lane:lane);var train=s.Fleet!.Trains[0];var count=s.Construction.Accounts.Transactions.Length;
        var before=SaveCodec.Write(s);var originalRevision=s.World.Revision;
        if(!wide && Environment.GetEnvironmentVariable("FREETRAIN_YARD_FLEET_OUTPUT") is {} path)File.WriteAllBytes(path,before);
        Call(s);Assert.Equal(originalRevision+1,s.World.Revision);Assert.Equal(1,Station(s).Trains.Today);
        if(!wide && Environment.GetEnvironmentVariable("FREETRAIN_YARD_FLEET_OUTPUT") is {} arrivalPath)File.WriteAllBytes(arrivalPath+".arrived.json",SaveCodec.Write(s));
        Assert.Equal(1,s.Fleet!.Trains[0].StopCallCount);Assert.Equal(count,s.Construction.Accounts.Transactions.Length);
        Assert.Empty(s.DepartureBells);Assert.Equal(0,s.Fleet.Trains[0].MoveCount);
        var reopen=Open(s);Assert.Empty(reopen.PassengerEffects);Assert.Empty(reopen.DepartureBells);
        var due=s.Clock.Capture().Pending.Single(t=>t.HandlerId==MixedSessionState.TrainHandler(train.Id)).DueMinute;
        Assert.Equal(s.Clock.Time.AbsoluteMinute+30,due);
        Call(s);Call(reopen);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopen));
        if(!wide && Environment.GetEnvironmentVariable("FREETRAIN_YARD_FLEET_OUTPUT") is {} departurePath)File.WriteAllBytes(departurePath+".departed.json",SaveCodec.Write(s));
        var departed=s.Fleet.Trains[0];var stock=s.Fleet.Profile.Resolve(departed.Definition);
        Assert.Equal(stock.Create(3).PackedCapacity,departed.Passengers);Assert.Equal(train.Head.State,departed.BoardingOrigin);
        Assert.Equal(2,Station(s).Trains.Today);Assert.Equal(departed.Passengers,Station(s).Exports.Today);
        Assert.Equal(1,departed.MoveCount);Assert.Single(s.DepartureBells);Assert.Equal(3,s.PassengerEffects.Length);
        var receipt=s.Construction.Accounts.Transactions[^1];Assert.Equal("trainMove",receipt.Operation);
        Assert.Equal((3*20+departed.Passengers/20)*2000,receipt.Expenditure);
        foreach(var car in departed.Cars)Assert.Equal(car.Id,s.World.At(((FleetCarState.Inside)car.State).Location)!.Traffic!.Car!.CarId);
    }

    [Theory]
    [InlineData(FleetRuleAction.Reverse2)][InlineData(FleetRuleAction.RForwarding2)]
    public void Production_TURNING_precedes_boarding_and_central_reversal_and_uses_entry_head_speed(FleetRuleAction action)
    {
        var s=Scene(count:2);var entry=s.Fleet!.Trains[0];var station=Station(s);
        s=Edit(s,controller:c=>c with {Kind=FleetControllerKind.Timetable,Harbors=[new(station.Location,FleetHarborKind.Advanced,0,false,
            [new(-1,-1,-1,-1,-1,action)])]},limits:new([((FleetCarState.Inside)entry.Head.State).Location],[],[]));
        entry=s.Fleet!.Trains[0];
        var controllerStop=FleetTimetable.Stop(s.World,s.Fleet!.Trains[0],s.Fleet.EffectiveController(entry.Id).Harbors[0].Rules,2,s.Clock.Time);
        var stop=FleetControllers.StopTime(s.World,s.Fleet.Trains[0],time:s.Clock.Time);
        Assert.Equal(controllerStop.Train.Head.State,stop.Train.Head.State);
        if(action==FleetRuleAction.Reverse2)Assert.Equal(controllerStop.Train.Head.State,stop.Train.BoardingOrigin);
        else {Assert.Null(stop.Exchange);Assert.Equal(0,stop.Train.Passengers);}
        var result=FleetMovement.Tick(s,entry.Id);Assert.Equal(4,result.NextDelay);
        var expected=FleetReversal.Reverse(s.World,stop.Train,s.Clock.Time);
        var after=result.Candidate.World.Fleet!.Train(entry.Id);
        Assert.Equal(expected.IsReversed,after.IsReversed);Assert.Equal(expected.Cars.Select(c=>c.Id),after.Cars.Select(c=>c.Id));
        Assert.Equal(action==FleetRuleAction.RForwarding2,after.Forwarding);
        Assert.Equal(entry.Revision+1,after.Revision);Assert.All(after.Cars,c=>Assert.InRange(c.Revision,entry.Cars.Single(x=>x.Id==c.Id).Revision,entry.Cars.Single(x=>x.Id==c.Id).Revision+1));
        Call(s);Assert.Single(s.DepartureBells);Assert.Equal(0,s.Fleet!.Trains[0].StopCallCount);
    }

    [Fact]
    public void Current_group_controller_and_station_identity_are_resolved_again_after_wait_remove_and_rebuild()
    {
        var s=Scene();Call(s);var old=Station(s);var platform=s.World.Stations!.Platforms[0];
        // Updating the inherited instance changes the very next call, with no cached Simple decision.
        s=Edit(s,controller:c=>c with {Kind=FleetControllerKind.Timetable,Harbors=[new(old.Location,FleetHarborKind.Fixed,5,true)]});
        var stop=FleetControllers.StopTime(s.World,s.Fleet!.Trains[0],time:s.Clock.Time);Assert.Equal(FleetStopAction.Reverse,stop.Decision.Action);Assert.NotNull(stop.Exchange);
        s=StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,old.Location,null,true,old.Id,old.Revision));
        Assert.Null(s.World.Stations!.HostStation(platform.Id));
        var called=false;stop=FleetControllers.StopTime(s.World,s.Fleet!.Trains[0],(_,t,_)=>{called=true;return new(t,10);},s.Clock.Time);
        Assert.False(called);Assert.Null(stop.Exchange);Assert.Null(stop.Bell);
        s=Build(s,at:old.Location);var rebuilt=Station(s);Assert.NotEqual(old.Id,rebuilt.Id);
        platform=s.World.Stations!.Platforms[0];
        s=StationHostConnections.Commit(s,StationHostConnections.Preview(s.World,platform.Id,platform.Revision,rebuilt.Id,rebuilt.Revision));
        stop=FleetControllers.StopTime(s.World,s.Fleet!.Trains[0],(_,t,c)=>{Assert.Equal(rebuilt.Id,Assert.IsType<FleetHarborReference.Station>(c.Harbor).StationId);return new(t,10);},s.Clock.Time);
        Assert.Equal(FleetStopAction.Wait,stop.Decision.Action);Assert.Null(stop.Exchange); // incoming later call, no second unload
    }

    [Fact]
    public void Controller_side_reversal_precedes_first_unload_and_incoming_count_survives_handler_mutation()
    {
        var s=Scene();var board=StationPassengerExchange.Board(s,Station(s).Id,s.Fleet!.Trains[0].Id);
        s=s.Successor(s.Revision,board.Candidate,false);
        var loaded=s.Fleet!.Trains[0];var fare=s.Fleet.Profile.Resolve(loaded.Definition).Fare;
        s=Bind(s,(w,t,_)=>new(FleetReversal.Reverse(w,t) with {StopCallCount=4},10));
        var count=s.Construction.Accounts.Transactions.Length;Call(s);
        Assert.Equal(loaded.Passengers,Station(s).Imports.Today);Assert.Equal(0,s.Fleet!.Trains[0].Passengers);
        Assert.Equal(5,s.Fleet.Trains[0].StopCallCount);Assert.Null(s.Fleet.Trains[0].BoardingOrigin);
        Assert.Equal(loaded.Cars.Reverse().Select(c=>c.Id),s.Fleet.Trains[0].Cars.Select(c=>c.Id));
        var receipt=Assert.Single(s.Construction.Accounts.Transactions.Skip(count));
        Assert.Equal(loaded.Passengers*fare*2*2,receipt.Revenue);Assert.Equal("stationUnload",receipt.Operation);
        Assert.Equal(0,receipt.Expenditure);Assert.Equal(s.World.Revision,receipt.WorldRevision);

        s=Bind(Scene(count:2),(_,t,_)=>new(t with {StopCallCount=0},0));Call(s);
        Assert.True(s.Fleet!.Trains[0].Passengers>0);Assert.Single(s.DepartureBells);Assert.Equal(1,Station(s).Trains.Today);
    }

    [Fact]
    public void Real_travel_between_two_constructed_stations_unloads_and_books_current_head_fare_before_waiting()
    {
        var s=Scene();s=Build(s,at:new(36,13,2));
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,new(37,15,2),Heading.East,4,false));
        var stations=s.World.Stations!.Buildings;var second=stations[1].Id;
        Assert.Equal(second,s.World.Stations.HostStation(s.World.Stations.Platforms[1].Id)!.Id);
        Call(s);Call(s);var departed=s.Fleet!.Trains[0];var fare=s.Fleet.Profile.Resolve(departed.Definition).Fare;
        Assert.True(departed.Passengers>0);var reopen=Open(s);
        for(var n=0;n<20 && s.World.Stations.Building(second).Trains.Today==0;n++){Call(s);Call(reopen);}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopen));
        Assert.Equal(1,s.World.Stations.Building(second).Trains.Today);Assert.Equal(departed.Passengers,s.World.Stations.Building(second).Imports.Today);
        Assert.Equal(new Location(39,15,2),((FleetCarState.Inside)s.Fleet.Trains[0].Head.State).Location);
        Assert.Equal(1,s.Fleet.Trains[0].StopCallCount);Assert.Equal(0,s.Fleet.Trains[0].Passengers);
        var receipt=s.Construction.Accounts.Transactions[^1];Assert.Equal("stationUnload",receipt.Operation);
        Assert.Equal(departed.Passengers*fare*12*2,receipt.Revenue);Assert.Equal(0,receipt.Expenditure);
        Assert.Equal(s.World.Revision,receipt.WorldRevision);Assert.Empty(s.DepartureBells);
    }

    [Theory]
    [InlineData("counter")][InlineData("cash")][InlineData("late")][InlineData("departureCash")][InlineData("queue")]
    public void Failed_yard_minute_discards_exchange_occupancy_bookings_queue_and_effects(string failure)
    {
        var s=Scene(count:failure is "late" or "departureCash"?2:0);
        if(failure=="cash")s=Edit(s,t=>t with {Passengers=39,BoardingOrigin=new FleetCarState.Inside(new(20,15,2),Heading.East)});
        if(failure=="counter")
        {
            var w=s.World;var g=w.Stations!;var b=Station(s) with {Trains=new(int.MaxValue)};
            var stations=new StationGraph(g.Profile,[b],g.Platforms,g.Passages,g.NextStationName,g.NextPlatformName,g.Listeners,g.ListenerMemberships);
            s=new(new(new MixedWorld(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,stations),s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),revision:s.Revision);
        }
        if(failure=="cash")s=new(new(s.World,new ConstructionAccounts(long.MaxValue)),s.Provenance,clock:s.Clock.Capture(),revision:s.Revision);
        if(failure=="departureCash")s=new(new(s.World,new ConstructionAccounts(long.MinValue)),s.Provenance,clock:s.Clock.Capture(),revision:s.Revision);
        if(failure=="queue")s=new(s.Construction,s.Provenance,clock:s.Clock.Capture() with {NextOrder=long.MaxValue},revision:s.Revision);
        if(failure=="late")
        {
            var clock=s.Clock.Capture();var due=540L;
            var pending=clock.Pending.Select(t=>t.HandlerId.StartsWith("train/",StringComparison.Ordinal)?t with {DueMinute=due,Order=0,Registration=0}:
                t with {Order=t.Order+1,Registration=t.Registration+1}).OrderBy(t=>t.DueMinute).ThenBy(t=>t.Order).ToImmutableArray();
            var w=s.World;var g=w.Stations!;
            var stations=new StationGraph(g.Profile,g.Buildings.Select(b=>b with {HourTimer=pending.Single(t=>t.HandlerId==b.HourTimer!.HandlerId),DayTimer=pending.Single(t=>t.HandlerId==b.DayTimer!.HandlerId)}),
                g.Platforms.Select(p=>p with {DayTimer=pending.Single(t=>t.HandlerId==p.DayTimer!.HandlerId)}),g.Passages,g.NextStationName,g.NextPlatformName,g.Listeners,g.ListenerMemberships);
            w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,stations);
            s=new(new(w,s.Construction.Accounts),s.Provenance,clock:new(539,clock.NextOrder+1,pending),revision:s.Revision,stationCallback:(_,_)=>throw new OverflowException("late station failure"));
        }
        var target=s.Clock.Capture().Pending.Single(t=>t.HandlerId.StartsWith("train/",StringComparison.Ordinal)).DueMinute;
        while(s.Clock.Time.AbsoluteMinute<target-1)s.AdvanceOneMinute();
        var before=SaveCodec.Write(s);Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());
        Assert.Equal(before,SaveCodec.Write(s));Assert.Empty(s.PassengerEffects);Assert.Empty(s.DepartureBells);
    }
}
