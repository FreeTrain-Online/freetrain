using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

// Integration fixture built through gameplay commands; source geometry/price
// expectations remain in the independent P4-02 and individual tool suites.
public class MixedContinuationTests
{
    static readonly Lazy<Task<byte[]>> Network = new(BuildNetwork);
    static MixedSessionState Decode(byte[] bytes) => SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static SessionHost Open(byte[] bytes) => new(Decode(bytes),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static async Task<byte[]> Capture(SessionHost h) { var t=h.CaptureSave();h.Pump(0);return await t; }
    static async Task Send(SessionHost h,CommandPayload command)
    {
        var t=h.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,command));h.Pump(0);
        var result=await t;Assert.True(result is AcceptedMessage,result.ToString());
    }
    static PositionRead Shift(PositionRead p) => new(p.X+60,p.Y+80,p.Z);
    static RailwayQuery Query(SessionHost h,string type,PositionRead from,PositionRead? to=null,string action="lay")
    {
        var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,Shift(from),Shift(to??from),action,"source",type);
    }
    static async Task Rail(SessionHost h,RailwayQuery q)
    {
        var before=await Capture(h);var p=h.PreviewRailway(q);Assert.True(p.Legal,$"{q}: {p.Reason}");
        Assert.Equal(before,await Capture(h));await Send(h,new EditRailwayCommand(q));
    }
    static async Task Slope(SessionHost h,PositionRead p,int climb,string action="lay")
    {
        var s=h.Snapshot();var q=new SlopeQuery(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,Shift(p),climb,action);
        Assert.True(h.PreviewSlope(q).Legal,h.PreviewSlope(q).Reason);await Send(h,new EditSlopeCommand(q));
    }
    static async Task<byte[]> BuildNetwork()
    {
        var dims=new WorldDimensions(128,160,10);
        var definition=TerrainProfiles.Bundled.Definitions.Single(b=>b.Kind==TerrainOccupantKind.Mountain&&b.TreeSpriteCount==0).Reference;
        var entities=new[]{new SpatialEntity(1,0,definition,new(64,96,2),new([new(new(0,0,0),false)]),true,false,0),
            new SpatialEntity(2,0,definition,new(72,96,2),new([new(new(0,0,0),false)]),true,false,0)};
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"P4-21 mixed railway network",dims,0,
            Enumerable.Repeat(new TerrainCell(2,TerrainSurface.Cut),128*160),new(3,entities.ToImmutableArray()),
            [new(1,TerrainOccupantKind.Mountain,new(0,1,2,3)),new(2,TerrainOccupantKind.Mountain,new(0,1,2,3))]);
        using var h=new SessionHost(new TerrainSessionState(terrain,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,dims,0)),clock:new(3420,0,[])),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
        await Rail(h,Query(h,"normal",new(4,4,2),new(16,4,2)));
        await Rail(h,Query(h,"normal",new(9,4,2),new(11,6,2))); // Junction.
        await Rail(h,Query(h,"bridge",new(4,8,4),new(16,8,4)));
        await Rail(h,Query(h,"girder",new(4,12,4),new(16,12,4)));
        await Rail(h,Query(h,"tunnel",new(4,16,2),new(12,16,2)));
        await Rail(h,Query(h,"garage",new(4,20,2),new(12,20,2)));
        await Slope(h,new(32,4,2),2);
        await Rail(h,Query(h,"normal",new(28,4,2),new(32,4,2)));
        await Rail(h,Query(h,"normal",new(35,4,3),new(40,4,3)));
        await Slope(h,new(32,12,1),2); // Underground walls and flags.
        await Rail(h,Query(h,"signal",new(6,4,2)) with {SignalDirection=2});
        var poles=RailwayProfiles.Bundled.Bindings.Where(b=>b.Handler==RailwayHandler.Pole).ToArray();
        Assert.Equal(4,poles.Length);
        for(var i=0;i<poles.Length;i++)await Rail(h,Query(h,"pole",new(11+i,4,2)) with {PoleType=poles[i].Reference.ContributionId});
        foreach(var (speed,y) in new[]{("low",4),("medium",8),("fast",12)})
            await Rail(h,Query(h,"speed",new(4,y,y==4?2:4),new(5,y,y==4?2:4)) with {Speed=speed});
        await Rail(h,Query(h,"speed",new(32,4,2)) with {Speed="medium",SpeedSlope=true,SpeedClimb=2});
        var types=RailwayProfiles.Bundled.Bindings.Where(b=>b.Handler==RailwayHandler.Stationary).ToArray();
        Assert.Equal(54,types.Length);
        for(var i=0;i<types.Length;i++)await Rail(h,Query(h,"stationary",new(4+i%18*4,36+i/18*6,2)) with {StationaryType=types[i].Reference.ContributionId});
        // Complete every variant, then retain another active site in the save.
        var built=Decode(await Capture(h));for(var i=0;i<1800;i++)built.AdvanceOneMinute();
        Assert.DoesNotContain(built.World.Voxels,v=>v.Construction!=null);
        using var completed=Open(SaveCodec.Write(built));
        await Rail(completed,Query(completed,"stationary",new(74,52,2)) with {StationaryType=types.First(t=>t.Stationary!.Height==2).Reference.ContributionId});
        var snapshot=completed.Snapshot();await Send(completed,new SetAuthoringModeCommand(snapshot.Mixed!.GeometryRevision,true));
        var tree=TerrainProfiles.Bundled.Definitions.First(d=>d.TreeSpriteCount>0).Reference;
        var s=completed.Snapshot();var forest=new CornerTerrainQuery(s.WorldEpoch,s.Mixed!.GeometryRevision,Shift(new(52,20,2)),"plantForest",Rectangle:new(Shift(new(53,21,2))),Forest:new(tree.PackId,tree.ContributionId,tree.RecipeId));
        Assert.True(completed.PreviewCorner(forest).Legal);await Send(completed,new EditCornerCommand(forest));
        var bytes=await Capture(completed);
        var output=Environment.GetEnvironmentVariable("FREETRAIN_MIXED_NETWORK_OUTPUT");
        if(output!=null)await File.WriteAllBytesAsync(output,bytes);
        return bytes;
    }

    [Fact]
    public async Task Complete_network_preserves_every_delivered_component_and_callback_continuation()
    {
        var bytes=await Network.Value;var a=Decode(bytes);var b=Decode(bytes);
        Assert.Equal(bytes,SaveCodec.Write(a));Assert.True(a.ConstructionMode);Assert.True(a.Paused);
        Assert.Equal(2,a.World.Slopes.Length);Assert.NotEmpty(a.World.Terrain.TunnelBackups);
        Assert.NotEmpty(a.World.Terrain.Occupants.Where(o=>o.Forest!=null));
        foreach(var kind in new[]{RailwayVoxelKind.Traffic,RailwayVoxelKind.Pier,RailwayVoxelKind.SlopeFiller,RailwayVoxelKind.SlopeSupport,RailwayVoxelKind.SlopeWall,RailwayVoxelKind.Stationary})
            Assert.Contains(a.World.Voxels,v=>v.Kind==kind);
        foreach(var handler in new[]{RailwayHandler.Normal,RailwayHandler.Junction,RailwayHandler.Bridge,RailwayHandler.Girder,RailwayHandler.Tunnel,RailwayHandler.Garage,RailwayHandler.Signal,RailwayHandler.Slope})
            Assert.Contains(a.World.Voxels,v=>v.Traffic?.Rail is {} r&&a.World.Profile.Bindings.Any(x=>x.Reference==r.Definition&&x.Handler==handler));
        Assert.Equal(54,a.World.Voxels.Where(v=>v.Kind==RailwayVoxelKind.Stationary&&v.Construction==null).Select(v=>v.Definition).Distinct().Count());
        Assert.Equal(4,a.World.Voxels.Where(v=>v.Traffic?.Accessory!=null).Select(v=>v.Traffic!.Accessory!.Definition).Distinct().Count());
        Assert.NotEmpty(a.Clock.Capture().Pending);Assert.Equal(2,a.Clock.Capture().Pending.Length);
        for(var minute=0;minute<2200;minute++){
            a.AdvanceOneMinute();b.AdvanceOneMinute();
            if(minute%100==0){Assert.Equal(SaveCodec.Write(a),SaveCodec.Write(b));b=Decode(SaveCodec.Write(b));}
        }
        Assert.Empty(a.Clock.Capture().Pending);Assert.Equal(SaveCodec.Write(a),SaveCodec.Write(b));
        foreach(var id in RandomStreams.Ids)Assert.Equal(a.Random[id].Next(),b.Random[id].Next());
    }

    static async Task Remove(SessionHost h,string type)
    {
        if(type=="slope"||type=="undergroundSlope"){await Slope(h,new(32,type=="slope"?4:12,type=="slope"?2:1),2,"remove");return;}
        var (p,to)=type switch {
            "bridge"=>(new PositionRead(4,8,4),new PositionRead(16,8,4)),
            "girder"=>(new(4,12,4),new(16,12,4)),"tunnel"=>(new(4,16,2),new(12,16,2)),
            "garage"=>(new(4,20,2),new(12,20,2)),"signal"=>(new(6,4,2),new(6,4,2)),
            "pole"=>(new(11,4,2),new(11,4,2)),"speed"=>(new(4,4,2),new(5,4,2)),
            "site"=>(new(74,52,2),new(74,52,2)),"stationary"=>(new(4,36,2),new(4,36,2)),
            _=>(new(4,4,2),new(16,4,2))};
        await Rail(h,Query(h,type=="site"?"stationary":type,p,to,"remove") with {SignalDirection=2});
        // Source half-open special removal requires the reverse endpoint too.
        if(type is "tunnel" or "garage")await Rail(h,Query(h,type,to,p,"remove"));
    }

    [Fact]
    public async Task Every_stationary_and_pole_variant_can_be_removed_from_the_same_network_after_reopen()
    {
        using var live=Open(await Network.Value);
        var directory=Path.Combine(Path.GetTempPath(),"mixed-variants-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try{
            for(var i=0;i<58;i++){
                var before=await Capture(live);using var reopened=Open(before);
                var q=i<54?Query(live,"stationary",new(4+i%18*4,36+i/18*6,2),action:"remove"):
                    Query(live,"pole",new(11+i-54,4,2),action:"remove");
                await Rail(live,q);await Rail(reopened,q);var after=await Capture(live);
                Assert.Equal(after,await Capture(reopened));Assert.NotEqual(before,after);
                using(var storage=new SaveStorage(directory))await storage.Write("checkpoint",after);
                using(var storage=new SaveStorage(directory)){
                    var restart=new NetworkSession(new(),storage);Assert.Null(restart.StartupSaveError);
                    var capture=restart.CaptureSave();restart.Tick(DateTimeOffset.UtcNow);Assert.Equal(after,await capture);
                }
            }
        }finally{Directory.Delete(directory,true);}
    }

    [Theory]
    [InlineData("normal")][InlineData("bridge")][InlineData("girder")][InlineData("tunnel")]
    [InlineData("garage")][InlineData("signal")][InlineData("pole")][InlineData("speed")]
    [InlineData("slope")][InlineData("undergroundSlope")][InlineData("stationary")][InlineData("site")]
    public async Task Each_subtype_removal_agrees_before_after_reopen_and_real_checkpoint_restart(string type)
    {
        var bytes=await Network.Value;using var live=Open(bytes);using var reopened=Open(bytes);
        var directory=Path.Combine(Path.GetTempPath(),"mixed-continuation-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try{
            using(var storage=new SaveStorage(directory))await storage.Write("checkpoint",bytes);
            using(var storage=new SaveStorage(directory)){
                var session=new NetworkSession(new(),storage);Assert.Null(session.StartupSaveError);
                var capture=session.CaptureSave();session.Tick(DateTimeOffset.UtcNow);Assert.Equal(bytes,await capture);
            }
            await Remove(live,type);await Remove(reopened,type);var after=await Capture(live);
            Assert.NotEqual(bytes,after);Assert.Equal(after,await Capture(reopened));
            using(var storage=new SaveStorage(directory))await storage.Write("checkpoint",after);
            using(var storage=new SaveStorage(directory)){
                var session=new NetworkSession(new(),storage);Assert.Null(session.StartupSaveError);
                var capture=session.CaptureSave();session.Tick(DateTimeOffset.UtcNow);Assert.Equal(after,await capture);
                var checkpoint=session.Checkpoint();session.Tick(DateTimeOffset.UtcNow);await checkpoint;
            }
            var a=Decode(after);var b=Decode(File.ReadAllBytes(Path.Combine(directory,"checkpoint.json")));
            Assert.True(a.World.Spatial.NextEntityId>=Decode(bytes).World.Spatial.NextEntityId);
            for(var i=0;i<1000;i++){a.AdvanceOneMinute();b.AdvanceOneMinute();}
            Assert.Equal(SaveCodec.Write(a),SaveCodec.Write(b));
        }finally{Directory.Delete(directory,true);}
    }

    [Fact]
    public async Task Full_network_http_slots_replacement_reconnect_and_startup_agree()
    {
        var data=await Network.Value;var directory=Path.Combine(Path.GetTempPath(),"mixed-full-http-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try{
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true)){
                await server.Bootstrap();using var first=await server.Connect();await NetworkTests.Read(first);
                foreach(var endpoint in new[]{"validate","upload"})Assert.True((await server.Http.PostAsync($"/api/saves/{endpoint}?expectedWorldEpoch=1",new ByteArrayContent(data))).IsSuccessStatusCode);
                var published=Assert.IsType<SnapshotMessage>(await NetworkTests.Until(first,m=>m is SnapshotMessage));Assert.Equal(2,published.WorldEpoch);
                Assert.Equal(Decode(data).World.Spatial.Entities.Length,published.Entities!.Length);
                Assert.Equal(data,await server.Http.GetByteArrayAsync("/api/saves/download"));
                foreach(var slot in new[]{"slot1","slot2","slot3","checkpoint"}){
                    Assert.True((await server.Http.PostAsync($"/api/saves/{slot}/save?expectedWorldEpoch=2",null)).IsSuccessStatusCode);
                    Assert.Equal(data,File.ReadAllBytes(Path.Combine(directory,slot+".json")));
                }
                var cookies=new CookieContainer();using var peer=new HttpClient(new HttpClientHandler{CookieContainer=cookies}){BaseAddress=server.Http.BaseAddress};
                await peer.GetStringAsync("/api/bootstrap?protocolVersion=6&contentVersion=mixed-railway-content-1");
                using var second=await server.Connect(cookies);var join=Assert.IsType<JoinMessage>(await NetworkTests.Read(second));
                Assert.Equal(System.Text.Json.JsonSerializer.Serialize(published.Mixed,NetworkSession.Json),System.Text.Json.JsonSerializer.Serialize(join.Snapshot.Mixed,NetworkSession.Json));
                Assert.True((await server.Http.PostAsync("/api/saves/slot2/load?expectedWorldEpoch=2",null)).IsSuccessStatusCode);
                Assert.Equal(data,await server.Http.GetByteArrayAsync("/api/saves/download"));
            }
            await using(var restarted=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true)){
                await restarted.Bootstrap();Assert.Equal(data,await restarted.Http.GetByteArrayAsync("/api/saves/download"));
                using var socket=await restarted.Connect();var join=Assert.IsType<JoinMessage>(await NetworkTests.Read(socket));
                Assert.Equal(Decode(data).World.Voxels.Length,join.Snapshot.Mixed!.Voxels.Length);
            }
        }finally{Directory.Delete(directory,true);}
    }

    [Theory]
    [InlineData("corrupt")][InlineData("handler")][InlineData("pack")][InlineData("hash")][InlineData("oversize")]
    public async Task Invalid_full_network_input_and_startup_checkpoint_survive_until_explicit_recovery(string kind)
    {
        var good=await Network.Value;var node=JsonNode.Parse(good)!;
        if(kind=="handler")node["mixed"]!["voxels"]!.AsArray().First(v=>v!["traffic"]?["rail"]!=null)!["traffic"]!["rail"]!["definition"]!["recipeId"]="unregistered:p4-21";
        if(kind=="pack")node["requiredPacks"]![0]!["id"]="absent:p4-21";
        if(kind=="hash")node["requiredPacks"]![0]!["catalogHash"]=new string('0',64);
        var bad=kind=="corrupt"?Encoding.UTF8.GetBytes("{broken"):Encoding.UTF8.GetBytes(node.ToJsonString());
        var directory=Path.Combine(Path.GetTempPath(),"mixed-recovery-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"checkpoint.json");
        try{
            // Sparse oversized file exercises bounded slot/startup input without
            // allocating or posting 256 MiB in each integration case.
            File.WriteAllBytes(path,bad);
            if(kind=="oversize"){using var file=File.OpenWrite(path);file.SetLength(SaveCodec.MaximumBytes+1L);}
            var length=new FileInfo(path).Length;
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true)){
                await server.Bootstrap();var status=await server.Http.GetStringAsync("/api/saves/status");Assert.Contains("startupError",status);
                Assert.Contains(kind=="oversize"?"saveTooLarge":kind is "pack" or "hash"?"incompatibleSave":"invalidSave",status);
                var before=await server.Http.GetByteArrayAsync("/api/saves/download");
                var rejected=kind=="oversize"
                    ?await server.Http.PostAsync("/api/saves/checkpoint/load?expectedWorldEpoch=1",null)
                    :await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1",new ByteArrayContent(bad));
                Assert.Equal(kind=="oversize"?HttpStatusCode.RequestEntityTooLarge:HttpStatusCode.BadRequest,rejected.StatusCode);
                Assert.Equal(before,await server.Http.GetByteArrayAsync("/api/saves/download"));
                Assert.Equal(1,(await server.Bootstrap()).WorldEpoch);Assert.Equal(length,new FileInfo(path).Length);
                if(kind!="oversize")Assert.Equal(bad,File.ReadAllBytes(path));
                Assert.True((await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1",new ByteArrayContent(good))).IsSuccessStatusCode);
                Assert.Equal(good,await server.Http.GetByteArrayAsync("/api/saves/download"));
                Assert.Equal(length,new FileInfo(path).Length); // Recovery is deliberate and does not rewrite the input.
                Assert.True((await server.Http.PostAsync("/api/saves/slot2/save?expectedWorldEpoch=2",null)).IsSuccessStatusCode);
                Assert.Equal(good,File.ReadAllBytes(Path.Combine(directory,"slot2.json")));
            }
        }finally{Directory.Delete(directory,true);}
    }
}
