// Stable ExAV 883cf7d Platform.listHosts/host and Station/ThinPlatform/FatPlatform.get.
// Original authors: Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record StationHostCandidate(long Id, long Revision, Location Location, bool IsStation,
    long StationId, long StationRevision, Location StationLocation);

public sealed record StationHostConnectionPlan(MixedWorld Source, MixedWorld Candidate);

public static class StationHostConnections
{
    public const int Range = 8;

    public static Location OtherEnd(StationPlatform platform) => StationGraph.CenterLocation(platform,platform.Length);

    // Iterate actual occupied members instead of every empty cell in the source
    // scan. Membership is the same inclusive rectangle + L full-height columns.
    public static ImmutableArray<StationHostCandidate> Candidates(MixedWorld world, StationPlatform platform)
    {
        if (world.Stations is not {} graph) return [];
        var end = OtherEnd(platform);
        var columns = Enumerable.Range(0,platform.Length).Select(i=>StationGraph.CenterLocation(platform,i))
            .Select(p=>(p.X,p.Y)).ToHashSet();
        bool Contains(Location p) => world.Terrain.Space.ContainsVoxel(p) &&
            (columns.Contains((p.X,p.Y)) || p.Z >= platform.Location.Z-1 && p.Z <= platform.Location.Z+1 &&
             p.X >= Math.Min(platform.Location.X,end.X)-Range && p.X <= Math.Max(platform.Location.X,end.X)+Range &&
             p.Y >= Math.Min(platform.Location.Y,end.Y)-Range && p.Y <= Math.Max(platform.Location.Y,end.Y)+Range);
        var result = ImmutableArray.CreateBuilder<StationHostCandidate>();
        foreach (var b in graph.Buildings)
            if (graph.Profile.Resolve(b.Definition).Footprint.At(b.Location).Any(c=>Contains(c.Position)))
                result.Add(new(b.Id,b.Revision,b.Location,true,b.Id,b.Revision,b.Location));
        foreach (var p in graph.Platforms)
            if (p.Id != platform.Id && graph.HostStation(p.Id) is {} station &&
                (p.Kind == PlatformKind.Thin ? p.Yards.Any(y=>Contains(y.Location)) :
                 Enumerable.Range(0,p.Length).Any(i=>Contains(StationGraph.CenterLocation(p,i)))))
                result.Add(new(p.Id,p.Revision,p.Location,false,station.Id,station.Revision,station.Location));
        // Source Hashtable order is unspecified. STATION-D002 chooses stable IDs,
        // never distance, and construction filters to direct Stations only.
        return result.OrderBy(c=>c.Id).ToImmutableArray();
    }

    internal static StationGraph Created(MixedWorld world, StationGraph graph, StationPlatform platform)
    {
        var host = Candidates(world,platform).FirstOrDefault(c=>c.IsStation);
        var buildings = graph.Buildings.Select(b=>b.Id == host?.Id
            ? b with {Revision=checked(b.Revision+1),Children=b.Children.Add(platform.Id)} : b);
        return Copy(graph,buildings,graph.Platforms.Add(platform with {HostId=host?.Id}),checked(graph.NextPlatformName+1));
    }

    // All intermediate edits are private dictionaries. A descendant target is
    // resolved AFTER recursive disconnection, just as in the source setter.
    public static StationGraph Reparent(StationGraph graph, long platformId, long? hostId)
    {
        var platform = graph.Platform(platformId);
        if (hostId == platformId || hostId is {} target && !graph.Identities.Any(i=>i.Id == target))
            throw new ArgumentException("Invalid platform host.");
        if (platform.HostId == hostId) return graph;
        var buildings = graph.Buildings.ToDictionary(b=>b.Id);
        var platforms = graph.Platforms.ToDictionary(p=>p.Id);
        void RemoveChild(long parent, long child)
        {
            if (buildings.TryGetValue(parent,out var b)) buildings[parent] = b with {Children=b.Children.Remove(child)};
            else platforms[parent] = platforms[parent] with {Children=platforms[parent].Children.Remove(child)};
        }
        var pending = new Stack<long>(); pending.Push(platformId);
        while (pending.TryPop(out var id))
        {
            var p = platforms[id];
            if (p.HostId is {} parent) RemoveChild(parent,id);
            platforms[id] = platforms[id] with {HostId=null,Children=[]};
            foreach (var child in p.Children.Reverse()) pending.Push(child);
        }
        var connected = hostId is {} host && (buildings.ContainsKey(host) || platforms[host].HostId != null);
        if (connected)
        {
            var parent = hostId!.Value;
            platforms[platformId] = platforms[platformId] with {HostId=parent};
            if (buildings.TryGetValue(parent,out var b)) buildings[parent] = b with {Children=b.Children.Add(platformId)};
            else platforms[parent] = platforms[parent] with {Children=platforms[parent].Children.Add(platformId)};
        }
        return Copy(graph,
            graph.Buildings.Select(b=>buildings[b.Id].Children.SequenceEqual(b.Children) ? b : buildings[b.Id] with {Revision=checked(b.Revision+1)}),
            graph.Platforms.Select(p=>platforms[p.Id].HostId == p.HostId && platforms[p.Id].Children.SequenceEqual(p.Children)
                ? p : platforms[p.Id] with {Revision=checked(p.Revision+1)}));
    }

    public static StationHostConnectionPlan Preview(MixedWorld world, long platformId, long expectedRevision,
        long? hostId, long expectedHostRevision = 0)
    {
        var graph = world.Stations ?? throw new ArgumentException("Missing station graph.");
        if (graph.Platform(platformId).Revision != expectedRevision || hostId is {} id &&
            graph.Identities.SingleOrDefault(i=>i.Id == id) != (id,expectedHostRevision))
            throw new InvalidOperationException("staleRevision");
        if (hostId != null && !Candidates(world,graph.Platform(platformId)).Any(c=>c.Id == hostId))
            throw new ArgumentException("Unavailable platform host.");
        var changed = Reparent(graph,platformId,hostId);
        if (ReferenceEquals(graph,changed)) return new(world,world);
        var revisions = changed.Identities.ToDictionary(i=>i.Id,i=>i.Revision);
        var spatial = world.Spatial.Capture() with {Entities=world.Spatial.Entities.Select(e=>revisions.TryGetValue(e.Id,out var revision)
            ? e with {Revision=revision} : e).ToImmutableArray()};
        return new(world,world.Replace(world.Revision,world.Terrain,spatial,world.Voxels,world.Slopes,stations:changed));
    }

    public static MixedSessionState Commit(MixedSessionState before, StationHostConnectionPlan plan)
    {
        if (!ReferenceEquals(before.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (ReferenceEquals(plan.Source,plan.Candidate)) return before;
        return StationLifecycle.Commit(before,plan.Candidate,ConstructionBooking.None,"platformHost");
    }

    private static StationGraph Copy(StationGraph graph, IEnumerable<StationBuilding> buildings,
        IEnumerable<StationPlatform> platforms, int? nextPlatformName = null) => new(graph.Profile,buildings,platforms,
        graph.Passages,graph.NextStationName,nextPlatformName ?? graph.NextPlatformName,graph.Listeners,graph.ListenerMemberships);
}
