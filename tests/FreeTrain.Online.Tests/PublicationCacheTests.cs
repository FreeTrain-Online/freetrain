using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using Xunit;

namespace FreeTrain.Online.Tests;

public class PublicationCacheTests
{
    [Fact]
    public async Task Committed_snapshots_are_shared_and_invalidate_on_playback_minutes_and_load()
    {
        var state = CityPersistenceTests.Fixture();
        using var host = new SessionHost(state, "owner", TerrainProfiles.Resolve, RailwayProfiles.Resolve);
        var initial = host.Snapshot();
        Assert.Same(initial, host.Snapshot());
        var rate = host.Enqueue(new("owner"), new("rate", host.WorldEpoch, new SetPlaybackRateCommand(2)));
        host.Pump(0);
        Assert.IsType<AcceptedMessage>(await rate);
        var changed = host.Snapshot();
        Assert.NotSame(initial, changed);
        Assert.Equal(2, changed.PlaybackRate);
        Assert.Same(initial.Mixed!.City!.Heat, changed.Mixed!.City!.Heat);
        var step = host.Enqueue(new("owner"), new("step", host.WorldEpoch, new StepCommand()));
        host.Pump(0);
        Assert.IsType<AcceptedMessage>(await step);
        Assert.NotSame(changed, host.Snapshot());
        Assert.Same(host.Snapshot(), host.Snapshot());
        var load = host.LoadSave(SaveCodec.Write(state));
        host.Pump(0);
        await load;
        Assert.NotEqual(initial.WorldEpoch, host.Snapshot().WorldEpoch);
        Assert.Equal(1, host.Snapshot().PlaybackRate);
    }

    [Fact]
    public async Task Peers_share_only_their_exact_predecessor_delta_and_encoding()
    {
        using var host = new SessionHost(CityPersistenceTests.Fixture(), "a", TerrainProfiles.Resolve, RailwayProfiles.Resolve);
        var network = new NetworkSession(new(), hostFactory: (_, _) => host);
        var now = DateTimeOffset.UtcNow;
        var a = network.Connect(now, new("a"))!;
        var b = network.Connect(now, new("b"))!;
        a.Output.Reader.TryRead(out _); b.Output.Reader.TryRead(out _);
        var command = network.Submit(a, new("step", host.WorldEpoch, new StepCommand()), now);
        network.Tick(now);
        Assert.IsType<AcceptedMessage>(await command);
        var first = Assert.IsType<DeltaMessage>(await a.Output.Reader.ReadAsync());
        Assert.Same(first, await b.Output.Reader.ReadAsync());
        Assert.True(OutboundWire.Serialize(first).Equals(OutboundWire.Serialize(first)));

        // Resync gives b a distinct baseline object, even at the same revision.
        network.Resync(b);
        var baseline = Assert.IsType<SnapshotMessage>(await b.Output.Reader.ReadAsync());
        command = network.Submit(a, new("step2", host.WorldEpoch, new StepCommand()), now);
        network.Tick(now.AddMilliseconds(100));
        Assert.IsType<AcceptedMessage>(await command);
        var nextA = Assert.IsType<DeltaMessage>(await a.Output.Reader.ReadAsync());
        var nextB = Assert.IsType<DeltaMessage>(await b.Output.Reader.ReadAsync());
        Assert.NotSame(nextA, nextB);
        Assert.Equal(first.Revision, nextA.BaseRevision);
        Assert.Equal(baseline.Revision, nextB.BaseRevision);
    }

    [Fact]
    public void Envelope_matches_wire_json_and_rejects_oversized_messages()
    {
        WireMessage message = new RejectedMessage("r", "invalidInput", new() { ["detail"] = "å\n\"" });
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(message, NetworkSession.Json), OutboundWire.Serialize(message).ToArray());
        Assert.Throws<InvalidDataException>(() => OutboundWire.Serialize(new RejectedMessage("r", new string('x', OutboundWire.MaximumBytes), new())));
    }
}
