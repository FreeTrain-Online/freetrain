// Stable ExAV 883cf7d YardRailRoad.getStopTimeSpan. LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

// Capture the post-handler head before the central reversal/movement.
public sealed record DepartureBellIntent(long TrainId, long PlatformId, DefinitionReference Bell, Location Location);

internal static class StationYardStop
{
    internal static FleetStopResult Dispatch(MixedWorld world, FleetTrain train, StationPlatform platform,
        PlatformYard yard, FleetHarborHandler? handler, GameTime? time)
    {
        var head=(FleetCarState.Inside)train.Head.State;
        var position=StationStopGeometry.Index(platform.Length,train.Cars.Length,platform.Direction,head.Heading);
        if(position==null ||
            yard.Location!=head.Location || position<0 || position>=platform.Length || position!=yard.Index ||
            world.Stations!.HostStation(platform.Id) is not {} station)
            return new(train,FleetStopDecision.Proceed);

        // Capture incoming count before controller-side reversal/re-placement.
        var count=train.StopCallCount;
        var result=FleetControllers.AtHarbor(world,train,
            new FleetHarborReference.Station(station.Location,station.Id,station.Revision),handler,time);
        if(result.Train.Id!=train.Id) throw new ArgumentException("Stop handler changed train identity.");
        var stock=world.Fleet!.Profile.Resolve(result.Train.Definition);
        StationExchange? exchange=null;
        if(count==0 && result.Decision.Action==FleetStopAction.Wait)
            exchange=StationPassengerExchange.Unload(station,result.Train,stock.Fare);
        else if(count!=0 && result.Decision.Action!=FleetStopAction.Wait &&
            result.Decision.Action is not (FleetStopAction.Forwarding or FleetStopAction.ReverseForwarding))
            exchange=StationPassengerExchange.Board(station,result.Train,
                StationCatchment.Demand(world.Stations,station.Id,time ?? throw new ArgumentException("Boarding requires game time.")),
                stock.Create(result.Train.Cars.Length).PackedCapacity,stock.Amenity);
        return result with {Train=exchange?.Train ?? result.Train,Exchange=exchange,
            Bell=count!=0 && result.Decision.Action!=FleetStopAction.Wait && result.Train.Head.State is FleetCarState.Inside current
                ? new(train.Id,platform.Id,platform.Bell,current.Location) : null};
    }
}
