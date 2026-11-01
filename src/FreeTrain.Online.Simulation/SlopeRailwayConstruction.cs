// Behavioral extraction: ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail/
// {SlopeRailRoad,BridgePierVoxel}.cs. Original authors, LGPL-2.1-or-later.
// See doc/web-parity-railway-slopes.md for the entity/tool removal boundary.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public enum SlopeOperation { Build, Remove, RemoveEntity }
public sealed record SlopeRailPosition(int Level, Location Position, int Pattern, int ZDiff, Heading ZAngle);

public sealed class SlopeRailwayConstructionPlan
{
    public MixedWorld Source { get; }
    public SlopeOperation Operation { get; }
    public Location Selected { get; }
    public Location Base { get; }
    public Heading Climb { get; }
    public long? OwnerId { get; }
    public string? Reason { get; }
    public bool Legal => Reason == null;
    public ConstructionBooking Booking { get; }
    public ImmutableArray<SlopeRailPosition> Rails { get; }
    public ImmutableArray<RailwayVoxel> Members { get; }
    public ImmutableArray<RailwayVoxel> RemovedPiers { get; }
    public ImmutableArray<RailwayPierPlacement> ElectedPiers { get; }
    public ImmutableArray<RailwayComponentLoss> ComponentLosses { get; }
    internal MixedWorld? Candidate { get; }
    internal SlopeRailwayConstructionPlan(MixedWorld source, SlopeOperation operation, Location selected,
        Location @base, Heading climb, long? ownerId, string? reason = null, MixedWorld? candidate = null,
        ConstructionBooking? booking = null, IEnumerable<SlopeRailPosition>? rails = null,
        IEnumerable<RailwayVoxel>? members = null, IEnumerable<RailwayVoxel>? removed = null,
        IEnumerable<RailwayPierPlacement>? elected = null, IEnumerable<RailwayComponentLoss>? losses = null)
    {
        Source=source; Operation=operation; Selected=selected; Base=@base; Climb=climb; OwnerId=ownerId;
        Reason=reason; Candidate=candidate; Booking=booking ?? ConstructionBooking.None;
        Rails=rails?.ToImmutableArray() ?? []; Members=members?.ToImmutableArray() ?? [];
        RemovedPiers=removed?.ToImmutableArray() ?? []; ElectedPiers=elected?.ToImmutableArray() ?? []; ComponentLosses=losses?.ToImmutableArray() ?? [];
    }
}

public static class SlopeRailwayConstruction
{
    // Source pattern.level/zdiff/zangle. Both surface and underground patterns
    // share transitions; only travel matching zangle changes voxel elevation.
    public static (int Level, Heading Climb, int ZDiff, Heading ZAngle) Pattern(int pattern)
    {
        if (pattern is < 28 or >= 60) throw new ArgumentOutOfRangeException(nameof(pattern));
        var index=(pattern-28)%16; var level=index%4; var climb=new Heading(index/4*2);
        return (level,climb,level==1 ? 1 : level==2 ? -1 : 0,level==2 ? climb.Opposite : climb);
    }
    public static int ZDiff(int pattern, Heading travel)
    {
        var p=Pattern(pattern); return travel==p.ZAngle ? p.ZDiff : 0;
    }
    // Any of the eight occupied members resolves through canonical ownership,
    // including invisible filler and support cells (no pixel inference).
    public static (Location Base, Heading Climb, long OwnerId)? Resolve(MixedWorld world, Location selected)
    {
        if (world.At(selected) is not {} member) return null;
        var owner=world.Slopes.FirstOrDefault(s=>s.EntityId==member.OwnerId);
        return owner == null ? null : (world.Spatial.At(selected).Entity!.Anchor,owner.Climb,owner.EntityId);
    }
    private static Location Column(Location @base, Heading climb, int i)
    {
        var (dx,dy)=climb.Offset;
        return new(checked(@base.X+i*dx),checked(@base.Y+i*dy),@base.Z);
    }
    private static RailwayBinding? Binding(MixedWorld world, RailwayHandler handler, int variant)
    {
        var matches=world.Profile.Bindings.Where(b=>b.Handler==handler && b.Variant==variant).Take(2).ToArray();
        return matches.Length==1 ? matches[0] : null;
    }

    public static SlopeRailwayConstructionPlan PreviewBuild(MixedWorld world, Location @base, Heading climb) =>
        Preview(world,@base,climb,SlopeOperation.Build);
    public static SlopeRailwayConstructionPlan PreviewRemoval(MixedWorld world, Location selected, Heading climb) =>
        Preview(world,selected,climb,SlopeOperation.Remove);
    // Trusted whole-entity consumer; no normal-track route calls this method.
    // No tool booking. The cube scan excludes piers directly, but its
    // TrafficVoxel.remove callbacks still tear down supports below each rail.
    public static SlopeRailwayConstructionPlan PreviewEntityRemoval(MixedWorld world, Location selected) =>
        Preview(world,selected,Resolve(world,selected)?.Climb ?? Heading.North,SlopeOperation.RemoveEntity);

    private static SlopeRailwayConstructionPlan Preview(MixedWorld world, Location selected, Heading climb, SlopeOperation operation)
    {
        var @base=selected; long? ownerId=null;
        SlopeRailwayConstructionPlan Reject(string reason) => new(world,operation,selected,@base,climb,ownerId,reason);
        if (!climb.IsCardinal) return Reject("nonCardinalSlope");
        if (operation!=SlopeOperation.Build)
        {
            if (Resolve(world,selected) is not {} resolved) return Reject("notSlope");
            @base=resolved.Base; ownerId=resolved.OwnerId;
            if (climb!=resolved.Climb) return Reject("slopeDirectionMismatch");
        }
        try
        {
            var entities=world.Spatial.Entities.ToDictionary(e=>e.Id);
            var voxels=world.Voxels.ToDictionary(v=>v.Position);
            var slopes=world.Slopes.ToList(); var nextId=world.Spatial.NextEntityId;
            long Allocate() { var id=nextId; nextId=checked(nextId+1); return id; }
            var rails=new List<SlopeRailPosition>(); var members=new List<RailwayVoxel>();
            var removed=new List<RailwayVoxel>(); var elected=new List<RailwayPierPlacement>();
            var losses=new List<RailwayComponentLoss>();
            if (operation==SlopeOperation.Build)
            {
                // Validate all eight stored cells before binding, allocating or booking.
                for (var i=0;i<4;i++)
                {
                    var p=Column(@base,climb,i);
                    foreach (var z in new[]{@base.Z,checked(@base.Z+1)})
                    {
                        var cell=p with { Z=z };
                        if (!world.Terrain.Space.ContainsVoxel(cell)) return Reject("outside");
                        if (world.Spatial.At(cell).Status==VoxelStatus.Occupied) return Reject("obstacle");
                    }
                }
                var pier=Binding(world,RailwayHandler.Pier,climb.Index is 0 or 2 ? 0 : 1);
                if (pier==null) return Reject("unavailablePierBinding");
                ownerId=Allocate();
                for (var i=0;i<4;i++)
                {
                    var column=Column(@base,climb,i); var s=world.Terrain.Space.ToStorage(column);
                    var ground=world.Terrain.Cell(s.H,s.V).Ground; var underground=@base.Z<ground;
                    var pattern=(underground ? 44 : 28)+climb.Index*2+i;
                    var railBinding=Binding(world,RailwayHandler.Slope,pattern);
                    if (railBinding==null) return Reject("unavailableSlopeBinding");
                    var p=column with { Z=checked(@base.Z+i/2) }; var transition=Pattern(pattern);
                    rails.Add(new(i,p,pattern,transition.ZDiff,transition.ZAngle));
                    members.Add(new(p,ownerId.Value,RailwayVoxelKind.Traffic,new(new(railBinding.Reference,pattern))));
                    var pair=column with { Z=checked(@base.Z+1-i/2) };
                    var kind=i<2 ? RailwayVoxelKind.SlopeFiller : underground ? RailwayVoxelKind.SlopeWall : RailwayVoxelKind.SlopeSupport;
                    var support=i<2 ? null : Binding(world,underground ? RailwayHandler.SlopeWall : RailwayHandler.SlopeSupport,climb.Index+i-2);
                    if (i>=2 && support==null) return Reject("unavailableSlopeBinding");
                    members.Add(new(pair,ownerId.Value,kind,Definition:support?.Reference,Sprite:i<2 ? 0 : climb.Index+i-2,
                        DrawGroundBelow:!(i<2 && ground>=pair.Z),DrawGroundAbove:!(i>=2 && ground>=pair.Z+1)));
                    // Source elects at BASE Z in each column, including upper rails.
                    foreach (var placement in RailwaySupports.Elect(world,column,pier,pier))
                    {
                        var id=Allocate(); elected.Add(placement);
                        entities.Add(id,SingleOwner(id,placement.Definition,placement.Position));
                        voxels.Add(placement.Position,new(placement.Position,id,RailwayVoxelKind.Pier,
                            Definition:placement.Definition,Sprite:placement.Sprite,SupportedOwnerId:ownerId));
                    }
                }
                entities.Add(ownerId.Value,new(ownerId.Value,0,world.Profile.SlopeOwner,@base,
                    new(members.Select(v=>new VoxelMember(new(v.Position.X-@base.X,v.Position.Y-@base.Y,v.Position.Z-@base.Z),true))),true,false,0));
                foreach (var member in members) voxels.Add(member.Position,member);
                slopes.Add(new(ownerId.Value,climb));
            }
            else
            {
                members.AddRange(world.Voxels.Where(v=>v.OwnerId==ownerId));
                // Both deletion entry points protect the COMPLETE ramp before changes.
                if (members.Any(v=>v.Traffic?.Car!=null)) return Reject("occupiedCar");
                if (members.Any(v=>v.Traffic?.Platform!=null)) return Reject("platformDependency");
                foreach (var member in members)
                {
                    voxels.Remove(member.Position);
                    if (member.Traffic?.Rail is {} rail)
                    {
                        var p=Pattern(rail.Pattern); rails.Add(new(p.Level,member.Position,rail.Pattern,p.ZDiff,p.ZAngle));
                        if (operation==SlopeOperation.Remove)
                        {
                            // Source tool clears RAIL, preserving road/pole components.
                            // Surviving traffic receives a new standalone owner; the
                            // eight-cell slope owner must never become a partial ramp.
                            var t=TrafficLifecycle.Refresh(world.Profile,member.Traffic with { Rail=null });
                            if(member.Traffic.Accessory is {} a && t.Accessory!=a)losses.Add(new(member.Position,a));
                            if (t.Road!=null || t.Accessory!=null)
                            {
                                var id=Allocate(); entities.Add(id,SingleOwner(id,world.Profile.TrafficOwner,member.Position));
                                voxels.Add(member.Position,new(member.Position,id,RailwayVoxelKind.Traffic,t));
                            }
                        }
                        else
                            foreach (var pier in RailwaySupports.Teardown(world,member.Position))
                            { removed.Add(pier); voxels.Remove(pier.Position); entities.Remove(pier.OwnerId); }
                    }
                }
                entities.Remove(ownerId!.Value); slopes.RemoveAll(s=>s.EntityId==ownerId);
                if (operation==SlopeOperation.Remove)
                    for (var i=0;i<4;i++)
                        foreach (var pier in RailwaySupports.Teardown(world,Column(@base,climb,i)))
                        { removed.Add(pier); voxels.Remove(pier.Position); entities.Remove(pier.OwnerId); }
            }
            if (voxels.Count>NormalRailwayConstruction.MaximumRailwayVoxels) return Reject("railwayCapacityLimit");
            var booking=operation switch {
                SlopeOperation.Build=>RailwayBooking.SlopeBuild(@base.Z,world.Terrain.WaterLevel),
                SlopeOperation.Remove=>RailwayBooking.SlopeRemoval(@base.Z,world.Terrain.WaterLevel),
                _=>ConstructionBooking.None };
            var candidate=world.Replace(world.Revision,world.Terrain,new(nextId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),voxels.Values,slopes);
            return new(world,operation,selected,@base,climb,ownerId,candidate:candidate,booking:booking,
                rails:rails.OrderBy(r=>r.Level),members:members,removed:removed,elected:elected,losses:losses);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }
    private static SpatialEntity SingleOwner(long id, DefinitionReference definition, Location p) =>
        new(id,0,definition,p,new([new(new(0,0,0),true)]),true,false,0);

    public static MixedConstructionState Apply(MixedConstructionState state, SlopeRailwayConstructionPlan plan, GameTime time)
    {
        if (!ReferenceEquals(state.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (!plan.Legal) throw new InvalidOperationException(plan.Reason);
        var actual=Preview(state.World,plan.Selected,plan.Climb,plan.Operation);
        if (!actual.Legal) throw new InvalidOperationException(actual.Reason);
        if (state.Accounts.Transactions.Length+actual.Booking.Charges.Length>NormalRailwayConstruction.MaximumAccountTransactions)
            throw new InvalidOperationException("accountCapacityLimit");
        var c=actual.Candidate!;
        return state.Replace(state.World.Revision,c.Terrain,c.Spatial.Capture(),c.Voxels,c.Slopes,actual.Booking,time,
            plan.Operation switch { SlopeOperation.Build=>"slopeBuild",SlopeOperation.Remove=>"slopeRemoval",_=>"slopeEntityRemoval" });
    }
}
