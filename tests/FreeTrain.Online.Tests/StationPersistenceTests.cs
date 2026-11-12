using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;

namespace FreeTrain.Online.Tests;

public class StationPersistenceTests
{
    static string Json<T>(T value) => JsonSerializer.Serialize(value,WireJson.CreateOptions());
    static MixedConstructionState Tick(MixedSessionState s,string h) => StationLifecycle.Tick(s,h,g=>g);
    static MixedSessionState Decode(byte[] bytes,StationCallback? callback=null) =>
        SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve,stationCallback:callback??Tick).Mixed!;
    internal static MixedSessionState Scene()
    {
        var buildings=Enumerable.Range(1,5).Select(id=>Building(id,id==1?[11,10]:[]) with {
            Location=new(10+id*2,12,2),Name=$"駅 {id}",Passengers=new(7,11,13),
            Imports=new(17,19,23.125,29.5),Exports=new(31,37,41.75,43.25),Trains=new(47,53,59.5,61.125)});
        var thin=Platform(10,host:1,firstTraffic:100,roof:true) with {Name="ホーム A"};
        var wide=Platform(11,kind:PlatformKind.Wide,host:1,firstTraffic:110,lane1:true,location:new(24,24,2));
        thin=thin with {Yards=thin.Yards.SetItem(0,thin.Yards[0] with {Outlook=new(PlatformOutlookKind.Stair,PassageMaterial.Green,Upward:true)})};
        var graph=new StationGraph(Profile,buildings,[thin,wide],
            [new(140,new(26,16,2),Heading.East,PassageMaterial.Brown),new(141,new(28,19,2),Heading.East,PassageMaterial.Green)],
            17,23,[new(140,[5,2,3,4]),new(141,[2,1])],[new(2,[141,140]),new(3,[140]),new(4,[140]),new(1,[141])]);
        var schedule=StationLifecycle.Reconcile(null,graph,new(517,9007199254740993,[]));
        var world=World(schedule.Graph);
        return new(new(world,new()),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,world.Terrain.Name,new(64,24,8),0)),
            clock:schedule.Clock,stationCallback:Tick);
    }
    [Fact]
    public void Default_dispatch_after_save_restore_runs_passenger_decay_logs_and_upkeep_exactly_once()
    {
        var s=SaveCodec.ReadWorld(SaveCodec.Write(Scene()),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        // Omit the fixture's injected dispatcher, as production checkpoint loading does.
        var restored=SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        for(var minute=517;minute<1957;minute++) {s.AdvanceOneMinute();restored.AdvanceOneMinute();}
        Assert.Equal(1957,restored.Clock.Time.AbsoluteMinute);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.All(restored.World.Stations!.Buildings,b=> {
            Assert.Equal(new StationPassengers(),b.Passengers);
            Assert.Equal(0,b.Imports.Today);Assert.Equal(17,b.Imports.Yesterday);
            Assert.Equal(23.125+Math.Pow(17,0.2),b.Imports.ThisWeek);
            Assert.Equal(31,b.Exports.Yesterday);Assert.Equal(47,b.Trains.Yesterday);
        });
        var reopened=SaveCodec.ReadWorld(SaveCodec.Write(restored),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        s.AdvanceOneMinute();reopened.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));
    }
    [Fact]
    public void Exact_detached_roundtrip_preserves_recipes_logs_asymmetric_listeners_names_and_repeated_FIFO()
    {
        var s=Scene();var bytes=SaveCodec.Write(s);var restored=Decode(bytes);var g=restored.World.Stations!;
        Assert.Equal(11,SaveCodec.Version);Assert.Equal(10,ProtocolIdentity.Version);
        Assert.Equal(bytes,SaveCodec.Write(restored));Assert.NotSame(s.World.Stations,g);
        Assert.Equal(new long[]{11,10},g.Building(1).Children);
        Assert.Equal(new long[]{5,2,3,4},g.Listeners[0].Stations);
        Assert.DoesNotContain(g.ListenerMemberships,m=>m.StationId==5);
        Assert.Equal(new long[]{141,140},g.ListenerMemberships[0].Providers);
        Assert.Equal(new StationTransportLog(17,19,23.125,29.5),g.Building(1).Imports);
        Assert.Equal(9007199254740993,g.Building(1).HourTimer!.Registration);
        Assert.Null(restored.Fleet);Assert.NotNull(MixedWire.Read(restored).Queue);
        for(var i=0;i<924;i++){s.AdvanceOneMinute();restored.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.Equal(new long[]{5,2,3,4},restored.World.Stations!.Listeners[0].Stations);
        Assert.Equal(new long[]{141,140},restored.World.Stations.ListenerMemberships[0].Providers);
        Assert.Equal(1441,restored.Clock.Time.AbsoluteMinute);
        Assert.True(g.Building(1).HourTimer!.Order<restored.World.Stations!.Building(1).HourTimer!.Order);
        var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
        var purchased=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,3,1));
        var fleetSave=SaveCodec.Write(purchased);var withFleet=Decode(fleetSave);
        Assert.Equal(fleetSave,SaveCodec.Write(withFleet));Assert.Single(withFleet.Fleet!.Trains);
        Assert.Equal(3,withFleet.Fleet.Trains[0].Cars.Length);
        Assert.Equal(Json(s.Clock.Capture()),Json(withFleet.Clock.Capture()));
        Assert.Equal(new long[]{5,2,3,4},withFleet.World.Stations!.Listeners[0].Stations);
        // The decoded DTO arrays cannot mutate either immutable world.
        var dto=MixedWire.Read(s);dto.StationGraph!.Buildings[0].Children[0]=999;
        Assert.Equal(11,s.World.Stations!.Building(1).Children[0]);
    }
    [Fact]
    public void Independent_literal_fixture_matches_the_server_snapshot_and_reopens_exactly()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/stations-v11.json"));
        var loaded=Decode(bytes);var expected=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/snapshot-stations.json"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected),JsonNode.Parse(Json<WireMessage>(MixedWire.Snapshot(loaded,1)))));
        Assert.Equal(SaveCodec.Write(loaded),SaveCodec.Write(Decode(SaveCodec.Write(loaded))));
        Assert.Equal(new long[]{3,2},loaded.World.Stations!.Building(1).Children);
        Assert.Empty(loaded.World.Stations.ListenerMemberships);
    }
    [Fact]
    public void Listener_changes_require_provider_and_station_revisions_and_wire_limits_are_bounded()
    {
        var s=Scene();var w=s.World;var g=w.Stations!;
        var dto=StationWire.Read(g)!;
        Assert.Throws<ArgumentException>(()=>StationWire.Restore(dto with {Platforms=Enumerable.Repeat(dto.Platforms[0],8193).ToArray()},Profile));
        Assert.Throws<ArgumentException>(()=>StationWire.Restore(dto with {Listeners=[dto.Listeners[0] with {Stations=[1,2,3,4,5]}]},Profile));
        var changed=new StationGraph(Profile,g.Buildings,g.Platforms,g.Passages,g.NextStationName,g.NextPlatformName,
            g.Listeners.Select(l=>l.ProviderId==140?l with {Stations=[2,3,4,5]}:l),g.ListenerMemberships);
        Assert.Throws<ArgumentException>(()=>w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,stations:changed));
        var spatial=w.Spatial.Capture() with {Entities=w.Spatial.Entities.Select(e=>e.Id==140?e with {Revision=1}:e).ToImmutableArray()};
        Assert.NotNull(w.Replace(w.Revision,w.Terrain,spatial,w.Voxels,w.Slopes,stations:changed));
        changed=new(Profile,g.Buildings,g.Platforms,g.Passages,g.NextStationName,g.NextPlatformName,g.Listeners,
            g.ListenerMemberships.Select(l=>l.StationId==2?l with {Providers=[140,141]}:l));
        Assert.Throws<ArgumentException>(()=>w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,stations:changed));
    }
    [Theory]
    [InlineData("host")][InlineData("children")][InlineData("definition")][InlineData("bell")][InlineData("yard")]
    [InlineData("timer")][InlineData("queue")][InlineData("missingQueue")][InlineData("listener")][InlineData("provider")]
    [InlineData("membership")][InlineData("numeric")][InlineData("logs")][InlineData("oldVersion")]
    public async Task Malformed_graph_or_missing_content_rejects_without_replacing_world_or_mutating_input(string bad)
    {
        var s=Scene();var bytes=SaveCodec.Write(s);var n=JsonNode.Parse(bytes)!;var m=n["mixed"]!;var g=m["stationGraph"]!;
        switch(bad){
            case "host":g["platforms"]![0]!["hostId"]="10";break;
            case "children":g["buildings"]![0]!["children"]=new JsonArray();break;
            case "definition":g["buildings"]![0]!["definition"]!["recipeId"]="missing";break;
            case "bell":g["platforms"]![0]!["bell"]!["contributionId"]="missing";break;
            case "yard":g["platforms"]![0]!["yards"]![0]!["trafficOwnerId"]="999";break;
            case "timer":g["buildings"]![0]!["hourTimer"]!["order"]="9007199254740994";break;
            case "queue":m["queue"]!["nextOrder"]="0";break;
            case "missingQueue":m.AsObject().Remove("queue");break;
            case "listener":g["listeners"]![0]!["stations"]=new JsonArray("5","5");break;
            case "provider":g["listeners"]![0]!["providerId"]="999";break;
            case "membership":g["listenerMemberships"]![0]!["providers"]=new JsonArray("999");break;
            case "numeric":g["buildings"]![0]!["id"]=1;break;
            case "logs":g["buildings"]![0]!["imports"]!["thisWeek"]=-1;break;
            case "oldVersion":n["saveVersion"]=9;break;
        }
        var corrupt=System.Text.Encoding.UTF8.GetBytes(n.ToJsonString());var retained=corrupt.ToArray();
        Assert.Throws<InvalidDataException>(()=>Decode(corrupt));
        var host=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var before=Json(host.Snapshot());var load=host.LoadSave(corrupt,1);host.Pump();
        await Assert.ThrowsAsync<InvalidDataException>(()=>load);
        Assert.Equal(before,Json(host.Snapshot()));Assert.Equal(bytes,SaveCodec.Write(s));Assert.Equal(retained,corrupt);
    }
    [Fact]
    public void Chunks_and_complete_delta_preserve_empty_graph_name_high_water_and_coherent_removals()
    {
        var s=Scene();var before=MixedWire.Snapshot(s,1);var graph=new StationGraph(Profile,[],[],nextStationName:17,nextPlatformName:23);
        var schedule=StationLifecycle.Reconcile(s.World.Stations,graph,s.Clock.Capture());var empty=World(schedule.Graph);
        var world=s.World.Replace(0,s.World.Terrain,empty.Spatial.Capture() with {NextEntityId=s.World.Spatial.NextEntityId},[],[],stations:schedule.Graph);
        var next=s.Successor(s.Revision,new(world,s.Construction.Accounts),false,clock:schedule.Clock);
        var after=MixedWire.Snapshot(next,1);var delta=StateDifference.Between(before,after);
        Assert.Equal(before.Entities!.Select(e=>e.Id).Order(),delta.Removed.EntityIds!.Order());
        Assert.Empty(delta.Mixed!.StationGraph!.Buildings);Assert.Equal(17,delta.Mixed.StationGraph.NextStationName);
        Assert.Empty(delta.Mixed.Queue!.Pending);Assert.Equal(s.Clock.NextOrder,delta.Mixed.Queue.NextOrder);
        Assert.Equal(SaveCodec.Write(next),SaveCodec.Write(Decode(SaveCodec.Write(next))));
        var chunks=StateTransfer.Chunks(before);Assert.Equal(StateTransfer.Serialize(before),chunks.SelectMany(c=>c.Data).ToArray());
        using var payload=JsonDocument.Parse(StateTransfer.Serialize(before));
        Assert.Equal(5,payload.RootElement.GetProperty("message").GetProperty("mixed").GetProperty("stationGraph").GetProperty("buildings").GetArrayLength());
    }
    [Fact]
    public async Task Two_peers_roles_revisions_receipts_and_save_replacement_share_one_station_successor()
    {
        SessionHost host=null!;
        var network=new NetworkSession(new(),hostFactory:(epoch,_)=>host=new(Scene(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve,worldEpoch:epoch));
        var now=DateTimeOffset.UtcNow;var owner=network.Connect(now,new("owner"))!;
        var observer=network.Connect(now,new("observer",ParticipantRole.Observer))!;
        var a=Assert.IsType<JoinMessage>(await owner.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));var b=Assert.IsType<JoinMessage>(await observer.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.Equal(Json(a.Snapshot),Json(b.Snapshot));Assert.NotNull(a.Snapshot.Mixed!.StationGraph);
        var denied=network.Submit(observer,new("denied",1,new StepCommand()),now);network.Tick(now=now.AddMilliseconds(100));
        Assert.IsType<RejectedMessage>(await denied);
        var command=new CommandMessage("station-step",1,new StepCommand());var accepted=network.Submit(owner,command,now);network.Tick(now=now.AddMilliseconds(100));
        Assert.IsType<AcceptedMessage>(await accepted);
        var first=Assert.IsType<DeltaMessage>(await owner.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));var second=Assert.IsType<DeltaMessage>(await observer.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));Assert.Equal(Json(first),Json(second));
        var duplicate=network.Submit(owner,command,now);network.Tick(now=now.AddMilliseconds(100));Assert.Equal(Json(await accepted),Json(await duplicate));
        Assert.Equal(518,host.Snapshot().Tick);
        var capture=network.CaptureSave();network.Tick(now=now.AddMilliseconds(100));var bytes=await capture;
        var load=host.LoadSave(bytes,1);network.Tick(now=now.AddMilliseconds(100));await load;
        foreach(var peer in new[]{owner,observer}){var read=Assert.IsType<SnapshotMessage>(await peer.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)));Assert.Equal(2,read.WorldEpoch);Assert.Equal(Json(first.Mixed),Json(read.Mixed));}
        var stale=network.Submit(owner,new("stale",1,new StepCommand()),now);network.Tick(now=now.AddMilliseconds(100));Assert.IsType<RejectedMessage>(await stale);
        // Trusted station dispatch survives host preparation/replacement; no delegate is read from input.
        var loaded=Decode(bytes);for(var i=0;i<22;i++)loaded.AdvanceOneMinute();
        for(var i=0;i<22;i++){var step=network.Submit(owner,new("after-"+i,2,new StepCommand()),now);network.Tick(now=now.AddMilliseconds(100));Assert.IsType<AcceptedMessage>(await step);}
        Assert.Equal(Json(MixedWire.Read(loaded)),Json(host.Snapshot().Mixed));
    }
}
