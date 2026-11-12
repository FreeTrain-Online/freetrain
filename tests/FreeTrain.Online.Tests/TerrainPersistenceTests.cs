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

public class TerrainPersistenceTests
{
    public static TerrainSessionState Scene() => TerrainTransportScenario.Create();
    private static TerrainSessionState Decode(byte[] bytes) => SaveCodec.ReadWorld(bytes, TerrainProfiles.Resolve).Terrain!;
    private static byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());

    private static void EditPacked(JsonNode node, string field, Func<byte[], byte[]> edit)
    { node["packedTerrain"]![field] = Convert.ToBase64String(edit(Convert.FromBase64String(node["packedTerrain"]![field]!.GetValue<string>()))); }

    [Fact]
    public void Actual_state_round_trip_digest_and_continuation_preserve_edits_without_regeneration()
    {
        var original = Scene(); var bytes = SaveCodec.Write(original); var restored = Decode(bytes);
        Assert.Equal(bytes, SaveCodec.Write(restored)); Assert.Equal(SaveCodec.Digest(original), SaveCodec.Digest(restored));
        Assert.Empty(restored.World.Rails); Assert.Equal(7, restored.World.Spatial.NextEntityId);
        Assert.Equal(3, restored.World.WaterLevel); Assert.Equal(2, restored.World.Cell(0, 0).Ground);
        Assert.True(restored.ConstructionMode); Assert.Equal(0x1234, restored.World.Occupants[1].Corners.Packed);
        Assert.Equal(new TerrainTree(30, 15, 8), restored.World.Occupants[1].Forest!.Trees[1]);
        Assert.Equal(13, restored.World.Occupants[2].CreekConnections);
        foreach (var id in RandomStreams.Ids) Assert.Equal(original.Random[id].Next(), restored.Random[id].Next());
        original.AdvanceOneMinute(); restored.AdvanceOneMinute(); Assert.Equal(SaveCodec.Write(original), SaveCodec.Write(restored));
        foreach (var field in new[] { "worldEpoch", "participantId", "ConnectionId", "lease", "imageBytes", "/Users/" })
            Assert.DoesNotContain(field, Encoding.UTF8.GetString(bytes));
        var removed = restored.World.Replace(restored.World.Revision, restored.World.Cells.Select(c => c with { Surface = TerrainSurface.Cut }),
            1, new(7, []), []);
        restored.Replace(restored.Revision, removed, false);
        var final = Decode(SaveCodec.Write(restored)); Assert.Empty(final.World.Occupants); Assert.Equal(7, final.World.Spatial.NextEntityId);
        Assert.False(final.ConstructionMode);
    }

    [Theory]
    [InlineData("missingPack")][InlineData("wrongHash")][InlineData("extraPack")][InlineData("definition")]
    [InlineData("nullCell")][InlineData("shortGrid")][InlineData("surface")][InlineData("corner")][InlineData("forest")]
    [InlineData("overlap")][InlineData("allocator")][InlineData("mode")][InlineData("provenance")]
    [InlineData("geometryRevision")][InlineData("clock")][InlineData("random")][InlineData("coordinate")]
    [InlineData("duplicate")][InlineData("unknown")][InlineData("rails")][InlineData("dimension")]
    public async Task Rejected_terrain_keeps_current_world_epoch_and_checkpoint_bytes(string mutation)
    {
        var original = Scene(); var saved = SaveCodec.Write(original); var node = JsonNode.Parse(saved)!;
        switch (mutation)
        {
            case "missingPack": node["requiredPacks"] = new JsonArray(); break;
            case "wrongHash": node["requiredPacks"]![0]!["catalogHash"] = new string('0', 64); break;
            case "extraPack": node["requiredPacks"]!.AsArray().Add(node["requiredPacks"]![0]!.DeepClone()); break;
            case "definition": node["packedTerrain"]!["definitions"]![0]!["packId"] = "missing"; break;
            case "nullCell": node["packedTerrain"]!["cells"] = null; break;
            case "shortGrid": EditPacked(node, "cells", b => b[..^1]); break;
            case "surface": EditPacked(node, "cells", b => { b[0] = 255; return b; }); break;
            case "corner": EditPacked(node, "occupants", b => { b[81] = 0x52; return b; }); break;
            case "forest": EditPacked(node, "occupants", b => { b[88] = 99; return b; }); break;
            case "overlap": EditPacked(node, "occupants", b => { b.AsSpan(61, 9).CopyTo(b.AsSpan(110, 9)); return b; }); break;
            case "allocator": node["spatial"]!["nextEntityId"] = "2"; node["packedTerrain"]!["nextEntityId"] = "2"; break;
            case "mode": node["packedTerrain"]!["constructionMode"] = "true"; break;
            case "provenance": node["packedTerrain"]!["provenance"]!["requestHash"] = new string('0', 64); break;
            case "geometryRevision": node["packedTerrain"]!["geometryRevision"] = "99"; break;
            case "clock": node["clock"]!["absoluteMinute"] = "-1"; break;
            case "random": node["random"]!["streams"]!.AsArray().RemoveAt(0); break;
            case "coordinate": node["map"]!["coordinateSystem"] = "prototypeXY"; break;
            case "unknown": node["packedTerrain"]!["fallback"] = true; break;
            case "rails": node["rails"]!.AsArray().Add(new JsonObject()); break;
            case "dimension": node["map"]!["width"] = int.MaxValue; break;
        }
        var bad = mutation == "duplicate" ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(saved).Replace("\"saveVersion\":11", "\"saveVersion\":11,\"saveVersion\":11")) : Bytes(node);
        var directory = Path.Combine(Path.GetTempPath(), "freetrain-terrain-reject-" + Guid.NewGuid());
        try
        {
            using var storage = new SaveStorage(directory); await storage.Write("checkpoint", saved);
            var host = new SessionHost(original, "owner", TerrainProfiles.Resolve);
            var before = JsonSerializer.Serialize(host.Snapshot()); var load = host.LoadSave(bad, 1); host.Pump();
            await Assert.ThrowsAsync<InvalidDataException>(() => load);
            Assert.Equal(1, host.WorldEpoch); Assert.Equal(before, JsonSerializer.Serialize(host.Snapshot()));
            Assert.Equal(saved, storage.Read("checkpoint"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Two_peers_receive_replacement_snapshot_then_clock_delta_and_restart_real_checkpoint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "freetrain-terrain-network-" + Guid.NewGuid());
        try
        {
            byte[] saved;
            using (var storage = new SaveStorage(directory))
            {
                var session = new NetworkSession(new(), storage, startupWorld: TestWorlds.SmallStartup());
                var now = DateTimeOffset.UtcNow;
                var a = session.Connect(now, new("a"))!; var b = session.Connect(now, new("b", ParticipantRole.Observer))!;
                a.Output.Reader.TryRead(out _); b.Output.Reader.TryRead(out _);
                var load = session.LoadSave(SaveCodec.Write(Scene()), 1);
                while (!load.IsCompleted) { session.Tick(now); await Task.Delay(1); }
                await load;
                foreach (var peer in new[] { a, b })
                {
                    var snapshot = Assert.IsType<SnapshotMessage>(await peer.Output.Reader.ReadAsync());
                    Assert.Equal(2, snapshot.WorldEpoch); Assert.NotNull(snapshot.Terrain); Assert.Empty(snapshot.Tracks);
                }
                var stale = session.Submit(a, new("stale", 1, new RunCommand()), now); session.Tick(now); Assert.Equal("staleEpoch", Assert.IsType<RejectedMessage>(await stale).Code);
                var denied = session.Submit(b, new("denied", 2, new StepCommand()), now); Assert.Equal("notAuthorized", Assert.IsType<RejectedMessage>(await denied).Code);
                var step = session.Submit(a, new("step", 2, new StepCommand()), now); session.Tick(now); session.Reply(a, await step);
                foreach (var peer in new[] { a, b })
                {
                    var delta = Assert.IsType<DeltaMessage>(await peer.Output.Reader.ReadAsync()); Assert.Empty(delta.Terrain!.Cells);
                    Assert.Empty(delta.Terrain.Occupants); Assert.Empty(delta.Removed.EntityIds!); Assert.Equal(2, delta.WorldEpoch);
                }
                var capture = session.CaptureSave(); session.Tick(now); saved = await capture;
                var checkpoint = session.Checkpoint(); session.Tick(now); await checkpoint;
            }
            using (var storage = new SaveStorage(directory))
            {
                var restarted = new NetworkSession(new(), storage, startupWorld: TestWorlds.SmallStartup()); Assert.Null(restarted.StartupSaveError);
                Assert.Equal(TerrainProfile.Ruleset, restarted.Bootstrap().Ruleset);
                var capture = restarted.CaptureSave(); restarted.Tick(DateTimeOffset.UtcNow); Assert.Equal(saved, await capture);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Changed_ground_occupants_and_removals_are_explicit_and_old_immutable_snapshot_survives()
    {
        var state = Scene(); var before = TerrainWire.Snapshot(state, 1);
        var cells = state.World.Cells.Select(c => c with { Ground = 1, Surface = TerrainSurface.Cut });
        state.Replace(state.Revision, state.World.Replace(4, cells, 0, new(7, []), []), false);
        var after = TerrainWire.Snapshot(state, 1); var delta = NetworkSession.Difference(before, after);
        Assert.Equal(9, delta.Terrain!.Cells.Length); Assert.Empty(delta.Entities!); Assert.Empty(delta.Terrain.Occupants);
        Assert.Equal(new long[] { 1, 2, 3 }, delta.Removed.EntityIds); Assert.Equal(3, before.Terrain!.Occupants.Length);
        Assert.Equal(7, delta.Terrain.NextEntityId); Assert.False(delta.Terrain.ConstructionMode);
        Assert.Throws<ArgumentException>(() => NetworkSession.Difference(before, after with { WorldEpoch = 2 }));
    }

    [Fact]
    public async Task Roles_epoch_and_revision_are_fenced_on_terrain_adapter_boundary()
    {
        var state = Scene(); var host = new SessionHost(state, "owner", TerrainProfiles.Resolve);
        var candidate = state.World.Replace(4, state.World.Cells.Select(c => c with { Surface = TerrainSurface.Cut }), 1, new(7, []), []);
        var deny = host.ApplyTerrain(1, state.Revision, candidate, false, new("viewer", ParticipantRole.Observer));
        var stale = host.ApplyTerrain(2, state.Revision, candidate, false, new("owner"));
        var revision = host.ApplyTerrain(1, state.Revision - 1, candidate, false, new("owner"));
        var ok = host.ApplyTerrain(1, state.Revision, candidate, false, new("owner")); host.Pump();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => deny); await Assert.ThrowsAsync<InvalidOperationException>(() => stale);
        await Assert.ThrowsAsync<InvalidOperationException>(() => revision); await ok;
        Assert.Empty(host.Snapshot().Entities!); Assert.Equal(7, host.Snapshot().Terrain!.NextEntityId);
    }
    [Fact]
    public async Task Empty_world_and_import_provenance_restore_without_input_image_or_rail_loop()
    {
        var image = new HeightmapInput(new string('a', 64), "image/png", 67, 1, 1, 1);
        foreach (WorldCreationRequest request in new WorldCreationRequest[] { WorldCreationProfile.Empty(""), WorldCreationProfile.Heightmap(image, "import") })
        {
            var empty = TerrainWorld.Empty(new(request.Profile, request.Name, request.Dimensions, request.WaterLevel), TerrainProfiles.Bundled);
            var state = new TerrainSessionState(empty, TerrainProvenance.From(request));
            var restored = Decode(SaveCodec.Write(state));
            Assert.Empty(restored.World.Spatial.Entities); Assert.Empty(restored.World.Occupants); Assert.Empty(restored.World.Rails);
            Assert.Equal(state.Provenance, restored.Provenance);
            var host = new SessionHost(restored, "owner", TerrainProfiles.Resolve);
            var p = empty.Space.ToLocation(new(0, 0, 1));
            var preview = host.PreviewSpatial(new(1, new(p.X, p.Y, p.Z), null, 0));
            Assert.True(preview.Inside); Assert.Equal(new StoragePositionRead(0, 0, 1), preview.Storage);
            var run = host.Enqueue(new("owner"), new("run", 1, new RunCommand())); host.Pump(1); await run;
            Assert.False(host.Snapshot().Paused); Assert.Equal(GameTime.StartMinute + 1, host.Snapshot().Tick);
        }
    }

    [Fact]
    public async Task Real_http_validation_upload_two_sockets_and_restart_keep_terrain_and_preserve_bad_input()
    {
        var directory = Path.Combine(Path.GetTempPath(), "freetrain-terrain-http-" + Guid.NewGuid());
        Directory.CreateDirectory(directory); var data = SaveCodec.Write(Scene());
        var query = "/api/bootstrap?protocolVersion=10&contentVersion=" + TerrainProfile.ContentVersion;
        try
        {
            await using (var server = await NetworkTests.Harness.Start(saveDirectory: directory, canonical: true))
            {
                await server.Bootstrap();
                using var first = await server.Connect();
                var secondCookies = new System.Net.CookieContainer();
                using var secondHttp = new HttpClient(new HttpClientHandler { CookieContainer = secondCookies }) { BaseAddress = server.Http.BaseAddress };
                await secondHttp.GetStringAsync("/api/bootstrap?protocolVersion=10&contentVersion=" + TerrainProfile.ContentVersion);
                using var second = await server.Connect(secondCookies);
                await NetworkTests.Read(first); await NetworkTests.Read(second);
                Assert.True((await server.Http.PostAsync("/api/saves/validate?expectedWorldEpoch=1", new ByteArrayContent(data))).IsSuccessStatusCode);
                Assert.True((await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1", new ByteArrayContent(data))).IsSuccessStatusCode);
                foreach (var socket in new[] { first, second })
                {
                    var snapshot = Assert.IsType<SnapshotMessage>(await NetworkTests.Until(socket, m => m is SnapshotMessage));
                    Assert.Equal(2, snapshot.WorldEpoch); Assert.Equal(3, snapshot.Terrain!.WaterLevel); Assert.Empty(snapshot.Tracks);
                }
                Assert.Equal(data, await server.Http.GetByteArrayAsync("/api/saves/download"));
                var bad = JsonNode.Parse(data)!; bad["requiredPacks"] = new JsonArray();
                var response = await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=2", new ByteArrayContent(Bytes(bad)));
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(data, await server.Http.GetByteArrayAsync("/api/saves/download"));
                var bootstrap = JsonSerializer.Deserialize<WireMessage>(await server.Http.GetStringAsync(query), NetworkSession.Json) as JoinMessage;
                Assert.Equal(2, bootstrap!.WorldEpoch);
            }
            await using (var restarted = await NetworkTests.Harness.Start(saveDirectory: directory, canonical: true))
            {
                var bootstrap = Assert.IsType<JoinMessage>(JsonSerializer.Deserialize<WireMessage>(await restarted.Http.GetStringAsync(query), NetworkSession.Json));
                Assert.Equal(2, bootstrap.WorldEpoch); Assert.Equal(data, await restarted.Http.GetByteArrayAsync("/api/saves/download"));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Missing_pack_checkpoint_is_discarded_and_replaced_on_shutdown()
    {
        var node = JsonNode.Parse(SaveCodec.Write(Scene()))!; node["requiredPacks"]![0]!["catalogHash"] = new string('f', 64);
        var rejected = Bytes(node); var directory = Path.Combine(Path.GetTempPath(), "freetrain-terrain-pack-" + Guid.NewGuid());
        try
        {
            using var storage = new SaveStorage(directory); await storage.Write("checkpoint", rejected);
            var session = new NetworkSession(new(), storage, startupWorld: TestWorlds.SmallStartup()); Assert.Equal("incompatibleSave", session.StartupSaveError);
            Assert.Equal(TerrainProfile.Ruleset, session.Bootstrap().Ruleset);
            Assert.False(storage.Exists("checkpoint"));
            Assert.True(session.AutomaticCheckpointAvailable);
            session.PrepareShutdown(); await session.FinalCheckpoint();
            Assert.NotNull(SaveCodec.ReadWorld(storage.Read("checkpoint"), TerrainProfiles.Resolve).Terrain);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Shared_fixtures_bind_real_codec_snapshot_and_changed_payloads_with_monotonic_ids()
    {
        var state = Scene(); var before = TerrainWire.Snapshot(state, 1);
        var options = WireJson.CreateOptions();
        var snapshot = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/snapshot-terrain.json")));
        Assert.True(JsonNode.DeepEquals(snapshot, JsonSerializer.SerializeToNode<WireMessage>(before, options)));
        var saved = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Saves/terrain-v11.json"));
        Assert.Equal(SaveCodec.Write(state), SaveCodec.Write(Decode(saved)));
        state.Replace(state.Revision, TerrainTransportScenario.Edit(state), true);
        var delta = NetworkSession.Difference(before, TerrainWire.Snapshot(state, 1));
        Assert.Equal(new long[] { 3 }, delta.Removed.EntityIds); Assert.Equal(new long[] { 2, 7 }, delta.Entities!.Select(e => e.Id));
        Assert.Equal(new TerrainTreeRead(17, 9, 1), Assert.Single(delta.Terrain!.Occupants.Single(o => o.EntityId == 2).Forest!.Trees));
        Assert.Empty(delta.Terrain.Cells); Assert.Equal(8, delta.Terrain.NextEntityId);
        var shared = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/delta-terrain-update.json")));
        Assert.True(JsonNode.DeepEquals(shared, JsonSerializer.SerializeToNode<WireMessage>(delta, options)));
        Assert.Equal(SaveCodec.Write(state), SaveCodec.Write(Decode(SaveCodec.Write(state))));
    }

    [Fact]
    public void Equivalent_decoded_forest_arrays_do_not_require_a_spurious_entity_revision()
    {
        var state = Scene(); var decoded = Decode(SaveCodec.Write(state)); var world = state.World;
        var replacement = world.Replace(world.Revision, decoded.World.Cells, decoded.World.WaterLevel,
            new(decoded.World.Spatial.NextEntityId, decoded.World.Spatial.Entities), decoded.World.Occupants);
        Assert.Equal(0, replacement.Spatial.Entities.Single(e => e.Id == 2).Revision);
        Assert.Equal(world.Occupants[1].Forest!.Trees.ToArray(), replacement.Occupants[1].Forest!.Trees.ToArray());
    }

}
