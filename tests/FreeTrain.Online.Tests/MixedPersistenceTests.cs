using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class MixedPersistenceTests
{
    static byte[] Fixture => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/mixed-v11.json"));
    static MixedSessionState Decode(byte[] bytes) => SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static string Json<T>(T data) => JsonSerializer.Serialize(data,WireJson.CreateOptions());
    static MixedConstructionState Edit(MixedSessionState s)
    {
        var w=s.World;
        var entities=w.Spatial.Entities.Where(e=>e.Id!=7).Select(e=>e.Id==9?e with {Revision=1}:e).ToImmutableArray();
        var voxels=w.Voxels.Where(v=>v.OwnerId!=7).Select(v=>v.OwnerId==9?v with {Traffic=v.Traffic! with {Rail=v.Traffic.Rail! with {Variant=1}}}:v);
        return s.Construction.Replace(5,w.Terrain,new(11,entities),voxels,[],ConstructionBooking.Spend(200_000),s.Clock.Time,"fixture.normal");
    }
    [Fact]
    public void Literal_mixed_save_snapshot_delta_and_continuation_agree()
    {
        var state=Decode(Fixture);Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Fixture),JsonNode.Parse(SaveCodec.Write(state))));
        Assert.Equal(SaveCodec.Write(state),SaveCodec.Write(Decode(SaveCodec.Write(state))));
        var expected=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/snapshot-mixed.json")));
        Assert.True(JsonNode.DeepEquals(expected,JsonNode.Parse(Json<WireMessage>(MixedWire.Snapshot(state,1)))));
        Assert.Equal(7_000_000_000,state.Construction.Accounts.Cash);Assert.Equal(1,state.Construction.Accounts.TopUps);
        Assert.Equal(11,state.World.Spatial.NextEntityId);Assert.Equal(7,state.World.Terrain.Spatial.NextEntityId);
        var next=state.Successor(state.Revision,Edit(state),true);
        var delta=NetworkSession.Difference(MixedWire.Snapshot(state,1),MixedWire.Snapshot(next,1));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/delta-mixed.json"))),JsonNode.Parse(Json<WireMessage>(delta))));
        Assert.Null(next.World.At(new(1,0,4)));Assert.Equal(7,next.World.At(new(1,0,3))!.SupportedOwnerId);
        var restored=Decode(SaveCodec.Write(next));
        foreach(var id in RandomStreams.Ids)Assert.Equal(next.Random[id].Next(),restored.Random[id].Next());
        next.AdvanceOneMinute();restored.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(next),SaveCodec.Write(restored));
    }
    [Fact]
    public void Every_literal_slope_and_normal_pattern_round_trips_with_stored_flags()
    {
        using var fixtures=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Mixed persistence",new(32,16,8),0);
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,request.Name,request.Dimensions,0,Enumerable.Repeat(new TerrainCell(2),512),new(1,[]),[]);
        var count=0;
        foreach(var row in fixtures.RootElement.GetProperty("groups").GetProperty("slopes").GetProperty("cases").EnumerateArray()
            .Where(r=>r.GetProperty("expected").GetProperty("outcome").GetString()=="apply")){
            var (e,v,o)=MixedWorldTests.Slope(row);
            var world=new MixedWorld(RailwayProfiles.Bundled,terrain,new(2,[e]),v,[o]);
            var state=new MixedSessionState(new(world),TerrainProvenance.From(request));
            var loaded=Decode(SaveCodec.Write(state));
            Assert.Equal(Json(MixedWire.Read(state.Construction)),Json(MixedWire.Read(loaded.Construction)));count++;
            var node=JsonNode.Parse(SaveCodec.Write(state))!;node["mixed"]!["voxels"]!.AsArray().RemoveAt(0);
            Assert.Throws<InvalidDataException>(()=>Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
        }
        Assert.Equal(15,count);
        for(var pattern=0;pattern<28;pattern++){
            var definition=new DefinitionReference(PreparedContentCatalog.BundledPackId,$"system:pattern/{pattern}",$"system:pattern/{pattern}");
            var e=new SpatialEntity(1,0,RailwayProfiles.Bundled.TrafficOwner,new(8,8,2),new([new(new(0,0,0),true)]),true,false,0);
            var w=new MixedWorld(RailwayProfiles.Bundled,terrain,new(2,[e]),[new(e.Anchor,1,RailwayVoxelKind.Traffic,new(new(definition,pattern)))],[]);
            var state=new MixedSessionState(new(w),TerrainProvenance.From(request));
            Assert.Equal(pattern,Decode(SaveCodec.Write(state)).World.Voxels[0].Traffic!.Rail!.Pattern);
        }
    }
    [Theory]
    [InlineData("pack")][InlineData("hash")][InlineData("content")][InlineData("ruleset")][InlineData("version")]
    [InlineData("recipe")][InlineData("owner")][InlineData("overlap")][InlineData("allocator")][InlineData("terrainAllocator")]
    [InlineData("pattern")][InlineData("backup")][InlineData("cash")][InlineData("genre")][InlineData("sequence")]
    [InlineData("bookingRevision")][InlineData("bookingTime")][InlineData("duplicate")][InlineData("mode")][InlineData("tooMany")]
    public async Task Untrusted_mixed_input_preserves_world_epoch_and_file(string change)
    {
        var bytes=Fixture;var node=JsonNode.Parse(bytes)!;var mixed=node["mixed"]!;
        switch(change){
            case "pack":node["requiredPacks"]![0]!["id"]="missing";break;
            case "hash":node["requiredPacks"]![0]!["catalogHash"]=new string('0',64);break;
            case "content":node["contentVersion"]="missing";break;
            case "ruleset":node["ruleset"]="missing";break;
            case "version":node["saveVersion"]=7;break;
            case "recipe":mixed["voxels"]![0]!["traffic"]!["rail"]!["definition"]!["recipeId"]="unknown";break;
            case "owner":mixed["voxels"]![0]!["ownerId"]="1";break;
            case "overlap":mixed["voxels"]![0]!["position"]!["z"]=2;node["spatial"]!["entities"]![0]!["anchor"]!["z"]=2;break;
            case "allocator":mixed["nextEntityId"]="7";node["spatial"]!["nextEntityId"]="7";break;
            case "terrainAllocator":node["packedTerrain"]!["nextEntityId"]="12";break;
            case "pattern":mixed["voxels"]![0]!["traffic"]!["rail"]!["pattern"]=12;break;
            case "backup":mixed["voxels"]![2]!["traffic"]!["rail"]!["terrainBackup"]=null;break;
            case "cash":mixed["accounts"]!["transactions"]![0]!["cashAfter"]="1";break;
            case "genre":mixed["accounts"]!["transactions"]![0]!["genre"]="unknown";break;
            case "sequence":mixed["accounts"]!["transactions"]![0]!["sequence"]="2";break;
            case "bookingRevision":mixed["geometryRevision"]="4";break;
            case "bookingTime":node["clock"]!["absoluteMinute"]="130080";break;
            case "mode":node["packedTerrain"]!["constructionMode"]="true";break;
            case "tooMany":var a=mixed["voxels"]!.AsArray();while(a.Count<=ProtocolIdentity.MaximumRailwayVoxels)a.Add(a[0]!.DeepClone());break;
        }
        var text=node.ToJsonString();if(change=="duplicate")text=text.Replace("\"saveVersion\":11","\"saveVersion\":11,\"saveVersion\":11");
        using var host=SessionComposition.CreateSavedHost(bytes);
        var snapshot=Json(host.Snapshot());var load=host.LoadSave(Encoding.UTF8.GetBytes(text),host.WorldEpoch);host.Pump();
        await Assert.ThrowsAsync<InvalidDataException>(()=>load);Assert.Equal(snapshot,Json(host.Snapshot()));Assert.Equal(bytes,Fixture);
    }
    [Fact]
    public async Task Fifo_publication_roles_epoch_and_geometry_remain_atomic()
    {
        var state=Decode(Fixture);using var host=new SessionHost(state,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var owner=new ParticipantContext("owner");var viewer=new ParticipantContext("viewer",ParticipantRole.Observer);host.RegisterParticipant(viewer);
        var denied=host.ApplyMixed(1,6,Edit(state),true,viewer);host.Pump();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>denied);
        var apply=host.ApplyMixed(1,6,Edit(state),true,owner);var capture=host.CaptureSave();Assert.False(apply.IsCompleted);host.Pump();await apply;
        var loaded=Decode(await capture);Assert.Equal(6,loaded.World.Revision);Assert.Equal(6_999_800_000,loaded.Construction.Accounts.Cash);
        var stale=host.ApplyMixed(1,6,Edit(state),true,owner);host.Pump();await Assert.ThrowsAsync<InvalidOperationException>(()=>stale);
        var unavailable=host.Enqueue(owner,new("prototype",1,new BuildTrackCommand(new(1,0,4),"straightX")));host.Pump();Assert.Equal("operationUnavailable",Assert.IsType<RejectedMessage>(await unavailable).Code);
        var step=host.Enqueue(owner,new("step",1,new StepCommand()));host.Pump();Assert.IsType<AcceptedMessage>(await step);
        Assert.Equal(130082,host.Snapshot().Tick);Assert.Equal(loaded.World.Revision,host.Snapshot().Mixed!.GeometryRevision);
    }
    [Fact]
    public async Task Real_checkpoint_restart_retains_mixed_state_and_current_only_policy()
    {
        var directory=Path.Combine(Path.GetTempPath(),"mixed-restart-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try{
            using(var storage=new SaveStorage(directory))await storage.Write("checkpoint",Fixture);
            using(var storage=new SaveStorage(directory)){
                var session=new NetworkSession(new(),storage);Assert.Null(session.StartupSaveError);
                Assert.Equal(ProtocolIdentity.MixedRuleset,session.Bootstrap().Snapshot.Ruleset);
                var capture=session.CaptureSave();session.Tick(DateTimeOffset.UtcNow);Assert.Equal(SaveCodec.Write(Decode(Fixture)),await capture);
                var save=session.Checkpoint();session.Tick(DateTimeOffset.UtcNow);await save;
            }
            var before=File.ReadAllBytes(Path.Combine(directory,"checkpoint.json"));
            using(var storage=new SaveStorage(directory)){
                var restarted=new NetworkSession(new(),storage);Assert.Null(restarted.StartupSaveError);
                Assert.Equal(7_000_000_000,restarted.Bootstrap().Snapshot.Cash);
                Assert.Equal(before,File.ReadAllBytes(Path.Combine(directory,"checkpoint.json")));
            }
        }finally{Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task Real_http_two_peer_upload_restart_and_incompatible_peer_preserve_mixed_state()
    {
        var directory=Path.Combine(Path.GetTempPath(),"mixed-http-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        var data=SaveCodec.Write(Decode(Fixture));
        try{
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true)){
                await server.Bootstrap();using var first=await server.Connect();
                var cookies=new System.Net.CookieContainer();using var http=new HttpClient(new HttpClientHandler{CookieContainer=cookies}){BaseAddress=server.Http.BaseAddress};
                await http.GetStringAsync("/api/bootstrap?protocolVersion=10&contentVersion=terrain-content-1");using var second=await server.Connect(cookies);
                await NetworkTests.Read(first);await NetworkTests.Read(second);
                Assert.True((await server.Http.PostAsync("/api/saves/validate?expectedWorldEpoch=1",new ByteArrayContent(data))).IsSuccessStatusCode);
                Assert.True((await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1",new ByteArrayContent(data))).IsSuccessStatusCode);
                foreach(var socket in new[]{first,second}){
                    var snapshot=Assert.IsType<SnapshotMessage>(await NetworkTests.Until(socket,m=>m is SnapshotMessage));
                    Assert.Equal(2,snapshot.WorldEpoch);Assert.Equal(ProtocolIdentity.MixedRuleset,snapshot.Ruleset);Assert.Equal(7_000_000_000,snapshot.Cash);
                    Assert.Equal(4,snapshot.Mixed!.Voxels.Length);
                }
                Assert.Equal(data,await server.Http.GetByteArrayAsync("/api/saves/download"));
                Assert.Equal((System.Net.HttpStatusCode)426,(await http.GetAsync("/api/bootstrap?protocolVersion=5&contentVersion=mixed-railway-content-1")).StatusCode);
                var bad=JsonNode.Parse(data)!;bad["mixed"]!["accounts"]!["transactions"]![0]!["cashAfter"]="1";
                Assert.Equal(System.Net.HttpStatusCode.BadRequest,(await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=2",new ByteArrayContent(Encoding.UTF8.GetBytes(bad.ToJsonString())))).StatusCode);
                Assert.Equal(data,await server.Http.GetByteArrayAsync("/api/saves/download"));
            }
            await using(var restarted=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true)){
                var join=await restarted.Bootstrap();Assert.Equal(ProtocolIdentity.MixedRuleset,join.Ruleset);Assert.Equal(2,join.WorldEpoch);
                Assert.Equal(data,await restarted.Http.GetByteArrayAsync("/api/saves/download"));
            }
        }finally{Directory.Delete(directory,true);}
    }
    [Fact]
    public void Packed_chunks_keep_railway_entities_separate_from_terrain_and_match_full_snapshot()
    {
        var snapshot=MixedWire.Snapshot(Decode(Fixture),1);
        var payload=JsonSerializer.Deserialize<StateTransferPayload>(StateTransfer.Serialize(snapshot),WireJson.CreateOptions())!;
        var header=Assert.IsType<SnapshotMessage>(payload.Message);Assert.Null(header.Terrain);Assert.Equal(4,header.Entities!.Length);
        Assert.Equal(3,payload.PackedTerrain!.OccupantCount);Assert.Equal(Json(snapshot.Mixed),Json(header.Mixed));
        var chunks=StateTransfer.Chunks(snapshot).ToArray();Assert.NotEmpty(chunks);Assert.Equal(StateTransfer.Serialize(snapshot),chunks.SelectMany(c=>c.Data).ToArray());
    }
    [Fact]
    public void Generated_browser_bindings_match_every_registered_exact_state_handler()
    {
        using var document=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-profile.json")));
        var root=document.RootElement;var profile=RailwayProfiles.Bundled;
        Assert.Equal(RailwayProfile.Ruleset,root.GetProperty("ruleset").GetString());
        var bindings=root.GetProperty("bindings").EnumerateArray().ToArray();Assert.Equal(profile.Bindings.Length,bindings.Length);
        foreach(var actual in profile.Bindings){
            var expected=Assert.Single(bindings,b=>b.GetProperty("reference").GetProperty("contributionId").GetString()==actual.Reference.ContributionId);
            Assert.Equal(actual.Reference.PackId,expected.GetProperty("reference").GetProperty("packId").GetString());
            Assert.Equal(actual.Reference.RecipeId,expected.GetProperty("reference").GetProperty("recipeId").GetString());
            Assert.Equal(actual.Handler.ToString().ToLowerInvariant(),expected.GetProperty("handler").GetString());
            Assert.Equal(actual.Variant,expected.GetProperty("variant").GetInt32());
        }
    }
    [Fact]
    public void Maximum_initial_railway_and_journal_counts_are_admitted_and_next_entry_is_rejected()
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Admission",new(128,128,8),0);
        var terrain=TerrainWorld.Empty(request,TerrainProfiles.Bundled);var profile=RailwayProfiles.Bundled;
        var e=new List<SpatialEntity>();var v=new List<RailwayVoxel>();var rail=new RailComponent(profile.Bindings.First(b=>b.Handler==RailwayHandler.Normal).Reference,0);
        for(var i=0;i<ProtocolIdentity.MaximumRailwayVoxels;i++){
            var p=terrain.Space.ToLocation(new(i%128,i/128,1));var id=i+1;
            e.Add(new(id,0,profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
            v.Add(new(p,id,RailwayVoxelKind.Traffic,new(rail)));
        }
        var accounts=new ConstructionAccounts().Book(new ConstructionBooking(Enumerable.Repeat(new AccountCharge(AccountGenre.RailService,0),ProtocolIdentity.MaximumAccountTransactions)),1,new(GameTime.StartMinute),"bound");
        var world=new MixedWorld(profile,terrain,new(e.Count+1,e.ToImmutableArray()),v,[],1);
        var state=new MixedSessionState(new(world,accounts),TerrainProvenance.From(request),revision:1);
        MixedWire.ValidateAdmission(state);var loaded=Decode(SaveCodec.Write(state));
        Assert.Equal(8192,loaded.World.Voxels.Length);Assert.Equal(8192,loaded.Construction.Accounts.Transactions.Length);
        var nextAccounts=accounts.Book(ConstructionBooking.Spend(0),2,new(GameTime.StartMinute),"bound");
        var successor=world.Replace(1,terrain,world.Spatial.Capture(),v,[]);
        var next=new MixedSessionState(new(successor,nextAccounts),state.Provenance,revision:2);
        Assert.Throws<InvalidDataException>(()=>MixedWire.ValidateAdmission(next));
    }
    [Fact]
    public void Limits_and_exact_profiles_reject_without_admitting_unimplemented_pack_handlers()
    {
        Assert.Equal(RailwayProfile.Ruleset,ProtocolIdentity.MixedRuleset);Assert.Equal(RailwayProfile.ContentVersion,ProtocolIdentity.MixedContent);
        Assert.Throws<InvalidDataException>(()=>SaveCodec.ReadWorld(Fixture,TerrainProfiles.Resolve));
        var zero=TerrainTransportScenario.Create();var state=new MixedSessionState(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,zero.World)),zero.Provenance,true,
            zero.Clock.Capture(),zero.Paused,zero.Random.Capture(),zero.Revision);
        Assert.Empty(Decode(SaveCodec.Write(state)).World.Voxels);
        Assert.Equal(15_000_000_000,Decode(SaveCodec.Write(state)).Construction.Accounts.Cash);
    }
}
