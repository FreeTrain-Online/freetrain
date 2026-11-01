// ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail/BridgeRail.cs.
// Original authors, LGPL-2.1-or-later. See doc/web-parity-bridge-tool.md.
namespace FreeTrain.Online.Simulation;

public static class BridgeRailwayConstruction
{
    public const string Contribution = "{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}";
    public static Location Snap(Location from, Location to) => Math.Abs((long)to.X-from.X) >= Math.Abs((long)to.Y-from.Y)
        ? new(to.X,from.Y,from.Z) : new(from.X,to.Y,from.Z);
    public static RailwayConstructionPlan Preview(MixedWorld world,Location from,Location to,bool removal=false) =>
        ElevatedRailwayConstruction.Preview(world,from,to,removal,false);
    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time) =>
        ElevatedRailwayConstruction.Apply(state,plan,time,false);
}
