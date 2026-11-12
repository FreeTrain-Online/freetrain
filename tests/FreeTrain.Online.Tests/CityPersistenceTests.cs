using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class CityPersistenceTests
{
    static RailwayProfile Rail=>RailwayProfiles.Bundled;
    static CityProfile City=>Rail.City!;
    static CityStructureDefinition Definition=>City.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX==1&&d.SizeY==1&&d.MinimumHeight==1&&d.Population!=null&&!d.ComputerCannotBuild);
    static TerrainProvenance Provenance(TerrainWorld t)=>TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,t.Name,
        new(t.Space.HorizontalSize,t.Space.VerticalSize,t.Space.Levels),t.WaterLevel));
    internal static MixedSessionState Fixture(bool callbacks=true,bool consumers=true)
    {
        var buildings=Enumerable.Range(1,5).Select(i=>StationStateTests.Building(i) with {Location=new(i==5?13:13-i,12,2)});
        var basis=StationStateTests.World(StationStateTests.Graph(buildings)).EnableFleet(Rail.FleetProfile!);
        const long minute=1980; // Monday 09:00, fixture clock; no seed regeneration.
        var d=Definition;
        var completed=new CityStructure(10,0,d.Reference,new(14,12,2),1,true,new(123456789,20),[],[]);
        var a=new ScheduledAction(0,0,minute+1,CityHandlers.Construction(20,new(0,0,0)),0);
        var site=new CityStructure(20,0,d.Reference,new(22,22,2),1,false,null,[],[new(new(0,0,0),new(3,0,false),a)],CityConstructionCaller.CatalogPlayer);
        var half=City.Structures.First(d=>d.Kind==CityStructureKind.Half);
        var variable=City.Structures.First(d=>d.Kind==CityStructureKind.Variable&&d.SizeX==1&&d.SizeY==1&&d.MinimumHeight<6);
        var ground=City.Ground.First(d=>d.Handler=="land-0d"&&d.Population!=null);
        var fence=City.Ground.First(d=>d.Handler=="fence"&&!City.Bindings.Single(b=>b.Reference==d.Reference).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal));
        var state=CityGraph.Empty(City,basis.Terrain.Space).State;
        var structures=new[]{completed,new CityStructure(11,0,half.Reference,new(18,18,2),half.MinimumHeight,true,new(80,20),[new(half.Reference,HalfSide.Back,0)],[]),
            new CityStructure(12,0,variable.Reference,new(25,24,2),variable.MinimumHeight,false,new(90,20),[],[])};
        state=state with {Properties=[new(13,0,new(20,20,2))],Structures=structures.Append(site).ToImmutableArray(),
            Producers=structures.Where(s=>City.Structure(s.Definition).Population!=null).Select(s=>new CityProducer(s.Id,s.Definition,s.Height)).Append(new(14,ground.Reference,1)).ToImmutableArray(),
            Ground=[new(14,0,ground.Reference,new(21,20,2),false)],FenceHosts=[new(15,0,new(20,21,2))],Fences=[new(15,new(20,21,2),0,fence.Reference)],
            Heat=state.Heat with {Q=state.Heat.Q.SetItem(0,-12.5f),Back=state.Heat.Back.SetItem(0,8.25f),Rho=state.Heat.Rho.SetItem(0,0.25f)},
            Plans=[new(1,CitySearchPhase.Build,24,7,[1,3,1,3],new(24,21,2),new(d.Reference,new(24,21,2),1,1,1,d.UnitPrice,d.UnitPrice))],
            LandCache=[new(new(24,21,2),1,1,20,500)]};
        if(callbacks)state=state with {HeatCallback=new(1,1,minute+20,CityHandlers.Heat,600),DevelopmentCallback=new(2,2,minute+20,CityHandlers.Development,60),
            Structures=state.Structures.Replace(completed,completed with {Subsidiary=completed.Subsidiary! with {Callback=new(3,3,minute+20,CityHandlers.Subsidiary(10),0)}})};
        var world=CityStateTests.World(state,basis);
        world=StationCatchment.Attach(world,10,world.City!.Provider(10,7));
        foreach(var id in new long[]{11,12,14}) world=StationCatchment.Attach(world,id,world.City!.Provider(id,7));
        var pending=world.City!.Callbacks.OrderBy(t=>t.DueMinute).ThenBy(t=>t.Order).ToImmutableArray();
        return new(new(world),Provenance(world.Terrain),clock:new(minute,callbacks?4:1,pending),random:new RandomStreams(73).Capture(),revision:world.Revision,
            cityLandValue:consumers?(_,_)=>20:null);
    }
    static MixedSessionState Read(byte[] bytes,bool consumers=true)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve,
        cityLandValue:consumers?(_,_)=>20:null).Mixed!;
    static SessionHost Host(MixedSessionState state)=>new(state,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static async Task<byte[]> Save(SessionHost h){var task=h.CaptureSave();h.Pump(0);return await task;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,ParticipantContext? p=null,long? epoch=null)
    {var task=h.Enqueue(p??new("owner"),new(Guid.NewGuid().ToString(),epoch??h.WorldEpoch,payload));h.Pump(0);return await task;}
    static CityQuery Query(SnapshotMessage s)=>new(s.WorldEpoch,s.Revision,s.Tick,s.ContentVersion,20,s.Mixed!.City!.Structures.Single(o=>o.Id==20).Revision);

    [Fact]
    public void Current_save_roundtrip_preserves_all_city_fields_queue_random_and_both_asymmetric_listener_directions()
    {
        var before=Fixture();var bytes=SaveCodec.Write(before);var after=Read(bytes);
        if(Environment.GetEnvironmentVariable("FREETRAIN_CITY_OUTPUT") is {} path)
        {
            File.WriteAllBytes(path,bytes);
            File.WriteAllText(path+".snapshot.json",JsonSerializer.Serialize<WireMessage>(MixedWire.Snapshot(before,1),WireJson.CreateOptions()));
            var removed=CityConstruction.Remove(new(before.Construction,before.Clock.Capture(),before.Random.Capture()),20);
            var successor=before.Successor(before.Revision,removed.Construction,before.ConstructionMode,removed.Random,removed.Clock);
            File.WriteAllText(path+".delta.json",JsonSerializer.Serialize<WireMessage>(StateDifference.Between(MixedWire.Snapshot(before,1),MixedWire.Snapshot(successor,1)),WireJson.CreateOptions()));
        }
        Assert.Equal(11,SaveCodec.Version);Assert.Equal(10,ProtocolIdentity.Version);
        Assert.Equal(bytes,SaveCodec.Write(after));
        Assert.Equal(new long[]{1,2,3,5},after.World.Stations!.Listeners.Single(l=>l.ProviderId==10).Stations);
        Assert.DoesNotContain(10,after.World.Stations!.ListenerMemberships.SingleOrDefault(m=>m.StationId==5)?.Providers??[]);
        Assert.Equal("city-fixed",after.World.Stations!.Listeners.Single(l=>l.ProviderId==10).Provider!.Input);
        Assert.Equal(-12.5f,after.World.City!.State.Heat.Q[0]);Assert.Equal(8.25f,after.World.City.State.Heat.Back[0]);
        Assert.Equal(500,Assert.Single(after.World.City.State.LandCache).EntityValue);
        var a=MixedWire.Snapshot(before,1);var b=MixedWire.Snapshot(after,1);
        Assert.Equal(JsonSerializer.Serialize(a,WireJson.CreateOptions()),JsonSerializer.Serialize(b,WireJson.CreateOptions()));
        before.AdvanceOneMinute();after.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(before),SaveCodec.Write(after));
    }
    [Fact]
    public void Restored_site_callbacks_and_random_continue_exactly_to_fresh_completion_identity_without_double_completion()
    {
        var before=Fixture(false);var after=Read(SaveCodec.Write(before));
        for(var i=0;i<1200&&before.World.City!.State.Structures.Any(s=>s.Id==20);i++)
        {before.AdvanceOneMinute();after.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(before),SaveCodec.Write(after));}
        Assert.DoesNotContain(before.World.City!.State.Structures,s=>s.Id==20);
        Assert.Single(before.World.City.State.Structures.Where(s=>s.Id>20));
        Assert.DoesNotContain(before.Clock.Capture().Pending,a=>a.HandlerId.StartsWith("city/construction/20/",StringComparison.Ordinal));
    }
    [Fact]
    public void Missing_land_value_or_later_timer_consumer_rolls_back_whole_minute_including_random_and_callbacks()
    {
        var missing=Fixture(false,false);var original=SaveCodec.Write(missing);
        Assert.Throws<CityConsumerUnavailableException>(()=>missing.AdvanceOneMinute());Assert.Equal(original,SaveCodec.Write(missing));
        var state=Fixture();for(var i=0;i<19;i++)state.AdvanceOneMinute();var bytes=SaveCodec.Write(state);
        Assert.Throws<CityConsumerUnavailableException>(()=>state.AdvanceOneMinute());Assert.Equal(bytes,SaveCodec.Write(state));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_city_consumers_reject_Step_without_stranding_receipts_or_save_load(bool laterTimer)
    {
        using var host=Host(Fixture(laterTimer,laterTimer));
        if(laterTimer)for(var i=0;i<19;i++)Assert.IsType<AcceptedMessage>(await Send(host,new StepCommand()));
        var before=await Save(host);var snapshot=host.Snapshot();
        Assert.Equal("operationUnavailable",Assert.IsType<RejectedMessage>(await Send(host,new StepCommand())).Code);
        Assert.Equal(before,await Save(host));Assert.Equal(snapshot.Revision,host.Snapshot().Revision);
        if(!laterTimer)Assert.IsType<AcceptedMessage>(await Send(host,new CancelCityConstructionCommand(Query(host.Snapshot()))));
        var load=host.LoadSave(before,host.WorldEpoch,new("owner"));host.Pump(0);await load;
        Assert.Equal(before,await Save(host));
    }
    public static IEnumerable<object[]> BadFields()=>new string[]{"unknown","missing","format","definition","callback","random","heat","producer","controlled","input","search","allocator","owner","fence","queue","old-save","duplicate"}.Select(s=>new object[]{s});
    [Theory,MemberData(nameof(BadFields))]
    public async Task Invalid_city_input_preserves_valid_host_and_original_checkpoint_file(string kind)
    {
        using var host=Host(Fixture());var original=await Save(host);var node=JsonNode.Parse(original)!;var city=node["mixed"]!["city"]!;
        switch(kind)
        {
            case "unknown":city["unexpected"]=1;break;
            case "missing":city.AsObject().Remove("heat");break;
            case "format":city["format"]=2;break;
            case "definition":city["structures"]![0]!["definition"]!["contributionId"]="missing";break;
            case "callback":city["structures"]![3]!["construction"]![0]!["callback"]!["handlerId"]="unknown";break;
            case "random":node["random"]!["streams"]![0]!["id"]="missing";break;
            case "heat":city["heat"]!["q"]!.AsArray().RemoveAt(0);break;
            case "producer":city["producers"]![0]!["ownerId"]="20";break;
            case "controlled":node["mixed"]!["stationGraph"]!["listeners"]![0]!["provider"]!["input"]="controlled";break;
            case "input":node["mixed"]!["stationGraph"]!["listeners"]![0]!["provider"]!["input"]="city-ground";break;
            case "search":city["plans"]![0]!["stationId"]="999";break;
            case "allocator":node["mixed"]!["nextEntityId"]="20";node["spatial"]!["nextEntityId"]="20";break;
            case "owner":node["spatial"]!["entities"]!.AsArray().RemoveAt(node["spatial"]!["entities"]!.AsArray().Count-1);break;
            case "fence":city["fences"]![0]!["hostOwnerId"]="999";break;
            case "queue":node["clock"]!["pending"]!.AsArray().RemoveAt(0);break;
            case "old-save":node["saveVersion"]=10;break;
        }
        var bytes=System.Text.Encoding.UTF8.GetBytes(node.ToJsonString());
        if(kind=="duplicate")bytes=System.Text.Encoding.UTF8.GetBytes(node.ToJsonString().Replace("\"format\":1","\"format\":1,\"format\":1",StringComparison.Ordinal));
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try
        {
            using var storage=new SaveStorage(dir);await storage.Write("checkpoint",bytes);
            Assert.Throws<InvalidDataException>(()=>host.PrepareSave(storage.Read("checkpoint")));
            Assert.Equal(bytes,storage.Read("checkpoint"));Assert.Equal(original,await Save(host));Assert.Equal(1,host.WorldEpoch);
        }
        finally{Directory.Delete(dir,true);}
    }
    [Fact]
    public async Task Read_only_preview_FIFO_cancel_roles_stale_drafts_and_two_client_publications_are_coherent()
    {
        using var host=Host(Fixture());var before=host.Snapshot();var query=Query(before);var original=await Save(host);
        Assert.True(host.PreviewCity(query).CanCancel);Assert.Equal(original,await Save(host));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);var op=new ParticipantContext("operator",ParticipantRole.Operator);
        host.RegisterParticipant(observer);host.RegisterParticipant(op);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(host,new CancelCityConstructionCommand(query),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,new CancelCityConstructionCommand(query with {ExpectedRevision=query.ExpectedRevision+1}),op)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,new CancelCityConstructionCommand(query with {ExpectedMinute=query.ExpectedMinute+1}),op)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,new CancelCityConstructionCommand(query with {ExpectedOwnerRevision=1}),op)).Code);
        Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await Send(host,new CancelCityConstructionCommand(query with {ExpectedContentVersion="unknown"}),op)).Code);
        Assert.Equal(original,await Save(host));
        var first=host.Enqueue(op,new("cancel",1,new CancelCityConstructionCommand(query)));
        var second=host.Enqueue(op,new("stale",1,new CancelCityConstructionCommand(query)));host.Pump(0);
        Assert.IsType<AcceptedMessage>(await first);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await second).Code);
        var a=host.Snapshot();var b=host.Snapshot();var delta=StateDifference.Between(before,a);
        Assert.Equal(JsonSerializer.Serialize(a,WireJson.CreateOptions()),JsonSerializer.Serialize(b,WireJson.CreateOptions()));
        Assert.Contains(20,delta.Removed.EntityIds!);Assert.DoesNotContain(a.Mixed!.City!.Structures,s=>s.Id==20);
        Assert.DoesNotContain(a.Mixed.Queue!.Pending,t=>t.HandlerId.StartsWith("city/construction/20/",StringComparison.Ordinal));
        Assert.NotEmpty(original);Assert.Equal(before.Cash,a.Cash);
        var saved=await Save(host);Assert.Equal(saved,SaveCodec.Write(Read(saved)));
        var load=host.LoadSave(saved,1,new("owner"));host.Pump(0);await load;
        Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await Send(host,new CancelCityConstructionCommand(query),op)).Code);
    }
    [Fact]
    public void City_transfer_uses_existing_bounded_chunks_and_hashes_the_complete_coherent_payload()
    {
        var state=Fixture();var snapshot=MixedWire.Snapshot(state,8);var bytes=StateTransfer.Serialize(snapshot);
        var chunks=StateTransfer.Chunks(snapshot,bytes).ToArray();
        Assert.All(chunks,c=>Assert.InRange(c.Data.Length,1,ProtocolIdentity.StateChunkBytes));
        Assert.Equal(bytes,chunks.SelectMany(c=>c.Data).ToArray());
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),chunks[0].Sha256);
        var payload=JsonSerializer.Deserialize<StateTransferPayload>(bytes,WireJson.CreateOptions())!;
        var header=Assert.IsType<SnapshotMessage>(payload.Message);Assert.Null(header.Terrain);Assert.NotNull(header.Mixed!.City);
        Assert.Equal(snapshot.Mixed!.City!.Heat.Q,header.Mixed.City!.Heat.Q);
        Assert.Equal(snapshot.Mixed.StationGraph!.ListenerMemberships.SelectMany(m=>m.Providers),header.Mixed.StationGraph!.ListenerMemberships.SelectMany(m=>m.Providers));
    }
    [Fact]
    public void City_roads_restore_in_the_existing_Traffic_slot_and_missing_or_unknown_city_packs_reject()
    {
        var state=Fixture();var world=state.World;var p=new Location(23,22,2);var id=world.Spatial.NextEntityId;
        var entity=new SpatialEntity(id,0,Rail.TrafficOwner,p,Footprint.Box(new(new(0,0,0),1,1,1),true),true,false,0);
        var changed=world.Replace(world.Revision,world.Terrain,new(id+1,world.Spatial.Entities.Add(entity)),
            world.Voxels.Add(new(p,id,RailwayVoxelKind.Traffic,new(Road:new(City.Roads[0].Reference,CityRoadDefinition.Edges(10))))),world.Slopes);
        var next=state.Successor(state.Revision,new(changed),state.ConstructionMode);
        var bytes=SaveCodec.Write(next);Assert.Equal(bytes,SaveCodec.Write(Read(bytes)));
        var node=JsonNode.Parse(bytes)!;node["requiredPacks"]![0]!["catalogHash"]=new string('0',64);
        Assert.Throws<InvalidDataException>(()=>Read(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
        node=JsonNode.Parse(bytes)!;node["mixed"]!.AsObject().Remove("city");
        Assert.Throws<InvalidDataException>(()=>Read(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString())));
    }
    [Fact]
    public void City_only_live_world_restores_without_inventing_station_fleet_or_default_timers()
    {
        var terrain=StationStateTests.Terrain();var city=CityGraph.Empty(City,terrain.Space);
        var world=new MixedWorld(Rail,terrain,terrain.Spatial.Capture(),[],[],city:city);
        var state=new MixedSessionState(new(world),Provenance(terrain));var bytes=SaveCodec.Write(state);
        var restored=Read(bytes);Assert.Null(restored.Fleet);Assert.Null(restored.World.Stations);Assert.Empty(restored.Clock.Capture().Pending);
        Assert.Equal(bytes,SaveCodec.Write(restored));restored.AdvanceOneMinute();Assert.Equal(1,restored.Revision);
    }
    [Fact]
    public void Controlled_city_timer_adapter_preserves_repeated_FIFO_cookies_and_new_orders_after_reopen()
    {
        static MixedConstructionState Tick(MixedConstructionState before,Clock clock,RandomStreams random,string handler)
        {
            var w=before.World;var state=w.City!.State;var timer=w.City.Callbacks.Single(a=>a.HandlerId==handler);
            random["development"].Next(80);
            var next=timer with {Order=clock.NextOrder,DueMinute=checked(clock.Time.AbsoluteMinute+timer.Interval)};
            state=handler==CityHandlers.Heat ? state with {HeatCallback=next} : handler==CityHandlers.Development ? state with {DevelopmentCallback=next} :
                state with {Structures=state.Structures.Select(s=>s.Subsidiary?.Callback?.HandlerId==handler
                    ? s with {Revision=s.Revision+1,Subsidiary=s.Subsidiary with {Callback=null}} : s).ToImmutableArray()};
            var graph=new CityGraph(w.City.Profile,state);
            var entities=w.Spatial.Entities.Select(e=>graph.IsSpatialOwner(e.Id)?graph.SpatialOwner(e.Id):e).ToImmutableArray();
            return before.Replace(w.Revision,w.Terrain,new(w.Spatial.NextEntityId,entities),w.Voxels,w.Slopes,ConstructionBooking.None,clock.Time,"controlledCityTimer",city:graph);
        }
        var seed=Fixture();var before=new MixedSessionState(seed.Construction,seed.Provenance,clock:seed.Clock.Capture(),random:seed.Random.Capture(),revision:seed.Revision,
            cityCallback:Tick,cityLandValue:(_,_)=>20);
        var after=SaveCodec.ReadWorld(SaveCodec.Write(before),TerrainProfiles.Resolve,RailwayProfiles.Resolve,cityCallback:Tick,cityLandValue:(_,_)=>20).Mixed!;
        for(var i=0;i<20;i++){before.AdvanceOneMinute();after.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(before),SaveCodec.Write(after));
        var pending=before.Clock.Capture().Pending;
        Assert.Equal(1,before.World.City!.State.HeatCallback!.Registration);Assert.Equal(2,before.World.City.State.DevelopmentCallback!.Registration);
        Assert.True(before.World.City.State.HeatCallback.Order<before.World.City.State.DevelopmentCallback.Order);
        Assert.Contains(pending,t=>t.HandlerId==CityHandlers.Heat&&t.Interval==600);
    }
    [Fact]
    public void Generated_client_city_profile_matches_every_server_definition_population_and_half_selector()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/city-profile.json")));
        var root=doc.RootElement;
        static DefinitionReference Reference(JsonElement row)=>new(row.GetProperty("packId").GetString()!,row.GetProperty("contributionId").GetString()!,row.GetProperty("recipeId").GetString()!);
        static PopulationRead? PopulationRead(Population? p)=>p==null ? null : new(p.Kind,p.Base,p.Factor,p.Entering,
            (p.Children.IsDefault?[]:p.Children).Select(c=>PopulationRead(c)!).ToArray(),(p.Tables.IsDefault?[]:p.Tables).Select(t=>t.ToArray()).ToArray());
        static void Population(JsonElement row,Population? p)=>Assert.True(JsonNode.DeepEquals(JsonNode.Parse(row.GetProperty("population").GetRawText()),
            JsonSerializer.SerializeToNode(PopulationRead(p),WireJson.CreateOptions())));
        Assert.Equal(City.PropertyDefinition,Reference(root.GetProperty("propertyDefinition")));Assert.Equal(City.FenceHostDefinition,Reference(root.GetProperty("fenceHostDefinition")));
        Assert.Equal(City.Packs.Select(p=>new RequiredPackRead(p.Id,p.Version,p.CatalogHash)),root.GetProperty("requiredPacks").Deserialize<RequiredPackRead[]>(WireJson.CreateOptions())!);
        var structures=root.GetProperty("structures").EnumerateArray().ToArray();Assert.Equal(City.Structures.Length,structures.Length);
        foreach(var row in structures)
        {
            var d=City.Structure(Reference(row.GetProperty("reference")));
            Assert.Equal(d.Kind.ToString().ToLowerInvariant(),row.GetProperty("kind").GetString());
            Assert.Equal(d.SizeX,row.GetProperty("sizeX").GetInt32());Assert.Equal(d.SizeY,row.GetProperty("sizeY").GetInt32());
            Assert.Equal(d.MinimumHeight,row.GetProperty("minimumHeight").GetInt32());Assert.Equal(d.MaximumHeight,row.GetProperty("maximumHeight").GetInt32());
            Assert.Equal(d.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),row.GetProperty("unitPrice").GetString());
            Assert.Equal(d.ComputerCannotBuild,row.GetProperty("computerCannotBuild").GetBoolean());Population(row,d.Population);
            var variants=row.GetProperty("halfVariants").EnumerateArray().Select(v=>new CityHalfVariant(v.GetProperty("direction").GetInt32(),v.GetProperty("side").GetInt32(),v.GetProperty("color").GetInt32())).ToHashSet();
            Assert.True(variants.SetEquals(d.HalfVariants.IsDefault?[]:d.HalfVariants));
        }
        var ground=root.GetProperty("ground").EnumerateArray().ToArray();Assert.Equal(City.Ground.Length,ground.Length);
        foreach(var row in ground)
        {
            var d=City.GroundDefinition(Reference(row.GetProperty("reference")));
            Assert.Equal(d.Handler,row.GetProperty("handler").GetString());Assert.Equal(d.GridX,row.GetProperty("gridX").GetInt32());Assert.Equal(d.GridY,row.GetProperty("gridY").GetInt32());
            Assert.Equal(d.Price.ToString(System.Globalization.CultureInfo.InvariantCulture),row.GetProperty("price").GetString());
            Assert.Equal(d.ComputerCannotBuild,row.GetProperty("computerCannotBuild").GetBoolean());Population(row,d.Population);
            Assert.Equal(City.Bindings.Single(b=>b.Reference==d.Reference).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal),row.GetProperty("remover").GetBoolean());
        }
        Assert.True(root.GetProperty("roads").EnumerateArray().Select(Reference).ToHashSet().SetEquals(City.Roads.Select(r=>r.Reference)));
    }
    [Fact]
    public void City_wire_admission_rejects_aggregate_owner_cell_and_buffer_overflow_before_publication()
    {
        var data=MixedWire.Read(Fixture()).City!;
        Assert.Throws<ArgumentException>(()=>CityWire.ValidateLimits(data with {Properties=Enumerable.Repeat(data.Properties[0],8193).ToArray()}));
        Assert.Throws<ArgumentException>(()=>CityWire.ValidateLimits(data with {Heat=data.Heat with {Q=[]}}));
        Assert.Throws<ArgumentException>(()=>CityWire.ValidateLimits(data with {Format=2}));
    }
}
