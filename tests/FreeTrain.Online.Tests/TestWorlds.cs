using FreeTrain.Online.Server;

internal static class TestWorlds
{
    // Integration tests replace this world or exercise transport/storage behavior.
    // StartupWorldTests separately covers production defaults and water variants.
    public static StartupWorldOptions SmallStartup() => new()
    {
        Width = 32,
        Height = 32,
        Seed = 42,
        Water = StartupWater.Dry,
        Hills = 0,
        TreesPercent = 0
    };
}
