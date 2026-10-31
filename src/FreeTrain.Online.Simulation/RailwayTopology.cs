// Behavioral extraction: ExAV 2.1.1.12, archive 883cf7d/trunk/core/world/rail/
// {RailPattern,RailRoad,SingleRailRoad,JunctionRailRoad,SlopeRailRoad,SpecialPurposeRailRoad}.cs.
// Original copyright Kohsuke Kawaguchi et al.; LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class RailwayTopology
{
    private static readonly ImmutableArray<ImmutableArray<int>> ports = CreatePorts();
    private static ImmutableArray<ImmutableArray<int>> CreatePorts()
    {
        var result = NormalRailPatterns.Ports.Select(p => ImmutableArray.Create(p.A,p.B)).ToList();
        result.AddRange(new int[][] { [1,5,4], [1,5,6], [2,6,5], [2,6,7],
            [3,7,6], [3,7,0], [4,0,7], [4,0,1], [5,1,0], [5,1,2],
            [6,2,1], [6,2,3], [7,3,2], [7,3,4], [0,4,3], [0,4,5] }
            .Select(p => p.ToImmutableArray()));
        for (var i = 28; i < 60; i++)
        {
            var d = (i-28)%16/4*2;
            result.Add([d,(d+4)%8]);
        }
        return result.ToImmutableArray();
    }

    public static ImmutableArray<int> Ports(int pattern) => pattern is >= 0 and < 60
        ? ports[pattern] : throw new ArgumentOutOfRangeException(nameof(pattern));
    public static bool HasPort(int pattern, Heading direction) => Ports(pattern).Contains(direction.Index);
    private static int Find(IEnumerable<int> directions)
    {
        var edges = directions.ToHashSet();
        for (var i = 0; i < 28; i++)
            if (edges.SetEquals(ports[i])) return i;
        throw new InvalidOperationException("invalidRailPattern");
    }

    private static void Validate(RailwayHandler handler, int pattern)
    {
        if (!(handler switch {
            RailwayHandler.Normal => pattern is >= 0 and < 12,
            RailwayHandler.Junction => pattern is >= 12 and < 28,
            RailwayHandler.Slope => pattern is >= 28 and < 60,
            RailwayHandler.Bridge or RailwayHandler.Girder or RailwayHandler.Tunnel or
                RailwayHandler.Garage or RailwayHandler.Signal => pattern is >= 0 and < 12,
            _ => false })) throw new ArgumentException("Invalid rail subtype/pattern.");
    }

    // Null means attachment rejected; an existing edge succeeds without replacing
    // its subtype. Open normal rails retain the first suitable edge in source order.
    public static int? Attach(RailwayHandler handler, int pattern, Heading direction, bool wellConnected)
    {
        Validate(handler,pattern);
        var edges = Ports(pattern);
        if (edges.Contains(direction.Index)) return pattern;
        if (handler != RailwayHandler.Normal) return null;
        if (wellConnected)
        {
            if (edges.Any(e => new Heading(e).AngleTo(direction) is not (1 or 3 or 4))) return null;
            return Find(edges.Add(direction.Index));
        }
        foreach (var e in edges.Order())
            if (new Heading(e).AngleTo(direction) >= 3) return Find([e,direction.Index]);
        return null;
    }

    // Null means cleared. The 16 source null-pattern junction hazards reject the
    // whole prospective route instead of producing corrupt rail or clearing it.
    public static int? Detach(RailwayHandler handler, int pattern, Heading first, Heading second)
    {
        Validate(handler,pattern);
        var edges = Ports(pattern);
        bool a = edges.Contains(first.Index), b = edges.Contains(second.Index);
        if (handler == RailwayHandler.Normal && (a || b) || handler == RailwayHandler.Junction && a && b) return null;
        if (handler == RailwayHandler.Junction && (a || b))
            return Find(edges.Where(e => e != first.Index && e != second.Index));
        return pattern; // Slope and all special-purpose subtypes cannot detach.
    }

    public static bool IsWellConnected(MixedWorld world, Location position)
    {
        var rail = world.At(position)?.Traffic?.Rail;
        if (rail == null) return false;
        var count = 0;
        foreach (var edge in Ports(rail.Pattern))
        {
            var d = new Heading(edge);
            var x = (long)position.X+d.Offset.X; var y = (long)position.Y+d.Offset.Y;
            if (x is < int.MinValue or > int.MaxValue || y is < int.MinValue or > int.MaxValue) continue;
            if (world.At(new((int)x,(int)y,position.Z))?.Traffic?.Rail is {} neighbor && HasPort(neighbor.Pattern,d.Opposite))
                if (++count >= 2) return true;
        }
        return false;
    }

    // A source sign-vector walk, inclusive at both ends. Widen differences before
    // admission so extreme input rejects before allocating or iterating a huge route.
    public static ImmutableArray<Location> Route(Location from, Location to)
    {
        if (from.Z != to.Z) throw new ArgumentException("noncoplanar");
        if (from == to) return [];
        long dx = (long)to.X-from.X, dy = (long)to.Y-from.Y;
        var length = Math.Max(Math.Abs(dx),Math.Abs(dy));
        if (length+1 > NormalRailway.MaximumRouteCells) throw new ArgumentException("routeWorkLimit");
        var result = ImmutableArray.CreateBuilder<Location>((int)length+1);
        for (var i = 0; i <= length; i++)
            result.Add(new((int)(from.X+Math.Clamp(dx,-i,i)),(int)(from.Y+Math.Clamp(dy,-i,i)),from.Z));
        return result.MoveToImmutable();
    }
}

public sealed record NormalRailwayCell(Location Position, Heading Incoming, Heading Outgoing,
    RailComponent? Before, RailComponent? After, bool ProtectedByCar)
{
    public bool Changed => Before != After;
}

// This is a topology draft, NOT terrain legality or permission to publish a world.
// P4-07 must validate occupancy/reclamation/water/supports and price the complete
// transaction. No allocator, terrain, account, clock or random state changes here.
public sealed class NormalRailwayPlan
{
    public MixedWorld Source { get; }
    public Location From { get; }
    public Location To { get; }
    public bool Removal { get; }
    public string? Reason { get; }
    public bool TopologyValid => Reason == null;
    public ImmutableArray<NormalRailwayCell> Cells { get; }
    public int ChargedRailCells => Removal ? Cells.Count(c => c.Before != null && !c.ProtectedByCar) : Cells.Length;
    // Source calls support election only for newly created rail; teardown runs
    // at EVERY visited column, including empty, wrong-edge and protected-car cells.
    public IEnumerable<Location> SupportColumns => Cells.Where(c => Removal || c.Before == null).Select(c => c.Position);
    internal NormalRailwayPlan(MixedWorld source, Location from, Location to, bool removal,
        string? reason, ImmutableArray<NormalRailwayCell> cells)
    { Source=source; From=from; To=to; Removal=removal; Reason=reason; Cells=cells; }
}

public static class NormalRailway
{
    // Initial bounded domain work; P4-23 owns measured legal-network capacity.
    public const int MaximumRouteCells = 8192;
    public static NormalRailwayPlan PlanBuild(MixedWorld world, Location from, Location to) => Plan(world,from,to,false);
    public static NormalRailwayPlan PlanRemoval(MixedWorld world, Location from, Location to) => Plan(world,from,to,true);
    private static NormalRailwayPlan Plan(MixedWorld world, Location from, Location to, bool removal)
    {
        ArgumentNullException.ThrowIfNull(world);
        NormalRailwayPlan Reject(string reason) => new(world,from,to,removal,reason,[]);
        ImmutableArray<Location> route;
        try { route = RailwayTopology.Route(from,to); }
        catch (ArgumentException e) { return Reject(e.Message); }
        if (route.IsEmpty) return Reject("samePoint");
        var cells = ImmutableArray.CreateBuilder<NormalRailwayCell>(route.Length);
        for (var n = 0; n < route.Length; n++)
        {
            var p = route[n];
            try { if (!world.Terrain.Space.ContainsVoxel(p)) return Reject("outside"); }
            catch (OverflowException) { return Reject("outside"); }
            var incoming = n == 0 ? p.DirectionTo(route[1]).Opposite : route[n-1].DirectionTo(p).Opposite;
            var outgoing = n == route.Length-1 ? incoming.Opposite : p.DirectionTo(route[n+1]);
            var traffic = world.At(p)?.Traffic;
            var before = traffic?.Rail; var after = before;
            var car = traffic?.Car != null;
            try
            {
                if (removal)
                {
                    if (before != null && !car)
                        after = Replace(world.Profile,before,RailwayTopology.Detach(world.Profile.Resolve(before.Definition).Handler,
                            before.Pattern,incoming,outgoing));
                }
                else
                {
                    if (car) return Reject("carOccupied");
                    if (before != null)
                    {
                        if (n != 0 && n != route.Length-1) return Reject("existingMiddleRail");
                        var pattern = RailwayTopology.Attach(world.Profile.Resolve(before.Definition).Handler,before.Pattern,
                            n == 0 ? outgoing : incoming,RailwayTopology.IsWellConnected(world,p));
                        if (pattern == null) return Reject("cannotAttach");
                        after = Replace(world.Profile,before,pattern);
                    }
                    else after = Create(world.Profile,NormalRailPatterns.Find(incoming.Index,outgoing.Index));
                }
            }
            catch (InvalidOperationException e) when (e.Message is "invalidRailPattern" or "unavailableRailBinding")
            { return Reject(e.Message); }
            cells.Add(new(p,incoming,outgoing,before,after,car));
        }
        return new(world,from,to,removal,null,cells.MoveToImmutable());
    }

    private static RailComponent? Replace(RailwayProfile profile, RailComponent before, int? pattern) =>
        pattern == null ? null : pattern == before.Pattern ? before : Create(profile,pattern.Value);
    private static RailComponent Create(RailwayProfile profile, int pattern)
    {
        var handler = pattern < 12 ? RailwayHandler.Normal : RailwayHandler.Junction;
        var matches = profile.Bindings.Where(b => b.Handler == handler && b.Variant == pattern).Take(2).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("unavailableRailBinding");
        return new(matches[0].Reference,pattern);
    }
}
