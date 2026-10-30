// State contracts: pinned ExAV 883cf7d Traffic, RailPattern, SlopeRailRoad,
// BridgePierVoxel and special rails; see doc/web-parity-railway-contract.md.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public enum RailwayHandler
{
    Normal, Junction, Slope, Bridge, Girder, Tunnel, Garage, Signal,
    Pole, Crossing, Road, Car, Platform, Pier, SlopeSupport, SlopeWall
}
public sealed record RailwayBinding(DefinitionReference Reference, RailwayHandler Handler, int Variant = -1);

// Trusted state handlers, not command or renderer registration. Road/car/platform
// consumers must supply explicit bindings; bundled art alone never enables them.
public sealed class RailwayProfile
{
    public TerrainProfile Terrain { get; }
    public ImmutableArray<RailwayBinding> Bindings { get; }
    private readonly FrozenDictionary<DefinitionReference, RailwayBinding> bindings;
    public DefinitionReference TrafficOwner { get; }
    public DefinitionReference SlopeOwner { get; }
    public RailwayProfile(TerrainProfile terrain, IEnumerable<RailwayBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        Terrain = terrain;
        var pack = terrain.Packs[0].Id;
        TrafficOwner = new(pack, "system:traffic", "geometry:traffic-components-1");
        SlopeOwner = new(pack, "system:slope", "geometry:eight-cell-slope-1");
        Bindings = bindings.Take(4097).ToImmutableArray();
        if (Bindings.Length > 4096 || Bindings.Any(b => b is null || b.Reference is null || !Enum.IsDefined(b.Handler)) ||
            Bindings.Select(b => b.Reference).Distinct().Count() != Bindings.Length)
            throw new ArgumentException("Invalid railway handlers.");
        foreach (var b in Bindings)
        {
            b.Reference.Validate();
            if (!terrain.Packs.Any(p => p.Id == b.Reference.PackId) || b.Reference == TrafficOwner || b.Reference == SlopeOwner ||
                b.Handler == RailwayHandler.Normal && b.Variant is not (>= 0 and < 12) ||
                b.Handler == RailwayHandler.Junction && b.Variant is not (>= 12 and < 28) ||
                b.Handler == RailwayHandler.Slope && b.Variant is not (>= 28 and < 60))
                throw new ArgumentException("Invalid railway binding.");
        }
        this.bindings = Bindings.ToFrozenDictionary(b => b.Reference);
    }
    public RailwayBinding Resolve(DefinitionReference reference) => bindings.TryGetValue(reference, out var b)
        ? b : throw new ArgumentException("Unavailable railway definition/recipe.");
}

// Rail pattern is independent of subtype recipe, and is always a serialized P0 ID.
// Variant stores special art selection; tunnel alone carries its four-corner backup.
public sealed record RailComponent(DefinitionReference Definition, int Pattern, int Variant = 0,
    QuarterCorners? TerrainBackup = null, int Direction = 0, long? ControllerId = null);
public sealed record RoadComponent(DefinitionReference Definition, int Edges);
public sealed record AccessoryComponent(DefinitionReference Definition, int Direction = 0);
// Future train consumer reference, not a second whole-voxel owner.
public sealed record CarComponent(DefinitionReference Definition, long TrainId, long CarId);
public sealed record TrafficComponents(RailComponent? Rail = null, RoadComponent? Road = null,
    AccessoryComponent? Accessory = null, CarComponent? Car = null, DefinitionReference? Platform = null);
public enum RailwayVoxelKind { Traffic, SlopeFiller, SlopeSupport, SlopeWall, Pier }
// OwnerId resolves through the ONE combined SpatialIndex. Pier's supported owner
// is separate; its deletion never implicitly deletes that owner or its rail.
public sealed record RailwayVoxel(Location Position, long OwnerId, RailwayVoxelKind Kind,
    TrafficComponents? Traffic = null, DefinitionReference? Definition = null, int Sprite = 0,
    bool DrawGroundBelow = true, bool DrawGroundAbove = true, long? SupportedOwnerId = null);
public sealed record SlopeOwnership(long EntityId, Heading Climb);
