// Stable ExAV 883cf7d/trunk/core/world/rail/Station.cs, clockHandler*Just,
// average*Passengers and TransportLog. Original authors, LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

public static class StationStatistics
{
    public const float PassengerRatio = 0.9996f;
    public const float PerDayFactor = 24.0f * (1.0f - PassengerRatio);

    // Keep source Single intermediates and conversion toward zero, including
    // conversion of large Int32 counters to Single before multiplication.
    public static StationPassengers Hour(StationPassengers passengers) => new(
        checked((int)(passengers.Gone * 0.8f)),
        checked((int)(passengers.AccumulatedLoaded * PassengerRatio)),
        checked((int)(passengers.AccumulatedUnloaded * PassengerRatio)));

    public static int AverageLoaded(StationPassengers passengers) => checked((int)(passengers.AccumulatedLoaded * PerDayFactor));
    public static int AverageUnloaded(StationPassengers passengers) => checked((int)(passengers.AccumulatedUnloaded * PerDayFactor));

    public static StationTransportLog Day(StationTransportLog log, GameTime time)
    {
        var week = log.ThisWeek + Math.Pow(log.Today, 1.0 / 5.0);
        var last = time.DayOfWeek == 6 ? Math.Pow(week / 7.0, 5.0) : log.LastWeekPerDay;
        if (!double.IsFinite(week) || !double.IsFinite(last)) throw new OverflowException("Station transport log overflow.");
        return new(0, log.Today, time.DayOfWeek == 6 ? 0 : week, last);
    }

    public static double ThisWeekPerDay(StationTransportLog log, GameTime time)
    {
        var value = Math.Pow(log.ThisWeek / (time.DayOfWeek + 1), 5.0);
        return double.IsFinite(value) ? value : throw new OverflowException("Station transport average overflow.");
    }

    public static MixedConstructionState Tick(MixedSessionState session, string handlerId) =>
        StationLifecycle.Tick(session, handlerId, graph => {
            // Platforms own only relative-24h upkeep. Station demand is queried
            // at current GameTime through StationCatchment, never cached/reset.
            if (!handlerId.StartsWith("station/", StringComparison.Ordinal)) return graph;
            var timer = StationLifecycle.Timers(graph).Single(t => t.HandlerId == handlerId);
            var building = graph.Buildings.Single(b => b.HourTimer == timer || b.DayTimer == timer);
            var changed = building.HourTimer == timer ? building with { Passengers = Hour(building.Passengers) } :
                building with { Imports = Day(building.Imports, session.Clock.Time),
                    Exports = Day(building.Exports, session.Clock.Time), Trains = Day(building.Trains, session.Clock.Time) };
            return new(graph.Profile, graph.Buildings.Select(b => b.Id == building.Id ? changed : b), graph.Platforms,
                graph.Passages, graph.NextStationName, graph.NextPlatformName, graph.Listeners, graph.ListenerMemberships);
        });
}
