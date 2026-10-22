using FreeTrain.Online.Server;

// Original-runtime multiplayer regressions; never evidence of canonical gameplay.
internal static class PrototypeNetwork
{
    public static NetworkSession Create(NetworkOptions options) => new(options,
        hostFactory: (epoch, expiry) => SessionComposition.CreatePrototypeHost(epoch, expiry));
}
