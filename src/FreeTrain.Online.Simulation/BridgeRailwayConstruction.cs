// Behavioral extraction: ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail/BridgeRail.cs,
// controllers/AbstractLineController.cs and world/Location.cs (align4To).
// Original authors, LGPL-2.1-or-later. See doc/web-parity-bridge-tool.md.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class BridgeRailwayConstruction
{
    public const string Contribution = "{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}";

    public static Location Snap(Location from, Location to) => Math.Abs((long)to.X-from.X) >= Math.Abs((long)to.Y-from.Y)
        ? new(to.X,from.Y,from.Z) : new(from.X,to.Y,from.Z);

    public static RailwayConstructionPlan Preview(MixedWorld world, Location from, Location to, bool removal = false)
    {
        to=Snap(from,to);
        var cells=ImmutableArray.CreateBuilder<NormalRailwayCell>();
        NormalRailwayPlan Topology(string? reason=null) => new(world,from,to,removal,reason,cells.ToImmutable());
        RailwayConstructionPlan Reject(string reason) => new(Topology(reason),reason);
        if (from == to) return Reject("samePoint");
        var length=Math.Max(Math.Abs((long)to.X-from.X),Math.Abs((long)to.Y-from.Y));
        if (length >= NormalRailway.MaximumRouteCells) return Reject("routeWorkLimit");
        var d=from.DirectionTo(to); var (dx,dy)=d.Offset;
        var bridges=world.Profile.Bindings.Where(b=>b.Handler==RailwayHandler.Bridge && b.Reference.ContributionId==Contribution).Take(2).ToArray();
        if (bridges.Length != 1) return Reject("unavailableRailBinding");
        var bridge=bridges[0];
        var pattern=d.Index is 2 or 6 ? 4 : 10;
        var building=false; var elect=true;
        var elected=new List<RailwayPierPlacement>(); var removed=new List<RailwayVoxel>();
        var losses=new List<RailwayComponentLoss>();
        var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);
        var voxels=world.Voxels.ToDictionary(v=>v.Position);
        var nextId=world.Spatial.NextEntityId;
        long Allocate() { var id=nextId;nextId=checked(nextId+1);return id; }
        SpatialEntity Owner(long id,DefinitionReference definition,Location p) =>
            new(id,0,definition,p,new([new(new(0,0,0),true)]),true,false,0);
        var charges=new List<RailConstructionCharge>();
        try
        {
            for (var i=0;i<=length;i++)
            {
                var p=new Location(checked(from.X+i*dx),checked(from.Y+i*dy),from.Z);
                if (!world.Terrain.Space.ContainsVoxel(p)) return Reject("outside");
                var s=world.Terrain.Space.ToStorage(p);
                charges.Add(new(p.Z,world.Terrain.Cell(s.H,s.V).Ground));
                var before=world.At(p);var rail=before?.Traffic?.Rail;var after=rail;
                var car=before?.Traffic?.Car != null;
                if (!removal)
                {
                    if (car) return Reject("carOccupied");
                    // The source tests occupancy and both directional ports. It
                    // does not test water, ground, reclaimability, or endpoint status.
                    if (world.Spatial.At(p).Status==VoxelStatus.Occupied &&
                        (rail == null || !RailwayTopology.Ports(rail.Pattern).Contains(d.Index) || !RailwayTopology.Ports(rail.Pattern).Contains(d.Opposite.Index)))
                        return Reject("cannotAttach");
                    if (rail != null) building=false;
                    else
                    {
                        var next=p with { X=checked(p.X+dx),Y=checked(p.Y+dy) };
                        var mode=!building ? 0 : i==length || world.At(next)?.Traffic?.Rail != null ? 2 : 1;
                        building=true;
                        var variant=d.Index switch { 0=>3+mode,2=>2-mode,4=>5-mode,_=>mode };
                        after=new(bridge.Reference,pattern,variant,Direction:d.Index);
                    }
                }
                else if (rail != null && world.Profile.Resolve(rail.Definition).Handler==RailwayHandler.Bridge &&
                    RailwayTopology.Ports(rail.Pattern).Contains(d.Index) && !car)
                {
                    if (before!.Traffic!.Platform != null) return Reject("platformDependency");
                    after=null;
                }
                cells.Add(new(p,d.Opposite,d,rail,after,car));
                if (rail==after) continue;
                var traffic=(before?.Traffic ?? new()) with { Rail=after };
                if (after == null && traffic.Accessory is {} a && world.Profile.Resolve(a.Definition).Handler==RailwayHandler.Crossing)
                { losses.Add(new(p,a));traffic=traffic with { Accessory=null }; }
                if (after==null && traffic.Road==null && traffic.Accessory==null)
                { voxels.Remove(p);entities.Remove(before!.OwnerId); }
                else if (before != null)
                {
                    voxels[p]=before with { Traffic=traffic };
                    entities[before.OwnerId]=entities[before.OwnerId] with { Revision=checked(entities[before.OwnerId].Revision+1) };
                }
                else
                {
                    var id=Allocate();entities.Add(id,Owner(id,world.Profile.TrafficOwner,p));
                    voxels.Add(p,new(p,id,RailwayVoxelKind.Traffic,traffic));
                }
                if (removal)
                {
                    // Unlike normal removal, only actually removed matching rails
                    // call teardown; occupied and wrong-subtype cells retain piers.
                    foreach (var pier in RailwaySupports.Teardown(world,p))
                    { removed.Add(pier);voxels.Remove(pier.Position);entities.Remove(pier.OwnerId); }
                }
                else
                {
                    if (elect)
                    {
                        var axis=d.Index is 2 or 6 ? 0 : 2;
                        RailwayBinding? Pier(int n)
                        {
                            var matches=world.Profile.Bindings.Where(b=>b.Handler==RailwayHandler.Pier && b.Reference.ContributionId==$"system:bridge-pier/{n}").Take(2).ToArray();
                            return matches.Length==1?matches[0]:null;
                        }
                        var top=Pier(axis);var body=Pier(axis+1);
                        if (top==null || body==null) return Reject("unavailablePierBinding");
                        foreach (var placement in RailwaySupports.Elect(world,p,top,body))
                        {
                            var id=Allocate();elected.Add(placement);
                            entities.Add(id,Owner(id,placement.Definition,placement.Position));
                            voxels.Add(placement.Position,new(placement.Position,id,RailwayVoxelKind.Pier,
                                Definition:placement.Definition,Sprite:placement.Sprite,SupportedOwnerId:voxels[p].OwnerId));
                        }
                    }
                    elect=!elect;
                }
            }
            if (voxels.Count > NormalRailwayConstruction.MaximumRailwayVoxels) return Reject("railwayCapacityLimit");
            var booking=RailwayBooking.Elevated(charges,from.Z,charges[0].Ground,world.Terrain.WaterLevel,removal);
            var candidate=world.Replace(world.Revision,world.Terrain,new(nextId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),voxels.Values,world.Slopes);
            return new(Topology(),candidate,booking,[],removed,elected,losses);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }

    public static MixedConstructionState Apply(MixedConstructionState state,RailwayConstructionPlan plan,GameTime time)
    {
        if (!ReferenceEquals(state.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (!plan.Legal) throw new InvalidOperationException(plan.Reason);
        var actual=Preview(state.World,plan.Topology.From,plan.Topology.To,plan.Topology.Removal);
        if (!actual.Legal) throw new InvalidOperationException(actual.Reason);
        if (state.Accounts.Transactions.Length>=NormalRailwayConstruction.MaximumAccountTransactions) throw new InvalidOperationException("accountCapacityLimit");
        var w=actual.Candidate!;
        return state.Replace(state.World.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,actual.Booking,time,
            plan.Topology.Removal ? "bridgeRemoval" : "bridgeBuild");
    }
}
