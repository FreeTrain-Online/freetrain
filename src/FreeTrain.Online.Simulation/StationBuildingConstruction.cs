// Stable ExAV 883cf7d StationContribution/Station/PThreeDimStructure.
// Original authors, LGPL-2.1-or-later. Stations are built immediately.
using System.Collections.Immutable;
using System.Globalization;

namespace FreeTrain.Online.Simulation;

public sealed class StationBuildingPlan
{
    public MixedWorld Source { get; }
    public MixedWorld? Candidate { get; }
    public string? Reason { get; }
    public bool Legal => Reason == null && Candidate != null;
    public bool Removal { get; }
    public StationBuilding? Building { get; }
    public ImmutableArray<Location> Positions { get; }
    public ImmutableArray<RailwayReclamation> Reclaimed { get; }
    public ConstructionBooking Booking { get; }
    internal StationBuildingPlan(MixedWorld source, bool removal, string reason)
    { Source=source; Removal=removal; Reason=reason; Positions=[]; Reclaimed=[]; Booking=ConstructionBooking.None; }
    internal StationBuildingPlan(MixedWorld source, MixedWorld candidate, bool removal, StationBuilding building,
        IEnumerable<Location> positions, IEnumerable<RailwayReclamation> reclaimed, ConstructionBooking booking)
    { Source=source; Candidate=candidate; Removal=removal; Building=building; Positions=positions.ToImmutableArray(); Reclaimed=reclaimed.ToImmutableArray(); Booking=booking; }
}

public static class StationBuildingConstruction
{
    public static StationBuildingPlan Preview(MixedWorld world, Location position, DefinitionReference? definition,
        bool removal = false, long stationId = 0, long expectedStationRevision = 0)
    {
        StationBuildingPlan Reject(string reason) => new(world,removal,reason);
        if (!world.Terrain.Space.ContainsVoxel(position)) return Reject("outside");
        if (world.Profile.Stations is not {} profile) return Reject("unavailableStationBinding");
        try
        {
            var graph = world.Stations ?? new StationGraph(profile,[],[]);
            var entities = world.Spatial.Entities.ToDictionary(e=>e.Id);
            var terrain = world.Terrain;
            var next = world.Spatial.NextEntityId;
            var reclaimed = new Dictionary<long,RailwayReclamation>();
            StationBuilding building;
            StationDefinition type;
            StationGraph changed;
            ConstructionBooking booking;
            if (removal)
            {
                building = graph.Buildings.SingleOrDefault(b=>b.Id == stationId)!;
                if (building == null || world.Spatial.At(position).Entity?.Id != stationId) return Reject("stationNotFound");
                if (building.Revision != expectedStationRevision) return Reject("staleRevision");
                type = profile.Resolve(building.Definition);
                changed = StationBuildingLifecycle.Removed(graph,building.Id);
                entities.Remove(building.Id);
                booking = StationBooking.BuildingRemoval(type.SizeX,type.SizeY);
            }
            else
            {
                if (definition == null) return Reject("unavailableStationBinding");
                type = profile.Definitions.SingleOrDefault(d=>d.Reference == definition)!;
                if (type == null) return Reject("unavailableStationBinding");
                var positions = type.Bounds(position).Locations().ToArray();
                if (positions.Any(p=>!terrain.Space.ContainsVoxel(p))) return Reject("outside");
                foreach (var p in positions)
                {
                    var column = terrain.Space.ToStorage(p);
                    if (terrain.Cell(column.H,column.V).Ground != position.Z) return Reject("stationGround");
                    var occupancy = world.Spatial.At(p);
                    if (!occupancy.Reusable) return Reject("obstacle");
                    if (occupancy.Entity is {} entity)
                    {
                        var occupant = terrain.OccupantAt(p);
                        if (occupant == null) return Reject("unavailableReclamationHandler");
                        reclaimed.TryAdd(entity.Id,new(entity,occupant));
                    }
                }
                foreach (var id in reclaimed.Keys) entities.Remove(id);
                if (reclaimed.Count != 0)
                    terrain = terrain.Replace(terrain.Revision,terrain.Cells,terrain.WaterLevel,
                        new(terrain.Spatial.NextEntityId,terrain.Spatial.Entities.Where(e=>!reclaimed.ContainsKey(e.Id)).ToImmutableArray()),
                        terrain.Occupants.Where(o=>!reclaimed.ContainsKey(o.EntityId)));
                var name = "ST"+graph.NextStationName.ToString(CultureInfo.InvariantCulture).PadLeft(2);
                building = new(next,0,type.Reference,position,name,[],new(),new(),new(),new());
                next = checked(next+1);
                entities.Add(building.Id,new(building.Id,0,type.Reference,position,type.Footprint,true,false,0));
                changed = StationCatchment.Advertise(StationBuildingLifecycle.Created(StationBuildingLifecycle.Reclaimed(graph,reclaimed.Keys.ToHashSet()),building),
                    building.Id, world.Spatial, world.Terrain.Space);
                booking = StationBooking.BuildingPlacement(type);
            }
            // Lifecycle changes revise their actual spatial owners, including
            // wide centers and demand providers whose ordered edges changed.
            foreach (var identity in changed.Identities)
                if (entities.TryGetValue(identity.Id,out var owner)) entities[identity.Id] = owner with {Revision=identity.Revision};
            foreach (var listener in changed.Listeners)
                if (!listener.Stations.SequenceEqual(graph.Listeners.Single(l=>l.ProviderId == listener.ProviderId).Stations))
                    entities[listener.ProviderId] = entities[listener.ProviderId] with {Revision=checked(entities[listener.ProviderId].Revision+1)};
            var candidate = world.Replace(world.Revision,terrain,new(next,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),
                world.Voxels,world.Slopes,stations:changed,city:world.City?.WithSpatialRevisions(entities.Values));
            return new(world,candidate,removal,building,type.Bounds(building.Location).Locations(),reclaimed.Values,booking);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }

    public static MixedSessionState Commit(MixedSessionState before, StationBuildingPlan plan)
    {
        if (!ReferenceEquals(before.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (!plan.Legal) throw new InvalidOperationException("invalidPlacement");
        return StationLifecycle.Commit(before,plan.Candidate!,plan.Booking,plan.Removal ? "stationRemoval" : "stationPlacement");
    }
}

// Pure lifecycle extension points for station creation/removal and P6-16 listener
// advertising. Station creation never auto-rehosts disconnected platforms.
public static class StationBuildingLifecycle
{
    public static StationGraph Created(StationGraph graph, StationBuilding building) => new(graph.Profile,
        graph.Buildings.Add(building),graph.Platforms,graph.Passages,checked(graph.NextStationName+1),graph.NextPlatformName,
        graph.Listeners,graph.ListenerMemberships);

    public static StationGraph Reclaimed(StationGraph graph, IReadOnlySet<long> providerIds)
    {
        if (providerIds.Count == 0) return graph;
        var changed = graph.ListenerMemberships.Where(m=>m.Providers.Any(providerIds.Contains)).Select(m=>m.StationId).ToHashSet();
        return new(graph.Profile,graph.Buildings.Select(b=>changed.Contains(b.Id) ? b with {Revision=checked(b.Revision+1)} : b),
            graph.Platforms,graph.Passages,graph.NextStationName,graph.NextPlatformName,
            graph.Listeners.Where(l=>!providerIds.Contains(l.ProviderId)),
            graph.ListenerMemberships.Select(m=>m with {Providers=m.Providers.Where(id=>!providerIds.Contains(id)).ToImmutableArray()}));
    }

    public static StationGraph Removed(StationGraph graph, long stationId)
    {
        var station = graph.Building(stationId);
        var disconnected = new HashSet<long>();
        var pending = new Stack<long>(station.Children);
        while (pending.TryPop(out var id))
        {
            if (!disconnected.Add(id)) continue;
            foreach (var child in graph.Platform(id).Children) pending.Push(child);
        }
        var platforms = graph.Platforms.Select(p=>disconnected.Contains(p.Id)
            ? p with {Revision=checked(p.Revision+1),HostId=null,Children=[]} : p);
        // Preserve separate, ordered forward/reverse memberships. A removed ID
        // cannot survive even in the source's missing fifth reverse membership.
        var listeners = graph.Listeners.Select(l=>l.Stations.Contains(stationId)
            ? l with {Stations=l.Stations.Remove(stationId)} : l);
        return new(graph.Profile,graph.Buildings.Where(b=>b.Id != stationId),platforms,graph.Passages,
            graph.NextStationName,graph.NextPlatformName,listeners,graph.ListenerMemberships.Where(l=>l.StationId != stationId));
    }
}
