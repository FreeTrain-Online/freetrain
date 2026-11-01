// ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail/StealSupportedRailContributionImpl.cs,
// world/Location.cs (align8To). Original authors, LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

public static class GirderRailwayConstruction
{
    public const string Contribution = "{7FD493FF-D6A7-4a13-A471-FB737641D8A0}";
    public static Location Snap(Location from,Location to)
    {
        var x=Math.Abs((long)to.X-from.X);var y=Math.Abs((long)to.Y-from.Y);
        // Preserve source integer division and strict comparisons, including ties.
        if (y/2>x) return new(from.X,to.Y,from.Z);
        if (x/2>y) return new(to.X,from.Y,from.Z);
        return new(to.X,checked((int)(from.Y+x*Math.Sign((long)to.Y-from.Y))),from.Z);
    }
    public static RailwayConstructionPlan Preview(MixedWorld world,Location from,Location to,bool removal=false) =>
        ElevatedRailwayConstruction.Preview(world,from,to,removal,true);
    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time) =>
        ElevatedRailwayConstruction.Apply(state,plan,time,true);
}
