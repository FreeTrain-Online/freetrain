// ExAV 883cf7d: Train.clockHandler, CalcNextTrainCarState.onInside, move,
// reverse, RailRoad.zdiff and SimpleTrainControllerImpl. LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class FleetMovement
{
    // Called on the detached minute successor. Never mutates the caller's world,
    // queue or random bank. MixedSessionState installs the result and one timer.
    public static TrainCallbackResult Tick(MixedSessionState session,long trainId)
        => Tick(session,trainId,(world,train)=>FleetControllers.StopTime(world,train,time:session.Clock.Time));

    public static TrainCallbackResult Tick(MixedSessionState session,long trainId,FleetStopHandler stopHandler)
    {
        var w=session.World;var fleet=w.Fleet ?? throw new InvalidOperationException("Fleet is not initialized.");
        var before=fleet.Train(trainId);
        if (before.Head.State is FleetCarState.Outside)
            return CompleteMove(session,before,Next(w,fleet,before,session.Clock.Time));
        // Capture before any handler mutation/reversal. CompleteMove reads this
        // original train from the unchanged session, never the new head location.
        var stop=stopHandler(w,before);
        var train=stop.Train;
        if (train.Id!=before.Id) throw new ArgumentException("Stop handler changed train identity.");
        if (stop.Decision.Action==FleetStopAction.Wait)
        {
            train=train with {StopCallCount=checked(train.StopCallCount+1),State=TrainOperationalState.StoppingAtStation};
            return Result(Publish(session,before,train,false,stop:stop),stop.Decision.WaitMinutes,stop);
        }
        train=train with {Forwarding=false};
        if (stop.Decision.Action==FleetStopAction.Forwarding) train=train with {Forwarding=true};
        else if (stop.Decision.Action is FleetStopAction.Reverse or FleetStopAction.ReverseForwarding)
        {
            train=FleetReversal.Reverse(w,train,session.Clock.Time);
            if (stop.Decision.Action==FleetStopAction.ReverseForwarding) train=train with {Forwarding=true};
        }
        train=train with {StopCallCount=0};
        var next=Next(w,fleet,train,session.Clock.Time);
        if (next==null)
        {
            train=FleetReversal.Reverse(w,train,session.Clock.Time);
            next=Next(w,fleet,train,session.Clock.Time); // one retry; TRAIN-D001 handles both-null without dereferencing it
        }
        return CompleteMove(session,train,next,stop);
    }

    // Shared continuation after route/stop effects. Exchange uses the current
    // head BEFORE the shift, and operating expense uses post-exchange passengers.
    public static TrainCallbackResult CompleteMove(MixedSessionState session,FleetTrain train,FleetCarState? next,FleetStopResult? stop=null)
    {
        var w=session.World;var fleet=w.Fleet!;var before=fleet.Train(train.Id);
        train=train with {StopCallCount=0};
        // A handler may have re-placed cars. Check its current slots, plus other
        // trains in the source world, rather than the old slots it vacated.
        var moved=next!=null && (next is not FleetCarState.Inside target ||
            !train.Cars.Any(c=>c.State is FleetCarState.Inside i && i.Location==target.Location) &&
            (w.At(target.Location)!.Traffic!.Car is not {} occupant || occupant.TrainId==train.Id));
        int? revenue=null;
        if (moved)
        {
            if(next is FleetCarState.Outside {Countdown:50})
            {
                var stock=fleet.Profile.Resolve(train.Definition);
                var unloaded=StationPassengerExchange.UnloadTrain(train,stock.Fare);
                train=unloaded.Train;revenue=unloaded.Revenue;
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
        return Result(Publish(session,before,train,moved,revenue,stop),delay,stop);
    }

    private static TrainCallbackResult Result(MixedConstructionState candidate,long delay,FleetStopResult? stop) =>
        new(candidate,delay) {PassengerEffects=stop?.Exchange?.Effects ?? [],DepartureBells=stop?.Bell is {} bell ? [bell] : []};

    private static MixedConstructionState Publish(MixedSessionState session,FleetTrain before,FleetTrain train,bool moved,int? revenue=null,FleetStopResult? stop=null)
    {
        // Reversal plus a shift still changes each stable car revision only once.
        train=FleetReversal.NormalizeCars(before,train);
        var changed=train with {Cars=before.Cars}!=before || !train.Cars.SequenceEqual(before.Cars);
        var candidate=session.Construction;
        if (changed || stop?.Exchange!=null)
        {
            train=train with {Revision=changed?checked(before.Revision+1):before.Revision};
            var world=FleetOccupancy.ReplaceTrain(session.World,train,stop?.Exchange?.Station);
            var accounts=candidate.Accounts;
            if(stop?.Exchange?.Revenue is {} stationRevenue)
                accounts=accounts.Earn(stationRevenue,world.Revision,session.Clock.Time,"stationUnload");
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

    private static FleetCarState? Next(MixedWorld w,FleetGraph fleet,FleetTrain train,GameTime time)
    {
        if(train.Head.State is FleetCarState.Outside outside)
            return outside.Countdown!=0 ? outside with {Countdown=outside.Countdown-1} :
                (FleetCarState?)FleetReversal.ReturnReference(w,outside) ?? outside with {Countdown=10};
        var state=(FleetCarState.Inside)train.Head.State;
        var rail=w.At(state.Location)!.Traffic!.Rail!;
        var go=FleetRailGuidance.GuideHead(w,fleet,train,state.Location,rail,state.Heading,time);
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
