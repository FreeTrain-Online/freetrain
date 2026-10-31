using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Detached canonical composition. Terrain retains its exact geometry/backups;
// the combined index is authoritative for occupancy and shares one ID namespace.
// No synthetic-loop connectivity invariant, live dispatch or persistence here.
public sealed class MixedWorld
{
    public RailwayProfile Profile { get; }
    public TerrainWorld Terrain { get; }
    public SpatialIndex Spatial { get; }
    public long Revision { get; }
    public ImmutableArray<RailwayVoxel> Voxels { get; }
    public ImmutableArray<SlopeOwnership> Slopes { get; }
    private readonly FrozenDictionary<Location, RailwayVoxel> voxels;
    private readonly FrozenDictionary<long, ImmutableArray<RailwayVoxel>> byOwner;
    private readonly FrozenDictionary<long, SlopeOwnership> slopeOwners;
    private readonly FrozenDictionary<long, SpatialEntity> entities;
    public RailwayVoxel? At(Location p) => voxels.GetValueOrDefault(p);

    public MixedWorld(RailwayProfile profile, TerrainWorld terrain, SpatialState spatial,
        IEnumerable<RailwayVoxel> voxels, IEnumerable<SlopeOwnership> slopes, long revision = 0)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(spatial);
        if (spatial.Entities.IsDefault || profile.Terrain != terrain.Profile || revision < terrain.Revision ||
            spatial.NextEntityId < terrain.Spatial.NextEntityId)
            throw new ArgumentException("Invalid mixed-world profile/revision/allocator.");
        Profile = profile; Terrain = terrain; Revision = revision;
        Voxels = voxels.Take(SpatialIndex.MaximumOccupiedCells + 1).ToImmutableArray();
        Slopes = slopes.Take(SpatialIndex.MaximumOccupiedCells / 8 + 1).ToImmutableArray();
        if (Voxels.Length > SpatialIndex.MaximumOccupiedCells || Voxels.Any(v => v is null || !Enum.IsDefined(v.Kind)) ||
            Voxels.Select(v => v.Position).Distinct().Count() != Voxels.Length ||
            Slopes.Length > SpatialIndex.MaximumOccupiedCells / 8 || Slopes.Any(s => s is null) ||
            Slopes.Select(s => s.EntityId).Distinct().Count() != Slopes.Length)
            throw new ArgumentException("Invalid mixed-world members.");
        Spatial = new(terrain.Space, spatial.NextEntityId, spatial.Entities);
        this.voxels = Voxels.ToFrozenDictionary(v => v.Position);
        byOwner = Voxels.GroupBy(v => v.OwnerId).ToFrozenDictionary(g => g.Key, g => g.ToImmutableArray());
        var terrainIds = terrain.Spatial.Entities.ToDictionary(e => e.Id);
        slopeOwners = Slopes.ToFrozenDictionary(s => s.EntityId);
        var slopeIds = Slopes.ToDictionary(s => s.EntityId);
        entities = Spatial.Entities.ToFrozenDictionary(e => e.Id);
        foreach (var e in Spatial.Entities)
        {
            if (terrainIds.Remove(e.Id, out var te))
            {
                if (!SameEntity(e, te) || byOwner.ContainsKey(e.Id)) throw new ArgumentException("Terrain identity was overwritten.");
                continue;
            }
            if (!byOwner.TryGetValue(e.Id, out var members) || !e.Owned || e.SilentlyReclaimable || e.Value != 0 ||
                members.Length != e.Footprint.Members.Length)
                throw new ArgumentException("Invalid railway owner.");
            // Traffic, EmptyVoxel slope fillers/supports/walls and piers all
            // report source transparency. None is silently reclaimable.
            foreach (var (p, transparent) in e.Footprint.At(e.Anchor))
                if (At(p) is not {} v || v.OwnerId != e.Id || !transparent)
                    throw new ArgumentException("Railway footprint/payload mismatch.");
            if (slopeIds.Remove(e.Id, out var slope)) ValidateSlope(e, slope, members);
            else if (members.Length != 1 || members[0].Position != e.Anchor ||
                members[0].Kind is not (RailwayVoxelKind.Traffic or RailwayVoxelKind.Pier) ||
                e.Definition != (members[0].Kind == RailwayVoxelKind.Traffic ? profile.TrafficOwner : members[0].Definition))
                throw new ArgumentException("Invalid standalone railway owner.");
        }
        if (terrainIds.Count != 0 || slopeIds.Count != 0 || byOwner.Keys.Any(id => !entities.ContainsKey(id)))
            throw new ArgumentException("Missing mixed-world owner.");
        foreach (var v in Voxels) ValidateVoxel(v);
        var cars = Voxels.Where(v => v.Traffic?.Car != null).Select(v => v.Traffic!.Car!).ToArray();
        if (cars.Select(c => c.CarId).Distinct().Count() != cars.Length)
            throw new ArgumentException("Car occupies multiple voxels.");
    }

    public static MixedWorld FromTerrain(RailwayProfile profile, TerrainWorld terrain) =>
        new(profile, terrain, terrain.Spatial.Capture(), [], [], terrain.Revision);

    private void ValidateSlope(SpatialEntity e, SlopeOwnership slope, ImmutableArray<RailwayVoxel> members)
    {
        if (e.Definition != Profile.SlopeOwner || !slope.Climb.IsCardinal || members.Length != 8)
            throw new ArgumentException("Invalid slope owner.");
        for (var i = 0; i < 4; i++)
        {
            var p = e.Anchor;
            for (var j = 0; j < i; j++) p = p.Move(slope.Climb);
            p = p with { Z = checked(e.Anchor.Z + i / 2) };
            var rail = At(p);
            var pair = At(p with { Z = checked(e.Anchor.Z + (i < 2 ? 1 : 0)) });
            var pattern = rail?.Traffic?.Rail?.Pattern ?? -1;
            var expected = 28 + slope.Climb.Index / 2 * 4 + i;
            if (rail?.OwnerId != e.Id || rail.Kind != RailwayVoxelKind.Traffic || (pattern != expected && pattern != expected + 16) ||
                pair?.OwnerId != e.Id || (i < 2 ? pair.Kind != RailwayVoxelKind.SlopeFiller :
                    pair.Kind != (pattern >= 44 ? RailwayVoxelKind.SlopeWall : RailwayVoxelKind.SlopeSupport)) ||
                i >= 2 && pair.Sprite != slope.Climb.Index + i - 2)
                throw new ArgumentException("Malformed/partial slope.");
            // Art and draw-ground flags are STORED, never inferred from current terrain.
        }
    }

    private void ValidateVoxel(RailwayVoxel v)
    {
        if (Spatial.At(v.Position).Entity?.Id != v.OwnerId) throw new ArgumentException("Unowned railway voxel.");
        if (v.Kind != RailwayVoxelKind.Traffic)
        {
            if (v.Traffic != null || v.Kind == RailwayVoxelKind.SlopeFiller && (v.Definition != null || v.Sprite != 0) ||
                v.Kind != RailwayVoxelKind.Pier && v.SupportedOwnerId != null ||
                v.Kind == RailwayVoxelKind.Pier && (!v.DrawGroundBelow || !v.DrawGroundAbove))
                throw new ArgumentException("Invalid paired/support payload.");
            if (v.Kind != RailwayVoxelKind.SlopeFiller)
            {
                var expected = v.Kind switch { RailwayVoxelKind.Pier => RailwayHandler.Pier,
                    RailwayVoxelKind.SlopeSupport => RailwayHandler.SlopeSupport, _ => RailwayHandler.SlopeWall };
                var b = Profile.Resolve(v.Definition ?? throw new ArgumentException("Missing support recipe."));
                if (b.Handler != expected || v.Sprite < 0 || b.Variant >= 0 && b.Variant != v.Sprite)
                    throw new ArgumentException("Invalid support binding.");
            }
            // A historical supported ID may outlive its rail (for example a
            // pier above an explicit teardown call's height), but cannot refer
            // to an unrelated live entity. SlopeEntity's Traffic.remove callbacks
            // do tear down lower piers despite its direct cube-scan exclusion.
            if (v.Kind == RailwayVoxelKind.Pier && (v.SupportedOwnerId is not {} id || id < 1 || id >= Spatial.NextEntityId ||
                entities.ContainsKey(id) && (!byOwner.TryGetValue(id, out var owner) || !owner.Any(m => m.Traffic?.Rail != null &&
                    m.Position.X == v.Position.X && m.Position.Y == v.Position.Y && m.Position.Z > v.Position.Z))))
                throw new ArgumentException("Invalid pier supported owner.");
            return;
        }
        var t = v.Traffic ?? throw new ArgumentException("Missing Traffic components.");
        if (v.Definition != null || v.Sprite != 0 || v.SupportedOwnerId != null || !v.DrawGroundBelow || !v.DrawGroundAbove ||
            t.Rail == null && t.Road == null && t.Accessory == null)
            throw new ArgumentException("Empty/invalid Traffic voxel.");
        if (t.Rail is {} r)
        {
            var b = Profile.Resolve(r.Definition);
            if (b.Handler is not (RailwayHandler.Normal or RailwayHandler.Junction or RailwayHandler.Slope or RailwayHandler.Bridge or
                RailwayHandler.Girder or RailwayHandler.Tunnel or RailwayHandler.Garage or RailwayHandler.Signal) || r.Pattern is < 0 or >= 60 ||
                b.Handler is RailwayHandler.Normal or RailwayHandler.Junction or RailwayHandler.Slope && b.Variant != r.Pattern ||
                b.Handler != RailwayHandler.Slope && r.Pattern >= 28 ||
                b.Handler == RailwayHandler.Slope && !slopeOwners.ContainsKey(v.OwnerId) ||
                r.TerrainBackup.HasValue != (b.Handler == RailwayHandler.Tunnel) ||
                r.Direction is < 0 or > 7 || r.ControllerId is <= 0 ||
                b.Handler != RailwayHandler.Signal && r.ControllerId != null || r.Variant < 0 ||
                b.Handler is RailwayHandler.Normal or RailwayHandler.Junction or RailwayHandler.Slope && (r.Variant != 0 || r.Direction != 0) ||
                b.Handler is RailwayHandler.Bridge && r.Variant > 5 ||
                b.Handler is RailwayHandler.Girder or RailwayHandler.Signal && r.Variant > 3 ||
                b.Handler is RailwayHandler.Tunnel or RailwayHandler.Garage && r.Variant > 1)
                throw new ArgumentException("Invalid rail component.");
            if (r.TerrainBackup is {} backup)
            {
                var maximum = 4 * (Terrain.Space.Levels - v.Position.Z) - 1;
                if (Enumerable.Range(0,4).Any(c => backup[c] > maximum))
                    throw new ArgumentException("Tunnel backup exceeds world height.");
            }
            if (b.Handler is RailwayHandler.Bridge or RailwayHandler.Girder or RailwayHandler.Tunnel or RailwayHandler.Garage or RailwayHandler.Signal)
            {
                if (r.Pattern >= 12) throw new ArgumentException("Special rail must have two straight ports.");
                var (firstPort, secondPort) = NormalRailPatterns.Ports[r.Pattern];
                if ((firstPort + 4) % 8 != secondPort || b.Handler != RailwayHandler.Girder && firstPort % 2 != 0 ||
                    b.Handler == RailwayHandler.Signal && (r.Direction % 2 != 0 || firstPort != r.Direction && secondPort != r.Direction))
                    throw new ArgumentException("Invalid special rail orientation.");
            }
        }
        if (t.Road is {} road && (Profile.Resolve(road.Definition).Handler != RailwayHandler.Road || road.Edges is < 1 or > 255))
            throw new ArgumentException("Invalid road component.");
        if (t.Accessory is {} a && (Profile.Resolve(a.Definition).Handler is not (RailwayHandler.Pole or RailwayHandler.Crossing) ||
            a.Direction is < 0 or > 7)) throw new ArgumentException("Invalid accessory component.");
        if (t.Accessory is {} crossing && Profile.Resolve(crossing.Definition).Handler == RailwayHandler.Crossing &&
            (t.Rail == null || t.Road == null ||
                !(t.Rail.Pattern == 10 && (t.Road.Edges & 68) == 68 || t.Rail.Pattern == 4 && (t.Road.Edges & 17) == 17)))
            throw new ArgumentException("Invalid crossing components.");
        if (t.Car is {} car && (Profile.Resolve(car.Definition).Handler != RailwayHandler.Car || car.TrainId < 1 || car.CarId < 1))
            throw new ArgumentException("Invalid car reference.");
        if (t.Platform is {} platform && (Profile.Resolve(platform).Handler != RailwayHandler.Platform || t.Rail == null))
            throw new ArgumentException("Invalid platform component.");
    }

    private static bool SameEntity(SpatialEntity a, SpatialEntity b) =>
        a with { Footprint = b.Footprint } == b && a.Footprint.Members.SequenceEqual(b.Footprint.Members);

    // Candidate validation and identity checks finish before a replacement is returned.
    public MixedWorld Replace(long expectedRevision, TerrainWorld terrain, SpatialState spatial,
        IEnumerable<RailwayVoxel> voxels, IEnumerable<SlopeOwnership> slopes)
    {
        if (expectedRevision != Revision) throw new InvalidOperationException("staleRevision");
        if (spatial.NextEntityId < Spatial.NextEntityId) throw new ArgumentException("Allocator rewind.");
        var candidate = new MixedWorld(Profile, terrain, spatial, voxels, slopes, checked(Revision + 1));
        if (!ReferenceEquals(terrain, Terrain))
        {
            if (terrain.Profile != Terrain.Profile || terrain.Name != Terrain.Name ||
                terrain.Space.HorizontalSize != Terrain.Space.HorizontalSize || terrain.Space.VerticalSize != Terrain.Space.VerticalSize ||
                terrain.Space.Levels != Terrain.Space.Levels || terrain.Revision != checked(Terrain.Revision + 1))
                throw new ArgumentException("Invalid terrain successor.");
            Terrain.Replace(Terrain.Revision, terrain.Cells, terrain.WaterLevel, terrain.Spatial.Capture(), terrain.Occupants);
        }
        var old = Spatial.Entities.ToDictionary(e => e.Id);
        var oldTerrainIds = Terrain.Spatial.Entities.Select(e => e.Id).ToHashSet();
        var newTerrainIds = terrain.Spatial.Entities.Select(e => e.Id).ToHashSet();
        foreach (var e in candidate.Spatial.Entities)
        {
            if (!old.TryGetValue(e.Id, out var previous))
            {
                if (e.Id < Spatial.NextEntityId || e.Revision != 0) throw new ArgumentException("Reused entity identity.");
            }
            else if (oldTerrainIds.Contains(e.Id) != newTerrainIds.Contains(e.Id) ||
                byOwner.ContainsKey(e.Id) && e.Definition != previous.Definition)
                throw new ArgumentException("Entity owner kind cannot be repurposed.");
            else if (e.Revision == previous.Revision)
            {
                var before = byOwner.GetValueOrDefault(e.Id, []); var after = candidate.byOwner.GetValueOrDefault(e.Id, []);
                if (!SameEntity(e, previous) || !before.OrderBy(v => v.Position.X).ThenBy(v => v.Position.Y).ThenBy(v => v.Position.Z)
                    .SequenceEqual(after.OrderBy(v => v.Position.X).ThenBy(v => v.Position.Y).ThenBy(v => v.Position.Z)) ||
                    slopeOwners.GetValueOrDefault(e.Id) != candidate.slopeOwners.GetValueOrDefault(e.Id))
                    throw new ArgumentException("Changed entity requires revision.");
            }
            else if (e.Revision != checked(previous.Revision + 1)) throw new ArgumentException("Invalid entity revision.");
        }
        return candidate;
    }
}
