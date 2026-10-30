using System.Security.Cryptography;
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

public class ReferenceWorldTests
{
    [Fact]
    public void Independent_octagon_has_source_patterns_reciprocal_neighbors_and_all_headings()
    {
        // Hand-derived perimeter: E, SE, S, SW, W, NW, N, NE, four steps each.
        var world = SpatialReferenceScenario.Create();
        Assert.Equal(32, world.Rails.Length);
        Assert.Equal(new Location(27,24,0), world.Rails[0].Position);
        Assert.Equal(new Location(31,24,0), world.Rails[4].Position);
        Assert.Equal(new Location(35,28,0), world.Rails[8].Position);
        Assert.Equal(new Location(35,32,0), world.Rails[12].Position);
        Assert.Equal(new Location(31,36,0), world.Rails[16].Position);
        Assert.Equal(new Location(27,36,0), world.Rails[20].Position);
        Assert.Equal(new Location(23,32,0), world.Rails[24].Position);
        Assert.Equal(new Location(23,28,0), world.Rails[28].Position);
        Assert.Equal(new[] { 3,4,4,4,6,7,7,7,9,10,10,10,11,1,1,1,2,4,4,4,5,7,7,7,8,10,10,10,0,1,1,1 }, world.Rails.Select(r=>r.Pattern));
        Assert.All(world.Rails, r => Assert.Equal(r.Position, world.Space.ToLocation(world.Space.ToStorage(r.Position))));
    }

    [Fact]
    public void Shared_save_fixture_and_asset_free_pack_hash_match_the_runtime()
    {
        var bytes=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/reference-v7.json"));
        Assert.Equal(SaveCodec.Write(SpatialReferenceScenario.Create()),SaveCodec.Write(SaveCodec.Read(bytes)));
        var catalog=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/reference-spatial.json"));
        Assert.Equal(ProtocolIdentity.ReferencePacks[0].CatalogHash,Convert.ToHexStringLower(SHA256.HashData(catalog)));
        var patterns=JsonNode.Parse(catalog)!["normalRailPorts"]!.AsArray();
        Assert.Equal(NormalRailPatterns.Ports.Length,patterns.Count);
        for(var i=0;i<patterns.Count;i++)Assert.Equal(new[]{NormalRailPatterns.Ports[i].A,NormalRailPatterns.Ports[i].B},patterns[i]!.AsArray().Select(p=>(int)p!));
    }

    [Fact]
    public void Live_composition_does_not_advance_or_expose_prototype_gameplay()
    {
        var host = SessionComposition.CreateReferenceHost();
        var before = host.Snapshot();
        Assert.Equal("hv", before.Map.CoordinateSystem);
        Assert.Equal(ProtocolIdentity.ReferenceRuleset,before.Ruleset);
        Assert.Empty(before.Trains); Assert.Empty(before.Stations); Assert.Empty(before.Buildings);
        Assert.Equal(0,host.Pump(256).AdvancedTicks);
        Assert.Equal(JsonSerializer.Serialize(before),JsonSerializer.Serialize(host.Snapshot()));
        Assert.Throws<InvalidOperationException>(()=>host.ReadState());
    }

    [Fact]
    public async Task Prototype_gameplay_mutations_are_unavailable_in_the_canonical_world()
    {
        var host=SessionComposition.CreateReferenceHost();
        CommandPayload[] operations=[new BuildTrackCommand(new(27,24,0),"eastWest"),
            new RemoveTrackCommand(new(27,24,0),0),new RenameStationCommand("station-1",0,"name"),new AcquireControlCommand("train-1"),
            new SetControllerCommand("train-1",0,"manual","1")];
        foreach(var operation in operations)
        {
            var result=host.Enqueue(new("operator-1"),new(Guid.NewGuid().ToString(),1,operation));host.Pump();
            Assert.Equal("operationUnavailable",Assert.IsType<RejectedMessage>(await result).Code);
        }
        Assert.Equal(GameTime.StartMinute,host.Snapshot().Tick); Assert.Equal(0,host.Snapshot().Revision);
    }

    [Fact]
    public async Task Save_roundtrip_fences_queued_commands_and_a_second_load_and_omits_session_state()
    {
        var host=SessionComposition.CreateReferenceHost();
        var capture=host.CaptureSave(1);host.Pump();var bytes=await capture;
        Assert.Equal(bytes,SaveCodec.Write(SaveCodec.Read(bytes)));
        var load=host.LoadSave(bytes,1);
        var command=host.Enqueue(new("operator-1"),new("late",1,new RunCommand()));
        var staleLoad=host.LoadSave(bytes,1);host.Pump();
        Assert.Equal(2,(await load).WorldEpoch);
        Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await command).Code);
        Assert.Equal("staleEpoch",(await Assert.ThrowsAsync<InvalidOperationException>(()=>staleLoad)).Message);
        var text=Encoding.UTF8.GetString(bytes);
        foreach(var field in new[]{"worldEpoch","lease","participant","cash","tick","controller"})Assert.DoesNotContain(field,text);
        Assert.Equal(bytes,SaveCodec.Write(SaveCodec.Read(bytes)));
    }

    [Theory]
    [InlineData("wrongCase")][InlineData("multipleLoops")][InlineData("duplicateRail")][InlineData("wrongPattern")][InlineData("outOfBounds")][InlineData("nullRail")]
    [InlineData("missingRail")][InlineData("unknownField")][InlineData("wrongPack")][InlineData("wrongCoordinates")]
    [InlineData("oversizeMap")][InlineData("unknownVersion")][InlineData("duplicateField")]
    public async Task Invalid_graphs_and_incompatible_identities_leave_world_and_epoch_untouched(string mutation)
    {
        var host=SessionComposition.CreateReferenceHost();
        var before=JsonSerializer.Serialize(host.Snapshot());
        var node=JsonNode.Parse(SaveCodec.Write(SpatialReferenceScenario.Create()))!;
        switch(mutation)
        {
            case "wrongCase":node["Map"]=node["map"]!.DeepClone();node.AsObject().Remove("map");break;
            case "multipleLoops":
                node["map"]!["width"]=64;node["map"]!["height"]=128;
                var originals=node["rails"]!.AsArray().Select(r=>r!.DeepClone()).ToArray();
                node["rails"]=new JsonArray();
                foreach(var shift in new[]{(32,0),(48,16)})foreach(var original in originals)
                {
                    var rail=original.DeepClone();rail["x"]=(int)rail["x"]!+shift.Item1;rail["y"]=(int)rail["y"]!+shift.Item2;
                    node["rails"]!.AsArray().Add(rail);
                }
                break;
            case "duplicateRail":node["rails"]!.AsArray().Add(node["rails"]![0]!.DeepClone());break;
            case "wrongPattern":node["rails"]![0]!["pattern"]=99;break;
            case "outOfBounds":node["rails"]![0]!["x"]=int.MinValue;break;
            case "nullRail":node["rails"]![0]=null;break;
            case "missingRail":node["rails"]!.AsArray().RemoveAt(0);break;
            case "unknownField":node["unexpected"]=0;break;
            case "wrongPack":node["requiredPacks"]![0]!["catalogHash"]=new string('0',64);break;
            case "wrongCoordinates":node["map"]!["coordinateSystem"]="prototypeXY";break;
            case "oversizeMap":node["map"]!["width"]=int.MaxValue;break;
            case "unknownVersion":node["saveVersion"]=99;break;
        }
        var text=node.ToJsonString();
        if(mutation=="duplicateField")text=text.Replace("\"saveVersion\":7", "\"saveVersion\":7,\"saveVersion\":7");
        var result=host.LoadSave(Encoding.UTF8.GetBytes(text),1);host.Pump();
        await Assert.ThrowsAsync<InvalidDataException>(()=>result);
        Assert.Equal(before,JsonSerializer.Serialize(host.Snapshot()));
    }

    [Fact]
    public async Task Canonical_http_save_upload_socket_epoch_and_restart_are_real_operations()
    {
        var directory=Path.Combine(Path.GetTempPath(),"freetrain-canonical-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try
        {
            byte[] saved;
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true,referenceFixture:true))
            {
                var join=await server.Bootstrap(); Assert.Equal(ProtocolIdentity.ReferenceContent,join.ContentVersion);
                using var socket=await server.Connect();await NetworkTests.Read(socket);
                var query=new SpatialQuery(1,new(27,24,0),null,0);
                var response=await server.Http.PostAsync("/api/spatial/preview?protocolVersion=5&contentVersion="+ProtocolIdentity.ReferenceContent,
                    new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(query,NetworkSession.Json)));
                Assert.True(response.IsSuccessStatusCode);
                var preview=JsonSerializer.Deserialize<SpatialPreviewRead>(await response.Content.ReadAsStringAsync(),NetworkSession.Json)!;
                Assert.True(preview.Inside);Assert.Equal(new StoragePositionRead(10,28,0),preview.Storage);
                saved=await server.Http.GetByteArrayAsync("/api/saves/download");
                var validate=await server.Http.PostAsync("/api/saves/validate?expectedWorldEpoch=1",new ByteArrayContent(saved));
                Assert.True(validate.IsSuccessStatusCode);Assert.Equal(1,(await server.Bootstrap()).WorldEpoch);
                using var oversizedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/saves/validate?expectedWorldEpoch=1")
                    { Content = new ByteArrayContent(new byte[SaveCodec.MaximumBytes+1]) };
                oversizedRequest.Headers.ExpectContinue = true;
                var oversized=await server.Http.SendAsync(oversizedRequest);
                Assert.Equal(System.Net.HttpStatusCode.RequestEntityTooLarge,oversized.StatusCode);
                Assert.True((await server.Http.PostAsync("/api/saves/slot1/save?expectedWorldEpoch=1",null)).IsSuccessStatusCode);
                Assert.Equal(saved,File.ReadAllBytes(Path.Combine(directory,"slot1.json")));
                Assert.True((await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1",new ByteArrayContent(saved))).IsSuccessStatusCode);
                Assert.Equal(2,Assert.IsType<SnapshotMessage>(await NetworkTests.Until(socket,m=>m is SnapshotMessage)).WorldEpoch);
                Assert.Equal(2,(await server.Bootstrap()).WorldEpoch);
            }
            await using(var restarted=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                var join=await restarted.Bootstrap();Assert.Equal(2,join.WorldEpoch);
                Assert.Equal(saved,await restarted.Http.GetByteArrayAsync("/api/saves/download"));
            }
        }
        finally{Directory.Delete(directory,true);}
    }
}
