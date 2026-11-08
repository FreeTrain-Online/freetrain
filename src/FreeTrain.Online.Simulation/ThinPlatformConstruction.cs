// Stable ExAV 883cf7d ThinPlatform. Original authors, LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

public static class ThinPlatformConstruction
{
    // Native NumericUpDown defaults to Maximum=100; validate before allocating.
    public const int MaximumLength = 100;
    public static PlatformConstructionPlan Preview(MixedWorld world, Location position, Heading direction, int length, bool roof,
        bool removal = false, long platformId = 0, long expectedPlatformRevision = 0) =>
        PlatformConstruction.Preview(world,position,direction,length,roof,PlatformKind.Thin,false,
            removal,platformId,expectedPlatformRevision);

    public static MixedSessionState Commit(MixedSessionState before, PlatformConstructionPlan plan) =>
        PlatformConstruction.Commit(before,plan);
}
