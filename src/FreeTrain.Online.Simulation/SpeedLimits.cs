// Behavioral extraction: ExAV 883cf7d SpeedLimit.cs and SpeedLimit controllers/manager.
// Original authors (nanashi et al.), LGPL-2.1-or-later. World-local lifecycle adaptation.
using System.Collections.Immutable;
namespace FreeTrain.Online.Simulation;

public sealed record SpeedLimits(ImmutableArray<Location> Low, ImmutableArray<Location> Medium, ImmutableArray<Location> Fast)
{
    public static SpeedLimits Empty { get; } = new([],[],[]);
    public IEnumerable<Location> Locations => Low.Concat(Medium).Concat(Fast);
    public bool Contains(Location p) => Low.Contains(p)||Medium.Contains(p)||Fast.Contains(p);
    // Native Hashtable path deduplicates in the chosen list; ArrayList appends.
    public SpeedLimits Add(string speed,IEnumerable<Location> locations,bool distinct)
    {
        ImmutableArray<Location> Append(ImmutableArray<Location> list)
        {
            var result=list.ToBuilder();var seen=distinct?list.ToHashSet():null;
            foreach(var p in locations)if(seen==null||seen.Add(p))result.Add(p);
            return result.ToImmutable();
        }
        return speed switch {"low"=>this with{Low=Append(Low)},"medium"=>this with{Medium=Append(Medium)},
            "fast"=>this with{Fast=Append(Fast)},_=>throw new ArgumentException("invalidInput")};
    }
    // Remove ONE occurrence in EACH list per selected location, preserving order.
    public SpeedLimits Remove(IEnumerable<Location> locations)
    {
        var counts=locations.GroupBy(p=>p).ToDictionary(g=>g.Key,g=>g.Count());
        ImmutableArray<Location> RemoveFrom(ImmutableArray<Location> list)
        {
            var remaining=new Dictionary<Location,int>(counts);var result=ImmutableArray.CreateBuilder<Location>();
            foreach(var p in list)
                if(remaining.GetValueOrDefault(p) is > 0 and var count)remaining[p]=count-1;
                else result.Add(p);
            return result.ToImmutable();
        }
        return new(RemoveFrom(Low),RemoveFrom(Medium),RemoveFrom(Fast));
    }
    public SpeedLimits Clean(IReadOnlySet<Location> traffic) => new(Low.Where(traffic.Contains).ToImmutableArray(),
        Medium.Where(traffic.Contains).ToImmutableArray(),Fast.Where(traffic.Contains).ToImmutableArray());
}

public static class SpeedLimitConstruction
{
    public static RailwayConstructionPlan Preview(MixedWorld world,Location from,Location to,string speed,
        bool removal=false,bool slope=false,int climb=0)
    {
        var cells=ImmutableArray.CreateBuilder<NormalRailwayCell>();
        NormalRailwayPlan Topology()=>new(world,from,to,removal,null,cells.ToImmutable());
        RailwayConstructionPlan Reject(string reason)=>new(Topology(),reason);
        if(speed is not ("low" or "medium" or "fast")||climb is not (0 or 2 or 4 or 6))return Reject("invalidInput");
        try
        {
            ImmutableArray<Location> route;
            if(slope)
            {
                var origin=from;var direction=new Heading(climb);
                if(removal)
                {
                    var owner=SlopeRailwayConstruction.Resolve(world,from);
                    if(owner==null)return Reject("notSlope");
                    if(owner.Value.Climb!=direction)return Reject("slopeDirectionMismatch");
                    origin=owner.Value.Base;
                }
                var (dx,dy)=direction.Offset;
                route=Enumerable.Range(0,4).Select(i=>new Location(checked(origin.X+i*dx),checked(origin.Y+i*dy),checked(origin.Z+i/2))).ToImmutableArray();
            }
            else route=RailwayTopology.Route(from,to);
            if(route.IsEmpty)return Reject("samePoint");
            for(var i=0;i<route.Length;i++)
            {
                var p=route[i];if(!world.Terrain.Space.ContainsVoxel(p))return Reject("outside");
                var incoming=slope?new Heading(climb).Opposite:i==0?p.DirectionTo(route[1]).Opposite:route[i-1].DirectionTo(p).Opposite;
                var outgoing=slope?new Heading(climb):i==route.Length-1?incoming.Opposite:p.DirectionTo(route[i+1]);
                if(!slope)NormalRailPatterns.Find(incoming.Index,outgoing.Index); // Validate source RailPattern, not existing track shape.
                var rail=world.At(p)?.Traffic?.Rail;
                cells.Add(new(p,incoming,outgoing,rail,rail,false));
            }
            if(!removal&&route.Any(p=>world.At(p)?.Traffic==null))return Reject("speedLimitTraffic");
            var listed=world.SpeedLimits.Locations.ToHashSet();
            if(!removal&&route.Any(listed.Contains))return Reject("speedLimitOverlap");
            var limits=removal?world.SpeedLimits.Remove(route):world.SpeedLimits.Add(speed,route,!slope);
            var candidate=world.Replace(world.Revision,world.Terrain,world.Spatial.Capture(),world.Voxels,world.Slopes,limits);
            return new(Topology(),candidate,ConstructionBooking.None,[],[],[],[],speedLimit:true);
        }
        catch(ArgumentException e) {return Reject(e.Message);}
        catch(InvalidOperationException e) when(e.Message=="invalidRailPattern"){return Reject(e.Message);}
        catch(OverflowException){return Reject("constructionOverflow");}
    }
    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time)
    {
        if(!ReferenceEquals(state.World,plan.Source))throw new InvalidOperationException("staleRevision");
        if(!plan.Legal)throw new InvalidOperationException(plan.Reason);
        if(!plan.IsSpeedLimit||plan.Candidate is not {} w)throw new InvalidOperationException("invalidSpeedLimitPlan");
        return state.Replace(state.World.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,ConstructionBooking.None,time,
            plan.Topology.Removal?"speedLimitRemoval":"speedLimitBuild",w.SpeedLimits);
    }
}
