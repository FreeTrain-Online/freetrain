using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Plugins.ManualController;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation.Prototype;

namespace FreeTrain.Online.Server;

public static class SessionComposition
{
    public static SessionHost CreateGeneratedHost(StartupWorldOptions options, long epoch = 1)
        => new(options.Create(), "operator-1", TerrainProfiles.Resolve, worldEpoch: epoch);

    public static SessionHost CreateSavedHost(ReadOnlySpan<byte> bytes, long epoch = 1)
    {
        var saved = SaveCodec.ReadWorld(bytes, TerrainProfiles.Resolve);
        // Match the epoch advance of loading a save through SessionHost.
        return saved.Terrain is { } terrain
            ? new(terrain, "operator-1", TerrainProfiles.Resolve, worldEpoch: epoch + 1)
            : new(saved.Reference!, "operator-1", worldEpoch: epoch + 1, terrainResolver: TerrainProfiles.Resolve);
    }

    // Explicit canonical rail fixture for regression tests and headless tools.
    // Explicit contribution registration; no assembly scanning or arbitrary DLL loading.
    public static SessionHost CreateReferenceHost(long epoch = 1, TimeSpan? inputExpiry = null)
        => new(SpatialReferenceScenario.Create(), "operator-1", worldEpoch: epoch, terrainResolver: TerrainProfiles.Resolve);

    // Explicit historical regression harness only; never selected by server configuration.
    public static SessionHost CreatePrototypeHost(long epoch = 1, TimeSpan? inputExpiry = null) => new(ReferenceScenario.Create(), "operator-1",
        [() => new AutomaticController(), () => new ManualTrainController()], worldEpoch: epoch) { InputExpiry = inputExpiry ?? TimeSpan.FromMilliseconds(500) };
}
