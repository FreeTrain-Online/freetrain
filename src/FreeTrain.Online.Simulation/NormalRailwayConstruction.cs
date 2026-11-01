// Behavioral extraction: ExAV 2.1.1.12, 883cf7d/trunk/core/
// world/rail/{SingleRailRoad,BridgePierVoxel}.cs, world/Traffic.cs and
// controllers/Disambiguators.cs.
// Original authors, LGPL-2.1-or-later. See doc/web-parity-railway-construction.md.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record RailwayReclamation(SpatialEntity Entity, TerrainOccupant Occupant);
public sealed record RailwayComponentLoss(Location Position, AccessoryComponent Accessory);

// A detached, read-only quote. Candidate identities are provisional: no IDs,
// money, terrain or session state are reserved or published by preview.
public sealed class RailwayConstructionPlan
{
    public NormalRailwayPlan Topology { get; }
    public MixedWorld Source => Topology.Source;
    public string? Reason { get; }
    public bool Legal => Reason == null;
    public ConstructionBooking Booking { get; }
    public ImmutableArray<RailwayReclamation> Reclaimed { get; }
    public ImmutableArray<RailwayVoxel> RemovedPiers { get; }
    public ImmutableArray<RailwayPierPlacement> ElectedPiers { get; }
    public ImmutableArray<RailwayComponentLoss> ComponentLosses { get; }
    internal MixedWorld? Candidate { get; }
    internal RailwayHandler? PreparedHandler { get; }
    internal RailwayConstructionPlan(NormalRailwayPlan topology, string reason)
    { Topology=topology; Reason=reason; Booking=ConstructionBooking.None; Reclaimed=[]; RemovedPiers=[]; ElectedPiers=[]; ComponentLosses=[]; }
    internal RailwayConstructionPlan(NormalRailwayPlan topology, MixedWorld candidate, ConstructionBooking booking,
        IEnumerable<RailwayReclamation> reclaimed, IEnumerable<RailwayVoxel> removed,
        IEnumerable<RailwayPierPlacement> elected, IEnumerable<RailwayComponentLoss> losses, RailwayHandler? preparedHandler = null)
    {
        Topology=topology; Candidate=candidate; Booking=booking; PreparedHandler=preparedHandler;
        Reclaimed=reclaimed.ToImmutableArray(); RemovedPiers=removed.ToImmutableArray();
        ElectedPiers=elected.ToImmutableArray(); ComponentLosses=losses.ToImmutableArray();
    }
}

public static class NormalRailwayConstruction
{
    // Current durable mixed-world budget; P4-23 owns measured capacity. Keep
    // transport constants aligned without introducing a Simulation dependency.
    public const int MaximumRailwayVoxels = 8192;
    public const int MaximumAccountTransactions = 8192;

    public static bool IsUsableStart(MixedWorld world, Location p)
    {
        try
        {
            if (!world.Terrain.Space.ContainsVoxel(p)) return false;
            var s=world.Terrain.Space.ToStorage(p);
            return world.At(p)?.Traffic?.Rail != null || p.Z <= world.Terrain.Cell(s.H,s.V).Ground;
        }
        catch (OverflowException) { return false; }
    }

    public static RailwayConstructionPlan PreviewBuild(MixedWorld world, Location from, Location to, bool reverseBend = false) => Preview(world,from,to,false,reverseBend);
    public static RailwayConstructionPlan PreviewRemoval(MixedWorld world, Location from, Location to, bool reverseBend = false) => Preview(world,from,to,true,reverseBend);
    private static RailwayConstructionPlan Preview(MixedWorld world, Location from, Location to, bool removal, bool reverseBend)
    {
        var topology=removal ? NormalRailway.PlanRemoval(world,from,to,reverseBend) : NormalRailway.PlanBuild(world,from,to,reverseBend);
        RailwayConstructionPlan Reject(string reason) => new(topology,reason);
        if (!topology.TopologyValid) return Reject(topology.Reason!);
        if (!IsUsableStart(world,from)) return Reject("unusableStart");
        var reclaimed=new Dictionary<long,RailwayReclamation>();
        var charges=new List<RailConstructionCharge>();
        foreach (var cell in topology.Cells)
        {
            var p=cell.Position; var s=world.Terrain.Space.ToStorage(p); var ground=world.Terrain.Cell(s.H,s.V).Ground;
            if (!removal)
            {
                if (ground <= p.Z && p.Z <= world.Terrain.WaterLevel && ground < world.Terrain.WaterLevel)
                    return Reject("underwater");
                long value=0;
                if (world.Spatial.At(p).Entity is {} entity && world.At(p)?.Kind != RailwayVoxelKind.Traffic)
                {
                    if (!entity.SilentlyReclaimable) return Reject("obstacle");
                    // Every currently registered reclaimable occupant is terrain.
                    // Future handlers must implement whole-entity removal explicitly.
                    var occupant=world.Terrain.OccupantAt(p);
                    if (occupant == null) return Reject("unavailableReclamationHandler");
                    reclaimed.TryAdd(entity.Id,new(entity,occupant));
                    value=entity.Value; // Source quotes per visited cell, not per unique owner.
                }
                charges.Add(new(p.Z,ground,value));
            }
            // Platform callbacks belong to P6; refuse an orphan rather than
            // inventing deletion of a station/platform entity at this boundary.
            if (cell.After == null && cell.Before != null && world.At(p)?.Traffic?.Platform != null)
                return Reject("platformDependency");
        }

        try
        {
            var booking=removal ? RailwayBooking.NormalRemoval(topology.ChargedRailCells,to.Z,world.Terrain.WaterLevel)
                : RailwayBooking.NormalBuild(charges);
            var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);
            var voxels=world.Voxels.ToDictionary(v=>v.Position);
            var removed=new List<RailwayVoxel>(); var elected=new List<RailwayPierPlacement>();
            var losses=new List<RailwayComponentLoss>();
            var nextId=world.Spatial.NextEntityId;
            long Allocate() { var id=nextId; nextId=checked(nextId+1); return id; }
            foreach (var id in reclaimed.Keys) entities.Remove(id);
            foreach (var cell in topology.Cells)
            {
                var p=cell.Position;
                if (cell.Changed)
                {
                    var before=world.At(p);
                    var components=(before?.Traffic ?? new()) with { Rail=cell.After };
                    var crossing=components.Rail is {} r && components.Road is {} road &&
                        (r.Pattern == 10 && (road.Edges & 68) == 68 || r.Pattern == 4 && (road.Edges & 17) == 17);
                    if (!crossing && components.Accessory is {} a && world.Profile.Resolve(a.Definition).Handler == RailwayHandler.Crossing)
                    {
                        losses.Add(new(p,a)); components=components with { Accessory=null };
                    }
                    else if (crossing && components.Accessory == null)
                    {
                        var matches=world.Profile.Bindings.Where(b=>b.Handler==RailwayHandler.Crossing).Take(2).ToArray();
                        if (matches.Length != 1) return Reject("unavailableCrossingBinding");
                        components=components with { Accessory=new(matches[0].Reference) };
                    }
                    if (components.Rail == null && components.Road == null && components.Accessory == null)
                    {
                        voxels.Remove(p); entities.Remove(before!.OwnerId);
                    }
                    else if (before != null)
                    {
                        voxels[p]=before with { Traffic=components };
                        var owner=entities[before.OwnerId];
                        entities[owner.Id]=owner with { Revision=checked(owner.Revision+1) };
                    }
                    else
                    {
                        var id=Allocate();
                        entities.Add(id,Owner(id,world.Profile.TrafficOwner,p));
                        voxels.Add(p,new(p,id,RailwayVoxelKind.Traffic,components));
                    }
                }
                if (removal)
                {
                    foreach (var pier in RailwaySupports.Teardown(world,p))
                    { removed.Add(pier); voxels.Remove(pier.Position); entities.Remove(pier.OwnerId); }
                }
                else if (cell.Before == null)
                {
                    // Default normal/girder style. Slope and bridge consumers
                    // supply their own explicit top/body bindings to Elect.
                    var bindings=world.Profile.Bindings.Where(b=>b.Handler==RailwayHandler.Pier && b.Variant==0).Take(2).ToArray();
                    if (bindings.Length != 1) return Reject("unavailablePierBinding");
                    foreach (var placement in RailwaySupports.Elect(world,p,bindings[0],bindings[0]))
                    {
                        var id=Allocate(); elected.Add(placement);
                        entities.Add(id,Owner(id,placement.Definition,placement.Position));
                        voxels.Add(placement.Position,new(placement.Position,id,RailwayVoxelKind.Pier,
                            Definition:placement.Definition,Sprite:placement.Sprite,SupportedOwnerId:voxels[p].OwnerId));
                    }
                }
            }
            if (voxels.Count > MaximumRailwayVoxels) return Reject("railwayCapacityLimit");
            var terrain=world.Terrain;
            if (reclaimed.Count != 0)
                terrain=terrain.Replace(terrain.Revision,terrain.Cells,terrain.WaterLevel,
                    new(terrain.Spatial.NextEntityId,terrain.Spatial.Entities.Where(e=>!reclaimed.ContainsKey(e.Id)).ToImmutableArray()),
                    terrain.Occupants.Where(o=>!reclaimed.ContainsKey(o.EntityId)));
            var candidate=world.Replace(world.Revision,terrain,new(nextId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),voxels.Values,world.Slopes);
            return new(topology,candidate,booking,reclaimed.Values,removed,elected,losses);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }

    private static SpatialEntity Owner(long id, DefinitionReference definition, Location p) =>
        new(id,0,definition,p,new([new(new(0,0,0),true)]),true,false,0);

    // Recompute against the exact authoritative predecessor. A plan from a
    // different world/epoch is stale even if the numeric revision is identical.
    public static MixedConstructionState Apply(MixedConstructionState state, RailwayConstructionPlan plan, GameTime time)
    {
        if (!ReferenceEquals(state.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (!plan.Legal) throw new InvalidOperationException(plan.Reason);
        var actual=Preview(state.World,plan.Topology.From,plan.Topology.To,plan.Topology.Removal,plan.Topology.ReverseBend);
        if (!actual.Legal) throw new InvalidOperationException(actual.Reason);
        if (state.Accounts.Transactions.Length >= MaximumAccountTransactions) throw new InvalidOperationException("accountCapacityLimit");
        var candidate=actual.Candidate!;
        return state.Replace(state.World.Revision,candidate.Terrain,candidate.Spatial.Capture(),candidate.Voxels,candidate.Slopes,
            actual.Booking,time,plan.Topology.Removal ? "normalRailRemoval" : "normalRailBuild");
    }
}
