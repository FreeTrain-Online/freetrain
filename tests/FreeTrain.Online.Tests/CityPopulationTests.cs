using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class CityPopulationTests
{
    static readonly Location At=new(14,12,2);
    static CityStructureDefinition Apartment=>RailwayProfiles.Bundled.City!.Structures.Single(d=>d.Reference.ContributionId=="{C7536844-7CD0-49e0-8982-2819F5792B32}");
    static StationDefinition Station=>RailwayProfiles.Bundled.Stations!.Definitions.First(d=>d.SizeX==1&&d.SizeY==1&&d.Height==1);
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    static MixedSessionState Open(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static MixedSessionState Completed(int height=5,Location? stationAt=null,Location? elevatedStationAt=null)
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Population",new(40,32,16),0);
        var cells=Enumerable.Repeat(new TerrainCell(2),40*32).ToImmutableArray();
        if(elevatedStationAt is {} elevated)
        {
            var hv=new WorldSpace(40,32,16).ToStorage(elevated);
            cells=cells.SetItem(hv.V*40+hv.H,new TerrainCell(elevated.Z,TerrainSurface.Cut));
        }
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,request.Name,new(40,32,16),0,cells,new(1,[]),[]);
        var w=new MixedWorld(RailwayProfiles.Bundled,terrain,new(1,[]),[],[],city:CityGraph.Empty(RailwayProfiles.Bundled.City!,terrain.Space));
        var s=new MixedSessionState(new(w),TerrainProvenance.From(request),clock:new(1980,0,[]),random:new RandomStreams(7).Capture());
        if(stationAt is {} station)s=Build(s,station);
        s=FixedCityConstruction.Commit(s,FixedCityConstruction.Preview(Capture(s),At,Apartment.Reference,selectedHeight:height));
        for(var n=0;n<15000&&s.World.City!.State.Producers.IsEmpty;n++)s.AdvanceOneMinute();
        Assert.Single(s.World.City!.State.Producers);
        return s;
    }
    static MixedSessionState Build(MixedSessionState s,Location at)=>StationBuildingConstruction.Commit(s,
        StationBuildingConstruction.Preview(s.World,at,Station.Reference));
    static MixedSessionState HeatNext(MixedSessionState s)
    {
        var c=s.Clock.Capture();var graph=s.World.City!;
        // The provider's shifted public read is zero, with four 100 neighbors.
        // Diffusion gives ((4*100*.8)*.240)*.8 = 61.44 (Single),
        // then trunc(pow(61.44,.3f))*10 = 30. Initial land/cap are 0/10.
        var hv=s.World.Terrain.Space.ToStorage(At);
        var q=graph.State.Heat.Q.Select(_=>100f).ToImmutableArray();
        q=q.SetItem(graph.State.Heat.Index(hv.H+1,hv.V+1),0);
        var timer=new ScheduledAction(c.NextOrder,c.NextOrder,c.AbsoluteMinute+1,CityHandlers.Heat,600);
        graph=new(graph.Profile,graph.State with {Heat=graph.State.Heat with {Q=q},HeatCallback=timer});
        var w=s.World;
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,w.Stations,graph);
        return new(new(w,s.Construction.Accounts),s.Provenance,clock:c with {NextOrder=c.NextOrder+1,Pending=c.Pending.Add(timer).OrderBy(a=>a.DueMinute).ThenBy(a=>a.Order).ToImmutableArray()},random:s.Random.Capture(),revision:s.Revision);
    }
    [Fact]
    public void Actual_building_fifth_station_tie_refresh_and_demolition_preserve_both_saved_directions()
    {
        var s=Completed();
        foreach(var x in new[]{22,20,18,16,15})s=Build(s,new(x,12,2));
        var graph=s.World.Stations!;var owner=s.World.City!.State.Structures.Single();var stations=graph.Buildings.Select(b=>b.Id).ToArray();
        Assert.Equal(new[]{stations[4],stations[1],stations[2],stations[3]},graph.Listeners.Single().Stations);
        Assert.Empty(graph.ListenerMemberships.Single(m=>m.StationId==stations[0]).Providers);
        Assert.Empty(graph.ListenerMemberships.SingleOrDefault(m=>m.StationId==stations[4])?.Providers ?? []);
        Assert.Equal(new[]{0,10,10,10,0},stations.Select(id=>StationCatchment.Demand(graph,id,new(4860))));
        s=Build(s,new(14,13,2)); // Equal truncated distance 1: replaces distance 6, then another equal is rejected below.
        s=Build(s,new(13,12,2));s=Build(s,new(14,11,2));
        var tied=s.World.Stations!.Listeners.Single().Stations;
        s=Build(s,new(15,13,2));Assert.Equal(tied,s.World.Stations!.Listeners.Single().Stations);
        s=HeatNext(s);var before=SaveCodec.Write(s);var reopened=Open(before);var snap=MixedWire.Snapshot(s,1);
        var local=s.World.Stations!.Listeners.Single().Stations;var reverse=s.World.Stations.ListenerMemberships;
        s.AdvanceOneMinute();reopened.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));Assert.Equal(30,s.World.Stations!.Listeners.Single().Provider!.LandValue);
        Assert.Equal(local,s.World.Stations.Listeners.Single().Stations);Assert.Equal(JsonSerializer.Serialize(reverse),JsonSerializer.Serialize(s.World.Stations.ListenerMemberships));
        Assert.Equal(Open(before).World.City!.Structure(owner.Id).Revision+1,s.World.City!.Structure(owner.Id).Revision);
        MixedWire.ValidateTransition(Open(before),s);
        Assert.IsType<DeltaMessage>(StateDifference.Between(snap,MixedWire.Snapshot(s,1)));
        s=FixedCityConstruction.Commit(s,FixedCityConstruction.PreviewRemoval(Capture(s),At,owner.Id,s.World.City.Structure(owner.Id).Revision));
        Assert.Empty(s.World.Stations!.Listeners);Assert.All(s.World.Stations.ListenerMemberships,m=>Assert.Empty(m.Providers));
        s=FixedCityConstruction.Commit(s,FixedCityConstruction.Preview(Capture(s),At,Apartment.Reference,selectedHeight:2));
        Assert.Empty(s.World.City!.State.Producers);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
        for(var n=0;n<15000&&s.World.City.State.Producers.IsEmpty;n++)s.AdvanceOneMinute();
        var rebuilt=s.World.City.State.Structures.Single();Assert.NotEqual(owner.Id,rebuilt.Id);Assert.Empty(rebuilt.Construction);
        var provider=s.World.Stations!.Listeners.Single().Provider!;Assert.Equal(240,provider.Population.Residents);
        Assert.Equal(CityHeat.Value(s.World,At),provider.LandValue);
        Assert.Equal(4,s.World.Stations.Listeners.Single().Stations.Length);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
    }
    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public void Real_footprint_and_height_advertising_cube_differs_from_attach_sphere_without_graph_repair(bool stationFirst,bool vertical)
    {
        var at=vertical ? new Location(24,12,15) : new Location(24,22,2);
        var s=Completed(stationAt:stationFirst?at:null,elevatedStationAt:vertical?at:null);
        if(!stationFirst)s=Build(s,at);
        var id=s.World.Stations!.Buildings.Single().Id;
        Assert.Equal(stationFirst?Array.Empty<long>():new[]{id},s.World.Stations.Listeners.Single().Stations);
        Assert.Equal(stationFirst?0:10,StationCatchment.Demand(s.World.Stations,id,new(4860)));
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
        var provider=s.World.Stations.Listeners.Single().Provider!;var bytes=SaveCodec.Write(s);
        Assert.Throws<ArgumentException>(()=>StationCatchment.Update(s.World,s.World.City!.State.Producers.Single().OwnerId,provider with {ZSize=8}));
        Assert.Throws<ArgumentException>(()=>StationCatchment.Update(s.World,s.World.City!.State.Producers.Single().OwnerId,provider with {Special=true}));
        Assert.Equal(bytes,SaveCodec.Write(s));
    }
    [Fact]
    public void All_27_independent_city_provider_calendar_height_share_and_cap_vectors_bind_real_queries()
    {
        using var f=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/city-fixtures.json")));
        var count=0;
        foreach(var row in f.RootElement.GetProperty("groups").GetProperty("providers").GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var time=Enumerable.Range(0,365).Select(day=>new GameTime(day*GameTime.Day+input.GetProperty("hour").GetInt32()*60))
                .First(t=>(input.GetProperty("day").GetString()=="Saturday"?t.DayOfWeek==6:!t.IsWeekend)&&t.IsHoliday==input.GetProperty("holiday").GetBoolean());
            var p=new Population("MultiplierPopulation",Factor:input.GetProperty("height").GetInt32(),Children:[new("ResidentialPopulation",input.GetProperty("base").GetInt32())]);
            var provider=new StationProvider(p,input.GetProperty("land").GetInt32(),input.GetProperty("xy").GetInt32(),input.GetProperty("height").GetInt32(),Input:"city-variable");
            Assert.Equal(expected.GetProperty("residents").GetInt32(),p.Residents);
            Assert.Equal(expected.GetProperty("outgoing").GetInt32(),p.Outgoing(time));Assert.Equal(expected.GetProperty("entering").GetInt32(),p.Incoming(time));
            Assert.Equal(expected.GetProperty("perStation").GetInt32(),provider.Share(time,input.GetProperty("stations").GetInt32()));count++;
        }
        Assert.Equal(27,count);
    }
    [Fact]
    public void All_129_registered_building_bindings_retain_exact_actual_producer_identity_geometry_and_hourly_queries()
    {
        var profile=RailwayProfiles.Bundled.City!;var count=0;var producers=0;
        foreach(var d in profile.Structures)
        {
            var height=d.MaximumHeight;
            var owner=new CityStructure(1,0,d.Reference,new(20,20,2),height,true,new(0,0),
                d.Kind==CityStructureKind.Half?[new(d.Reference,HalfSide.Back,0,0)]:[],[]);
            var empty=CityGraph.Empty(profile,new WorldSpace(80,32,64));
            var graph=new CityGraph(profile,empty.State with {Structures=[owner],Producers=d.Population==null?[]:[new(1,d.Reference,height)]});
            var world=CityStateTests.World(graph.State);
            if(d.Population!=null)
            {
                world=StationCatchment.Attach(world,1,graph.Provider(1,0));
                var provider=world.Stations!.Listeners.Single().Provider!;
                Assert.Equal("city-"+d.Kind.ToString().ToLowerInvariant(),provider.Input);
                Assert.Equal(d.Area,provider.XYSize);Assert.Equal(d.Kind==CityStructureKind.Half?1:height,provider.ZSize);
                foreach(var minute in new long[]{1980,4860,9180,12000})
                {
                    var time=new GameTime(minute);var scale=d.Kind==CityStructureKind.Variable?height:1;
                    Assert.Equal(d.Population.Residents*scale,provider.Population.Residents);
                    Assert.Equal(d.Population.Outgoing(time)*scale,provider.Population.Outgoing(time));
                    Assert.Equal(d.Population.Incoming(time)*scale,provider.Population.Incoming(time));
                }
                producers++;
            }
            else Assert.Empty(graph.State.Producers);
            count++;
        }
        Assert.Equal(129,count);Assert.Equal(112,producers);
    }
    [Fact]
    public void Heat_refresh_owner_overflow_discards_entire_minute_and_keeps_queue_random_accounts_and_both_graphs()
    {
        var s=HeatNext(Completed());var graph=s.World.City!;var owner=graph.State.Structures.Single();
        graph=new(graph.Profile,graph.State with {Structures=[owner with {Revision=long.MaxValue}]});
        var w=s.World;var entities=w.Spatial.Entities.Select(e=>e.Id==owner.Id?e with {Revision=long.MaxValue}:e).ToImmutableArray();
        w=new(w.Profile,w.Terrain,new(w.Spatial.NextEntityId,entities),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,w.Stations,graph);
        s=new(new(w,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
        var before=SaveCodec.Write(s);Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());Assert.Equal(before,SaveCodec.Write(s));
    }
    [Fact]
    public void Population_refresh_fixture_has_real_completed_supply_and_atomic_heat_continuation()
    {
        var s=Completed();var clock=s.Clock.Capture();
        // January 4 weekday 09:00: residents 600, outgoing 96*5=480,
        // incoming 12*5=60 (120*10/100, independently from outgoing).
        s=new(s.Construction,s.Provenance,clock:clock with {AbsoluteMinute=4860},random:s.Random.Capture(),revision:s.Revision);
        s=HeatNext(s);var initial=SaveCodec.Write(s);var snapshot=MixedWire.Snapshot(s,1);var restored=Open(initial);
        s.AdvanceOneMinute();restored.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var owner=s.World.City!.State.Structures.Single();var population=s.World.City.ProducerPopulation(owner.Id);
        Assert.Equal(600,population.Residents);Assert.Equal(480,population.Outgoing(s.Clock.Time));
        using var host=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var read=host.Snapshot();var q=new CityQuery(host.WorldEpoch,read.Revision,read.Tick,read.ContentVersion,owner.Id,owner.Revision);
        var original=SaveCodec.Write(s);var info=host.PreviewCity(q).Population!;
        Assert.Equal((600,480,30),(info.Residents,info.Outgoing,info.LandValue));Assert.Equal(60,info.Incoming);
        Assert.Equal(original,SaveCodec.Write(s));
        if(Environment.GetEnvironmentVariable("FREETRAIN_CITY_POPULATION_OUTPUT") is {} path)
        {
            File.WriteAllBytes(path,initial);var options=WireJson.CreateOptions();
            File.WriteAllText(path+".before.json",JsonSerializer.Serialize<WireMessage>(snapshot,options));
            File.WriteAllText(path+".snapshot.json",JsonSerializer.Serialize<WireMessage>(MixedWire.Snapshot(s,1),options));
            File.WriteAllText(path+".delta.json",JsonSerializer.Serialize<WireMessage>(StateDifference.Between(snapshot,MixedWire.Snapshot(s,1)),options));
        }
    }
}
