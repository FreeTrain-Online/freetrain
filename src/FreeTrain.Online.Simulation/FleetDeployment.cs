// ExAV 883cf7d: Train.place/remove/reverse and the rail subtype guide methods.
// Original authors, LGPL-2.1-or-later. GEO-008 / TRAIN-D003: no deployment zdiff.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed class FleetDeploymentPlan
{
    internal MixedSessionState Source { get; }
    internal MixedConstructionState Before { get; }
    internal MixedConstructionState Candidate { get; }
    internal long ExpectedRevision { get; }
    internal ClockState BeforeClock { get; }
    internal ClockState AfterClock { get; }
    public FleetTrain Train { get; }
    public bool Withdrawal => Train.State == TrainOperationalState.Unplaced;

    internal FleetDeploymentPlan(MixedSessionState source, MixedConstructionState candidate, ClockState clock, long trainId)
    {
        Source=source; Before=source.Construction; Candidate=candidate; ExpectedRevision=source.Revision;
        BeforeClock=source.Clock.Capture(); AfterClock=clock; Train=candidate.World.Fleet!.Train(trainId);
        // Includes graph/occupancy, trusted dispatcher, queue and range admission.
        source.Successor(ExpectedRevision,candidate,source.ConstructionMode,clock:clock);
    }
}

public static class FleetDeployment
{
    // Reverse selects the source pose followed by source reversal IN PLACE. It
    // does not walk a new route from the clicked tail or substitute motion rules.
    public static FleetDeploymentPlan PreviewPlacement(MixedSessionState session, long trainId, Location tail, bool reverse = false)
    {
        var w=session.World; var f=w.Fleet ?? throw new ArgumentException("Fleet is not initialized.");
        var train=f.Train(trainId);
        if (train.State != TrainOperationalState.Unplaced) throw new InvalidOperationException("trainAlreadyPlaced");
        var cars=train.Cars.ToBuilder(); var visited=new HashSet<Location>(); var p=tail;
        Heading? heading=null;
        for (var n=cars.Count-1;n>=0;n--)
        {
            if (!w.Terrain.Space.ContainsVoxel(p)) throw new InvalidOperationException("outside");
            var traffic=w.At(p)?.Traffic;
            var rail=traffic?.Rail ?? throw new InvalidOperationException("missingRail");
            if (traffic!.Car != null) throw new InvalidOperationException("carOccupied");
            if (!visited.Add(p)) throw new InvalidOperationException("duplicateCell");
            heading ??= new Heading(RailwayTopology.Ports(rail.Pattern).Min()); // rr.dir1
            cars[n]=cars[n] with {Revision=checked(cars[n].Revision+1),State=new FleetCarState.Inside(p,heading.Value)};
            // Source temporarily installs cars[0], so every junction sees a HEAD,
            // including the iterations which will ultimately belong to followers.
            var outgoing=FleetRailGuidance.GuideHead(w,f,train,p,rail,heading.Value,time:session.Clock.Time);
            p=p.Move(outgoing); heading=outgoing; // deliberately no zdiff, even on the last cell
        }
        var after=train with {Revision=checked(train.Revision+1),Cars=cars.ToImmutable(),
            State=TrainOperationalState.Moving,StopCallCount=0};
        if(reverse)after=FleetReversal.Reverse(w,after,time:session.Clock.Time);
        var clock=session.Clock.Capture(); var order=clock.NextOrder;
        var due=checked(clock.AbsoluteMinute+f.Profile.Resolve(train.Definition).MinutesPerVoxel);
        clock=clock with {NextOrder=checked(order+1),Pending=clock.Pending.Add(new(order,order,due,MixedSessionState.TrainHandler(trainId),0))
            .OrderBy(a=>a.DueMinute).ThenBy(a=>a.Order).ToImmutableArray()};
        return Plan(session,after,clock);
    }

    public static FleetDeploymentPlan PreviewWithdrawal(MixedSessionState session, long trainId)
    {
        var train=(session.Fleet ?? throw new ArgumentException("Fleet is not initialized.")).Train(trainId);
        if (train.State==TrainOperationalState.Unplaced) throw new InvalidOperationException("trainNotPlaced");
        // Outside has no slot. Source removal retains original/current ordering,
        // passenger/boarding/forwarding/history and the owned train itself.
        var after=train with {Revision=checked(train.Revision+1),State=TrainOperationalState.Unplaced,
            Cars=train.Cars.Select(c=>c with {Revision=checked(c.Revision+1),State=new FleetCarState.Unplaced()}).ToImmutableArray()};
        return Plan(session,after,session.Clock.Capture()); // Successor cancels exactly this train's callback
    }

    public static FleetDeploymentPlan PreviewReversal(MixedSessionState session,long trainId)
    {
        var before=(session.Fleet ?? throw new ArgumentException("Fleet is not initialized.")).Train(trainId);
        if(before.State==TrainOperationalState.Unplaced)throw new InvalidOperationException("trainNotPlaced");
        var after=FleetReversal.NormalizeCars(before,FleetReversal.Reverse(session.World,before,time:session.Clock.Time))
            with {Revision=checked(before.Revision+1)};
        return Plan(session,after,session.Clock.Capture()); // retain the exact existing callback and history
    }

    private static FleetDeploymentPlan Plan(MixedSessionState session,FleetTrain train,ClockState clock)
        => new(session,new(FleetOccupancy.ReplaceTrain(session.World,train),session.Construction.Accounts),clock,train.Id);

    public static MixedSessionState Commit(MixedSessionState session,FleetDeploymentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var clock=session.Clock.Capture();
        if (!ReferenceEquals(session,plan.Source) || !ReferenceEquals(session.Construction,plan.Before) ||
            session.Revision!=plan.ExpectedRevision || clock.AbsoluteMinute!=plan.BeforeClock.AbsoluteMinute ||
            clock.NextOrder!=plan.BeforeClock.NextOrder || !clock.Pending.SequenceEqual(plan.BeforeClock.Pending))
            throw new InvalidOperationException("staleRevision");
        return session.Successor(plan.ExpectedRevision,plan.Candidate,session.ConstructionMode,clock:plan.AfterClock);
    }
}

// Shared complete-consist publication for placement, withdrawal and movement.
internal static class FleetOccupancy
{
    internal static MixedWorld ReplaceTrain(MixedWorld w,FleetTrain train)
    {
        var f=w.Fleet!;
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(t=>t.Id==train.Id?train:t),f.Groups,f.Controllers,f.ColorTest);
        var slots=train.Cars.Where(c=>c.State is FleetCarState.Inside).ToDictionary(c=>((FleetCarState.Inside)c.State).Location);
        var changed=new HashSet<long>();
        var voxels=w.Voxels.Select(v=>
        {
            var traffic=v.Traffic;if(traffic==null)return v;
            var car=slots.TryGetValue(v.Position,out var c)?new CarComponent(c.Definition,train.Id,c.Id):
                traffic.Car?.TrainId==train.Id?null:traffic.Car;
            if(car==traffic.Car)return v;
            changed.Add(v.OwnerId);return v with {Traffic=traffic with {Car=car}};
        }).ToImmutableArray();
        var spatial=new SpatialState(w.Spatial.NextEntityId,w.Spatial.Entities.Select(e=>changed.Contains(e.Id)
            ?e with {Revision=checked(e.Revision+1)}:e).ToImmutableArray());
        return w.Replace(w.Revision,w.Terrain,spatial,voxels,w.Slopes,w.SpeedLimits,graph);
    }
}

public static class FleetRailGuidance
{
    internal static Heading GuideHead(MixedWorld w,FleetGraph fleet,FleetTrain train,Location location,RailComponent rail,Heading heading, GameTime? time = null)
    {
        var handler=w.Profile.Resolve(rail.Definition).Handler;
        var route=FleetJunctionRoute.Straight;
        if (handler==RailwayHandler.Junction && RailwayTopology.HasPort(rail.Pattern,heading) &&
            (RailwayTopology.HasPort(rail.Pattern,heading.Left) || RailwayTopology.HasPort(rail.Pattern,heading.Right)))
            route=FleetControllers.OnJunction(train,new(fleet.EffectiveController(train.Id),
                location,rail.Pattern,heading),time);
        return Guide(handler,rail.Pattern,heading,route);
    }

    // A deployment heading can already name an outgoing curve port (rr.dir1).
    // Requiring its opposite would reject legal SOURCE initial/tail states.
    public static bool CanGuide(RailwayHandler handler,int pattern,Heading heading) =>
        RailwayTopology.HasPort(pattern,heading) || (handler is RailwayHandler.Normal or RailwayHandler.Junction) &&
        (RailwayTopology.HasPort(pattern,heading.Left) || RailwayTopology.HasPort(pattern,heading.Right));

    public static Heading Guide(RailwayHandler handler,int pattern,Heading heading,FleetJunctionRoute route=FleetJunctionRoute.Straight,
        Location? predecessor=null,Location? location=null)
    {
        if (!Enum.IsDefined(route)) throw new ArgumentException("Invalid junction route.");
        if (!CanGuide(handler,pattern,heading)) throw new InvalidOperationException("invalidPlacementHeading");
        bool Has(Heading d)=>RailwayTopology.HasPort(pattern,d);
        if (handler is not (RailwayHandler.Normal or RailwayHandler.Junction)) return heading;
        if (Has(heading))
        {
            if(handler!=RailwayHandler.Junction || !Has(heading.Left)&&!Has(heading.Right)) return heading;
            if(predecessor is {} previous ? previous==location!.Value.Move(heading) : route==FleetJunctionRoute.Straight) return heading;
        }
        return Has(heading.Left)?heading.Left:heading.Right;
    }
}
