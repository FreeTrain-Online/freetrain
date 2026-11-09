// Stable ExAV 883cf7d ThinPlatform/FatPlatform/PlatformController/YardRailRoad.
// Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;
using System.Globalization;

namespace FreeTrain.Online.Simulation;

public sealed record PlatformConstructionPlan(MixedWorld Source, MixedWorld? Candidate, string? Reason,
    bool Removal, StationPlatform? Platform, ImmutableArray<Location> Positions, ConstructionBooking Booking)
{
    public bool Legal => Reason == null && Candidate != null;
}

internal static class PlatformConstruction
{
    internal static PlatformConstructionPlan Preview(MixedWorld world, Location position, Heading direction, int length,
        bool roof, PlatformKind kind, bool secondLane, bool removal, long platformId, long expectedPlatformRevision)
    {
        PlatformConstructionPlan Reject(string reason) => new(world,null,reason,removal,null,[],ConstructionBooking.None);
        if (!direction.IsCardinal || length is < 1 or > ThinPlatformConstruction.MaximumLength) return Reject("invalidPlatformGeometry");
        if (world.Profile.Stations is not {} profile) return Reject("unavailableStationBinding");
        try
        {
            var graph = world.Stations ?? new StationGraph(profile,[],[]);
            StationPlatform platform;
            ConstructionBooking booking;
            var entities = world.Spatial.Entities.ToDictionary(e=>e.Id);
            var voxels = world.Voxels.ToDictionary(v=>v.Position);
            var next = world.Spatial.NextEntityId;
            if (removal)
            {
                platform = graph.Platforms.SingleOrDefault(p=>p.Id == platformId)!;
                if (platform == null || platform.Kind != kind || !platform.Yards.Any(y=>y.Location == position) &&
                    !(kind == PlatformKind.Wide && Enumerable.Range(0,platform.Length).Any(i=>StationGraph.CenterLocation(platform,i) == position)))
                    return Reject("platformNotFound");
                if (platform.Revision != expectedPlatformRevision) return Reject("staleRevision");
                if (platform.Yards.Any(y=>world.At(y.Location)?.Traffic?.Car != null)) return Reject("occupied");
                booking = StationBooking.PlatformRemoval;
            }
            else
            {
                var id = next; next = checked(next+1);
                platform = new(id,0,kind,position,direction,length,
                    "ホーム"+graph.NextPlatformName.ToString(CultureInfo.InvariantCulture).PadLeft(2),roof,true,secondLane,
                    profile.Bells.Single(b=>b.Reference.ContributionId=="{9B087AEA-6E9C-48cd-A1F3-1B774500752E}").Reference,null,[],[]);
                var yards = ImmutableArray.CreateBuilder<PlatformYard>();
                if (kind == PlatformKind.Wide)
                {
                    var members = ImmutableArray.CreateBuilder<VoxelMember>();
                    for (var i=0;i<length;i++)
                    {
                        var center = StationGraph.CenterLocation(platform,i);
                        if (!world.Terrain.Space.ContainsVoxel(center)) return Reject("outside");
                        if (world.At(center) != null || world.Spatial.At(center).Entity != null) return Reject("obstacle");
                        members.Add(new(new(center.X-position.X,center.Y-position.Y,0),false));
                    }
                    entities.Add(id,new(id,0,profile.Recipe(StationArtKind.WideCenter,direction.Index/2%2).Reference,
                        position,new(members.ToImmutable()),true,false,0));
                }
                for (var lane=0;lane<(secondLane ? 2 : 1);lane++)
                for (var i=0;i<length;i++)
                {
                    var p = StationGraph.YardLocation(platform,lane,i);
                    if (!world.Terrain.Space.ContainsVoxel(p)) return Reject("outside");
                    var v = world.At(p);
                    if (v == null)
                    {
                        if (world.Spatial.At(p).Entity != null) return Reject("obstacle");
                        var owner = next; next = checked(next+1);
                        var pattern = NormalRailPatterns.Find(direction.Index,direction.Opposite.Index);
                        var normal = world.Profile.Bindings.Single(b=>b.Handler == RailwayHandler.Normal && b.Variant == pattern).Reference;
                        v = new(p,owner,RailwayVoxelKind.Traffic,new(new(normal,pattern)));
                        entities.Add(owner,new(owner,0,world.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
                    }
                    else
                    {
                        if (v.Traffic?.Car != null) return Reject("occupied");
                        var rail = v.Traffic?.Rail;
                        if (v.Kind != RailwayVoxelKind.Traffic || rail == null || v.Traffic!.Platform != null ||
                            graph.Passages.Any(s=>s.TrafficOwnerId == v.OwnerId) ||
                            world.Profile.Resolve(rail.Definition).Handler != RailwayHandler.Normal ||
                            NormalRailPatterns.Ports[rail.Pattern] is var ports &&
                            ((ports.A+4)%8 != ports.B || ports.A != direction.Index && ports.B != direction.Index))
                            return Reject("incompatiblePlatformRail");
                        entities[v.OwnerId] = entities[v.OwnerId] with {Revision=checked(entities[v.OwnerId].Revision+1)};
                    }
                    var hasRoof = roof && i >= length/4 && i < length-length/4;
                    yards.Add(new(v.OwnerId,p,lane,i,hasRoof,new()));
                    voxels[p] = v with {Traffic=v.Traffic! with {Platform=(kind == PlatformKind.Thin ? profile.ThinRecipe(direction,hasRoof) : profile.Recipe(StationArtKind.Yard,0)).Reference}};
                }
                platform = platform with {Yards=yards.ToImmutable()};
                var cell = world.Terrain.Space.ToStorage(position);
                booking = StationBooking.PlatformPlacement(length,position.Z,world.Terrain.Cell(cell.H,cell.V).Ground,world.Terrain.WaterLevel);
            }
            StationGraph changed;
            if (removal)
            {
                foreach (var y in platform.Yards)
                {
                    var v = voxels[y.Location];
                    // Restoration keeps the Traffic identity, rail, accessories,
                    // speed limits and ground flags. No rail or pier demolition.
                    voxels[y.Location] = v with {Traffic=v.Traffic! with {Platform=null}};
                    entities[v.OwnerId] = entities[v.OwnerId] with {Revision=checked(entities[v.OwnerId].Revision+1)};
                }
                if (kind == PlatformKind.Wide) entities.Remove(platform.Id);
                var descendants = new HashSet<long>();
                var pending = new Stack<long>(platform.Children);
                while (pending.TryPop(out var id))
                    if (descendants.Add(id)) foreach (var child in graph.Platform(id).Children) pending.Push(child);
                var buildings = graph.Buildings.Select(b=>b.Children.Contains(platform.Id)
                    ? b with {Revision=checked(b.Revision+1),Children=b.Children.Remove(platform.Id)} : b);
                var platforms = graph.Platforms.Where(p=>p.Id != platform.Id).Select(p=>descendants.Contains(p.Id)
                    ? p with {Revision=checked(p.Revision+1),HostId=null,Children=[]}
                    : p.Children.Contains(platform.Id) ? p with {Revision=checked(p.Revision+1),Children=p.Children.Remove(platform.Id)} : p);
                changed = new(profile,buildings,platforms,graph.Passages,graph.NextStationName,graph.NextPlatformName,graph.Listeners,graph.ListenerMemberships);
            }
            else
            {
                changed = StationHostConnections.Created(world,graph,platform);
                platform = changed.Platform(platform.Id);
            }
            foreach (var identity in changed.Identities)
                if (entities.TryGetValue(identity.Id,out var owner)) entities[identity.Id] = owner with {Revision=identity.Revision};
            if (voxels.Count > NormalRailwayConstruction.MaximumRailwayVoxels) return Reject("railwayCapacityLimit");
            var candidate = world.Replace(world.Revision,world.Terrain,new(next,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),
                voxels.Values,world.Slopes,stations:changed);
            var centers = kind == PlatformKind.Wide
                ? Enumerable.Range(0,platform.Length).Select(i=>StationGraph.CenterLocation(platform,i)) : [];
            return new(world,candidate,null,removal,platform,centers.Concat(platform.Yards.Select(y=>y.Location)).ToImmutableArray(),booking);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }
    public static MixedSessionState Commit(MixedSessionState before, PlatformConstructionPlan plan)
    {
        if (!ReferenceEquals(before.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (!plan.Legal) throw new InvalidOperationException("invalidPlacement");
        return StationLifecycle.Commit(before,plan.Candidate!,plan.Booking,plan.Removal ? "platformRemoval" : "platformPlacement");
    }
}
