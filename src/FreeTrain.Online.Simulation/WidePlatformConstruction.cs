// Stable ExAV 883cf7d FatPlatform: center owner and right/left yard lanes.
// Original authors, LGPL-2.1-or-later. Lane extension UI belongs to P6-13.
namespace FreeTrain.Online.Simulation;

public static class WidePlatformConstruction
{
    public const int MaximumLength = ThinPlatformConstruction.MaximumLength;
    // Lane 0 is mandatory at creation. The optional second lane uses the same
    // source canAddLane predicate and free LaneChange booking, atomically.
    public static PlatformConstructionPlan Preview(MixedWorld world, Location position, Heading direction, int length,
        bool secondLane = false, bool removal = false, long platformId = 0, long expectedPlatformRevision = 0) =>
        PlatformConstruction.Preview(world,position,direction,length,false,PlatformKind.Wide,secondLane,
            removal,platformId,expectedPlatformRevision);

    public static MixedSessionState Commit(MixedSessionState before, PlatformConstructionPlan plan) =>
        PlatformConstruction.Commit(before,plan);
}
