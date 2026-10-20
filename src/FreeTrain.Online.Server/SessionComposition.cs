using FreeTrain.Online.Content;
using FreeTrain.Online.Plugins.ManualController;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Server;

public static class SessionComposition
{
    // Explicit contribution registration; no assembly scanning or arbitrary DLL loading.
    public static SessionHost CreateReferenceHost(long epoch = 1, TimeSpan? inputExpiry = null) => new(ReferenceScenario.Create(), "operator-1",
        [() => new AutomaticController(), () => new ManualTrainController()], worldEpoch: epoch) { InputExpiry = inputExpiry ?? TimeSpan.FromMilliseconds(500) };
}
