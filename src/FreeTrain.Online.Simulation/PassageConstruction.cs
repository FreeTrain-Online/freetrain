// Stable ExAV 883cf7d StationPassagewayController. Original authors;
// LGPL-2.1-or-later. Empty path cells are deliberately legal/no-effect.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record PassageConstructionPlan(MixedWorld Source, MixedWorld? Candidate, string? Reason,
    ImmutableArray<Location> Positions)
{
    public bool Legal => Reason == null && Candidate != null;
}

public static class PassageConstruction
{
    public const int MaximumLength = 100;
    public static PassageConstructionPlan Preview(MixedWorld world, Location from, Location to,
        PassageMaterial material, bool removal = false)
    {
        PassageConstructionPlan Reject(string reason) => new(world,null,reason,[]);
        var dx = (long)to.X-from.X; var dy = (long)to.Y-from.Y;
        var length = Math.Abs(dx)+Math.Abs(dy)+1;
        if (!Enum.IsDefined(material) || from.Z != to.Z || dx != 0 && dy != 0 || length < 2 || length > MaximumLength)
            return Reject("invalidPassageGeometry");
        if (world.Profile.Stations is not {} profile) return Reject("unavailableStationBinding");
        var graph = world.Stations ?? new StationGraph(profile,[],[]);
        var direction = new Heading(dx > 0 ? 2 : dx < 0 ? 6 : dy > 0 ? 4 : 0);
        try
        {
            var positions = Enumerable.Range(0,(int)length).Select(i=>new Location(
                checked(from.X+Math.Sign(dx)*i),checked(from.Y+Math.Sign(dy)*i),from.Z)).ToImmutableArray();
            var yards = graph.Platforms.Where(p=>p.Kind == PlatformKind.Thin)
                .SelectMany(p=>p.Yards.Select(y=>(Platform:p,Yard:y))).ToDictionary(row=>row.Yard.Location);
            foreach (var at in positions)
            {
                if (!world.Terrain.Space.ContainsVoxel(at)) return Reject("outside");
                // Source remove skips every non-passage cell, including obstacles.
                if (!removal && (world.At(at) != null || world.Spatial.At(at).Entity != null) &&
                    (!yards.TryGetValue(at,out var row) || row.Platform.Direction.Index%4 == direction.Index%4))
                    return Reject("passageTarget");
            }
            var changes = new Dictionary<Location,PlatformOutlook>();
            foreach (var at in positions)
            {
                if (!yards.TryGetValue(at,out var row)) continue;
                var old = row.Yard.Outlook;
                PlatformOutlook outlook;
                if (removal)
                {
                    if (old.Kind != PlatformOutlookKind.Passage) continue;
                    outlook = new();
                }
                else if (old.Kind == PlatformOutlookKind.Passage && old.Bridge) continue; // Keep material too.
                else outlook = new(PlatformOutlookKind.Passage,material,
                    old.Kind == PlatformOutlookKind.Passage ||
                    !(at == from && direction.Right90 == row.Platform.Direction || at == to && direction.Left90 == row.Platform.Direction));
                if (outlook != old) changes.Add(at,outlook);
            }
            if (changes.Count == 0) return new(world,world,null,positions);
            var entities = world.Spatial.Entities.ToDictionary(e=>e.Id);
            foreach (var at in changes.Keys)
            {
                var id = yards[at].Yard.TrafficOwnerId;
                entities[id] = entities[id] with {Revision=checked(entities[id].Revision+1)};
            }
            var platforms = graph.Platforms.Select(p=>p.Yards.Any(y=>changes.ContainsKey(y.Location))
                ? p with {Revision=checked(p.Revision+1),Yards=p.Yards.Select(y=>changes.TryGetValue(y.Location,out var o) ? y with {Outlook=o} : y).ToImmutableArray()} : p);
            var changed = new StationGraph(profile,graph.Buildings,platforms,graph.Passages,graph.NextStationName,
                graph.NextPlatformName,graph.Listeners,graph.ListenerMemberships);
            var candidate = world.Replace(world.Revision,world.Terrain,
                new(world.Spatial.NextEntityId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),world.Voxels,world.Slopes,stations:changed);
            return new(world,candidate,null,positions);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }

    // STATION-D011: pure preview; preserve source forward-then-backward precedence.
    public static PassageConstructionPlan PreviewStair(MixedWorld world, Location at,
        PassageMaterial material, bool removal = false)
    {
        PassageConstructionPlan Reject(string reason) => new(world,null,reason,[]);
        if (!Enum.IsDefined(material)) return Reject("invalidPassageGeometry");
        if (!world.Terrain.Space.ContainsVoxel(at)) return Reject("outside");
        if (world.Profile.Stations is not {} profile) return Reject("unavailableStationBinding");
        var graph = world.Stations ?? new StationGraph(profile,[],[]);
        var rows = graph.Platforms.Where(p=>p.Kind == PlatformKind.Thin)
            .SelectMany(p=>p.Yards.Select(y=>(Platform:p,Yard:y))).ToDictionary(r=>r.Yard.Location);
        if (!rows.TryGetValue(at,out var row))
            return removal ? new(world,world,null,[at]) : Reject("stairTarget");
        try
        {
            var old = row.Yard.Outlook;
            PlatformOutlook outlook;
            if (removal)
            {
                if (old.Kind != PlatformOutlookKind.Stair) return new(world,world,null,[at]);
                outlook = new();
            }
            else
            {
                bool Passage(Location p) => rows.TryGetValue(p,out var neighbor) && neighbor.Yard.Outlook.Kind == PlatformOutlookKind.Passage;
                var upward = Passage(at.Move(row.Platform.Direction));
                if (!upward && !Passage(at.Move(row.Platform.Direction.Opposite))) return Reject("stairNeighbor");
                outlook = new(PlatformOutlookKind.Stair,material,Upward:upward);
            }
            if (outlook == old) return new(world,world,null,[at]);
            var platforms = graph.Platforms.Select(p=>p.Id == row.Platform.Id ? p with {
                Revision=checked(p.Revision+1), Yards=p.Yards.Select(y=>y.Location == at ? y with {Outlook=outlook} : y).ToImmutableArray()
            } : p);
            var entities = world.Spatial.Entities.Select(e=>e.Id == row.Yard.TrafficOwnerId ? e with {Revision=checked(e.Revision+1)} : e).ToImmutableArray();
            var changed = new StationGraph(profile,graph.Buildings,platforms,graph.Passages,graph.NextStationName,
                graph.NextPlatformName,graph.Listeners,graph.ListenerMemberships);
            var candidate = world.Replace(world.Revision,world.Terrain,new(world.Spatial.NextEntityId,entities),world.Voxels,world.Slopes,stations:changed);
            return new(world,candidate,null,[at]);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }

    public static MixedSessionState Commit(MixedSessionState before, PassageConstructionPlan plan)
    {
        if (!ReferenceEquals(before.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (!plan.Legal) throw new InvalidOperationException("invalidPlacement");
        return ReferenceEquals(plan.Source,plan.Candidate) ? before :
            StationLifecycle.Commit(before,plan.Candidate!,ConstructionBooking.None,"passageEdit");
    }
}
