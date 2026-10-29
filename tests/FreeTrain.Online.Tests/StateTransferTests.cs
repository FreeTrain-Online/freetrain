using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using Xunit;

public class StateTransferTests
{
    [Fact]
    public void Dense_128_world_uses_interned_definitions_and_exact_bounded_chunks()
    {
        var state = TerrainCapacityScenario.Create(128, 128, 32);
        var snapshot = TerrainWire.Snapshot(state, 9); var bytes = StateTransfer.Serialize(snapshot);
        Assert.InRange(bytes.Length, 2_000_000, 3_000_000);
        var chunks = StateTransfer.Chunks(snapshot).ToArray();
        Assert.Equal(47, chunks.Length); var reassembled = new List<byte>();
        for (var i = 0; i < chunks.Length; i++)
        {
            var c = chunks[i]; Assert.Equal(i, c.Index); Assert.Equal(9, c.WorldEpoch); Assert.Equal(snapshot.Revision, c.Revision);
            Assert.Equal(bytes.Length, c.TotalBytes); Assert.Equal(chunks.Length, c.Count);
            Assert.Equal(chunks[0].TransferId, c.TransferId); Assert.InRange(c.Data.Length, 1, ProtocolIdentity.StateChunkBytes);
            Assert.True(JsonSerializer.SerializeToUtf8Bytes<WireMessage>(c, NetworkSession.Json).Length < 66 * 1024);
            reassembled.AddRange(c.Data);
        }
        Assert.Equal(bytes, reassembled);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), chunks[0].Sha256);
        var payload = JsonSerializer.Deserialize<StateTransferPayload>(bytes, NetworkSession.Json)!;
        Assert.Null(Assert.IsType<SnapshotMessage>(payload.Message).Terrain);
        Assert.Equal(3, payload.PackedTerrain!.Definitions.Length);
        Assert.Equal(32768, payload.PackedTerrain.OccupantCount);
        Assert.Equal(SaveCodec.Write(state), SaveCodec.Write(payload.PackedTerrain.Restore(snapshot.Map, TerrainProfiles.Bundled,
            state.Clock.Capture(), state.Paused, state.Random.Capture(), state.Revision)));
    }

    [Fact]
    public void Scalar_publication_reuses_geometry_and_does_not_reserialize_or_report_terrain_changes()
    {
        var state = TerrainCapacityScenario.Create(128, 128, 32); var a = TerrainWire.Snapshot(state, 1);
        Assert.Same(a, TerrainWire.Snapshot(state, 1));
        state.AdvanceOneMinute(); var b = TerrainWire.Snapshot(state, 1);
        Assert.Same(a.Entities, b.Entities); Assert.Same(a.Terrain!.Cells, b.Terrain!.Cells); Assert.Same(a.Terrain.Occupants, b.Terrain.Occupants);
        var delta = NetworkSession.Difference(a, b);
        Assert.Empty(delta.Entities!); Assert.Empty(delta.Terrain!.Cells); Assert.Empty(delta.Terrain.Occupants);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes<WireMessage>(delta, NetworkSession.Json).Length < 1024);
    }

    [Fact]
    public async Task Slow_snapshot_writer_coalesces_live_changes_without_blocking_other_peers_or_losing_the_base_revision()
    {
        var session = new NetworkSession(new() { OutputCapacity = 2 }, hostFactory: (epoch, _) =>
            new SessionHost(TerrainCapacityScenario.Create(32, 32, 16), "owner", TerrainProfiles.Resolve, worldEpoch: epoch));
        var now = DateTimeOffset.UtcNow;
        var owner = session.Connect(now, new("owner"))!;
        var observer = session.Connect(now, new("observer", ParticipantRole.Observer))!;
        owner.Output.Reader.TryRead(out _);
        var join = Assert.IsType<JoinMessage>(await observer.Output.Reader.ReadAsync());
        observer.StateTransferInProgress = true;
        for (var i = 0; i < 10; i++)
        {
            now = now.AddMilliseconds(100);
            var step = session.Submit(owner, new("step-" + i, join.WorldEpoch, new StepCommand()), now);
            session.Tick(now); session.Reply(owner, await step);
            while (owner.Output.Reader.TryRead(out _)) { }
            Assert.False(observer.Output.Reader.TryRead(out _));
            Assert.False(observer.Stop.IsCancellationRequested);
        }
        observer.StateTransferInProgress = false;
        session.Tick(now.AddMilliseconds(100));
        var delta = Assert.IsType<DeltaMessage>(await observer.Output.Reader.ReadAsync());
        Assert.Equal(join.Snapshot.Revision, delta.BaseRevision); Assert.Equal(join.Snapshot.Tick + 10, delta.Tick);
        Assert.Empty(delta.Entities!); Assert.Empty(delta.Terrain!.Cells); Assert.Empty(delta.Terrain.Occupants);
        Assert.False(observer.Output.Reader.TryRead(out _));
    }
}
