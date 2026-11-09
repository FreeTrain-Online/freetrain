// Stable ExAV 883cf7d FatPlatform: center owner and right/left yard lanes.
// Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;

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

    // Both lane actions use the same source legality as initial construction.
    // A detached candidate retains center/host/timers and every untouched component.
    public static PlatformConstructionPlan PreviewLane(MixedWorld world, Location position, long platformId,
        long expectedRevision, int lane, bool removal)
    {
        PlatformConstructionPlan Reject(string reason) => new(world,null,reason,removal,null,[],ConstructionBooking.None);
        var graph = world.Stations;
        var platform = graph?.Platforms.SingleOrDefault(p=>p.Id == platformId);
        if (platform == null || platform.Kind != PlatformKind.Wide || platform.Location != position) return Reject("platformNotFound");
        if (platform.Revision != expectedRevision) return Reject("staleRevision");
        if (lane is not (0 or 1)) return Reject("invalidPlatformGeometry");
        if ((lane == 0 ? platform.Lane0 : platform.Lane1) != removal) return Reject("invalidPlatformGeometry");
        try
        {
            var entities = world.Spatial.Entities.ToDictionary(e=>e.Id);
            var voxels = world.Voxels.ToDictionary(v=>v.Position);
            var next = world.Spatial.NextEntityId;
            var yards = platform.Yards.Where(y=>y.Lane != lane).ToList();
            var positions = Enumerable.Range(0,platform.Length).Select(i=>StationGraph.YardLocation(platform,lane,i)).ToImmutableArray();
            for (var i=0;i<positions.Length;i++)
            {
                var at = positions[i];
                if (!world.Terrain.Space.ContainsVoxel(at)) return Reject("outside");
                var v = world.At(at);
                if (v?.Traffic?.Car != null) return Reject("occupied");
                if (removal)
                {
                    voxels[at] = v! with {Traffic=v.Traffic! with {Platform=null}};
                    entities[v.OwnerId] = entities[v.OwnerId] with {Revision=checked(entities[v.OwnerId].Revision+1)};
                    continue;
                }
                if (v == null)
                {
                    if (world.Spatial.At(at).Entity != null) return Reject("obstacle");
                    var owner = next; next = checked(next+1);
                    var pattern = NormalRailPatterns.Find(platform.Direction.Index,platform.Direction.Opposite.Index);
                    var normal = world.Profile.Bindings.Single(b=>b.Handler == RailwayHandler.Normal && b.Variant == pattern).Reference;
                    v = new(at,owner,RailwayVoxelKind.Traffic,new(new(normal,pattern)));
                    entities.Add(owner,new(owner,0,world.Profile.TrafficOwner,at,new([new(new(0,0,0),true)]),true,false,0));
                }
                else
                {
                    var rail = v.Traffic?.Rail;
                    if (v.Kind != RailwayVoxelKind.Traffic || rail == null || v.Traffic!.Platform != null ||
                        graph!.Passages.Any(s=>s.TrafficOwnerId == v.OwnerId) ||
                        world.Profile.Resolve(rail.Definition).Handler != RailwayHandler.Normal ||
                        NormalRailPatterns.Ports[rail.Pattern] is var ports &&
                        ((ports.A+4)%8 != ports.B || ports.A != platform.Direction.Index && ports.B != platform.Direction.Index))
                        return Reject("incompatiblePlatformRail");
                    entities[v.OwnerId] = entities[v.OwnerId] with {Revision=checked(entities[v.OwnerId].Revision+1)};
                }
                voxels[at] = v with {Traffic=v.Traffic! with {Platform=graph!.Profile.Recipe(StationArtKind.Yard,0).Reference}};
                yards.Add(new(v.OwnerId,at,lane,i,false,new()));
            }
            platform = platform with {Revision=checked(platform.Revision+1),Lane0=lane == 0 ? !removal : platform.Lane0,
                Lane1=lane == 1 ? !removal : platform.Lane1,Yards=yards.OrderBy(y=>y.Lane).ThenBy(y=>y.Index).ToImmutableArray()};
            entities[platform.Id] = entities[platform.Id] with {Revision=platform.Revision};
            var changed = new StationGraph(graph!.Profile,graph.Buildings,graph.Platforms.Select(p=>p.Id == platform.Id ? platform : p),
                graph.Passages,graph.NextStationName,graph.NextPlatformName,graph.Listeners,graph.ListenerMemberships);
            if (voxels.Count > NormalRailwayConstruction.MaximumRailwayVoxels) return Reject("railwayCapacityLimit");
            var candidate = world.Replace(world.Revision,world.Terrain,new(next,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),
                voxels.Values,world.Slopes,stations:changed);
            return new(world,candidate,null,removal,platform,positions,StationBooking.LaneChange);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }

    public static MixedSessionState Commit(MixedSessionState before, PlatformConstructionPlan plan) =>
        PlatformConstruction.Commit(before,plan);
}
