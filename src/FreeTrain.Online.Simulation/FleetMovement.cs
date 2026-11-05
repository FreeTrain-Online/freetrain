// ExAV 883cf7d: Train.clockHandler, CalcNextTrainCarState.onInside, move,
// reverse, RailRoad.zdiff and SimpleTrainControllerImpl. LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class FleetMovement
{
    // Called on the detached minute successor. Never mutates the caller's world,
    // queue or random bank. MixedSessionState installs the result and one timer.
    public static TrainCallbackResult Tick(MixedSessionState session,long trainId)
    {
        var w=session.World;var fleet=w.Fleet ?? throw new InvalidOperationException("Fleet is not initialized.");
        var before=fleet.Train(trainId);
        if (before.Head.State is not FleetCarState.Inside prior)
            throw new InvalidOperationException("unboundOutsideMovement"); // P5-14 supplies countdown/exchange/return together
        var rail=w.At(prior.Location)!.Traffic!.Rail!;
        if (w.Profile.Resolve(rail.Definition).Handler==RailwayHandler.Signal &&
            SignalRailwayConstruction.DelegatesStopTime(rail.Direction,prior.Heading) &&
            fleet.EffectiveController(trainId).Kind!=FleetControllerKind.Simple)
            throw new InvalidOperationException("unboundHarborController"); // simple ignores this non-Station harbor

        // Ordinary rail's stop result is zero, including junctions/ramps and
        // special straight rails. Source clears forwarding even for zero.
        var train=before with {Forwarding=false,StopCallCount=0};
        var next=NextInside(w,fleet,train);
        if (next==null)
        {
            train=FleetReversal.Reverse(w,train);
            next=NextInside(w,fleet,train); // one retry; TRAIN-D001 handles both-null without dereferencing it
        }
        var moved=next!=null && (next is not FleetCarState.Inside target || w.At(target.Location)!.Traffic!.Car==null);
        if (moved)
        {
            var cars=train.Cars.ToBuilder();FleetCarState state=next!;
            for (var n=0;n<cars.Count;n++)
            {
                var old=cars[n].State;cars[n]=cars[n] with {State=state};state=old;
            }
            train=train with {Cars=cars.ToImmutable(),MoveCount=checked(train.MoveCount+1)};
        }
        train=train with {State=moved?TrainOperationalState.Moving:TrainOperationalState.EmergencyStopping};
        // Reversal plus a shift still changes each stable car revision only once.
        train=FleetReversal.NormalizeCars(before,train);
        var changed=train with {Cars=before.Cars}!=before || !train.Cars.SequenceEqual(before.Cars);
        var candidate=session.Construction;
        if (changed)
        {
            train=train with {Revision=checked(before.Revision+1)};
            var world=FleetOccupancy.ReplaceTrain(w,train);
            var accounts=candidate.Accounts;
            if (moved && (before.MoveCount&3)==0)
            {
                // Keep the source Int32 intermediate width; admission/overflow
                // rejects the whole detached minute, including earlier callbacks.
                var expense=checked((train.Cars.Length*20+train.Passengers/20)*2000);
                accounts=accounts.Book(ConstructionBooking.Spend(expense),world.Revision,session.Clock.Time,"trainMove");
                if (accounts.Transactions.Length>NormalRailwayConstruction.MaximumAccountTransactions)
                    throw new ArgumentException("Account transaction capacity exceeded.");
            }
            candidate=new(world,accounts);
        }
        var native=fleet.Profile.Resolve(train.Definition).MinutesPerVoxel;
        var delay=next is FleetCarState.Outside ? native : Delay(w.SpeedLimits,prior.Location,native);
        return new(candidate,delay);
    }

    private static FleetCarState? NextInside(MixedWorld w,FleetGraph fleet,FleetTrain train)
    {
        if(train.Head.State is not FleetCarState.Inside)
            throw new InvalidOperationException("unboundOutsideMovement"); // Outside callbacks/return remain P5-14
        var state=(FleetCarState.Inside)train.Head.State;
        var rail=w.At(state.Location)!.Traffic!.Rail!;
        var go=FleetRailGuidance.GuideHead(w,fleet,train,rail,state.Heading);
        var location=state.Location.Move(go);
        // zdiff is a property of the CURRENT pattern and INCOMING heading.
        if (w.Profile.Resolve(rail.Definition).Handler==RailwayHandler.Slope)
            location=location with {Z=checked(location.Z+SlopeRailwayConstruction.ZDiff(rail.Pattern,state.Heading))};
        if (w.Terrain.IsTrainBorder(location)) return new FleetCarState.Outside(location,go,100);
        return w.At(location)?.Traffic?.Rail is {} destination && RailwayTopology.HasPort(destination.Pattern,go.Opposite)
            ? new FleetCarState.Inside(location,go) : null;
    }

    private static int Delay(SpeedLimits limits,Location prior,int native) => limits.Low.Contains(prior)?4:
        limits.Medium.Contains(prior)?native==4?native:3:
        limits.Fast.Contains(prior)?native is 4 or 3?native:2:native;
}
