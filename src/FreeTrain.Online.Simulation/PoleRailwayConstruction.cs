// ExAV 883cf7d ElectricPoleContribution/ElectricPole/Traffic; LGPL-2.1-or-later.
using System.Collections.Immutable;
namespace FreeTrain.Online.Simulation;

public static class PoleRailwayConstruction
{
    public const string Double1 = "{DDF7F835-8100-45fd-8AE7-EC498796D387}";
    public const string Double2 = "{DD7D4548-5E0E-48e4-AB6E-64A443C240F7}";
    public const string Single1 = "{86E56A60-19DF-4ba7-BFE8-ABFBDF5DFA0A}";
    public const string Single2 = "{FD1635C1-05BF-41af-9185-4891B983E2A4}";
    public static bool IsBundledType(string type) => type is Double1 or Double2 or Single1 or Single2;
    public static RailwayConstructionPlan Preview(MixedWorld world,Location p,string type,bool removal=false)
    {
        var cells=ImmutableArray.CreateBuilder<NormalRailwayCell>();
        NormalRailwayPlan Topology()=>new(world,p,p,removal,null,cells.ToImmutable());
        RailwayConstructionPlan Reject(string reason)=>new(Topology(),reason);
        if(!IsBundledType(type))return Reject("invalidInput");
        if(!world.Terrain.Space.ContainsVoxel(p))return Reject("outside");
        var before=world.At(p);var traffic=before?.Traffic;var rail=traffic?.Rail;
        if(removal ? traffic?.Accessory is not {} a || world.Profile.Resolve(a.Definition).Handler!=RailwayHandler.Pole
            : rail==null || rail.Pattern is not (4 or 10))return Reject(removal?"poleNotFound":"poleTarget");
        if(removal && traffic!.Rail==null && traffic.Road==null && traffic.Car!=null)return Reject("carOccupied");
        var binding=world.Profile.Bindings.SingleOrDefault(b=>b.Handler==RailwayHandler.Pole&&b.Reference.ContributionId==type);
        if(binding==null)return Reject("unavailableRailBinding");
        // Two opposite cardinal ports; subtype and cars do not exclude placement.
        // Non-null replacement does not invoke the old accessory's onRemoved.
        var after=traffic! with{Accessory=removal?null:new(binding.Reference,rail!.Pattern==10?1:0)};
        cells.Add(new(p,new(0),new(4),rail,rail,false));
        try
        {
            after=TrafficLifecycle.Refresh(world.Profile,after);
            var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);var voxels=world.Voxels.ToDictionary(v=>v.Position);
            if(after.Rail==null&&after.Road==null&&after.Car==null&&after.Platform==null&&after.Accessory==null){voxels.Remove(p);entities.Remove(before!.OwnerId);}
            else{voxels[p]=before! with{Traffic=after};entities[before!.OwnerId]=entities[before.OwnerId] with{Revision=checked(entities[before.OwnerId].Revision+1)};}
            var candidate=world.Replace(world.Revision,world.Terrain,new(world.Spatial.NextEntityId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),voxels.Values,world.Slopes);
            var losses=traffic!.Accessory is {} old?new[]{new RailwayComponentLoss(p,old)}:[];
            return new(Topology(),candidate,ConstructionBooking.None,[],[],[],losses,RailwayHandler.Pole);
        }
        catch(InvalidOperationException e) when(e.Message=="unavailableCrossingBinding"){return Reject(e.Message);}
        catch(OverflowException){return Reject("constructionOverflow");}
    }
    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time)
    {
        if(!ReferenceEquals(state.World,plan.Source))throw new InvalidOperationException("staleRevision");
        if(!plan.Legal)throw new InvalidOperationException(plan.Reason);
        if(plan.PreparedHandler!=RailwayHandler.Pole||plan.Candidate==null)throw new InvalidOperationException("invalidPolePlan");
        var w=plan.Candidate;return state.Replace(state.World.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,plan.Booking,time,plan.Topology.Removal?"poleRemoval":"poleBuild");
    }
}
