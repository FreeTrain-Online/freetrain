// Behavioral extraction: ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail/TunnelRail.cs.
// Original authors, LGPL-2.1-or-later. See doc/web-parity-tunnel-tool.md.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class TunnelRailwayConstruction
{
    public const string Contribution = "{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}";
    public static RailwayConstructionPlan Preview(MixedWorld world, Location from, Location to, bool removal=false)
    {
        var cells=ImmutableArray.CreateBuilder<NormalRailwayCell>();
        NormalRailwayPlan Topology(string? reason=null) => new(world,from,to,removal,reason,cells.ToImmutable());
        RailwayConstructionPlan Reject(string reason) => new(Topology(reason),reason);
        to=BridgeRailwayConstruction.Snap(from,to);
        if (from==to) return Reject("samePoint");
        var length=Math.Max(Math.Abs((long)to.X-from.X),Math.Abs((long)to.Y-from.Y));
        if (length >= NormalRailway.MaximumRouteCells) return Reject("routeWorkLimit");
        if (!removal && from.Z < world.Terrain.WaterLevel) return Reject("underwater");
        var d=from.DirectionTo(to);var (dx,dy)=d.Offset;
        var pattern=d.Index%4==0 ? 10 : 4;
        var binding=world.Profile.Bindings.SingleOrDefault(b=>b.Handler==RailwayHandler.Tunnel && b.Reference.ContributionId==Contribution);
        var normal=world.Profile.Bindings.SingleOrDefault(b=>b.Handler==RailwayHandler.Normal && b.Variant==pattern);
        if (binding==null || normal==null) return Reject("unavailableRailBinding");
        var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);
        var terrainEntities=world.Terrain.Spatial.Entities.ToDictionary(e=>e.Id);
        var occupants=world.Terrain.Occupants.ToDictionary(o=>o.EntityId);
        var backups=world.Terrain.TunnelBackups.ToBuilder();
        var voxels=world.Voxels.ToDictionary(v=>v.Position);
        var reclaimed=new List<RailwayReclamation>();
        var nextId=world.Spatial.NextEntityId;var changed=0;var mountainFound=false;var terrainChanged=false;
        long Allocate() {var id=nextId;nextId=checked(nextId+1);return id;}
        try
        {
            // Keep the inclusive route visible; only removal mutation is half-open (GEO-006).
            for(var i=0;i<=length;i++)
            {
                var p=new Location(checked(from.X+i*dx),checked(from.Y+i*dy),from.Z);
                if (!world.Terrain.Space.ContainsVoxel(p)) return Reject("outside");
                var before=world.At(p);var rail=before?.Traffic?.Rail;var after=rail;
                var car=before?.Traffic?.Car!=null;
                var mountain=world.Terrain.OccupantAt(p);
                var isMountain=mountain?.Kind==TerrainOccupantKind.Mountain;
                if (!removal)
                {
                    if (car) return Reject("carOccupied");
                    if (isMountain) mountainFound=true;
                    else if(world.Spatial.At(p).Status==VoxelStatus.Occupied &&
                        (rail==null || !RailwayTopology.Ports(rail.Pattern).Contains(d.Index) || !RailwayTopology.Ports(rail.Pattern).Contains(d.Opposite.Index)))
                        return Reject("cannotAttach");
                    if(rail==null)
                    {
                        if(isMountain)
                        {
                            var owner=entities[mountain!.EntityId];reclaimed.Add(new(owner,mountain));
                            entities.Remove(owner.Id);terrainEntities.Remove(owner.Id);occupants.Remove(owner.Id);
                            backups[p]=mountain.Corners;terrainChanged=true;
                            after=new(binding.Reference,pattern,d.Index%4==0?1:0,mountain.Corners,d.Index);
                        }
                        else after=new(normal.Reference,pattern);
                        var id=Allocate();
                        entities.Add(id,new(id,0,world.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
                        voxels.Add(p,new(p,id,RailwayVoxelKind.Traffic,new(after)));
                        changed++;
                    }
                }
                else if(i<length && rail!=null && world.Profile.Resolve(rail.Definition).Handler==RailwayHandler.Tunnel &&
                    RailwayTopology.Ports(rail.Pattern).Contains(d.Index) && !car)
                {
                    // Restoration replaces Traffic entirely. Protect future component
                    // owners until their explicit cross-tool policy is implemented.
                    if(before!.Traffic!.Platform!=null) return Reject("platformDependency");
                    if(before.Traffic.Road!=null || before.Traffic.Accessory!=null) return Reject("tunnelComponentDependency");
                    after=null;voxels.Remove(p);entities.Remove(before.OwnerId);
                    var id=Allocate();var corners=rail.TerrainBackup!.Value;
                    var definition=world.Terrain.Profile.Definitions.Single(b=>b.Kind==TerrainOccupantKind.Mountain && b.TreeSpriteCount==0).Reference;
                    var owner=new SpatialEntity(id,0,definition,p,new([new(new(0,0,0),corners.Flat)]),false,false,0);
                    entities.Add(id,owner);terrainEntities.Add(id,owner);
                    occupants.Add(id,new(id,TerrainOccupantKind.Mountain,corners));
                    backups.Remove(p);terrainChanged=true;changed++;
                }
                cells.Add(new(p,d.Opposite,d,rail,after,car));
            }
            if(!removal && !mountainFound) return Reject("tunnelMountainRequired");
            if(voxels.Count>NormalRailwayConstruction.MaximumRailwayVoxels) return Reject("railwayCapacityLimit");
            var terrain=world.Terrain;
            if(terrainChanged) terrain=terrain.Replace(terrain.Revision,terrain.Cells,terrain.WaterLevel,
                new(nextId,terrainEntities.Values.OrderBy(e=>e.Id).ToImmutableArray()),occupants.Values,backups);
            var candidate=world.Replace(world.Revision,terrain,new(nextId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),voxels.Values,world.Slopes);
            return new(Topology(),candidate,removal?RailwayBooking.TunnelRemoval(changed):RailwayBooking.TunnelBuild(changed),reclaimed,[],[],[],RailwayHandler.Tunnel);
        }
        catch(OverflowException) {return Reject("constructionOverflow");}
    }
    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time)
    {
        if(!ReferenceEquals(state.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if(!plan.Legal) throw new InvalidOperationException(plan.Reason);
        // The session already planned against this exact immutable predecessor.
        // Only this handler can issue its prepared candidate; another tool's
        // plan cannot be substituted. A stale source still rejects above.
        if(plan.PreparedHandler!=RailwayHandler.Tunnel || plan.Candidate==null) throw new InvalidOperationException("invalidTunnelPlan");
        if(state.Accounts.Transactions.Length>=NormalRailwayConstruction.MaximumAccountTransactions) throw new InvalidOperationException("accountCapacityLimit");
        var w=plan.Candidate;
        return state.Replace(state.World.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,plan.Booking,time,
            plan.Topology.Removal?"tunnelRemoval":"tunnelBuild");
    }
}
