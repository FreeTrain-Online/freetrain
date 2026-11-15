// ExAV 883cf7d StationListenerImpl.getPopulation reads live land value.
// Refresh only metadata; stored local/reverse catchment order is never repaired.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class CityPopulation
{
    internal static (CityGraph City, StationGraph? Stations, SpatialState Spatial) Refresh(MixedWorld world, CityGraph city)
    {
        var stations=world.Stations;
        if(stations==null || city.State.Producers.IsEmpty) return (city,stations,world.Spatial.Capture());
        var owners=city.State.Producers.Select(p=>p.OwnerId).ToHashSet();
        var spatial=world.Spatial.Entities.Where(e=>owners.Contains(e.Id)).ToDictionary(e=>e.Id);
        var changed=new HashSet<long>();
        var listeners=stations.Listeners.Select(listener=>
        {
            if(!owners.Contains(listener.ProviderId) || listener.Provider is not {} provider) return listener;
            var owner=spatial[listener.ProviderId];
            var hv=world.Terrain.Space.ToStorage(owner.Anchor);
            var value=CityHeat.Value(city.State.Heat,city.State.Settings,hv.H,hv.V);
            if(value==provider.LandValue) return listener;
            changed.Add(owner.Id);
            return listener with {Provider=provider with {LandValue=value}};
        }).ToImmutableArray();
        if(changed.Count==0) return (city,stations,world.Spatial.Capture());
        var entities=world.Spatial.Entities.Select(e=>changed.Contains(e.Id)?e with {Revision=checked(e.Revision+1)}:e).ToImmutableArray();
        var graph=new StationGraph(stations.Profile,stations.Buildings,stations.Platforms,stations.Passages,
            stations.NextStationName,stations.NextPlatformName,listeners,stations.ListenerMemberships);
        return (city.WithSpatialRevisions(entities),graph,new(world.Spatial.NextEntityId,entities));
    }
}
