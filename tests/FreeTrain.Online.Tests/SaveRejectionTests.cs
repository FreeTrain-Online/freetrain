using System.Text;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation.Prototype;
using Xunit;

public class SaveRejectionTests
{
    [Fact]
    public void Actual_retired_reference_file_is_rejected_without_deserializing_its_world()
    {
        var data = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetiredSaves/save-reference.json"));
        Assert.Equal("incompatibleSave", Assert.Throws<InvalidDataException>(() => SaveCodec.Read(data)).Message);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(999)]
    public void No_legacy_or_version_relabelled_save_is_accepted(int version)
    {
        var data = Encoding.UTF8.GetBytes($"{{\"saveVersion\":{version},\"contentVersion\":\"prototype-content-1\"}}");
        Assert.Equal("incompatibleSave", Assert.Throws<InvalidDataException>(() => SaveCodec.Read(data)).Message);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"saveVersion\":\"1\"}")]
    [InlineData("{\"saveVersion\":null}")]
    [InlineData("{\"saveVersion\":0}")]
    [InlineData("{\"saveVersion\":-1}")]
    [InlineData("{\"saveVersion\":1.5}")]
    public void Malformed_headers_have_an_invalid_data_error(string text)
        => Assert.Equal("invalidSave", Assert.Throws<InvalidDataException>(() => SaveCodec.Read(Encoding.UTF8.GetBytes(text))).Message);

    [Fact]
    public void Size_limit_is_checked_before_parsing()
        => Assert.Equal("saveTooLarge", Assert.Throws<InvalidDataException>(() => SaveCodec.Read(new byte[SaveCodec.MaximumBytes + 1])).Message);

    [Theory]
    [InlineData("{\"saveVersion\":1}", "incompatibleSave", false)]
    [InlineData("{\"saveVersion\":9}", "incompatibleSave", false)]
    [InlineData("{broken", "invalidSave", false)]
    [InlineData("{\"saveVersion\":1}", "incompatibleSave", true)]
    [InlineData("{broken", "invalidSave", true)]
    public async Task Rejected_checkpoint_is_discarded_and_fresh_checkpoints_survive_restart(string text, string error, bool customHost)
    {
        var directory = Path.Combine(Path.GetTempPath(), "freetrain-rejected-save-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "checkpoint.json");
        File.WriteAllText(path, text);
        File.WriteAllText(Path.Combine(directory, "slot2.json"), text);
        try
        {
            using var storage = new SaveStorage(directory);
            var session = new NetworkSession(new(), storage, startupWorld: TestWorlds.SmallStartup(),
                hostFactory: customHost ? (epoch, _) => SessionComposition.CreateGeneratedHost(TestWorlds.SmallStartup(), epoch) : null);
            Assert.Equal(error, session.StartupSaveError);
            Assert.True(session.SavingAvailable);
            Assert.Equal(1, session.Bootstrap().WorldEpoch); Assert.Equal(FreeTrain.Online.Simulation.GameTime.StartMinute, session.Bootstrap().Snapshot.Tick);
            Assert.NotNull(session.Bootstrap().Snapshot.Terrain);
            Assert.False(File.Exists(path));
            Assert.True(session.AutomaticCheckpointAvailable);
            var checkpoint = session.Checkpoint(); session.Tick(DateTimeOffset.UtcNow); await checkpoint;
            var saved = storage.Read("checkpoint");
            Assert.NotNull(SaveCodec.ReadWorld(saved, TerrainProfiles.Resolve).Terrain);
            var slot = session.Checkpoint("slot1"); session.Tick(DateTimeOffset.UtcNow); await slot;
            Assert.NotNull(SaveCodec.ReadWorld(File.ReadAllBytes(Path.Combine(directory, "slot1.json")), FreeTrain.Online.Content.TerrainProfiles.Resolve).Terrain);
            session.PrepareShutdown(); await session.FinalCheckpoint();
            Assert.Equal(saved, storage.Read("checkpoint"));
            Assert.Equal(text, File.ReadAllText(Path.Combine(directory, "slot2.json")));
            var restarted = new NetworkSession(new(), storage, startupWorld: new() { Width = -1 });
            Assert.Null(restarted.StartupSaveError);
            var restored = restarted.CaptureSave(); restarted.Tick(DateTimeOffset.UtcNow);
            Assert.Equal(saved, await restored);
            Assert.True(File.Exists(Path.Combine(directory, "slot1.json")));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public async Task Retired_canonical_fixtures_are_rejected_without_replacing_the_current_world(int version)
    {
        var data=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,$"RetiredSaves/reference-v{version}.json"));
        Assert.Equal("incompatibleSave",Assert.Throws<InvalidDataException>(()=>SaveCodec.Read(data)).Message);
        var host=SessionComposition.CreateReferenceHost();
        var capture=host.CaptureSave();host.Pump();var before=await capture;
        var load=host.LoadSave(data,1);host.Pump();
        Assert.Equal("incompatibleSave",(await Assert.ThrowsAsync<InvalidDataException>(()=>load)).Message);
        Assert.Equal(1,host.WorldEpoch);
        var after=host.CaptureSave();host.Pump();Assert.Equal(before,await after);
    }

    [Fact]
    public async Task Unsupported_load_and_capture_remain_in_the_authority_fifo_without_replacing_world()
    {
        var host = new SessionHost(ReferenceScenario.Create(), "owner", [() => new AutomaticController()]);
        var before = JsonSerializer.Serialize(host.Snapshot(), WireJson.CreateOptions());
        var load = host.LoadSave(Encoding.UTF8.GetBytes("{\"saveVersion\":1}"), 1);
        var save = host.CaptureSave(1);
        Assert.False(load.IsCompleted); Assert.False(save.IsCompleted);
        Assert.Equal(2, host.Pump().AppliedCommands);
        Assert.Equal("incompatibleSave", (await Assert.ThrowsAsync<InvalidDataException>(() => load)).Message);
        Assert.Equal("savingUnavailable", (await Assert.ThrowsAsync<InvalidDataException>(() => save)).Message);
        Assert.Equal(before, JsonSerializer.Serialize(host.Snapshot(), WireJson.CreateOptions()));
        var denied = host.LoadSave(Encoding.UTF8.GetBytes("{\"saveVersion\":1}"), 1, new("viewer", ParticipantRole.Observer));
        var stale = host.LoadSave(Encoding.UTF8.GetBytes("{\"saveVersion\":1}"), 2);
        host.Pump();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied);
        Assert.Equal("staleEpoch", (await Assert.ThrowsAsync<InvalidOperationException>(() => stale)).Message);
    }
}
