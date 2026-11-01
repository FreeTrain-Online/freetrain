// Stable ExAV 883cf7d RailStationaryStructure, FixedSizeStructController and
// ConstructionSite. Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class StationaryConstruction
{
    public static string Handler(RailwayVoxel v) => FormattableString.Invariant($"construction/{v.OwnerId}/{v.Position.X}/{v.Position.Y}/{v.Position.Z}");
    public static IEnumerable<RailwayVoxel> Pending(MixedWorld w) => w.Voxels.Where(v => v.Construction is {State: < 4});
    public static IReadOnlyDictionary<string, Action> Bindings(MixedWorld world, Action<RailwayVoxel>? tick = null) =>
        Pending(world).ToDictionary(Handler, v => (Action)(() => tick?.Invoke(v)));
    public static IEnumerable<Location> Positions(Location p, StationaryType type)
    {
        // ConstructionSite creates voxels in Z, X, Y order. RNG and FIFO depend on it.
        for (var z = 0; z < type.Height; z++)
            for (var x = 0; x < type.SizeX; x++)
                for (var y = 0; y < type.SizeY; y++) yield return new(checked(p.X+x),checked(p.Y+y),checked(p.Z+z));
    }
    public static void ValidateOwner(MixedWorld world, SpatialEntity e, ImmutableArray<RailwayVoxel> members)
    {
        var type = world.Profile.Resolve(e.Definition).Stationary ?? throw new ArgumentException("Missing stationary type.");
        var positions = Positions(e.Anchor,type).ToHashSet();
        var site = members[0].Construction != null;
        if (members.Length != positions.Count || members.Any(v => v.Kind != RailwayVoxelKind.Stationary ||
            v.Definition != e.Definition || !positions.Contains(v.Position) || (v.Construction != null) != site ||
            v.Construction is {NeedsDoor:true} && v.Position.X==e.Anchor.X && v.Position.Y==e.Anchor.Y+type.SizeY-1) ||
            e.Footprint.Members.Any(m => m.Transparent != (!site && type.SizeX == 1 && type.SizeY == 1)) ||
            site && members.All(v => v.Construction!.State == 4)) throw new ArgumentException("Malformed stationary owner.");
        // No ground check on reopen: later terrain legality is enforced by its tool.
    }
    public static RailwayConstructionPlan Preview(MixedWorld world, Location p, string typeId, bool removal = false)
    {
        var cells = ImmutableArray.CreateBuilder<NormalRailwayCell>();
        NormalRailwayPlan Topology(string? reason = null) => new(world,p,p,removal,reason,cells.ToImmutable());
        RailwayConstructionPlan Reject(string reason) => new(Topology(reason),reason);
        if (!world.Terrain.Space.ContainsVoxel(p)) return Reject("outside");
        var target = world.At(p);
        var binding = removal && target?.Kind == RailwayVoxelKind.Stationary ? world.Profile.Resolve(target.Definition!) :
            world.Profile.Bindings.SingleOrDefault(b => b.Handler == RailwayHandler.Stationary && b.Reference.ContributionId == typeId);
        if (removal && target?.Kind != RailwayVoxelKind.Stationary) return Reject("stationaryNotFound");
        if (binding?.Stationary is not {} type) return Reject("unavailableRailBinding");
        try
        {
            var entities = world.Spatial.Entities.ToDictionary(e => e.Id);
            var voxels = world.Voxels.ToDictionary(v => v.Position);
            var reclaimed = new Dictionary<long,RailwayReclamation>();
            var nextId = world.Spatial.NextEntityId;
            var terrain = world.Terrain;
            ConstructionBooking booking;
            if (removal)
            {
                var owner = entities[target!.OwnerId];
                foreach (var v in world.Voxels.Where(v => v.OwnerId == owner.Id))
                { voxels.Remove(v.Position); cells.Add(new(v.Position,new(0),new(4),null,null,false)); }
                entities.Remove(owner.Id);
                booking = target.Construction != null ? ConstructionBooking.None : RailwayBooking.StationaryRemoval(type.SizeX,type.SizeY);
            }
            else
            {
                var positions = Positions(p,type).ToArray();
                if (positions.Any(q => !terrain.Space.ContainsVoxel(q))) return Reject("outside");
                foreach (var q in positions)
                {
                    var column = terrain.Space.ToStorage(q);
                    if (terrain.Cell(column.H,column.V).Ground != p.Z) return Reject("stationaryGround");
                    if (world.Spatial.At(q).Entity is {} entity)
                    {
                        if (!entity.SilentlyReclaimable) return Reject("obstacle");
                        var occupant = terrain.OccupantAt(q);
                        if (occupant == null) return Reject("unavailableReclamationHandler");
                        reclaimed.TryAdd(entity.Id,new(entity,occupant));
                    }
                }
                foreach (var id in reclaimed.Keys) entities.Remove(id);
                if (reclaimed.Count != 0)
                    terrain = terrain.Replace(terrain.Revision,terrain.Cells,terrain.WaterLevel,
                        new(terrain.Spatial.NextEntityId,terrain.Spatial.Entities.Where(e => !reclaimed.ContainsKey(e.Id)).ToImmutableArray()),
                        terrain.Occupants.Where(o => !reclaimed.ContainsKey(o.EntityId)));
                var idNew = nextId; nextId = checked(nextId+1);
                entities.Add(idNew,new(idNew,0,binding.Reference,p,Footprint.Box(new(new(0,0,0),type.SizeX,type.SizeY,type.Height)),false,false,0));
                foreach (var q in positions)
                {
                    voxels.Add(q,new(q,idNew,RailwayVoxelKind.Stationary,Definition:binding.Reference,Construction:new()));
                    cells.Add(new(q,new(0),new(4),null,null,false));
                }
                booking = ConstructionBooking.None;
            }
            if (voxels.Count > NormalRailwayConstruction.MaximumRailwayVoxels) return Reject("railwayCapacityLimit");
            var candidate = world.Replace(world.Revision,terrain,new(nextId,entities.Values.OrderBy(e => e.Id).ToImmutableArray()),voxels.Values,world.Slopes);
            return new(Topology(),candidate,booking,reclaimed.Values,[],[],[],RailwayHandler.Stationary);
        }
        catch (OverflowException) { return Reject("constructionOverflow"); }
    }

    public static MixedSessionState Commit(MixedSessionState before, RailwayConstructionPlan plan)
    {
        if (!ReferenceEquals(before.World,plan.Source)) throw new InvalidOperationException("staleRevision");
        if (!plan.Legal || plan.PreparedHandler != RailwayHandler.Stationary || plan.Candidate == null) throw new InvalidOperationException("invalidStationaryPlan");
        var candidate = plan.Candidate;
        var random = RandomStreams.Restore(before.Random.Capture());
        var clock = before.Clock.Capture();
        var pending = clock.Pending.ToBuilder(); var order = clock.NextOrder;
        var voxels = candidate.Voxels.ToDictionary(v => v.Position);
        var oldIds = before.World.Spatial.Entities.Select(e => e.Id).ToHashSet();
        foreach (var owner in candidate.Spatial.Entities.Where(e => !oldIds.Contains(e.Id) && candidate.At(e.Anchor)?.Construction != null))
        {
            var type = candidate.Profile.Resolve(owner.Definition).Stationary!;
            foreach (var p in Positions(owner.Anchor,type))
            {
                var cell = voxels[p]; var rng = random["construction"];
                // The source suppresses the south-west corner door without drawing RNG.
                var door = p.X != owner.Anchor.X || p.Y != owner.Anchor.Y+type.SizeY-1;
                cell = cell with { Construction = new(NeedsDoor:door && rng.Next(4)==0) }; voxels[p] = cell;
                var due = checked(clock.AbsoluteMinute+40+rng.Next(80));
                pending.Add(new(order,order,due,Handler(cell),0)); order = checked(order+1);
            }
        }
        candidate = before.World.Replace(before.World.Revision,candidate.Terrain,candidate.Spatial.Capture(),voxels.Values,candidate.Slopes);
        var accounts = before.Construction.Accounts.Book(plan.Booking,candidate.Revision,before.Clock.Time,plan.Topology.Removal?"stationaryRemoval":"stationarySite");
        return before.Successor(before.Revision,new(candidate,accounts),before.ConstructionMode,random.Capture(),clock with { NextOrder=order,Pending=pending.ToImmutable() });
    }

    internal static bool CanProceed(MixedWorld world, RailwayVoxel v, GameTime time)
    {
        if (time.IsWeekend || time.HourOfDay < 9 || time.HourOfDay > 17) return false;
        var state = v.Construction!.State;
        var below = world.At(v.Position with {Z=v.Position.Z-1});
        var above = world.At(v.Position with {Z=v.Position.Z+1});
        var members = world.Voxels.Where(c => c.OwnerId == v.OwnerId).ToArray();
        int Floor(int z) => members.Where(c => c.Position.Z==z).Min(c => c.Construction!.State);
        if (below?.Construction != null && (below.Construction.State < 2 || Floor(below.Position.Z) <= state || state==1 && Floor(below.Position.Z)<3)) return false;
        if (above?.Construction != null && (state==2 && Floor(above.Position.Z)<1 || state==3 && Floor(above.Position.Z)<3)) return false;
        return true;
    }
    internal static MixedConstructionState Tick(MixedSessionState session, RailwayVoxel registered)
    {
        var w = session.World;
        var v = w.At(registered.Position) ?? throw new InvalidOperationException("Missing construction callback.");
        var c = v.Construction ?? throw new InvalidOperationException("Missing construction state.");
        var rng = session.Random["construction"];
        if (!CanProceed(w,v,session.Clock.Time))
        { session.Clock.Schedule(Handler(v),40+rng.Next(80)); return session.Construction; }
        if (c.Ground==0 && c.State==0 && rng.Next(2)==0)
        {
            var ground = rng.Next(4) switch {0=>3,1=>2,2=>1,_=>4+rng.Next(3)};
            c = c with { Ground=ground };
        }
        else c = c with {State=c.State+1};
        var members = w.Voxels.Where(m => m.OwnerId==v.OwnerId).Select(m => m.Position==v.Position ? m with {Construction=c} : m).ToArray();
        var finished = members.All(m => m.Construction!.State==4);
        var entities = w.Spatial.Entities.ToDictionary(e => e.Id); var owner = entities[v.OwnerId];
        var nextId = w.Spatial.NextEntityId;
        var booking = ConstructionBooking.None;
        if (finished)
        {
            entities.Remove(owner.Id); var id = nextId; nextId = checked(nextId+1);
            var type = w.Profile.Resolve(owner.Definition).Stationary!;
            owner = owner with {Id=id,Revision=0,Owned=true,Footprint=Footprint.Box(new(new(0,0,0),type.SizeX,type.SizeY,type.Height),type.SizeX==1&&type.SizeY==1)};
            members = members.Select(m => m with {OwnerId=id,Construction=null}).ToArray();
            booking = RailwayBooking.StationaryCompletion(type.Price);
        }
        else
        {
            owner = owner with {Revision=checked(owner.Revision+1)};
            if (c.State<4) session.Clock.Schedule(Handler(v),40+rng.Next(80));
        }
        entities[owner.Id] = owner;
        return session.Construction.Replace(w.Revision,w.Terrain,new(nextId,entities.Values.OrderBy(e=>e.Id).ToImmutableArray()),
            w.Voxels.Where(m => m.OwnerId!=v.OwnerId).Concat(members),w.Slopes,booking,session.Clock.Time,finished?"stationaryCompletion":"constructionProgress");
    }
}
