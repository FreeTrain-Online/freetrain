// ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail/BridgePierVoxel.cs.
// Original authors, LGPL-2.1-or-later. GEO-004 retains owner-agnostic teardown.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record RailwayPierPlacement(Location Position, DefinitionReference Definition, int Sprite);

public static class RailwaySupports
{
    // Occupancy is checked from the floor, even below ground, and is independent
    // of transparency/reclamation. Obstruction suppresses piers, never the rail.
    // Explicit top/body bindings allow later slope/bridge consumers their own art.
    public static ImmutableArray<RailwayPierPlacement> Elect(MixedWorld world, Location rail,
        RailwayBinding top, RailwayBinding body)
    {
        if (!world.Terrain.Space.ContainsVoxel(rail)) throw new ArgumentException("outside");
        if (top.Handler != RailwayHandler.Pier || body.Handler != RailwayHandler.Pier ||
            top.Variant < 0 || body.Variant < 0 || world.Profile.Resolve(top.Reference) != top ||
            world.Profile.Resolve(body.Reference) != body) throw new ArgumentException("unavailablePierBinding");
        for (var z = 0; z < rail.Z; z++)
            if (world.Spatial.At(rail with { Z = z }).Status == VoxelStatus.Occupied) return [];
        var s = world.Terrain.Space.ToStorage(rail);
        var result = ImmutableArray.CreateBuilder<RailwayPierPlacement>();
        for (var z = world.Terrain.Cell(s.H,s.V).Ground; z < rail.Z; z++)
        {
            var style = z == rail.Z - 1 ? top : body;
            result.Add(new(rail with { Z = z },style.Reference,style.Variant));
        }
        return result.ToImmutable();
    }

    public static ImmutableArray<RailwayVoxel> Teardown(MixedWorld world, Location selected)
    {
        if (!world.Terrain.Space.ContainsVoxel(selected)) throw new ArgumentException("outside");
        var result = ImmutableArray.CreateBuilder<RailwayVoxel>();
        for (var z = 0; z < selected.Z; z++)
            if (world.At(selected with { Z = z }) is { Kind: RailwayVoxelKind.Pier } pier) result.Add(pier);
        return result.ToImmutable();
    }
}
