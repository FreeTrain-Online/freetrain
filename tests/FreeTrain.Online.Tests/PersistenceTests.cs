using FreeTrain.Online.Contracts;
using FreeTrain.Online.Server;
using Xunit;

public class PersistenceTests
{
    [Fact]
    public async Task Station_sound_has_a_live_event_id_and_is_absent_from_reconnect_snapshots()
    {
        var session = new NetworkSession(new()); var peer = session.Connect(DateTimeOffset.UtcNow)!;
        while (peer.Output.Reader.TryRead(out _)) { }
        for (var i = 0; i < 8; i++)
        {
            var step = session.Submit(peer, new("step-" + i, 1, new StepCommand()), DateTimeOffset.UtcNow);
            session.Tick(DateTimeOffset.UtcNow); await step;
        }
        var sounds = new List<SoundEventMessage>();
        while (peer.Output.Reader.TryRead(out var message)) if (message is SoundEventMessage sound) sounds.Add(sound);
        Assert.Contains(":1:8:train-1:stationArrived", Assert.Single(sounds).EventId);
        session.Disconnect(peer, DateTimeOffset.UtcNow);
        var next = session.Connect(DateTimeOffset.UtcNow)!;
        Assert.IsType<JoinMessage>(await next.Output.Reader.ReadAsync());
        Assert.False(next.Output.Reader.TryRead(out _));
    }
}
