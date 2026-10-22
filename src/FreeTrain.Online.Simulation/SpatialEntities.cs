// Source contracts: ExAV 2.1.1.12, 883cf7d/trunk/core/world/{Cube,Entity,Voxel,EmptyVoxel,World}.cs.
// Original copyright Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// XYZ extent, independent of H/V storage. Upper endpoints are exclusive.
public readonly record struct Cube
{
    public Location Corner { get; }
    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public Location End { get; }
    public int Volume { get; }
    public Cube(Location corner, int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX < 0 || sizeY < 0 || sizeZ < 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
        Corner = corner; SizeX = sizeX; SizeY = sizeY; SizeZ = sizeZ;
        End = new(checked(corner.X + sizeX), checked(corner.Y + sizeY), checked(corner.Z + sizeZ));
        Volume = checked(sizeX * sizeY * sizeZ);
    }
    // Source reverse-exclusive intervals include the first endpoint, exclude the second.
    public static Cube Exclusive(Location first, Location second) => new(
        new(first.X <= second.X ? first.X : checked(second.X + 1),
            first.Y <= second.Y ? first.Y : checked(second.Y + 1),
            first.Z <= second.Z ? first.Z : checked(second.Z + 1)),
        Math.Abs(checked(second.X - first.X)), Math.Abs(checked(second.Y - first.Y)), Math.Abs(checked(second.Z - first.Z)));
    public static Cube Inclusive(Location first, Location second) => new(
        new(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Min(first.Z, second.Z)),
        checked(Math.Abs(checked(second.X - first.X)) + 1), checked(Math.Abs(checked(second.Y - first.Y)) + 1),
        checked(Math.Abs(checked(second.Z - first.Z)) + 1));
    public bool Contains(Location point) => point.X >= Corner.X && point.X < End.X &&
        point.Y >= Corner.Y && point.Y < End.Y && point.Z >= Corner.Z && point.Z < End.Z;
    // Source Cube.voxels order is X, then Y, then Z (Z changes fastest).
    public IEnumerable<Location> Locations()
    {
        for (var x = Corner.X; x < End.X; x++)
            for (var y = Corner.Y; y < End.Y; y++)
                for (var z = Corner.Z; z < End.Z; z++) yield return new(x, y, z);
    }
}

public sealed record DefinitionReference(string PackId, string ContributionId, string RecipeId)
{
    public void Validate()
    {
        foreach (var value in new[] { PackId, ContributionId, RecipeId })
            if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.Any(char.IsControl))
                throw new ArgumentException("Invalid definition identity.");
    }
}

// Invisible/transparent members still occupy a whole voxel. Half-voxel art does
// not imply half a storage cell. Irregular footprints list only their actual cells.
public sealed record VoxelMember(Location Offset, bool Transparent);
public sealed class Footprint
{
    public const int MaximumCells = 65536;
    public ImmutableArray<VoxelMember> Members { get; }
    public Footprint(IEnumerable<VoxelMember> members)
    {
        Members = members.Take(MaximumCells + 1).ToImmutableArray();
        if (Members.Length is < 1 or > MaximumCells || Members.Any(m => m == null) ||
            Members.Select(m => m.Offset).Distinct().Count() != Members.Length)
            throw new ArgumentException("Invalid voxel footprint.");
    }
    public static Footprint Box(Cube offsets, bool transparent = false)
    {
        if (offsets.Volume is < 1 or > MaximumCells) throw new ArgumentException("Invalid footprint volume.");
        return new(offsets.Locations().Select(p => new VoxelMember(p, transparent)));
    }
    public IEnumerable<(Location Position, bool Transparent)> At(Location anchor) => Members.Select(m =>
        (new Location(checked(anchor.X + m.Offset.X), checked(anchor.Y + m.Offset.Y), checked(anchor.Z + m.Offset.Z)), m.Transparent));
}

// IDs are world-local positive Int64 values, never positions, array indices or CLR references.
// Value, ownership and silent reclamation are separate source concepts.
public sealed record SpatialEntity(long Id, long Revision, DefinitionReference Definition, Location Anchor,
    Footprint Footprint, bool Owned, bool SilentlyReclaimable, long Value);
public sealed record SpatialState(long NextEntityId, ImmutableArray<SpatialEntity> Entities);
public enum VoxelStatus { Outside, Vacant, Occupied }
public sealed record VoxelOccupancy(VoxelStatus Status, SpatialEntity? Entity, bool Transparent)
{
    public bool Reusable => Status == VoxelStatus.Vacant || Status == VoxelStatus.Occupied && Entity!.SilentlyReclaimable;
}

// Immutable graph: a failed add/replace never partially changes occupancy or the allocator.
// Commands, financial effects and gameplay-specific removal callbacks belong to later slices.
public sealed class SpatialIndex
{
    public WorldSpace Space { get; }
    public long NextEntityId { get; }
    public ImmutableArray<SpatialEntity> Entities { get; }
    private readonly ImmutableDictionary<Location, (SpatialEntity Entity, bool Transparent)> cells;
    public SpatialIndex(WorldSpace space, long nextEntityId, IEnumerable<SpatialEntity> entities)
    {
        Space = space; NextEntityId = nextEntityId;
        Entities = entities.Take(Footprint.MaximumCells + 1).OrderBy(e => e?.Id).ToImmutableArray()!;
        if (nextEntityId < 1 || Entities.Length > Footprint.MaximumCells || Entities.Any(e => e == null || e.Id < 1 || e.Id >= nextEntityId || e.Revision < 0 || e.Definition == null || e.Footprint == null) ||
            Entities.Select(e => e.Id).Distinct().Count() != Entities.Length)
            throw new ArgumentException("Invalid entity graph.");
        var builder = ImmutableDictionary.CreateBuilder<Location, (SpatialEntity, bool)>();
        foreach (var entity in Entities)
        {
            entity.Definition.Validate();
            foreach (var cell in entity.Footprint.At(entity.Anchor))
                if (!space.ContainsVoxel(cell.Position) || !builder.TryAdd(cell.Position, (entity, cell.Transparent)) || builder.Count > Footprint.MaximumCells)
                    throw new ArgumentException("Outside or overlapping footprint.");
        }
        cells = builder.ToImmutable();
    }
    public VoxelOccupancy At(Location position) => !Space.ContainsVoxel(position) ? new(VoxelStatus.Outside, null, true) :
        cells.TryGetValue(position, out var cell) ? new(VoxelStatus.Occupied, cell.Entity, cell.Transparent) : new(VoxelStatus.Vacant, null, true);
    public SpatialState Capture() => new(NextEntityId, Entities);
    public SpatialIndex Add(DefinitionReference definition, Location anchor, Footprint footprint, bool owned, bool silentlyReclaimable, long value) =>
        new(Space, checked(NextEntityId + 1), Entities.Add(new(NextEntityId, 0, definition, anchor, footprint, owned, silentlyReclaimable, value)));
    public SpatialIndex Replace(SpatialEntity entity, long expectedRevision)
    {
        var previous = Entities.SingleOrDefault(e => e.Id == entity.Id) ?? throw new ArgumentException("Unknown entity.");
        if (previous.Revision != expectedRevision || entity.Revision != checked(expectedRevision + 1)) throw new InvalidOperationException("staleRevision");
        return new(Space, NextEntityId, Entities.Select(e => e.Id == entity.Id ? entity : e));
    }
    public SpatialIndex Remove(long id, long expectedRevision)
    {
        var entity = Entities.SingleOrDefault(e => e.Id == id) ?? throw new ArgumentException("Unknown entity.");
        if (entity.Revision != expectedRevision) throw new InvalidOperationException("staleRevision");
        return new(Space, NextEntityId, Entities.Where(e => e.Id != id));
    }
    public ImmutableArray<SpatialEntity> Intersecting(Cube cube) => cube.Locations().Select(At)
        .Where(v => v.Entity != null).Select(v => v.Entity!).DistinctBy(e => e.Id).OrderBy(e => e.Id).ToImmutableArray();
}
