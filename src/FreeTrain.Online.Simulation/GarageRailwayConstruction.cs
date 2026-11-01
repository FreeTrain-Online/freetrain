// ExAV 2.1.1.12, 883cf7d/trunk/plugins/org.kohsuke.freetrain.rail.garage/src/TrainGarage.cs.
// Original authors, LGPL-2.1-or-later. See doc/web-parity-garage-tool.md.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class GarageRailwayConstruction
{
    public const string Contribution = "{93481807-D998-46d7-B1BE-CB2B5D086D67}";
    public static RailwayConstructionPlan Preview(MixedWorld world,Location from,Location to,bool removal=false)
    {
        var cells=ImmutableArray.CreateBuilder<NormalRailwayCell>();
        NormalRailwayPlan Topology(string? reason=null) => new(world,from,to,removal,reason,cells.ToImmutable());
        RailwayConstructionPlan Reject(string reason) => new(Topology(reason),reason);
        to=BridgeRailwayConstruction.Snap(from,to);
        if(from==to)return Reject("samePoint");
        var length=Math.Max(Math.Abs((long)to.X-from.X),Math.Abs((long)to.Y-from.Y));
        if(length>=NormalRailway.MaximumRouteCells)return Reject("routeWorkLimit");
        var d=from.DirectionTo(to);var(dx,dy)=d.Offset;
        var pattern=d.Index%4==0?10:4;
        var binding=world.Profile.Bindings.SingleOrDefault(b=>b.Handler==RailwayHandler.Garage&&b.Reference.ContributionId==Contribution);
        var normal=world.Profile.Bindings.SingleOrDefault(b=>b.Handler==RailwayHandler.Normal&&b.Variant==pattern);
        if(binding==null||normal==null)return Reject("unavailableRailBinding");
        var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);
        var voxels=world.Voxels.ToDictionary(v=>v.Position);
        var nextId=world.Spatial.NextEntityId;
        var matches=new List<bool>();
        var losses=new List<RailwayComponentLoss>();
        try
        {
            for(var i=0;i<=length;i++)
            {
                var p=new Location(checked(from.X+i*dx),checked(from.Y+i*dy),from.Z);
                if(!world.Terrain.Space.ContainsVoxel(p))return Reject("outside");
                var before=world.At(p);var rail=before?.Traffic?.Rail;var after=rail;
                var car=before?.Traffic?.Car!=null;
                var aligned=rail!=null&&RailwayTopology.Ports(rail.Pattern).Contains(d.Index)&&RailwayTopology.Ports(rail.Pattern).Contains(d.Opposite.Index);
                if(!removal)
                {
                    if(car)return Reject("carOccupied");
                    var s=world.Terrain.Space.ToStorage(p);
                    if(world.Terrain.Cell(s.H,s.V).Ground!=p.Z)return Reject("garageGroundRequired");
                    if(world.Spatial.At(p).Status==VoxelStatus.Occupied&&!aligned)return Reject("cannotAttach");
                    // These owners carry backups/footprints which cannot be discarded by rail replacement.
                    if(rail!=null&&(rail.TerrainBackup!=null||world.Slopes.Any(s=>s.EntityId==before!.OwnerId)))return Reject("garageDependency");
                    after=new(binding.Reference,pattern,d.Index%4==0?1:0,Direction:d.Index);
                }
                else if(i<length)
                {
                    var match=aligned&&world.Profile.Resolve(rail!.Definition).Handler==RailwayHandler.Garage;
                    matches.Add(match); // Source bills matching cars and repeats the subtotal at gaps.
                    if(match&&!car)after=new(normal.Reference,pattern);
                }
                cells.Add(new(p,d.Opposite,d,rail,after,car));
                if(removal&&rail==after)continue;
                var traffic=TrafficLifecycle.Refresh(world.Profile,(before?.Traffic??new()) with{Rail=after});
                if(before?.Traffic?.Accessory is {} a && traffic.Accessory!=a)losses.Add(new(p,a));
                if(before!=null)
                {
                    voxels[p]=before with{Traffic=traffic};
                    entities[before.OwnerId]=entities[before.OwnerId] with{Revision=checked(entities[before.OwnerId].Revision+1)};
                }
                else
                {
                    var id=nextId;nextId=checked(nextId+1);
                    entities.Add(id,new(id,0,world.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
                    voxels.Add(p,new(p,id,RailwayVoxelKind.Traffic,traffic));
                }
            }
            if(voxels.Count>NormalRailwayConstruction.MaximumRailwayVoxels)return Reject("railwayCapacityLimit");
            var candidate=world.Replace(world.Revision,world.Terrain,new(nextId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),voxels.Values,world.Slopes);
            return new(Topology(),candidate,removal?RailwayBooking.GarageRemoval(matches):RailwayBooking.GarageBuild(length+1),[],[],[],losses,RailwayHandler.Garage);
        }
        catch(InvalidOperationException e) when(e.Message=="unavailableCrossingBinding"){return Reject(e.Message);}
        catch(OverflowException){return Reject("constructionOverflow");}
    }
    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time)
    {
        if(!ReferenceEquals(state.World,plan.Source))throw new InvalidOperationException("staleRevision");
        if(!plan.Legal)throw new InvalidOperationException(plan.Reason);
        if(plan.PreparedHandler!=RailwayHandler.Garage||plan.Candidate==null)throw new InvalidOperationException("invalidGaragePlan");
        if(plan.Booking.Charges.Length>NormalRailwayConstruction.MaximumAccountTransactions-state.Accounts.Transactions.Length)throw new InvalidOperationException("accountCapacityLimit");
        var w=plan.Candidate;
        return state.Replace(state.World.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,plan.Booking,time,plan.Topology.Removal?"garageRemoval":"garageBuild");
    }
}
