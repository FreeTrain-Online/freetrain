// Source semantics: ExAV 2.1.1.12, 883cf7d/trunk/core/world/{World,terrain/MountainVoxel}.cs
// and plugins/org.kohsuke.freetrain.terrain.terrace. Original authors, LGPL-2.1-or-later.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Source order, not compass headings: NE, SE, SW, NW (Direction indices 1,3,5,7).
public readonly record struct QuarterCorners
{
    public int NE { get; }
    public int SE { get; }
    public int SW { get; }
    public int NW { get; }
    public QuarterCorners(int ne, int se, int sw, int nw)
    {
        if (new[] { ne, se, sw, nw }.Any(n => n is < 0 or > 4))
            throw new ArgumentOutOfRangeException(nameof(ne));
        NE = ne; SE = se; SW = sw; NW = nw;
    }
    public int Packed => NE << 12 | SE << 8 | SW << 4 | NW;
    public bool Flat => Packed == 0;
    public bool Saturated => Packed == 0x4444;
    public int this[int corner] => corner switch
    { 0 => NE, 1 => SE, 2 => SW, 3 => NW, _ => throw new ArgumentOutOfRangeException(nameof(corner)) };
}

public enum TerrainSurface { Natural, Cut, Terrace }
public enum TerrainOccupantKind { Mountain, Terrace, Creek }
public enum GroundRelation { Outside, Underground, Ground, Elevated }
public sealed record TerrainCell(int Ground, TerrainSurface Surface = TerrainSurface.Natural);
// Realized positions and sprite indices, never a seed to reroll on display/load.
public sealed record TerrainTree(int X, int Y, int Sprite);
public sealed record ForestDecoration(DefinitionReference Definition, ImmutableArray<TerrainTree> Trees);
// Corners are current Mountain geometry or Terrace's original backed-up offsets.
public sealed record TerrainOccupant(long EntityId, TerrainOccupantKind Kind, QuarterCorners Corners,
    ForestDecoration? Forest = null, int CreekConnections = 0);
public sealed record TerrainDefinition(DefinitionReference Reference, TerrainOccupantKind Kind,
    int TreeSpriteCount = 0, int TreeDensity = 0, long Price = 0);
public sealed record TerrainPack(string Id, string Version, string CatalogHash);

// Trusted portable handler table supplied by Content. Art inventory alone is not registration.
public sealed class TerrainProfile
{
    public const string Ruleset = "terrain-state-1";
    public const string ContentVersion = "terrain-content-1";
    public const string MountainId = "system:terrain/mountain";
    public const string MountainGeometry = "geometry:quarter-mountain-1";
    public ImmutableArray<TerrainPack> Packs { get; }
    public ImmutableArray<DefinitionReference> GroundRecipes { get; }
    public ImmutableArray<TerrainDefinition> Definitions { get; }
    private readonly FrozenDictionary<DefinitionReference, TerrainDefinition> definitions;
    public TerrainProfile(IEnumerable<TerrainPack> packs, IEnumerable<DefinitionReference> groundRecipes,
        IEnumerable<TerrainDefinition> definitions)
    {
        Packs = packs.OrderBy(p => p.Id, StringComparer.Ordinal).ToImmutableArray();
        GroundRecipes = groundRecipes.ToImmutableArray(); Definitions = definitions.ToImmutableArray();
        if (Packs.IsEmpty || Packs.Any(p => string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Version) ||
            !WorldCreationProfile.IsHash(p.CatalogHash)) || Packs.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != Packs.Length ||
            GroundRecipes.Length != 4 || GroundRecipes.Distinct().Count() != 4)
            throw new ArgumentException("Invalid terrain profile.");
        foreach (var reference in GroundRecipes.Concat(Definitions.Select(d => d.Reference)))
        {
            reference.Validate();
            if (!Packs.Any(p => p.Id == reference.PackId)) throw new ArgumentException("Missing terrain pack.");
        }
        if (Definitions.Any(d => !Enum.IsDefined(d.Kind) || d.TreeSpriteCount < 0 || d.TreeDensity is < 0 or > 3 || d.Price < 0) ||
            Definitions.Select(d => d.Reference).Distinct().Count() != Definitions.Length)
            throw new ArgumentException("Invalid terrain handlers.");
        this.definitions = Definitions.ToFrozenDictionary(d => d.Reference);
    }
    public TerrainDefinition Resolve(DefinitionReference reference) => definitions.TryGetValue(reference, out var value)
        ? value : throw new ArgumentException("Unavailable terrain definition.");
    public bool Matches(string ruleset, string contentVersion, IEnumerable<TerrainPack> packs)
    {
        var actual = packs.ToArray();
        return ruleset == Ruleset && contentVersion == ContentVersion && actual.Length == Packs.Length &&
            actual.Distinct().Count() == actual.Length && Packs.All(actual.Contains);
    }
}

public sealed record TerrainVoxelRead(GroundRelation Relation, bool Underwater, VoxelOccupancy Occupancy);
// In-process immutable read model. Wire encoding/digest/save dispatch belong to P3-04.
public sealed record TerrainRead(string Ruleset, string ContentVersion, ImmutableArray<TerrainPack> RequiredPacks,
    string Name, WorldDimensions Dimensions, long Revision, int WaterLevel, ImmutableArray<TerrainCell> Cells,
    SpatialState Spatial, ImmutableArray<TerrainOccupant> Occupants);

// Detached canonical world. Zero rails are legal; the synthetic ReferenceWorld keeps
// its own exact loop invariant. No active session, gameplay RNG or constructor side effects.
public sealed class TerrainWorld
{
    public TerrainProfile Profile { get; }
    public string Name { get; }
    public WorldSpace Space { get; }
    public int WaterLevel { get; }
    public long Revision { get; }
    // Row-major H/V storage: index = V * HSize + H; includes all stored border cells.
    public ImmutableArray<TerrainCell> Cells { get; }
    public SpatialIndex Spatial { get; }
    public ImmutableArray<TerrainOccupant> Occupants { get; }
    public ImmutableArray<ReferenceRail> Rails => [];
    private readonly FrozenDictionary<long, TerrainOccupant> occupants;

    public TerrainWorld(TerrainProfile profile, string name, WorldDimensions dimensions, int waterLevel,
        IEnumerable<TerrainCell> cells, SpatialState spatial, IEnumerable<TerrainOccupant> occupants, long revision = 0)
    {
        var request = new EmptyWorldRequest(WorldCreationProfile.Id, name, dimensions, waterLevel);
        // Validate dimensions/budgets before enumerating any untrusted collection.
        if (profile is null || spatial is null || spatial.Entities.IsDefault || revision < 0 ||
            !WorldCreationProfile.Validate(request).IsEmpty) throw new ArgumentException("Invalid terrain world.");
        Profile = profile; Name = name; Revision = revision; WaterLevel = waterLevel;
        Space = new(dimensions.H, dimensions.V, dimensions.Z);
        var count = checked(dimensions.H * dimensions.V);
        Cells = cells.Take(count + 1).ToImmutableArray();
        if (Cells.Length != count || Cells.Any(c => c is null || c.Ground < 0 || c.Ground >= dimensions.Z || !Enum.IsDefined(c.Surface)))
            throw new ArgumentException("Invalid ground grid.");
        Spatial = new(Space, spatial.NextEntityId, spatial.Entities);
        Occupants = occupants.Take(2 * count + 1).OrderBy(o => o?.EntityId).ToImmutableArray()!;
        if (Occupants.Length > 2 * count || Occupants.Any(o => o is null) ||
            Occupants.Select(o => o.EntityId).Distinct().Count() != Occupants.Length || Spatial.Entities.Length != Occupants.Length)
            throw new ArgumentException("Invalid terrain occupants.");
        this.occupants = Occupants.ToFrozenDictionary(o => o.EntityId);
        foreach (var entity in Spatial.Entities) ValidateEntity(entity);
        ValidateContinuity();
    }

    public static TerrainWorld Empty(EmptyWorldRequest request, TerrainProfile profile)
    {
        if (!WorldCreationProfile.Validate(request).IsEmpty) throw new ArgumentException("Invalid empty world request.");
        return new(profile, request.Name, request.Dimensions, request.WaterLevel,
            Enumerable.Repeat(new TerrainCell(request.WaterLevel), checked(request.Dimensions.H * request.Dimensions.V)), new(1, []), []);
    }
    public TerrainCell Cell(int h, int v)
    {
        if (h < 0 || h >= Space.HorizontalSize || v < 0 || v >= Space.VerticalSize) throw new ArgumentOutOfRangeException(nameof(h));
        return Cells[v * Space.HorizontalSize + h];
    }
    private TerrainCell Cell(Location p) { var s = Space.ToStorage(p); return Cell(s.H, s.V); }
    public int FineHeight(Location p, int corner)
    {
        if (corner is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(corner));
        if (!Space.ContainsHorizontal(p)) return -1;
        var ground = Cell(p).Ground;
        var owner = Spatial.At(new(p.X, p.Y, ground)).Entity;
        var offset = owner is not null && this.occupants[owner.Id].Kind == TerrainOccupantKind.Mountain
            ? this.occupants[owner.Id].Corners[corner] : 0;
        return 4 * ground + offset;
    }
    public TerrainVoxelRead At(Location p)
    {
        if (!Space.ContainsVoxel(p)) return new(GroundRelation.Outside, false, Spatial.At(p));
        var ground = Cell(p).Ground;
        return new(p.Z < ground ? GroundRelation.Underground : p.Z == ground ? GroundRelation.Ground : GroundRelation.Elevated,
            p.Z < WaterLevel, Spatial.At(p));
    }
    public bool IsTrainBorder(Location p)
    {
        var s = Space.ToStorage(p);
        return s.H < 0 || s.H >= Space.HorizontalSize || s.V < checked(2 * p.Z) ||
            checked(Space.VerticalSize - checked(2 * checked(Space.Levels - p.Z))) <= s.V;
    }
    public TerrainRead Read() => new(TerrainProfile.Ruleset, TerrainProfile.ContentVersion, Profile.Packs, Name,
        new(Space.HorizontalSize, Space.VerticalSize, Space.Levels), Revision, WaterLevel, Cells,
        new(Spatial.NextEntityId, Spatial.Entities), Occupants);

    // A whole candidate is validated before returning a replacement. The caller's
    // old world/index/allocator remains intact on any failure. Authority/FIFO is P3-06/13.
    public TerrainWorld Replace(long expectedRevision, IEnumerable<TerrainCell> cells, int waterLevel,
        SpatialState spatial, IEnumerable<TerrainOccupant> occupants)
    {
        if (expectedRevision != Revision) throw new InvalidOperationException("Stale terrain revision.");
        if (spatial is null || spatial.Entities.IsDefault || spatial.Entities.Any(e => e is null) ||
            spatial.NextEntityId < Spatial.NextEntityId) throw new ArgumentException("Invalid terrain graph or allocator rewind.");
        var candidate = new TerrainWorld(Profile, Name, new(Space.HorizontalSize, Space.VerticalSize, Space.Levels), waterLevel,
            cells, spatial, occupants, checked(Revision + 1));
        var old = Spatial.Entities.ToDictionary(e => e.Id);
        foreach (var entity in candidate.Spatial.Entities)
        {
            if (!old.TryGetValue(entity.Id, out var previous))
            {
                if (entity.Id < Spatial.NextEntityId || entity.Revision != 0)
                    throw new ArgumentException("Entity identity cannot be reused.");
                continue;
            }
            if (entity.Revision == previous.Revision)
            {
                if (entity with { Footprint = previous.Footprint } != previous ||
                    !entity.Footprint.Members.SequenceEqual(previous.Footprint.Members) ||
                    candidate.occupants[entity.Id] != this.occupants[entity.Id])
                    throw new ArgumentException("Changed terrain entity requires a revision.");
            }
            else if (previous.Revision == long.MaxValue || entity.Revision != previous.Revision + 1)
                throw new ArgumentException("Invalid terrain entity revision.");
        }
        return candidate;
    }

    private void ValidateEntity(SpatialEntity entity)
    {
        if (!occupants.TryGetValue(entity.Id, out var occupant) || !Enum.IsDefined(occupant.Kind))
            throw new ArgumentException("Missing terrain occupant.");
        var binding = Profile.Resolve(entity.Definition);
        var cell = Cell(entity.Anchor);
        var terrace = occupant.Kind == TerrainOccupantKind.Terrace;
        if (entity.Footprint.Members.Length != 1 || entity.Footprint.Members[0].Offset != new Location(0, 0, 0) ||
            binding.Kind != occupant.Kind || binding.TreeSpriteCount != 0 || entity.SilentlyReclaimable != (occupant.Kind == TerrainOccupantKind.Creek) ||
            entity.Anchor.Z != cell.Ground - (terrace ? 1 : 0) ||
            entity.Footprint.Members[0].Transparent != (occupant.Kind == TerrainOccupantKind.Creek ||
                occupant.Kind == TerrainOccupantKind.Mountain && occupant.Corners.Flat) ||
            terrace && cell.Surface != TerrainSurface.Terrace)
            throw new ArgumentException("Terrain occupancy does not match surface.");
        if (occupant.Kind == TerrainOccupantKind.Mountain)
        {
            // All-four geometry is preserved at its original ground. The top boundary
            // cannot contain a fine corner above the upper boundary of the world.
            if (Enumerable.Range(0, 4).Any(i => 4 * cell.Ground + occupant.Corners[i] > 4 * Space.Levels - 1))
                throw new ArgumentException("Terrain corner exceeds world height.");
            if (occupant.CreekConnections != 0) throw new ArgumentException("Invalid mountain payload.");
        }
        else if (occupant.Forest is not null || occupant.Kind == TerrainOccupantKind.Creek && !occupant.Corners.Flat ||
            occupant.Kind == TerrainOccupantKind.Terrace && occupant.CreekConnections != 0)
            throw new ArgumentException("Invalid terrain payload.");
        if (occupant.CreekConnections is < 0 or > 15 || terrace && entity.Owned)
            throw new ArgumentException("Invalid terrain metadata.");
        long value = 0;
        if (occupant.Forest is { } forest)
        {
            var definition = Profile.Resolve(forest.Definition);
            if (definition.Kind != TerrainOccupantKind.Mountain || definition.TreeSpriteCount < 1 ||
                forest.Trees.IsDefault || forest.Trees.Length > 2 * definition.TreeDensity ||
                forest.Trees.Any(t => t is null || t.X is < 0 or > 30 || t.Y is < 0 or > 15 ||
                    t.Sprite < 0 || t.Sprite >= definition.TreeSpriteCount))
                throw new ArgumentException("Invalid realized forest.");
            value = definition.Price;
        }
        if (entity.Value != value) throw new ArgumentException("Invalid terrain entity value.");
    }

    private void ValidateContinuity()
    {
        for (var v = 0; v < Space.VerticalSize; v++)
            for (var h = 0; h < Space.HorizontalSize; h++)
            {
                var cell = Cell(h, v); var p = Space.ToLocation(new(h, v, cell.Ground));
                if (cell.Surface == TerrainSurface.Terrace &&
                    (Spatial.At(new(p.X, p.Y, p.Z - 1)).Entity is not { } owner || occupants[owner.Id].Kind != TerrainOccupantKind.Terrace))
                    throw new ArgumentException("Terrace surface has no backing entity.");
                if (cell.Surface != TerrainSurface.Natural) continue;
                for (var corner = 0; corner < 4; corner++)
                {
                    var d = new Heading(2 * corner + 1);
                    var samples = new[] { (p, corner), (p.Move(d), (corner + 2) % 4),
                        (p.Move(d.Left), (corner + 1) % 4), (p.Move(d.Right), (corner + 3) % 4) }
                        .Where(s => Space.ContainsHorizontal(s.Item1)).ToArray();
                    // Discontinuity must be explicitly classified; natural mismatches
                    // elsewhere cannot be excused by a distant cut or terrace.
                    if (samples.Any(s => Cell(s.Item1).Surface != TerrainSurface.Natural)) continue;
                    if (samples.Select(s => FineHeight(s.Item1, s.Item2)).Distinct().Count() != 1)
                        throw new ArgumentException("Unmatched natural terrain corners.");
                }
            }
    }
}
