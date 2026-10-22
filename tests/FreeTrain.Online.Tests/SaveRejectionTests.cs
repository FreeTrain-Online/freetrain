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
    [InlineData("{\"saveVersion\":1}", "incompatibleSave")]
    [InlineData("{broken", "invalidSave")]
    public async Task Ignored_checkpoint_is_reported_and_preserved_through_shutdown_and_manual_save(string text, string error)
    {
        var directory = Path.Combine(Path.GetTempPath(), "freetrain-rejected-save-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "checkpoint.json");
        File.WriteAllText(path, text);
        try
        {
            using var storage = new SaveStorage(directory);
            var session = new NetworkSession(new(), storage);
            Assert.Equal(error, session.StartupSaveError);
            Assert.True(session.SavingAvailable);
            Assert.Equal(1, session.Bootstrap().WorldEpoch); Assert.Equal(0, session.Bootstrap().Snapshot.Tick);
            Assert.False(session.AutomaticCheckpointAvailable);
            Assert.Equal(error, (await Assert.ThrowsAsync<InvalidDataException>(() => session.Checkpoint())).Message);
            var slot = session.Checkpoint("slot1"); session.Tick(DateTimeOffset.UtcNow); await slot;
            Assert.NotNull(SaveCodec.Read(File.ReadAllBytes(Path.Combine(directory, "slot1.json"))));
            session.PrepareShutdown(); await session.FinalCheckpoint();
            Assert.Equal(text, File.ReadAllText(path));
            Assert.True(File.Exists(Path.Combine(directory, "slot1.json")));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally { Directory.Delete(directory, true); }
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
