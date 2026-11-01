// ExAV 883cf7d SignalRailController/SignalRailRoad; original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// A harbor is addressed by its durable spatial owner, independently of the future
// shared P5 controller. Null ControllerId means no controller has been assigned.
public sealed record SignalControllerReference(long OwnerId, long? ControllerId);
public static class SignalRailwayConstruction
{
    public const string ThreeAspect = "{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}";
    public const string Shunting = "{6FA55842-E693-414f-A120-0A3D0C613B21}";
    public static bool DelegatesStopTime(int signalDirection, Heading heading)
    {
        if(signalDirection is not (0 or 2 or 4 or 6))throw new ArgumentException("invalidInput");
        var angle=Math.Abs(signalDirection-heading.Index);return Math.Min(angle,8-angle)<=1;
    }
    public static SignalControllerReference? ControllerReference(MixedWorld world,Location p)
    {
        var v=world.At(p);return v?.Traffic?.Rail is {} r&&world.Profile.Resolve(r.Definition).Handler==RailwayHandler.Signal
            ? new(v.OwnerId,r.ControllerId):null;
    }
    public static RailwayConstructionPlan Preview(MixedWorld world,Location p,string type,int direction,bool removal=false)
    {
        var cells=ImmutableArray.CreateBuilder<NormalRailwayCell>();
        NormalRailwayPlan Topology(string? reason=null)=>new(world,p,p,removal,reason,cells.ToImmutable());
        RailwayConstructionPlan Reject(string reason)=>new(Topology(reason),reason);
        if(direction is not (0 or 2 or 4 or 6)||type is not (ThreeAspect or Shunting))return Reject("invalidInput");
        if(!world.Terrain.Space.ContainsVoxel(p))return Reject("outside");
        var before=world.At(p);var rail=before?.Traffic?.Rail;
        if(before?.Traffic?.Car!=null)return Reject("carOccupied");
        var handler=rail==null?(RailwayHandler?)null:world.Profile.Resolve(rail.Definition).Handler;
        if(removal&&handler!=RailwayHandler.Signal)return Reject("signalNotFound");
        if(!removal&&(before!=null&&before.Kind!=RailwayVoxelKind.Traffic||world.Spatial.At(p).Status==VoxelStatus.Occupied&&before==null))return Reject("obstacle");
        // SingleRailRoad includes curves, but JunctionRailRoad is a separate class.
        if(!removal&&handler!=null&&handler!=RailwayHandler.Normal)return Reject("signalTarget");
        var pattern=removal?rail!.Pattern:direction%4==0?10:4;
        var binding=world.Profile.Bindings.SingleOrDefault(b=>removal?b.Handler==RailwayHandler.Normal&&b.Variant==pattern:b.Handler==RailwayHandler.Signal&&b.Reference.ContributionId==type);
        if(binding==null)return Reject("unavailableRailBinding");
        var after=new RailComponent(binding.Reference,pattern,Direction:removal?0:direction);
        cells.Add(new(p,new Heading(direction).Opposite,new(direction),rail,after,false));
        try
        {
            var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);var voxels=world.Voxels.ToDictionary(v=>v.Position);var nextId=world.Spatial.NextEntityId;
            var traffic=TrafficLifecycle.Refresh(world.Profile,(before?.Traffic??new()) with{Rail=after});
            var losses=before?.Traffic?.Accessory is {} old && old!=traffic.Accessory ? new[]{new RailwayComponentLoss(p,old)} : [];
            if(before!=null){voxels[p]=before with{Traffic=traffic};entities[before.OwnerId]=entities[before.OwnerId] with{Revision=checked(entities[before.OwnerId].Revision+1)};}
            else{
                var id=nextId;nextId=checked(nextId+1);
                entities.Add(id,new(id,0,world.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
                voxels.Add(p,new(p,id,RailwayVoxelKind.Traffic,traffic));
            }
            if(voxels.Count>NormalRailwayConstruction.MaximumRailwayVoxels)return Reject("railwayCapacityLimit");
            var candidate=world.Replace(world.Revision,world.Terrain,new(nextId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),voxels.Values,world.Slopes);
            return new(Topology(),candidate,ConstructionBooking.None,[],[],[],losses,RailwayHandler.Signal);
        }
        catch(InvalidOperationException e) when(e.Message=="unavailableCrossingBinding"){return Reject(e.Message);}
        catch(OverflowException){return Reject("constructionOverflow");}
    }
    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time)
    {
        if(!ReferenceEquals(state.World,plan.Source))throw new InvalidOperationException("staleRevision");
        if(!plan.Legal)throw new InvalidOperationException(plan.Reason);
        if(plan.PreparedHandler!=RailwayHandler.Signal||plan.Candidate==null)throw new InvalidOperationException("invalidSignalPlan");
        var w=plan.Candidate;return state.Replace(state.World.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,plan.Booking,time,plan.Topology.Removal?"signalRemoval":"signalBuild");
    }
}
