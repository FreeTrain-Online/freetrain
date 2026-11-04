using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class PlaybackRateTests
{
    static readonly DateTimeOffset Start = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static SessionHost Host() => new(SpatialReferenceScenario.Create(), "owner");
    static async Task<WireMessage> Send(SessionHost host, CommandPayload payload, ParticipantContext? participant = null)
    {
        var pending = host.Enqueue(participant ?? new("owner"), new(Guid.NewGuid().ToString(), host.WorldEpoch, payload));
        host.Pump();
        return await pending;
    }

    [Theory]
    [InlineData(0.125, 20)]
    [InlineData(0.25, 40)]
    [InlineData(0.5, 80)]
    [InlineData(1, 160)]
    [InlineData(2, 320)]
    public async Task Realtime_pacing_scales_all_minutes_and_keeps_pause_and_step_exact(double rate, int minutes)
    {
        using var host = Host();
        var before = host.Snapshot();
        Assert.IsType<AcceptedMessage>(await Send(host, new SetPlaybackRateCommand(rate)));
        Assert.Equal(before.Tick, host.Snapshot().Tick);
        Assert.True(host.Snapshot().Paused);
        Assert.Equal(rate, StateDifference.Between(before, host.Snapshot()).PlaybackRate);
        await Send(host, new RunCommand());
        host.PumpRealtime(Start);
        var running = host.Snapshot().Tick;
        for (var i = 1; i <= 160; i++) host.PumpRealtime(Start.AddMilliseconds(i * 50));
        Assert.Equal(minutes, host.Snapshot().Tick - running);
        await Send(host, new PauseCommand());
        var paused = host.Snapshot().Tick;
        Assert.Equal(0, host.PumpRealtime(Start.AddHours(1)).AdvancedTicks);
        await Send(host, new StepCommand());
        Assert.Equal(paused + 1, host.Snapshot().Tick);
        Assert.True(host.Snapshot().Paused);
    }

    [Fact]
    public async Task Speed_changes_are_owner_only_validated_and_do_not_wait_for_a_slow_minute()
    {
        using var host = Host();
        var op = new ParticipantContext("operator", ParticipantRole.Operator);
        host.RegisterParticipant(op);
        Assert.Equal("notAuthorized", Assert.IsType<RejectedMessage>(await Send(host, new SetPlaybackRateCommand(2), op)).Code);
        foreach (var rate in new[] { 0, -1, 0.3, 3, double.NaN, double.PositiveInfinity })
            Assert.Equal("invalidInput", Assert.IsType<RejectedMessage>(await Send(host, new SetPlaybackRateCommand(rate))).Code);
        await Send(host, new SetPlaybackRateCommand(0.125));
        await Send(host, new RunCommand());
        host.PumpRealtime(Start);
        var tick = host.Snapshot().Tick;
        var pending = host.Enqueue(new("owner"), new("pause", host.WorldEpoch, new PauseCommand()));
        host.PumpRealtime(Start.AddMilliseconds(50));
        Assert.IsType<AcceptedMessage>(await pending);
        Assert.Equal(tick, host.Snapshot().Tick);
        Assert.True(host.Snapshot().Paused);
    }

    [Fact]
    public async Task Pause_rate_changes_and_stalls_discard_old_time_allowances()
    {
        using var host = Host();
        await Send(host, new SetPlaybackRateCommand(0.125));
        await Send(host, new RunCommand());
        host.PumpRealtime(Start);
        host.PumpRealtime(Start.AddMilliseconds(50));
        var before = host.Snapshot().Tick;
        var change = host.Enqueue(new("owner"), new("speed", host.WorldEpoch, new SetPlaybackRateCommand(2)));
        Assert.Equal(0, host.PumpRealtime(Start.AddMilliseconds(100)).AdvancedTicks);
        await change;
        Assert.Equal(2, host.PumpRealtime(Start.AddMilliseconds(150)).AdvancedTicks);
        Assert.Equal(2, host.PumpRealtime(Start.AddHours(1)).AdvancedTicks);
        Assert.Equal(before + 4, host.Snapshot().Tick);
    }

    [Fact]
    public async Task Equal_minutes_have_identical_saves_at_every_rate_and_loading_resets_speed()
    {
        byte[]? baseline = null;
        foreach (var rate in new[] { 0.125, 0.25, 0.5, 1, 2 })
        {
            using var host = Host();
            await Send(host, new SetPlaybackRateCommand(rate));
            host.PumpRealtime(Start);
            await Send(host, new RunCommand());
            host.PumpRealtime(Start);
            for (var i = 1; i <= 16 / rate; i++) host.PumpRealtime(Start.AddMilliseconds(i * 50));
            await Send(host, new PauseCommand());
            var saveTask = host.CaptureSave(); host.Pump(); var save = await saveTask;
            if (baseline == null) baseline = save; else Assert.Equal(baseline, save);
            var load = host.LoadSave(save); host.Pump(); await load;
            Assert.Equal(1, host.Snapshot().PlaybackRate);
        }
    }
}
