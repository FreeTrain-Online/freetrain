using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class FleetPersistenceTests
{
    static byte[] Fixture => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v9.json"));
    // Controlled dispatcher verifies restored ownership/FIFO, without claiming motion.
    static TrainCallbackResult Wait(MixedSessionState s,long id) => new(s.Construction,2);
    static MixedSessionState Decode(byte[] data,TrainCallback? callback=null) => SaveCodec.ReadWorld(data,TerrainProfiles.Resolve,RailwayProfiles.Resolve,callback??Wait).Mixed!;
    static string Json<T>(T value) => JsonSerializer.Serialize(value,WireJson.CreateOptions());
    [Fact]
    public void Literal_fleet_save_and_snapshot_preserve_exact_shared_graph_and_queue()
    {
        var s=Decode(Fixture);var written=SaveCodec.Write(s);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Fixture),JsonNode.Parse(written)));
        Assert.Equal(written,SaveCodec.Write(Decode(written)));
        var expected=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/snapshot-fleet.json"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected),JsonNode.Parse(Json<WireMessage>(MixedWire.Snapshot(s,1)))));
        Assert.Equal(9007199254741003,s.World.Spatial.NextEntityId);
        Assert.Equal(9007199254740993,Assert.Single(s.Clock.Capture().Pending).Registration);
        Assert.Equal(new long[]{19,18},s.Fleet!.Train(17).Cars.Select(c=>c.Id));
        Assert.Equal(15,s.Fleet.EffectiveController(17).Id);
        Assert.Equal(new Location(-123,456,2),s.Fleet.Controllers.Single(c=>c.Id==15).Harbors[0].Location);
        Assert.Null(s.Fleet.Train(20).PurchaseMinute);Assert.Equal(int.MaxValue,s.Fleet.Train(17).MoveCount);
        var restored=Decode(written);
        s.AdvanceOneMinute();restored.AdvanceOneMinute();
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.Equal(130084,Assert.Single(restored.Clock.Capture().Pending).DueMinute);
        // Queue changes are published even if the immutable construction object stays identical.
        Assert.Equal(130084,Assert.Single(MixedWire.Snapshot(restored,1).Mixed!.Queue!.Pending).DueMinute);
    }
    [Fact]
    public void Deployed_restore_binds_production_dispatcher_and_never_activates_input_code()
    {
        var deployed=SaveCodec.ReadWorld(Fixture,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal((TrainCallback)FleetMovement.Tick,deployed.TrainDispatcher);
        Assert.Equal(SaveCodec.Write(Decode(Fixture)),SaveCodec.Write(deployed));
        var save=JsonNode.Parse(Fixture)!;save["mixed"]!["fleet"]!["trains"]!.AsArray().RemoveAt(0);
        save["mixed"]!["fleet"]!["groups"]![1]!["members"]=new JsonArray();
        save["mixed"]!["voxels"]![0]!["traffic"]!["car"]=null;
        save["clock"]!["pending"]=new JsonArray();save["mixed"]!["queue"]!["pending"]=new JsonArray();
        var loaded=SaveCodec.ReadWorld(Encoding.UTF8.GetBytes(save.ToJsonString()),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Single(loaded.Fleet!.Trains);loaded.AdvanceOneMinute();
    }
    [Theory]
    [InlineData("duplicate")][InlineData("cycle")][InlineData("dangling")][InlineData("stock")][InlineData("provider")]
    [InlineData("pose")][InlineData("occupancy")][InlineData("original")][InlineData("passengers")][InlineData("futurePurchase")]
    [InlineData("signal")][InlineData("queueMismatch")][InlineData("unknownAction")][InlineData("pastDue")][InlineData("duplicateAction")]
    [InlineData("repeated")][InlineData("missingAction")][InlineData("extraField")][InlineData("harborDuplicate")][InlineData("harborMode")]
    [InlineData("numeric")][InlineData("allocator")][InlineData("missingQueue")][InlineData("formation")][InlineData("capacity")]
    public async Task Malformed_fleet_rejects_before_world_epoch_or_file_replacement(string bad)
    {
        var node=JsonNode.Parse(Fixture)!;var m=node["mixed"]!;var f=m["fleet"]!;var t=f["trains"]![0]!;var q=m["queue"]!;
        switch(bad)
        {
            case "duplicate":f["trains"]![1]!["id"]="17";break;
            case "cycle":f["groups"]![1]!["parentId"]="16";break;
            case "dangling":t["controllerId"]="999";break;
            case "stock":t["definition"]!["catalogHash"]=new string('0',64);break;
            case "provider":f["controllers"]![3]!["provider"]!["contributionId"]="unknown";break;
            case "pose":t["cars"]![0]!["state"]!["countdown"]=10;break;
            case "occupancy":m["voxels"]![0]!["traffic"]!["car"]!["carId"]="18";break;
            case "original":t["cars"]![0]!["originalIndex"]=0;break;
            case "passengers":t["passengers"]=int.MaxValue;break;
            case "futurePurchase":t["purchaseMinute"]="130082";break;
            case "signal":m["voxels"]![3]!["traffic"]!["rail"]!["controllerId"]="15";break;
            case "queueMismatch":q["nextOrder"]="9007199254740995";break;
            case "unknownAction":q["pending"]![0]!["handlerId"]="CLR/System.Action";node["clock"]!["pending"]![0]!["handlerId"]="CLR/System.Action";break;
            case "pastDue":q["pending"]![0]!["dueMinute"]="130081";node["clock"]!["pending"]![0]!["dueMinute"]="130081";break;
            case "duplicateAction":q["pending"]!.AsArray().Add(q["pending"]![0]!.DeepClone());node["clock"]!["pending"]!.AsArray().Add(node["clock"]!["pending"]![0]!.DeepClone());break;
            case "repeated":q["pending"]![0]!["interval"]="2";node["clock"]!["pending"]![0]!["interval"]="2";break;
            case "missingAction":q["pending"]=new JsonArray();node["clock"]!["pending"]=new JsonArray();break;
            case "extraField":f["controllers"]![0]!["delegate"]="arbitrary";break;
            case "harborDuplicate":f["controllers"]![3]!["harbors"]!.AsArray().Add(f["controllers"]![3]!["harbors"]![0]!.DeepClone());break;
            case "harborMode":f["controllers"]![3]!["harbors"]![0]!["kind"]="unknown";break;
            case "numeric":q["nextOrder"]=9007199254740994L;break;
            case "allocator":m["nextEntityId"]="20";node["spatial"]!["nextEntityId"]="20";break;
            case "missingQueue":m.AsObject().Remove("queue");break;
            case "formation":t["cars"]![0]!["definition"]!["recipeId"]="unknown";break;
            case "capacity":for(var i=0;i<129;i++)f["trains"]!.AsArray().Add(t.DeepClone());break;
        }
        var bytes=Encoding.UTF8.GetBytes(node.ToJsonString());var original=Fixture.ToArray();
        Assert.Throws<InvalidDataException>(()=>Decode(bytes));
        var host=new FreeTrain.Online.Session.SessionHost(Decode(Fixture),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var before=host.Snapshot();var load=host.LoadSave(bytes,1);host.Pump();
        await Assert.ThrowsAsync<InvalidDataException>(()=>load);
        Assert.Equal(Json(before),Json(host.Snapshot()));Assert.Equal(original,Fixture);
    }
    [Fact]
    public void Complete_delta_has_literal_removals_and_chunked_state_keeps_the_fleet_header()
    {
        var s=Decode(Fixture);var w=s.World;var f=w.Fleet!;
        var nextFleet=new FleetGraph(f.Profile,f.RootGroupId,[f.Train(17) with {Revision=1,Name="Renamed"}],
            f.Groups.Select(g=>g.Id==11?g with{Revision=1,Members=[16]}:g),f.Controllers.Where(c=>c.Id!=14));
        var world=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,fleet:nextFleet);
        var next=s.Successor(s.Revision,new(world,s.Construction.Accounts),s.ConstructionMode);
        // Match the independent successor queue/tick without inventing movement.
        next.AdvanceOneMinute();
        var before=MixedWire.Snapshot(s,1);var after=MixedWire.Snapshot(next,1);
        var expected=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/delta-fleet.json")))!;
        expected["revision"]="8"; // explicit graph mutation + minute advancement
        Assert.True(JsonNode.DeepEquals(expected,JsonNode.Parse(Json<WireMessage>(StateDifference.Between(before,after)))));
        using var packed=JsonDocument.Parse(StateTransfer.Serialize(after));
        Assert.Equal(9007199254740995,packed.RootElement.GetProperty("message").GetProperty("mixed").GetProperty("queue").GetProperty("nextOrder").GetString() is {} order?long.Parse(order):0);
        Assert.Single(after.Mixed!.Fleet!.Trains);Assert.Equal(3,after.Mixed.Fleet.Controllers.Length);
        Assert.Equal(StateTransfer.Serialize(after).Length,StateTransfer.Chunks(after).Sum(c=>c.Data.Length));
    }
    [Fact]
    public async Task Two_live_peers_receive_one_coherent_fleet_and_queue_successor_then_reload()
    {
        FreeTrain.Online.Session.SessionHost host=null!;
        var session=new NetworkSession(new(),hostFactory:(epoch,_)=>host=new(Decode(Fixture),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve,worldEpoch:epoch));
        var now=DateTimeOffset.UtcNow;
        var owner=session.Connect(now,new("owner"))!;
        var observer=session.Connect(now,new("observer",FreeTrain.Online.Session.ParticipantRole.Observer))!;
        var first=Assert.IsType<JoinMessage>(await owner.Output.Reader.ReadAsync());
        var second=Assert.IsType<JoinMessage>(await observer.Output.Reader.ReadAsync());
        Assert.Equal(Json(first.Snapshot),Json(second.Snapshot));
        var step=session.Submit(owner,new("fleet-step",1,new StepCommand()),now);
        session.Tick(now);Assert.IsType<AcceptedMessage>(await step);
        var a=Assert.IsType<DeltaMessage>(await owner.Output.Reader.ReadAsync());
        var b=Assert.IsType<DeltaMessage>(await observer.Output.Reader.ReadAsync());
        Assert.Equal(Json(a),Json(b));Assert.Equal(130082,a.Tick);
        Assert.Equal(130084,Assert.Single(a.Mixed!.Queue!.Pending).DueMinute);
        Assert.Equal(2,a.Mixed.Fleet!.Trains.Length);
        var capture=session.CaptureSave();session.Tick(now.AddMilliseconds(100));var bytes=await capture;
        var prepared=host.PrepareSave(bytes);
        var load=host.ApplyPreparedSave(prepared,1,null);session.Tick(now.AddMilliseconds(200));await load;
        foreach(var peer in new[]{owner,observer}){
            var snapshot=Assert.IsType<SnapshotMessage>(await peer.Output.Reader.ReadAsync());
            Assert.Equal(2,snapshot.WorldEpoch);Assert.Equal(Json(a.Mixed),Json(snapshot.Mixed));
        }
        var restored=Decode(bytes);restored.AdvanceOneMinute();
        var continuation=session.Submit(owner,new("continued",2,new StepCommand()),now.AddMilliseconds(300));
        session.Tick(now.AddMilliseconds(300));Assert.IsType<AcceptedMessage>(await continuation);
        Assert.Equal(Json(MixedWire.Snapshot(restored,2) with {PlaybackRate=1,TrainMotion=new(0,false,[])}),Json(host.Snapshot()));
        foreach(var peer in new[]{owner,observer})Assert.IsType<DeltaMessage>(await peer.Output.Reader.ReadAsync());
        var live=prepared.Mixed!;var w=live.World;var f=w.Fleet!;
        var removal=new FleetGraph(f.Profile,f.RootGroupId,[f.Train(17)],
            f.Groups.Select(g=>g.Id==11?g with{Revision=1,Members=[16]}:g),f.Controllers.Where(c=>c.Id!=14));
        var candidate=new MixedConstructionState(w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,fleet:removal),live.Construction.Accounts);
        var apply=host.ApplyMixed(2,live.Revision,candidate,live.ConstructionMode,owner.Participant);
        session.Tick(now.AddMilliseconds(400));await apply;
        var removed=Assert.IsType<DeltaMessage>(await owner.Output.Reader.ReadAsync());
        Assert.Equal(Json(removed),Json(Assert.IsType<DeltaMessage>(await observer.Output.Reader.ReadAsync())));
        Assert.Single(removed.Mixed!.Fleet!.Trains);Assert.Equal(3,removed.Mixed.Fleet.Controllers.Length);
        Assert.Equal(new long[]{16},removed.Mixed.Fleet.Groups[0].Members);
    }

    [Theory]
    [InlineData("unplaced")][InlineData("moving")][InlineData("stoppingatstation")]
    [InlineData("stoppingatsignal")][InlineData("emergencystopping")]
    public void Every_operational_state_and_each_pose_round_trips_without_enum_or_type_coercion(string state)
    {
        var node=JsonNode.Parse(Fixture)!;var train=node["mixed"]!["fleet"]!["trains"]![0]!;train["state"]=state;
        if(state=="unplaced"){
            foreach(var car in train["cars"]!.AsArray())car!["state"]=new JsonObject{["kind"]="unplaced",["location"]=null,["heading"]=null,["countdown"]=null};
            node["mixed"]!["voxels"]![0]!["traffic"]!["car"]=null;
            node["clock"]!["pending"]=new JsonArray();node["mixed"]!["queue"]!["pending"]=new JsonArray();
        }
        var restored=Decode(Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.True(JsonNode.DeepEquals(node,JsonNode.Parse(SaveCodec.Write(restored))));
    }

    [Fact]
    public void Browser_stock_metadata_agrees_with_registered_identity_capacity_and_every_permitted_length()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/fleet-profile.json")));
        var root=doc.RootElement;var stock=RollingStockCatalog.Bundled;
        Assert.Equal(stock.Trains.Length,root.GetProperty("trains").GetArrayLength());
        foreach(var row in root.GetProperty("trains").EnumerateArray()){
            var b=JsonSerializer.Deserialize<ContentBindingRead>(row.GetProperty("binding"),WireJson.CreateOptions())!;
            var train=stock.ResolveTrain(new(b.PackId,b.Version,b.CatalogHash,b.ContributionId));
            Assert.Equal(train.PricePerCar,row.GetProperty("pricePerCar").GetInt32());
            foreach(var n in train.PermittedLengths){
                var ids=Enumerable.Repeat(row.GetProperty("body").GetString()!,n).ToArray();
                if(row.GetProperty("head").GetString() is {} head)ids[0]=head;
                if(row.GetProperty("tail").GetString() is {} tail)ids[^1]=tail;
                Assert.Equal(train.Create(n).Cars.Select(c=>c.Reference.ContributionId),ids);
            }
        }
        foreach(var row in root.GetProperty("cars").EnumerateArray()){
            var d=JsonSerializer.Deserialize<DefinitionRead>(row.GetProperty("reference"),WireJson.CreateOptions())!;
            var car=stock.ResolveCar(new(d.PackId,d.ContributionId,d.RecipeId));
            Assert.Equal(car.NormalCapacity,row.GetProperty("normal").GetInt32());
            Assert.Equal(car.SeatedCapacity,row.GetProperty("seated").GetInt32());
        }
        Assert.Equal(stock.FleetProfile.Providers.Length,root.GetProperty("providers").GetArrayLength());
        foreach(var row in root.GetProperty("providers").EnumerateArray()){
            var b=JsonSerializer.Deserialize<ContentBindingRead>(row.GetProperty("binding"),WireJson.CreateOptions())!;
            Assert.Contains(stock.FleetProfile.Providers,p=>p.Binding==new ContentBinding(b.PackId,b.Version,b.CatalogHash,b.ContributionId)&&
                p.Kind.ToString().ToLowerInvariant()==row.GetProperty("kind").GetString());
        }
    }

    [Fact]
    public async Task Fleet_payload_byte_limit_is_symmetric_for_writing_and_live_adoption()
    {
        var s=Decode(Fixture);var w=s.World;var f=w.Fleet!;
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        var harbors=System.Collections.Immutable.ImmutableArray.CreateRange(Enumerable.Range(0,8192)
            .Select(n=>new FleetHarborSetting(new(n,999,2),FleetHarborKind.Fixed,10,false)));
        var extra=Enumerable.Range(0,16).Select(n=>new FleetController(w.Spatial.NextEntityId+n,0,"Timetable",provider.Kind,provider.Binding,harbors));
        var fleet=new FleetGraph(f.Profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers.Concat(extra));
        var world=w.Replace(w.Revision,w.Terrain,new(w.Spatial.NextEntityId+16,w.Spatial.Entities),w.Voxels,w.Slopes,fleet:fleet);
        var candidate=new MixedConstructionState(world,s.Construction.Accounts);
        var proposed=s.Successor(s.Revision,candidate,s.ConstructionMode);
        Assert.Equal("saveTooLarge",Assert.Throws<InvalidDataException>(()=>SaveCodec.Write(proposed)).Message);
        var host=new FreeTrain.Online.Session.SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var before=Json(host.Snapshot());
        var adopt=host.ApplyMixed(1,s.Revision,candidate,s.ConstructionMode,new("owner"));host.Pump();
        Assert.Equal("saveTooLarge",(await Assert.ThrowsAsync<InvalidDataException>(()=>adopt)).Message);
        Assert.Equal(before,Json(host.Snapshot()));
    }

}
