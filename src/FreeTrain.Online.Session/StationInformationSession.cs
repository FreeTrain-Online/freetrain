using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    public StationInformationRead StationInformation(StationInformationQuery query)
    {
        lock (gate)
        {
            if (query.ExpectedWorldEpoch < 0 || query.ExpectedRevision < 0 || query.ExpectedMinute < 0 ||
                query.StationId < 0 || query.PlatformId < 0 || query.TrainId < 0 || query.StationId == 0 && query.PlatformId == 0)
                throw new ArgumentException("invalidInput");
            if (query.ExpectedWorldEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
            var state = mixedState ?? throw new InvalidOperationException("operationUnavailable");
            if (query.ExpectedRevision > state.Revision || query.ExpectedMinute > state.Clock.Time.AbsoluteMinute ||
                !query.Latest && (query.ExpectedRevision != state.Revision || query.ExpectedMinute != state.Clock.Time.AbsoluteMinute))
                throw new InvalidOperationException("staleRevision");
            var graph = state.World.Stations ?? throw new InvalidOperationException("operationUnavailable");
            var platform = query.PlatformId == 0 ? null : graph.Platforms.SingleOrDefault(p => p.Id == query.PlatformId)
                ?? throw new InvalidOperationException("staleRevision");
            var station = query.StationId == 0 ? null : graph.Buildings.SingleOrDefault(b => b.Id == query.StationId)
                ?? throw new InvalidOperationException("staleRevision");
            if (platform != null && graph.HostStation(platform.Id)?.Id != station?.Id)
                throw new InvalidOperationException("staleRevision");
            var train = query.TrainId == 0 ? null : state.Fleet?.Trains.SingleOrDefault(t => t.Id == query.TrainId)
                ?? throw new InvalidOperationException("staleRevision");
            var demand = station == null ? (int?)null : StationCatchment.Demand(graph,station.Id,state.Clock.Time);
            int? index = null, yardIndex = null; long? controllerId = null; var wait = 0;
            string Diagnose()
            {
                if (station == null) return "disconnected";
                if (platform == null) return "noPlatform";
                if (train == null) return "noTrain";
                if (train.Head.State is not FleetCarState.Inside head) return "unplaced";
                index = StationStopGeometry.Index(platform.Length,train.Cars.Length,platform.Direction,head.Heading);
                if (index == null) return "direction";
                if (index < 0 || index >= platform.Length) return "length";
                var yard = platform.Yards.SingleOrDefault(y => y.Location == head.Location);
                if (yard == null) return "away";
                yardIndex = yard.Index;
                if (yard.Index != index) return "position";
                controllerId = state.Fleet!.EffectiveController(train.Id).Id;
                // Controllers and TURNING return detached values. Never dispatch an
                // exchange, timer, booking, bell or world mutation from this read.
                var decision = FleetControllers.AtHarbor(state.World,train,
                    new FleetHarborReference.Station(station.Location,station.Id,station.Revision),time:state.Clock.Time).Decision;
                wait = decision.WaitMinutes;
                return decision.Action switch {
                    FleetStopAction.Wait => "wait",
                    FleetStopAction.Forwarding or FleetStopAction.ReverseForwarding => "forwarding",
                    _ => train.StopCallCount == 0 ? "pass" : "ready"
                };
            }
            var reason = Diagnose();
            return new(query,demand,demand == null ? null : Math.Max(0,demand.Value-station!.Passengers.Gone),
                station == null ? null : StationStatistics.AverageLoaded(station.Passengers),
                station == null ? null : StationStatistics.AverageUnloaded(station.Passengers),index,yardIndex,controllerId,reason,wait,state.Revision,state.Clock.Time.AbsoluteMinute);
        }
    }
}
