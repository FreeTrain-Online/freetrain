// ExAV 883cf7d: Train.clockHandler, CalcNextTrainCarState.onInside, move,
// reverse, RailRoad.zdiff and SimpleTrainControllerImpl. LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Trusted rail/harbor handlers can mutate the detached train before returning a
// source stop duration/sentinel. Controller selection remains with its own slice.
public sealed record FleetStopResult(FleetTrain Train, int Minutes);
public delegate FleetStopResult FleetStopHandler(MixedWorld world, FleetTrain train);

public static class FleetMovement
{
    // Called on the detached minute successor. Never mutates the caller's world,
    // queue or random bank. MixedSessionState installs the result and one timer.
    public static TrainCallbackResult Tick(MixedSessionState session,long trainId)
        => Tick(session,trainId,StopTime);

    public static TrainCallbackResult Tick(MixedSessionState session,long trainId,FleetStopHandler stopHandler)
    {
        var w=session.World;var fleet=w.Fleet ?? throw new InvalidOperationException("Fleet is not initialized.");
        var before=fleet.Train(trainId);
        if (before.Head.State is FleetCarState.Outside)
            return CompleteMove(session,before,Next(w,fleet,before));
        // Capture before any handler mutation/reversal. CompleteMove reads this
        // original train from the unchanged session, never the new head location.
        var stop=stopHandler(w,before);
        var train=stop.Train;
        if (train.Id!=before.Id) throw new ArgumentException("Stop handler changed train identity.");
        if (stop.Minutes>0)
        {
            train=train with {StopCallCount=checked(train.StopCallCount+1),State=TrainOperationalState.StoppingAtStation};
            return new(Publish(session,before,train,false),stop.Minutes);
        }
        train=train with {Forwarding=false};
        if (stop.Minutes==-999) train=train with {Forwarding=true};
        else if (stop.Minutes<0)
        {
            train=FleetReversal.Reverse(w,train);
            if (stop.Minutes==-1000) train=train with {Forwarding=true};
        }
        train=train with {StopCallCount=0};
        var next=Next(w,fleet,train);
        if (next==null)
        {
            train=FleetReversal.Reverse(w,train);
            next=Next(w,fleet,train); // one retry; TRAIN-D001 handles both-null without dereferencing it
        }
        return CompleteMove(session,train,next);
    }

    private static FleetStopResult StopTime(MixedWorld w,FleetTrain train)
    {
        var prior=(FleetCarState.Inside)train.Head.State;
        var rail=w.At(prior.Location)!.Traffic!.Rail!;
        if (w.Profile.Resolve(rail.Definition).Handler==RailwayHandler.Signal &&
            SignalRailwayConstruction.DelegatesStopTime(rail.Direction,prior.Heading) &&
            w.Fleet!.EffectiveController(train.Id).Kind!=FleetControllerKind.Simple)
            throw new InvalidOperationException("unboundHarborController");
        // Ordinary rail returns zero; simple ignores every non-Station harbor.
        return new(train,0);
    }

    // Shared continuation after route/stop effects. Exchange uses the current
    // head BEFORE the shift, and operating expense uses post-exchange passengers.
    public static TrainCallbackResult CompleteMove(MixedSessionState session,FleetTrain train,FleetCarState? next)
    {
        var w=session.World;var fleet=w.Fleet!;var before=fleet.Train(train.Id);
        train=train with {StopCallCount=0};
        var moved=next!=null && (next is not FleetCarState.Inside target || w.At(target.Location)!.Traffic!.Car==null);
        int? revenue=null;
        if (moved)
        {
            if(next is FleetCarState.Outside {Countdown:50})
            {
                var stock=fleet.Profile.Resolve(train.Definition);
                if(train.BoardingOrigin is {} origin)
                {
                    var distance=origin.Location.DistanceTo(((FleetCarState.Placed)train.Head.State).Location);
                    revenue=checked(train.Passengers*stock.Fare*distance*2);
                }
                train=train with {Passengers=train.Forwarding?0:Math.Min(100,stock.Create(train.Cars.Length).NormalCapacity),
                    BoardingOrigin=train.Forwarding?null:(FleetCarState.Placed)train.Head.State};
            }
            var cars=train.Cars.ToBuilder();FleetCarState state=next!;
            for (var n=0;n<cars.Count;n++)
            {
                var old=cars[n].State;cars[n]=cars[n] with {State=state};state=old;
            }
            train=train with {Cars=cars.ToImmutable(),MoveCount=checked(train.MoveCount+1)};
        }
        train=train with {State=moved?TrainOperationalState.Moving:TrainOperationalState.EmergencyStopping};
        var native=fleet.Profile.Resolve(train.Definition).MinutesPerVoxel;
        var delay=next is FleetCarState.Outside || before.Head.State is not FleetCarState.Inside prior
            ? native : LimitedDelay(w.SpeedLimits,prior.Location,native);
        return new(Publish(session,before,train,moved,revenue),delay);
    }

    private static MixedConstructionState Publish(MixedSessionState session,FleetTrain before,FleetTrain train,bool moved,int? revenue=null)
    {
        // Reversal plus a shift still changes each stable car revision only once.
        train=FleetReversal.NormalizeCars(before,train);
        var changed=train with {Cars=before.Cars}!=before || !train.Cars.SequenceEqual(before.Cars);
        var candidate=session.Construction;
        if (changed)
        {
            train=train with {Revision=checked(before.Revision+1)};
            var world=FleetOccupancy.ReplaceTrain(session.World,train);
            var accounts=candidate.Accounts;
            var booking=ConstructionBooking.None;
            if (moved && (before.MoveCount&3)==0)
            {
                // Preserve source Int32 arithmetic and post-handler passengers.
                // An overflow rejects the whole detached minute.
                var expense=checked((train.Cars.Length*20+train.Passengers/20)*2000);
                booking=ConstructionBooking.Spend(expense);
            }
            if(revenue is {} receipt)
                accounts=accounts.EarnAndBook(receipt,booking,world.Revision,session.Clock.Time,"trainMove");
            else if(!booking.Charges.IsEmpty)
                accounts=accounts.Book(booking,world.Revision,session.Clock.Time,"trainMove");
            if (accounts.Transactions.Length>NormalRailwayConstruction.MaximumAccountTransactions)
                throw new ArgumentException("Account transaction capacity exceeded.");
            candidate=new(world,accounts);
        }
        return candidate;
    }

    private static FleetCarState? Next(MixedWorld w,FleetGraph fleet,FleetTrain train)
    {
        if(train.Head.State is FleetCarState.Outside outside)
            return outside.Countdown!=0 ? outside with {Countdown=outside.Countdown-1} :
                (FleetCarState?)FleetReversal.ReturnReference(w,outside) ?? outside with {Countdown=10};
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

    // Pure source arithmetic, including diagnostic nonpositive native values.
    // StockTrain admission separately requires a positive Int32 native speed.
    public static int LimitedDelay(SpeedLimits limits,Location prior,int native) => limits.Low.Contains(prior)?4:
        limits.Medium.Contains(prior)?native==4?native:3:
        limits.Fast.Contains(prior)?native is 4 or 3?native:2:native;
}
