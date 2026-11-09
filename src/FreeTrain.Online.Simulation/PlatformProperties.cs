// Stable ExAV 883cf7d PlatformPropertyDialog.OKbutton_Click and Platform.host.
// Original authors: Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class PlatformProperties
{
    public static MixedSessionState Apply(MixedSessionState before, long platformId, long expectedRevision,
        string name, DefinitionReference bell, long? hostId, long expectedHostRevision, long expectedStationRevision)
    {
        var world = before.World;
        var graph = world.Stations ?? throw new ArgumentException("Missing station graph.");
        var platform = graph.Platforms.SingleOrDefault(p=>p.Id==platformId);
        if (platform == null || platform.Revision != expectedRevision) throw new InvalidOperationException("staleRevision");
        name = name.Trim();
        if (name.Length is < 1 or > 128 || name.Any(char.IsControl)) throw new ArgumentException("Invalid platform name.");
        graph.Profile.Bell(bell);
        if (hostId is {} id)
        {
            var candidate = StationHostConnections.Candidates(world,platform).SingleOrDefault(c=>c.Id==id);
            if (candidate == null || candidate.Revision != expectedHostRevision || candidate.StationRevision != expectedStationRevision)
                throw new InvalidOperationException("staleRevision");
        }
        else if (expectedHostRevision != 0 || expectedStationRevision != 0) throw new ArgumentException("Invalid empty host revisions.");
        // The setter disconnects descendants before resolving the requested host.
        var changed = StationHostConnections.Preview(world,platformId,expectedRevision,hostId,expectedHostRevision).Candidate.Stations!;
        var current = changed.Platform(platformId);
        var metadataChanged = current.Name != name || current.Bell != bell;
        if (ReferenceEquals(changed,graph) && !metadataChanged) return before;
        changed = new(changed.Profile,changed.Buildings,changed.Platforms.Select(p=>p.Id==platformId
            ? p with {Name=name,Bell=bell,Revision=checked(expectedRevision+1)} : p),changed.Passages,
            changed.NextStationName,changed.NextPlatformName,changed.Listeners,changed.ListenerMemberships);
        var revisions = changed.Identities.ToDictionary(i=>i.Id,i=>i.Revision);
        var spatial = world.Spatial.Capture() with {Entities=world.Spatial.Entities.Select(e=>revisions.TryGetValue(e.Id,out var revision)
            ? e with {Revision=revision} : e).ToImmutableArray()};
        var candidateWorld = world.Replace(world.Revision,world.Terrain,spatial,world.Voxels,world.Slopes,stations:changed);
        return StationLifecycle.Commit(before,candidateWorld,ConstructionBooking.None,"platformProperties");
    }
}
