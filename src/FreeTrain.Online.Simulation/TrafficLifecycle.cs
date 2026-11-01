// ExAV 883cf7d TrafficVoxel.onModified/shouldHaveCrossing; LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

public static class TrafficLifecycle
{
    public static bool HasCrossing(TrafficComponents traffic)
    {
        if (traffic.Rail is not {} rail || traffic.Road is not {} road) return false;
        var ports=RailwayTopology.Ports(rail.Pattern);
        return ports.Length==2 && ports[0]%2==0 && (ports[0]+4)%8==ports[1] &&
            (road.Edges & (1<<((ports[0]+2)%8)))!=0 && (road.Edges & (1<<((ports[0]+6)%8)))!=0;
    }

    // Run after rail/road/accessory setters, including null pole removal. A pole
    // suppresses a crossing; removing it can immediately recreate the crossing.
    public static TrafficComponents Refresh(RailwayProfile profile, TrafficComponents traffic)
    {
        if (!HasCrossing(traffic))
            return traffic.Accessory is {} a && profile.Resolve(a.Definition).Handler == RailwayHandler.Crossing
                ? traffic with { Accessory = null } : traffic;
        if (traffic.Accessory != null) return traffic;
        var matches = profile.Bindings.Where(b => b.Handler == RailwayHandler.Crossing).Take(2).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("unavailableCrossingBinding");
        return traffic with { Accessory = new(matches[0].Reference) };
    }
}
