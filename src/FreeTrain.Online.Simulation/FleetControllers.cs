// ExAV 883cf7d: TrainController, TrainHarbor, SimpleTrainControllerImpl,
// JunctionRailRoad.guide and Train.clockHandler. LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

public enum FleetJunctionRoute { Straight, Curve }
public enum FleetStopAction { Proceed, Wait, Reverse, Forwarding, ReverseForwarding }
// One train's transient intent at an authoritative callback boundary. Never saved.
public readonly record struct FleetManualInput(bool Pass, bool Depart, bool Reverse, bool Left, bool Right);

// A duration is positive; source negative values are actions, never timers.
// The default value is Proceed. Arbitrary source negatives mean Reverse except
// the two forwarding sentinels. SourceMinutes is the canonical action encoding.
public readonly record struct FleetStopDecision
{
    public FleetStopAction Action { get; }
    public int WaitMinutes { get; }
    private FleetStopDecision(FleetStopAction action, int minutes = 0)
    { Action=action; WaitMinutes=minutes; }
    public static FleetStopDecision Proceed => default;
    public static FleetStopDecision Reverse => new(FleetStopAction.Reverse);
    public static FleetStopDecision Forwarding => new(FleetStopAction.Forwarding);
    public static FleetStopDecision ReverseForwarding => new(FleetStopAction.ReverseForwarding);
    public static FleetStopDecision Wait(int minutes) => minutes > 0 ? new(FleetStopAction.Wait,minutes) :
        throw new ArgumentOutOfRangeException(nameof(minutes),"A controller wait must be positive.");
    public static FleetStopDecision FromSourceMinutes(int minutes) => minutes switch
    {
        > 0 => Wait(minutes), 0 => Proceed, -999 => Forwarding,
        -1000 => ReverseForwarding, _ => Reverse
    };
    public int SourceMinutes => Action switch
    {
        FleetStopAction.Wait => WaitMinutes, FleetStopAction.Reverse => -1,
        FleetStopAction.Forwarding => -999, FleetStopAction.ReverseForwarding => -1000, _ => 0
    };
}

// Handler-side changes are returned BEFORE the central stop action is applied.
// This preserves reversal/re-placement/yard effects without serializing code.
public sealed record FleetStopResult(FleetTrain Train, FleetStopDecision Decision)
{
    public FleetStopResult(FleetTrain train, int sourceMinutes)
        : this(train,FleetStopDecision.FromSourceMinutes(sourceMinutes)) { }
}
public delegate FleetStopResult FleetStopHandler(MixedWorld world, FleetTrain train);

// Ordinary rail is not a harbor. Signals supply themselves; future yards supply
// their resolved host Station, whose location is the controller's settings key.
// Station references define the P6 boundary, not platform/boarding gameplay.
public abstract record FleetHarborReference
{
    public Location Location { get; }
    private FleetHarborReference(Location location) { Location=location; }
    public sealed record Signal(Location Location, long OwnerId, long Revision) : FleetHarborReference(Location);
    public sealed record Station(Location Location, long StationId, long Revision) : FleetHarborReference(Location);
}

// Controller is the shared, immutable instance snapshot: Id is assignment
// identity and Revision changes with configuration. No per-signal override.
// RailLocation can differ from the Station's location/settings key.
public sealed record FleetHarborCall(FleetController Controller, FleetHarborReference Harbor,
    Location RailLocation, int CallCount);
public sealed record FleetJunctionCall(FleetController Controller, Location Location, int Pattern, Heading Incoming);
public delegate FleetStopResult FleetHarborHandler(MixedWorld world, FleetTrain train, FleetHarborCall call);

public static class FleetControllers
{
    public static FleetJunctionRoute OnJunction(FleetTrain train, FleetJunctionCall call, FleetManualInput input = default)
    {
        if(call.Controller.Kind is FleetControllerKind.Simple or FleetControllerKind.Timetable) return FleetJunctionRoute.Straight;
        if(call.Controller.Kind!=FleetControllerKind.Manual) throw new InvalidOperationException("unboundJunctionController");
        if(input.Left && RailwayTopology.HasPort(call.Pattern,call.Incoming.Left)) return FleetJunctionRoute.Curve;
        if(input.Right && RailwayTopology.HasPort(call.Pattern,call.Incoming.Right)) return FleetJunctionRoute.Curve;
        return FleetJunctionRoute.Straight;
    }

    public static FleetStopResult ManualStop(FleetTrain train, int callCount, FleetManualInput input = default)
        => new(train,callCount==0 ? input.Pass ? FleetStopDecision.Proceed : FleetStopDecision.Wait(5) :
            input.Depart ? FleetStopDecision.Proceed : input.Reverse ? FleetStopDecision.Reverse : FleetStopDecision.Wait(5));

    // P5-18's named TAT instances can run safely with their existing basic
    // location data. Junction settings/ordered rules remain P5-19/P5-20.
    public static FleetStopResult TimetableStop(FleetTrain train, FleetHarborCall call, GameTime time)
    {
        var setting=(call.Controller.Harbors.IsDefault?[]:call.Controller.Harbors).SingleOrDefault(h=>h.Location==call.Harbor.Location);
        if(setting?.Kind==FleetHarborKind.Pass)return new(train,FleetStopDecision.Proceed);
        if(call.CallCount!=0)return new(train,setting?.Reverse==true?FleetStopDecision.Reverse:FleetStopDecision.Proceed);
        var minutes=setting==null?10:setting.Kind==FleetHarborKind.Fixed?setting.Minutes:
            (setting.Minutes-(int)(time.AbsoluteMinute%1440)+1439)%1440+1;
        return new(train,FleetStopDecision.FromSourceMinutes(minutes));
    }

    public static FleetStopResult SimpleStop(MixedWorld world, FleetTrain train, FleetHarborCall call)
    {
        if(call.Controller.Kind!=FleetControllerKind.Simple)
            throw new InvalidOperationException("unboundHarborController");
        return new(train,call.Harbor is FleetHarborReference.Station && call.CallCount==0
            ? FleetStopDecision.Wait(30) : FleetStopDecision.Proceed);
    }

    // Trusted adapters may bind a test/future Station or controller provider.
    // The passing train resolves its shared instance on every call. Delegates
    // remain runtime bindings; only the existing graph/configuration is saved.
    public static FleetStopResult AtHarbor(MixedWorld world, FleetTrain train, FleetHarborReference harbor,
        FleetHarborHandler? handler = null, FleetManualInput input = default, GameTime? time = null)
    {
        if(train.Head.State is not FleetCarState.Inside head || train.StopCallCount<0 ||
            harbor is not (FleetHarborReference.Signal {OwnerId: > 0, Revision: >= 0} or
                FleetHarborReference.Station {StationId: > 0, Revision: >= 0}))
            throw new ArgumentException("Invalid harbor call.");
        var controller=world.Fleet!.EffectiveController(train.Id);
        var call=new FleetHarborCall(controller,harbor,head.Location,train.StopCallCount);
        return handler!=null ? handler(world,train,call) : controller.Kind==FleetControllerKind.Manual
            ? ManualStop(train,call.CallCount,input) : controller.Kind==FleetControllerKind.Timetable
                ? TimetableStop(train,call,time ?? throw new ArgumentException("A timetable call requires the game time.")) : SimpleStop(world,train,call);
    }

    public static FleetStopResult StopTime(MixedWorld world, FleetTrain train, FleetHarborHandler? handler = null, FleetManualInput input = default, GameTime? time = null)
    {
        var head=(FleetCarState.Inside)train.Head.State;
        var voxel=world.At(head.Location)!;var rail=voxel.Traffic!.Rail!;
        if(world.Profile.Resolve(rail.Definition).Handler==RailwayHandler.Signal &&
            SignalRailwayConstruction.DelegatesStopTime(rail.Direction,head.Heading))
        {
            var owner=world.Spatial.Entities.Single(e=>e.Id==voxel.OwnerId);
            return AtHarbor(world,train,new FleetHarborReference.Signal(head.Location,owner.Id,owner.Revision),handler,input,time);
        }
        return new(train,FleetStopDecision.Proceed);
    }
}
