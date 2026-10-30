using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

public class StartupWorldTests
{
    [Fact]
    public async Task Fresh_server_has_medium_generated_hills_sparse_trees_and_no_demo_rails()
    {
        var session = new NetworkSession(new(), startupWorld: new() { Seed = 42 });
        var snapshot = session.Bootstrap().Snapshot;
        Assert.Equal(TerrainProfile.Ruleset, snapshot.Ruleset);
        Assert.Equal(400, snapshot.Map.Width); Assert.Equal(600, snapshot.Map.Height);
        Assert.Empty(snapshot.Tracks); Assert.Empty(snapshot.Trains); Assert.True(snapshot.Paused);
        var capture = session.CaptureSave(); session.Tick(DateTimeOffset.UtcNow);
        var terrain = SaveCodec.ReadWorld(await capture, TerrainProfiles.Resolve).Terrain!;
        Assert.True(terrain.World.Cells.Select(c => c.Ground).Distinct().Count() > 2);
        Assert.InRange(terrain.World.RealizedTreeCount, 1, terrain.World.Cells.Length / 10);
        var request = JsonSerializer.Deserialize<WorldCreationRequest>(terrain.Provenance.RequestJson) as GeneratedWorldRequest;
        Assert.NotNull(request); Assert.Equal(42, request.Options.Seed);
        Assert.Equal(ReliefKind.Hills, request.Options.Relief.Kind);
    }

    [Theory]
    [InlineData(StartupWater.Dry)]
    [InlineData(StartupWater.Lake)]
    [InlineData(StartupWater.River)]
    [InlineData(StartupWater.Coast)]
    public void Fixed_seed_reproduces_every_water_variant_and_actual_terrain_round_trips(StartupWater water)
    {
        var options = new StartupWorldOptions { Width = 80, Height = 80, Seed = 123, Water = water };
        var a = options.Create(); var saved = SaveCodec.Write(a);
        Assert.Equal(saved, SaveCodec.Write(options.Create()));
        Assert.Equal(saved, SaveCodec.Write(SaveCodec.ReadWorld(saved, TerrainProfiles.Resolve).Terrain!));
        WaterTerrain.Validate(options.Request(123), a.World);
        var wetCells = CoastTerrain.SeaMask(a.World).Count(c => c);
        if (water == StartupWater.Dry) Assert.Equal(0, wetCells);
        else Assert.True(wetCells > 0);
    }

    [Fact]
    public void Random_water_choice_is_seeded_and_includes_dry_lake_river_and_coast()
    {
        var options = new StartupWorldOptions();
        var requests = Enumerable.Range(0, 64).Select(options.Request).ToArray();
        Assert.Contains(requests, r => r.Options.Coast.Kind == CoastKind.Sides);
        Assert.Contains(requests, r => r.Options.Lakes.Count > 0);
        Assert.Contains(requests, r => r.Options.Rivers.Direction != RiverDirection.None);
        Assert.Contains(requests, r => r.Options.Coast.Kind == CoastKind.Inland && r.Options.Lakes.Count == 0 && r.Options.Rivers.Count == 0);
    }

    [Fact]
    public async Task Checkpoint_restores_exact_generated_world_without_using_new_world_settings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "freetrain-startup-" + Guid.NewGuid());
        try
        {
            using var storage = new SaveStorage(directory);
            var session = new NetworkSession(new(), storage, startupWorld: new() { Width = 64, Height = 64, Seed = 765 });
            var capture = session.CaptureSave(); session.Tick(DateTimeOffset.UtcNow); var saved = await capture;
            session.PrepareShutdown(); await session.FinalCheckpoint();
            // Invalid generation parameters prove that restoration never invokes generation.
            var restarted = new NetworkSession(new(), storage, startupWorld: new() { Width = -1 });
            Assert.Null(restarted.StartupSaveError); Assert.Equal(2, restarted.Bootstrap().WorldEpoch);
            var restored = restarted.CaptureSave(); restarted.Tick(DateTimeOffset.UtcNow);
            Assert.Equal(saved, await restored);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Invalid_startup_parameters_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new StartupWorldOptions { Width = -1 }.Request(0));
        Assert.Throws<ArgumentException>(() => new StartupWorldOptions { Hills = 101 }.Request(0));
        Assert.Throws<ArgumentException>(() => new StartupWorldOptions { TreesPercent = -1 }.Request(0));
        Assert.Throws<ArgumentException>(() => new StartupWorldOptions { Water = (StartupWater)99 }.Request(0));
    }
}
