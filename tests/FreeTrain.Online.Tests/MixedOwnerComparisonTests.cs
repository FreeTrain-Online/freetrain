using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class MixedOwnerComparisonTests
{
    [Fact]
    public void Railway_order_and_equal_copies_preserve_identity_but_shared_owners_do_not_hide_payload_changes()
    {
        using var fixtures = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Spatial/railway-fixtures.json")));
        var row = fixtures.RootElement.GetProperty("groups").GetProperty("slopes").GetProperty("cases")[0];
        var (entity, voxels, slope) = MixedWorldTests.Slope(row);
        var world = new MixedWorld(RailwayProfiles.Bundled, StationStateTests.Terrain(), new(2, [entity]), voxels, [slope]);
        var copy = entity with { Footprint = new(entity.Footprint.Members.Select(m => m with { })) };
        var reordered = voxels.Reverse().Select(v => v with { }).ToImmutableArray();
        var next = world.Replace(0, world.Terrain, new(2, [copy]), reordered, [slope with { }]);
        Assert.Equal(0, next.Spatial.Entities[0].Revision);
        var changed = reordered.SetItem(0, reordered[0] with { DrawGroundAbove = !reordered[0].DrawGroundAbove });
        Assert.Throws<ArgumentException>(() => world.Replace(0, world.Terrain, world.Spatial.Capture(), changed, [slope]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_traffic_and_natural_owners_still_require_revision_for_added_changed_and_removed_fences(bool natural)
    {
        var profile = RailwayProfiles.Bundled;
        var terrain = StationStateTests.Terrain();
        var position = new Location(28, 20, 2);
        MixedWorld world;
        if (natural)
        {
            terrain = CornerTerrain.Apply(terrain, CornerTerrain.Preview(terrain, position, "raise"));
            world = MixedWorld.FromTerrain(profile, terrain);
        }
        else
        {
            var rail = profile.Bindings.First(b => b.Handler == RailwayHandler.Normal);
            var entity = new SpatialEntity(1, 0, profile.TrafficOwner, position,
                new([new(new(0, 0, 0), true)]), true, false, 0);
            world = new(profile, terrain, new(2, [entity]),
                [new(position, 1, RailwayVoxelKind.Traffic, new(new(rail.Reference, rail.Variant)))], []);
        }
        var owner = world.Spatial.At(position).Entity!;
        var city = CityGraph.Empty(profile.City!, terrain.Space);
        world = new(profile, terrain, world.Spatial.Capture(), world.Voxels, [], terrain.Revision, city: city);
        var definition = profile.City!.Ground.First(d => d.Handler == "fence" &&
            !profile.City.Bindings.Single(b => b.Reference == d.Reference).Implementation.EndsWith("FenceRemover", StringComparison.Ordinal));
        var fence = new CityFence(owner.Id, position, 0, definition.Reference);
        var decorated = new CityGraph(city.Profile, city.State with { Fences = [fence] });
        Assert.Throws<ArgumentException>(() => world.Replace(world.Revision, terrain, world.Spatial.Capture(), world.Voxels, [], city: decorated));

        // Load a valid decorated predecessor, sharing every spatial owner.
        world = new(profile, terrain, world.Spatial.Capture(), world.Voxels, [], terrain.Revision, city: decorated);
        foreach (var fences in new ImmutableArray<CityFence>[] { [], [fence with { Side = 1 }] })
        {
            var changed = new CityGraph(city.Profile, city.State with { Fences = fences });
            Assert.Throws<ArgumentException>(() => world.Replace(world.Revision, terrain, world.Spatial.Capture(), world.Voxels, [], city: changed));
        }
        var equal = new CityGraph(city.Profile, city.State with { Fences = [fence with { }] });
        Assert.Single(world.Replace(world.Revision, terrain, world.Spatial.Capture(), world.Voxels, [], city: equal).City!.State.Fences);
    }

    [Fact]
    public void Shared_traffic_owner_still_checks_station_listener_order_removal_and_provider()
    {
        var buildings = new[] { StationStateTests.Building(), StationStateTests.Building(2) with { Location = new(14, 12, 2) } };
        var passage = new StationPassage(30, new(28, 20, 2), Heading.East, PassageMaterial.Concrete);
        var basis = StationStateTests.World(StationStateTests.Graph(buildings, passages: [passage]));
        var graph = basis.Stations!;
        StationGraph WithListeners(params StationListener[] listeners) => new(graph.Profile, graph.Buildings, graph.Platforms,
            graph.Passages, listeners: listeners);
        var listener = new StationListener(30, [1, 2]);
        var world = new MixedWorld(basis.Profile, basis.Terrain, basis.Spatial.Capture(), basis.Voxels, [], stations: WithListeners(listener));
        foreach (var changed in new[] {
            WithListeners(), WithListeners(listener with { Stations = [2, 1] }),
            WithListeners(listener with { Provider = new(new("ConstantPopulation", 100), 3) }) })
        {
            Assert.Throws<ArgumentException>(() => world.Replace(0, world.Terrain, world.Spatial.Capture(), world.Voxels, [], stations: changed));
            var revised = world.Spatial.Entities.Select(e => e.Id == 30 ? e with { Revision = 1 } : e).ToImmutableArray();
            Assert.Same(changed, world.Replace(0, world.Terrain, new(world.Spatial.NextEntityId, revised), world.Voxels, [], stations: changed).Stations);
        }
        Assert.Throws<ArgumentException>(() => basis.Replace(0, basis.Terrain, basis.Spatial.Capture(), basis.Voxels, [], stations: world.Stations));
    }
}
