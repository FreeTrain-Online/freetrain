// Stable ExAV 883cf7d Station.load/unloadPassengers and Train.load/unloadPassengers.
// Original authors, LGPL-2.1-or-later. TRAIN-D008: checked source Int32 products.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// These are ordered, detached notifications, never callbacks into live state.
// P7 consumes land-value/traffic effects; real yards collect these during movement.
public abstract record StationPassengerEffect
{
    public sealed record LandValue(long StationId, Location Location, float Amount) : StationPassengerEffect;
    public sealed record Transport(long StationId, int Passengers) : StationPassengerEffect;
    public sealed record TrainChanged(long TrainId) : StationPassengerEffect;
    public sealed record StationChanged(long StationId) : StationPassengerEffect;
}
public sealed record StationExchange(StationBuilding Station, FleetTrain Train, int? Revenue,
    ImmutableArray<StationPassengerEffect> Effects);
public sealed record StationExchangeCandidate(MixedConstructionState Candidate, ImmutableArray<StationPassengerEffect> Effects);

public static class StationPassengerExchange
{
    public static StationExchange Board(StationBuilding station, FleetTrain train, int population, int packedCapacity, int amenity)
    {
        if (population < 0 || packedCapacity < 0 || amenity < 0) throw new ArgumentException("Invalid boarding input.");
        station = station with { Trains = station.Trains with { Today = checked(station.Trains.Today + 1) } };
        // Source returns before invoking the loader, including retaining any old
        // passengers/origin and permitting an unplaced train in this zero case.
        if (population == 0) return new(station, train, null, [new StationPassengerEffect.StationChanged(station.Id)]);
        var head = train.Head.State as FleetCarState.Placed ?? throw new ArgumentException("Boarding requires a placed head.");
        var available = Math.Max(0, checked(population - station.Passengers.Gone));
        // Do not widen the initial integer product or replace Single intermediates.
        var count = Math.Min(packedCapacity, checked((int)(checked(available * amenity) * 0.01f * 0.3f)));
        station = station with {
            Exports = station.Exports with { Today = checked(station.Exports.Today + count) },
            Passengers = station.Passengers with { Gone = checked(station.Passengers.Gone + count),
                AccumulatedLoaded = checked(station.Passengers.AccumulatedLoaded + count) }
        };
        train = train with { Passengers = count, BoardingOrigin = head };
        return new(station, train, null, [new StationPassengerEffect.LandValue(station.Id, station.Location, count),
            new StationPassengerEffect.TrainChanged(train.Id), new StationPassengerEffect.StationChanged(station.Id)]);
    }

    // Shared with Outside so both paths retain exact fare width, current-head
    // distance and origin clearing. A missing origin omits the earn call entirely.
    public static (FleetTrain Train, int? Revenue) UnloadTrain(FleetTrain train, int fare)
    {
        if (fare < 0) throw new ArgumentException("Invalid fare.");
        int? revenue = null;
        if (train.BoardingOrigin is {} origin)
        {
            var head = train.Head.State as FleetCarState.Placed ?? throw new ArgumentException("Unloading requires a placed head.");
            revenue = checked(train.Passengers * fare * origin.Location.DistanceTo(head.Location) * 2);
        }
        return (train with { Passengers = 0, BoardingOrigin = null }, revenue);
    }

    public static StationExchange Unload(StationBuilding station, FleetTrain train, int fare)
    {
        var count = train.Passengers;
        var unloaded = UnloadTrain(train, fare);
        station = station with {
            Imports = station.Imports with { Today = checked(station.Imports.Today + count) },
            Trains = station.Trains with { Today = checked(station.Trains.Today + 1) },
            Passengers = station.Passengers with { AccumulatedUnloaded = checked(station.Passengers.AccumulatedUnloaded + count) }
        };
        // Source casts the weekly average to Single BEFORE Math.Min.
        var landQ = Math.Min((float)(station.Imports.LastWeekPerDay / 24), count);
        if (!float.IsFinite(landQ)) throw new OverflowException("Invalid station land-value effect.");
        var effects = ImmutableArray.CreateBuilder<StationPassengerEffect>();
        effects.Add(new StationPassengerEffect.TrainChanged(train.Id));
        effects.Add(new StationPassengerEffect.LandValue(station.Id, station.Location, landQ));
        if (count != 0) effects.Add(new StationPassengerEffect.Transport(station.Id, count));
        effects.Add(new StationPassengerEffect.StationChanged(station.Id));
        return new(station, unloaded.Train, unloaded.Revenue, effects.ToImmutable());
    }

    public static StationExchangeCandidate Board(MixedSessionState session, long stationId, long trainId)
    {
        var graph = session.World.Stations ?? throw new ArgumentException("Missing stations.");
        var train = session.Fleet!.Train(trainId);
        var stock = session.Fleet.Profile.Resolve(train.Definition);
        return Publish(session, Board(graph.Building(stationId), train, StationCatchment.Demand(graph, stationId, session.Clock.Time),
            stock.Create(train.Cars.Length).PackedCapacity, stock.Amenity));
    }

    public static StationExchangeCandidate Unload(MixedSessionState session, long stationId, long trainId)
    {
        var train = session.Fleet!.Train(trainId);
        return Publish(session, Unload(session.World.Stations!.Building(stationId), train, session.Fleet.Profile.Resolve(train.Definition).Fare));
    }

    private static StationExchangeCandidate Publish(MixedSessionState session, StationExchange exchange)
    {
        var world = session.World; var graph = world.Stations!; var fleet = world.Fleet!;
        var before = fleet.Train(exchange.Train.Id);
        var train = exchange.Train == before ? before : exchange.Train with { Revision = checked(before.Revision + 1) };
        var station = exchange.Station with { Revision = checked(graph.Building(exchange.Station.Id).Revision + 1) };
        var stations = new StationGraph(graph.Profile, graph.Buildings.Select(b => b.Id == station.Id ? station : b), graph.Platforms,
            graph.Passages, graph.NextStationName, graph.NextPlatformName, graph.Listeners, graph.ListenerMemberships);
        var trains = new FleetGraph(fleet.Profile, fleet.RootGroupId, fleet.Trains.Select(t => t.Id == train.Id ? train : t),
            fleet.Groups, fleet.Controllers, fleet.ColorTest);
        var spatial = world.Spatial.Capture() with { Entities = world.Spatial.Entities.Select(e => e.Id == station.Id ? e with { Revision = station.Revision } : e).ToImmutableArray() };
        var candidate = world.Replace(world.Revision, world.Terrain, spatial, world.Voxels, world.Slopes, fleet: trains, stations: stations);
        var accounts = exchange.Revenue is {} revenue ? session.Construction.Accounts.Earn(revenue, candidate.Revision, session.Clock.Time, "stationUnload") : session.Construction.Accounts;
        if (accounts.Transactions.Length > NormalRailwayConstruction.MaximumAccountTransactions) throw new ArgumentException("Account transaction capacity exceeded.");
        return new(new(candidate, accounts), exchange.Effects);
    }
}
