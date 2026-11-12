// ExAV StationListenerImpl/SpecialStationListenerImpl, 883cf7d; LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class StationCatchment
{
    // Listener-first attachment follows stored station creation order, with the
    // strict truncated-distance predicate. Station-first advertising scans owners'
    // actual footprint voxels in the inclusive cube, without a sphere recheck.
    public static StationGraph Attach(StationGraph graph, long providerId, StationProvider provider, SpatialIndex spatial)
    {
        provider.Validate();
        var owner = spatial.Entities.SingleOrDefault(e => e.Id == providerId) ?? throw new ArgumentException("Unknown provider identity.");
        if (graph.Identities.Any(i => i.Id == providerId) || graph.Listeners.Any(l => l.ProviderId == providerId))
            throw new ArgumentException("Invalid/already attached provider identity.");
        var result = Copy(graph, graph.Listeners.Add(new(providerId, [], provider)), graph.ListenerMemberships);
        foreach (var station in graph.Buildings)
            if (Distance(owner.Anchor, station.Location) < (provider.Special ? 20 : 10))
                result = Offer(result, providerId, station.Id, owner.Anchor);
        return Revisions(graph, result);
    }

    public static StationGraph Advertise(StationGraph graph, long stationId, SpatialIndex spatial, WorldSpace space)
    {
        var station = graph.Building(stationId);
        var result = graph;
        foreach (var listener in graph.Listeners)
        {
            if (listener.Provider == null) continue; // Earlier detached, unbound edges supply no demand.
            var owner = spatial.Entities.Single(e => e.Id == listener.ProviderId);
            var range = listener.Provider.Special ? 20 : 10;
            if (owner.Footprint.At(owner.Anchor).Any(v => space.ContainsVoxel(v.Position) &&
                Math.Abs((long)v.Position.X - station.Location.X) <= range &&
                Math.Abs((long)v.Position.Y - station.Location.Y) <= range &&
                Math.Abs((long)v.Position.Z - station.Location.Z) <= range))
                result = Offer(result, listener.ProviderId, stationId, owner.Anchor);
        }
        return Revisions(graph, result, stationId);
    }

    // Also exposed as a pure operation for P7's provider lifecycle. Callers must
    // establish eligibility using Attach/Advertise, never rebuild edges on load.
    internal static StationGraph Offer(StationGraph graph, long providerId, long stationId, Location location)
    {
        var listener = graph.Listeners.Single(l => l.ProviderId == providerId);
        if (listener.Stations.Contains(stationId)) return graph;
        var memberships = graph.ListenerMemberships.ToDictionary(m => m.StationId, m => m.Providers);
        var revised = new HashSet<long>();
        var stations = listener.Stations;
        if (stations.Length < 4)
        {
            stations = stations.Add(stationId);
            memberships[stationId] = memberships.GetValueOrDefault(stationId, []).Add(providerId);
            revised.Add(stationId);
        }
        else
        {
            var farthest = Distance(location, graph.Building(stationId).Location);
            var slot = -1;
            for (var i = 0; i < stations.Length; i++)
            {
                var distance = Distance(location, graph.Building(stations[i]).Location);
                if (distance > farthest) { farthest = distance; slot = i; }
            }
            if (slot < 0) return graph;
            var removed = stations[slot];
            if (memberships.TryGetValue(removed, out var providers) && providers.Contains(providerId))
            {
                memberships[removed] = providers.Remove(providerId);
                revised.Add(removed);
            }
            stations = stations.SetItem(slot, stationId);
            // STATION-D003: no reverse membership is added for the fifth newcomer.
        }
        return new(graph.Profile, graph.Buildings.Select(b => revised.Contains(b.Id) ? b with { Revision = checked(b.Revision + 1) } : b),
            graph.Platforms, graph.Passages, graph.NextStationName, graph.NextPlatformName,
            graph.Listeners.Select(l => l.ProviderId == providerId ? l with { Stations = stations } : l),
            memberships.Select(m => new StationListenerMembership(m.Key, m.Value)));
    }

    public static MixedWorld Attach(MixedWorld world, long providerId, StationProvider provider) => Publish(world,
        Attach(world.Stations ?? new(world.Profile.Stations ?? throw new ArgumentException("Station profile required."), [], []), providerId, provider, world.Spatial));

    public static MixedWorld Update(MixedWorld world, long providerId, StationProvider provider)
    {
        provider.Validate();
        var graph = world.Stations!;
        var before = graph.Listeners.Single(l => l.ProviderId == providerId).Provider ?? throw new ArgumentException("Unbound provider.");
        if (provider.Special != before.Special || provider.XYSize != before.XYSize || provider.ZSize != before.ZSize)
            throw new ArgumentException("Changing provider geometry/kind requires removal and attachment.");
        return Publish(world, Copy(graph, graph.Listeners.Select(l => l.ProviderId == providerId ? l with { Provider = provider } : l), graph.ListenerMemberships));
    }

    public static MixedWorld Detach(MixedWorld world, long providerId) => Publish(world,
        StationBuildingLifecycle.Reclaimed(world.Stations!, new HashSet<long> { providerId }));

    private static MixedWorld Publish(MixedWorld world, StationGraph graph)
    {
        var identities = graph.Identities.ToDictionary(i => i.Id, i => i.Revision);
        var entities = world.Spatial.Entities.Select(e => identities.TryGetValue(e.Id, out var revision) ? e with { Revision = revision } :
            !(world.Stations?.ListenerStations(e.Id) ?? []).SequenceEqual(graph.ListenerStations(e.Id)) || world.Stations?.Provider(e.Id) != graph.Provider(e.Id)
            ? e with { Revision = checked(e.Revision + 1) } : e).ToImmutableArray();
        return world.Replace(world.Revision, world.Terrain, new(world.Spatial.NextEntityId, entities), world.Voxels, world.Slopes, stations: graph,city:world.City?.WithSpatialRevisions(entities));
    }

    public static int Demand(StationGraph graph, long stationId, GameTime time)
    {
        graph.Building(stationId);
        var total = 0;
        foreach (var id in graph.ListenerMemberships.SingleOrDefault(m => m.StationId == stationId)?.Providers ?? [])
        {
            var listener = graph.Listeners.Single(l => l.ProviderId == id);
            if (listener.Provider is {} provider) total = checked(total + provider.Share(time, listener.Stations.Length));
        }
        return total;
    }
    public static int Waiting(StationGraph graph, long stationId, GameTime time) =>
        Math.Max(0, Demand(graph, stationId, time) - graph.Building(stationId).Passengers.Gone);

    public static int Distance(Location a, Location b) => a.DistanceTo(b);
    private static StationGraph Revisions(StationGraph before, StationGraph after, long? created = null) => new(after.Profile,
        after.Buildings.Select(b => b with { Revision = checked(before.Building(b.Id).Revision + (b.Id != created &&
            !(before.ListenerMemberships.SingleOrDefault(m => m.StationId == b.Id)?.Providers ?? []).SequenceEqual(
                after.ListenerMemberships.SingleOrDefault(m => m.StationId == b.Id)?.Providers ?? []) ? 1 : 0)) }),
        after.Platforms, after.Passages, after.NextStationName, after.NextPlatformName, after.Listeners, after.ListenerMemberships);
    private static StationGraph Copy(StationGraph g, IEnumerable<StationListener> listeners, IEnumerable<StationListenerMembership> memberships) =>
        new(g.Profile, g.Buildings, g.Platforms, g.Passages, g.NextStationName, g.NextPlatformName, listeners, memberships);
}
