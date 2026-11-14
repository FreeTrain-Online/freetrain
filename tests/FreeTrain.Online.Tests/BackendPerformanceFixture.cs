using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Tests;

// Synthetic public-content fixture; never reads a developer checkpoint or tmp/.
// Build dynamic content on an empty map, then install the literal natural owners
// once. Setup, save encoding and reopening are outside every allocation window.
internal static class BackendPerformanceFixture
{
    private static readonly Lazy<byte[]> small = new(() => Build(64, 64, 1024, 1978));
    private static readonly Lazy<byte[]> large = new(() => Build(400, 600, 55179, 1978));
    private static readonly Lazy<byte[]> midnight = new(() => Build(64, 64, 1024, 1438));

    internal static MixedSessionState Open(string scene) => SaveCodec.ReadWorld(
        scene switch { "small" => small.Value, "large" => large.Value, "midnight" => midnight.Value,
            _ => throw new ArgumentException("Unknown backend performance scene.", nameof(scene)) },
        TerrainProfiles.Resolve, RailwayProfiles.Resolve).Mixed!;

    private static byte[] Build(int width, int height, int naturalOwners, long minute)
    {
        var dimensions = new WorldDimensions(width, height, 24);
        var request = new EmptyWorldRequest(WorldCreationProfile.Id, "Backend performance oracle", dimensions, 0);
        var terrain = new TerrainWorld(TerrainProfiles.Bundled, request.Name, dimensions, 0,
            Enumerable.Repeat(new TerrainCell(2), width * height), new(1, []), []);
        var initial = MixedWorld.FromTerrain(RailwayProfiles.Bundled, terrain)
            .EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var state = new MixedSessionState(new(initial), TerrainProvenance.From(request),
            clock: new(minute, 0, []), random: new RandomStreams(73).Capture(), revision: initial.Revision);
        var origin = terrain.Space.ToLocation(new(12, 32, 2));
        Location P(int x, int y) => new(origin.X + x, origin.Y + y, 2);
        var stationDefinition = RailwayProfiles.Bundled.Stations!.Definitions
            .First(d => d.SizeX == 1 && d.SizeY == 1 && d.Height == 1);
        foreach (var y in new[] { 0, 12 })
        {
            var rail = NormalRailwayConstruction.PreviewBuild(state.World, P(0, y), P(24, y));
            if (!rail.Legal) throw new InvalidOperationException(rail.Reason);
            state = state.Successor(state.Revision,
                NormalRailwayConstruction.Apply(state.Construction, rail, state.Clock.Time), false);
            state = StationBuildingConstruction.Commit(state,
                StationBuildingConstruction.Preview(state.World, P(7, y - 2), stationDefinition.Reference));
            state = ThinPlatformConstruction.Commit(state,
                ThinPlatformConstruction.Preview(state.World, P(9, y), Heading.East, 4, true));
        }
        var stock = RollingStockCatalog.Bundled.Trains
            .First(t => t.MinutesPerVoxel == 2 && t.PricePerCar == 30_000_000 && t.PermittedLengths.Contains(3));
        state = FleetTrading.Commit(state, FleetTrading.QuotePurchase(state, stock.Binding, 3, 2));
        for (var i = 0; i < 2; i++)
            state = FleetDeployment.Commit(state,
                FleetDeployment.PreviewPlacement(state, state.Fleet!.Trains[i].Id, P(9, i * 12)));
        state = CityHeat.Enable(state);
        var definition = RailwayProfiles.Bundled.City!.Structures.First(d => d.Kind == CityStructureKind.Fixed &&
            d.SizeX == 1 && d.SizeY == 1 && d.MinimumHeight == 1 && d.Population != null && !d.ComputerCannotBuild);
        for (var i = 0; i < 6; i++)
        {
            var candidate = CityConstruction.Start(new(state.Construction, state.Clock.Capture(), state.Random.Capture()),
                P(5 + i * 2, 5), definition.Reference, 1, CityConstructionCaller.CatalogPlayer);
            state = state.Successor(state.Revision, candidate.Construction, false, candidate.Random, candidate.Clock);
        }

        var world = state.World;
        var entities = ImmutableArray.CreateBuilder<SpatialEntity>(naturalOwners);
        var occupants = ImmutableArray.CreateBuilder<TerrainOccupant>(naturalOwners);
        var mountain = terrain.Profile.Definitions.Single(d => d.Kind == TerrainOccupantKind.Mountain && d.TreeSpriteCount == 0).Reference;
        var footprint = new Footprint([new(new(0, 0, 0), true)]);
        var nextId = world.Spatial.NextEntityId;
        // Reserve the populated module. H/V values here are literal fixture data.
        for (var v = 0; v < height && entities.Count < naturalOwners; v++)
            for (var h = 0; h < width && entities.Count < naturalOwners; h++)
            {
                if (h < 44 && v < 56) continue;
                var id = nextId++;
                entities.Add(new(id, 0, mountain, terrain.Space.ToLocation(new(h, v, 2)), footprint, false, false, 0));
                occupants.Add(new(id, TerrainOccupantKind.Mountain, new(0, 0, 0, 0)));
            }
        if (entities.Count != naturalOwners) throw new InvalidOperationException("Incomplete natural owner fixture.");
        terrain = new(terrain.Profile, terrain.Name, dimensions, 0, terrain.Cells,
            new(nextId, entities.ToImmutable()), occupants);

        var city = world.City!;
        var heat = city.State.Heat;
        var rho = heat.Rho.ToBuilder();
        foreach (var entity in entities)
        {
            var hv = terrain.Space.ToStorage(entity.Anchor);
            rho[heat.Index(hv.H, hv.V)] = 0.4f;
        }
        // Exercise a real full-heat publication and diffusion within the window,
        // including retained padding values. Subsequent callbacks keep interval 600.
        var timer = city.State.HeatCallback! with { DueMinute = minute + 2 };
        // Literal callback phases put construction inside the daytime recording
        // instead of accidentally measuring only trains waiting for distant sites.
        var structures = city.State.Structures.Select((site, i) => site with {
            Construction = site.Construction.Select(cell => cell with {
                Callback = cell.Callback! with { DueMinute = minute + 8 + 4 * i }
            }).ToImmutableArray()
        }).ToImmutableArray();
        city = new(city.Profile, city.State with { Structures = structures, HeatCallback = timer, Heat = heat with {
            Q = heat.Q.SetItem(heat.Index(8, 8), 16).SetItem(0, 3.5f),
            Back = heat.Back.SetItem(0, 1.25f), Rho = rho.ToImmutable() } });
        world = new(world.Profile, terrain, new(nextId, world.Spatial.Entities.AddRange(entities)),
            world.Voxels, world.Slopes, world.Revision, world.SpeedLimits, world.Fleet, world.Stations, city);
        var clock = state.Clock.Capture();
        var callbacks = city.Callbacks.ToDictionary(a => a.HandlerId, StringComparer.Ordinal);
        clock = clock with { Pending = clock.Pending.Select(a => callbacks.GetValueOrDefault(a.HandlerId, a))
            .OrderBy(a => a.DueMinute).ThenBy(a => a.Order).ToImmutableArray() };
        state = new(new(world, state.Construction.Accounts), state.Provenance, clock: clock,
            random: state.Random.Capture(), revision: state.Revision);
        return SaveCodec.Write(state);
    }
}
