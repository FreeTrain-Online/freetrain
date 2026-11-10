using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class StationPassengerServiceTests
{
    // P7 has no constructible city producer yet. Only this explicitly controlled
    // provider/rail baseline is authored; stations, yards and stock use gameplay.
    static MixedSessionState Baseline(bool west, int supply)
    {
        var terrain = new TerrainWorld(TerrainProfiles.Bundled, "Controlled passenger supply (P7 city sources pending)",
            new(64, 64, 8), 0, Enumerable.Repeat(new TerrainCell(2), 64 * 64), new(1, []), []);
        var world = MixedWorld.FromTerrain(RailwayProfiles.Bundled, terrain).EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var s = new MixedSessionState(new(world), TerrainProvenance.From(new EmptyWorldRequest(
            WorldCreationProfile.Id, terrain.Name, new(64, 64, 8), 0)), clock: new(517, 0, []), revision: world.Revision);
        var rails = NormalRailwayConstruction.PreviewBuild(s.World, new(16, 15, 2), new(44, 15, 2));
        Assert.True(rails.Legal, rails.Reason);
        s = s.Successor(s.Revision, NormalRailwayConstruction.Apply(s.Construction, rails, s.Clock.Time), false);
        world = StationCatchment.Attach(s.World, s.World.At(new(west ? 43 : 20, 15, 2))!.OwnerId,
            new(new("ConstantPopulation", supply), 10000));
        return s.Successor(s.Revision, new(world, s.Construction.Accounts), false);
    }

    static void NextTrainCall(MixedSessionState s)
    {
        var due = s.Clock.Capture().Pending.Single(t => t.HandlerId.StartsWith("train/", StringComparison.Ordinal)).DueMinute;
        while (s.Clock.Time.AbsoluteMinute < due) s.AdvanceOneMinute();
    }
    static MixedSessionState Open(MixedSessionState s) => SaveCodec.ReadWorld(SaveCodec.Write(s), TerrainProfiles.Resolve, RailwayProfiles.Resolve).Mixed!;

    [Theory]
    [InlineData(false, 10000)][InlineData(true, 10000)]
    [InlineData(false, 0)][InlineData(true, 0)]
    public void Two_actual_stations_Simple_service_both_directions_controlled_and_zero_supply_continue_exactly(bool west, int supply)
    {
        var s = Baseline(west, supply);
        var definition = RailwayProfiles.Bundled.Stations!.Definitions.First(d => d.SizeX == 1 && d.SizeY == 1 && d.Height == 1);
        var stock = RollingStockCatalog.Bundled.Trains.First(t => t.PermittedLengths.Contains(3) && t.Create(3).PackedCapacity > t.Create(3).NormalCapacity);
        if (supply > 0 && Environment.GetEnvironmentVariable("FREETRAIN_SERVICE_OUTPUT") is {} output)
        {
            var path = output + (west ? "-west" : "-east");
            File.WriteAllBytes(path + ".json", SaveCodec.Write(s));
            File.WriteAllText(path + ".metadata.json", JsonSerializer.Serialize(new {
                stationRecipe = definition.Reference.RecipeId, stockId = stock.Binding.ContributionId,
                normal = stock.Create(3).NormalCapacity, packed = stock.Create(3).PackedCapacity, fare = stock.Fare
            }));
        }
        if (supply > 0)
        {
            var fixture = Path.Combine(AppContext.BaseDirectory, "PassengerService", west ? "passenger-service-west" : "passenger-service-east");
            Assert.Equal(File.ReadAllBytes(fixture + ".json"), SaveCodec.Write(s));
            using var metadata = JsonDocument.Parse(File.ReadAllBytes(fixture + ".metadata.json"));
            var m = metadata.RootElement;
            Assert.Equal(definition.Reference.RecipeId, m.GetProperty("stationRecipe").GetString());
            Assert.Equal(stock.Binding.ContributionId, m.GetProperty("stockId").GetString());
            Assert.Equal(stock.Create(3).NormalCapacity, m.GetProperty("normal").GetInt32());
            Assert.Equal(stock.Create(3).PackedCapacity, m.GetProperty("packed").GetInt32());
            Assert.Equal(stock.Fare, m.GetProperty("fare").GetInt32());
        }
        foreach (var x in new[] { 23, 36 }) s = StationBuildingConstructionTests.Build(s, definition, new(x, 13, 2));
        foreach (var x in new[] { 25, 37 }) s = ThinPlatformConstruction.Commit(s,
            ThinPlatformConstruction.Preview(s.World, new(x, 15, 2), Heading.East, 4, true));
        var source = s.World.Stations!.Buildings[west ? 1 : 0].Id;
        var destination = s.World.Stations.Buildings[west ? 0 : 1].Id;
        Assert.Equal(supply, StationCatchment.Demand(s.World.Stations, source, s.Clock.Time));
        Assert.Equal(0, StationCatchment.Demand(s.World.Stations, destination, s.Clock.Time));
        s = FleetTrading.Commit(s, FleetTrading.QuotePurchase(s, stock.Binding, 3, 1));
        s = FleetDeployment.Commit(s, FleetDeployment.PreviewPlacement(s, s.Fleet!.Trains[0].Id, new(west ? 37 : 25, 15, 2), west));
        Assert.Equal(0, s.Fleet!.Trains[0].Passengers);
        var p = s.World.Stations!.Platforms[west ? 1 : 0];
        s = StationHostConnections.Commit(s, StationHostConnections.Preview(s.World, p.Id, p.Revision, null, 0));
        NextTrainCall(s); // Disconnected real yard is pass-through, with no harbor.
        Assert.Equal(0, s.World.Stations!.Building(source).Trains.Today);
        Assert.Empty(s.DepartureBells);
        Assert.Equal(0, s.Fleet!.Trains[0].Passengers);
        s = FleetDeployment.Commit(s, FleetDeployment.PreviewWithdrawal(s, s.Fleet.Trains[0].Id));
        p = s.World.Stations!.Platform(p.Id);
        var host = s.World.Stations.Building(source);
        s = StationHostConnections.Commit(s, StationHostConnections.Preview(s.World, p.Id, p.Revision, source, host.Revision));
        s = FleetDeployment.Commit(s, FleetDeployment.PreviewPlacement(s, s.Fleet!.Trains[0].Id, new(west ? 37 : 25, 15, 2), west));
        NextTrainCall(s);
        Assert.Equal(1, s.Fleet!.Trains[0].StopCallCount);
        Assert.Empty(s.DepartureBells);
        var reopen = Open(s);
        NextTrainCall(s); NextTrainCall(reopen);
        Assert.Equal(SaveCodec.Write(s), SaveCodec.Write(reopen));
        var count = supply == 0 ? 0 : stock.Create(3).PackedCapacity;
        Assert.Equal(count, s.Fleet.Trains[0].Passengers);
        Assert.Equal(count, s.World.Stations!.Building(source).Exports.Today);
        Assert.Equal(Math.Max(0, supply - count), StationCatchment.Waiting(s.World.Stations, source, s.Clock.Time));
        Assert.Single(s.DepartureBells);
        var origin = new Location(west ? 37 : 27, 15, 2);
        if (supply > 0) Assert.Equal(origin, ((FleetCarState.Inside)s.Fleet.Trains[0].BoardingOrigin!).Location);
        for (var n = 0; n < 20 && s.World.Stations.Building(destination).Trains.Today == 0; n++)
        { NextTrainCall(s); NextTrainCall(reopen); }
        Assert.Equal(SaveCodec.Write(s), SaveCodec.Write(reopen));
        Assert.Equal(count, s.World.Stations.Building(destination).Imports.Today);
        Assert.Equal(1, s.Fleet.Trains[0].StopCallCount);
        Assert.Equal(0, s.Fleet.Trains[0].Passengers);
        var distance = origin.DistanceTo(((FleetCarState.Inside)s.Fleet.Trains[0].Head.State).Location);
        Assert.Equal(12, distance);
        var fares = s.Construction.Accounts.Transactions.Where(t => t.Operation == "stationUnload").ToArray();
        if (supply == 0) Assert.Empty(fares);
        else Assert.Equal(count * stock.Fare * distance * 2, Assert.Single(fares).Revenue);
        NextTrainCall(s); NextTrainCall(reopen);
        Assert.Equal(0, s.Fleet.Trains[0].Passengers); // The destination has zero supply.
        Assert.Single(s.DepartureBells);
        Assert.Equal(2, s.World.Stations.Building(destination).Trains.Today);
        Assert.Equal(SaveCodec.Write(s), SaveCodec.Write(reopen));
        Assert.Single(s.Clock.Capture().Pending.Where(t => t.HandlerId.StartsWith("train/", StringComparison.Ordinal)));
    }
}
