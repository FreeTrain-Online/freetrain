using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

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
    public SpeedLimits SpeedLimits { get; }
    public FleetGraph? Fleet { get; }
    public StationGraph? Stations { get; }
    public CityGraph? City { get; }
    private readonly FrozenDictionary<Location, RailwayVoxel> voxels;
    private readonly FrozenDictionary<long, ImmutableArray<RailwayVoxel>> byOwner;
    private readonly FrozenDictionary<long, SlopeOwnership> slopeOwners;
    private readonly FrozenDictionary<long, SpatialEntity> entities;
    // Share membership across replacements of the same immutable terrain. Weak
    // keys and values containing only entities cannot retain obsolete worlds.
    private static readonly ConditionalWeakTable<TerrainWorld, FrozenDictionary<long, SpatialEntity>> TerrainOwners = new();
    private readonly FrozenDictionary<long, SpatialEntity> terrainOwners;
    // Each world owns a unique token. A completed replacement retains only its
    // predecessor's token, which cannot keep that world or its history alive.
    private readonly object validationIdentity = new();
    private object? previousValidationIdentity;
    internal bool IsValidatedSuccessorOf(MixedWorld previous) =>
        ReferenceEquals(previousValidationIdentity, previous.validationIdentity);
    public RailwayVoxel? At(Location p) => voxels.GetValueOrDefault(p);

    public MixedWorld(RailwayProfile profile, TerrainWorld terrain, SpatialState spatial,
        IEnumerable<RailwayVoxel> voxels, IEnumerable<SlopeOwnership> slopes, long revision = 0, SpeedLimits? speedLimits = null,
        FleetGraph? fleet = null, StationGraph? stations = null, CityGraph? city = null)
        : this(profile, terrain, spatial, voxels, slopes, revision, speedLimits, fleet, stations, city, null) { }

    private MixedWorld(RailwayProfile profile, TerrainWorld terrain, SpatialState spatial,
        IEnumerable<RailwayVoxel> voxels, IEnumerable<SlopeOwnership> slopes, long revision, SpeedLimits? speedLimits,
        FleetGraph? fleet, StationGraph? stations, CityGraph? city, SpatialIndex? previousSpatial)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(spatial);
        if (spatial.Entities.IsDefault || profile.Terrain != terrain.Profile || revision < terrain.Revision ||
            spatial.NextEntityId < terrain.Spatial.NextEntityId)
            throw new ArgumentException("Invalid mixed-world profile/revision/allocator.");
        Profile = profile; Terrain = terrain; Revision = revision; Fleet = fleet; Stations = stations; City = city;
        Voxels = voxels.Take(SpatialIndex.MaximumOccupiedCells + 1).ToImmutableArray();
        Slopes = slopes.Take(SpatialIndex.MaximumOccupiedCells / 8 + 1).ToImmutableArray();
        if (Voxels.Length > SpatialIndex.MaximumOccupiedCells || Voxels.Any(v => v is null || !Enum.IsDefined(v.Kind)) ||
            Voxels.Select(v => v.Position).Distinct().Count() != Voxels.Length ||
            Slopes.Length > SpatialIndex.MaximumOccupiedCells / 8 || Slopes.Any(s => s is null) ||
            Slopes.Select(s => s.EntityId).Distinct().Count() != Slopes.Length)
            throw new ArgumentException("Invalid mixed-world members.");
        Spatial = previousSpatial?.WithState(spatial) ?? new(terrain.Space, spatial.NextEntityId, spatial.Entities);
        this.voxels = Voxels.ToFrozenDictionary(v => v.Position);
        byOwner = Voxels.GroupBy(v => v.OwnerId).ToFrozenDictionary(g => g.Key, g => g.ToImmutableArray());
        terrainOwners = TerrainOwners.GetValue(terrain, static t => t.Spatial.Entities.ToFrozenDictionary(e => e.Id));
        var matchedTerrainOwners = 0;
        slopeOwners = Slopes.ToFrozenDictionary(s => s.EntityId);
        var slopeIds = Slopes.ToDictionary(s => s.EntityId);
        entities = Spatial.Entities.ToFrozenDictionary(e => e.Id);
        foreach (var e in Spatial.Entities)
        {
            if (terrainOwners.TryGetValue(e.Id, out var te))
            {
                matchedTerrainOwners++;
                if (!SameEntity(e, te) || byOwner.ContainsKey(e.Id)) throw new ArgumentException("Terrain identity was overwritten.");
                continue;
            }
            if (Stations?.IsSpatialOwner(e.Id) == true)
            {
                if (byOwner.ContainsKey(e.Id)) throw new ArgumentException("Station cannot own railway payloads.");
                continue;
            }
            if (City?.IsSpatialOwner(e.Id) == true)
            {
                if (byOwner.ContainsKey(e.Id)) throw new ArgumentException("City cannot own railway payloads.");
                continue;
            }
            if (!byOwner.TryGetValue(e.Id, out var members) || e.Owned != (members[0].Construction == null) || e.SilentlyReclaimable || e.Value != 0 ||
                members.Length != e.Footprint.Members.Length)
                throw new ArgumentException("Invalid railway owner.");
            // Traffic, EmptyVoxel slope fillers/supports/walls and piers all
            // report source transparency. None is silently reclaimable.
            foreach (var (p, transparent) in e.Footprint.At(e.Anchor))
                if (At(p) is not {} v || v.OwnerId != e.Id || members[0].Kind != RailwayVoxelKind.Stationary && !transparent)
                    throw new ArgumentException("Railway footprint/payload mismatch.");
            if (members[0].Kind == RailwayVoxelKind.Stationary) StationaryConstruction.ValidateOwner(this,e,members);
            else if (slopeIds.Remove(e.Id, out var slope)) ValidateSlope(e, slope, members);
            else if (members.Length != 1 || members[0].Position != e.Anchor ||
                members[0].Kind is not (RailwayVoxelKind.Traffic or RailwayVoxelKind.Pier) ||
                e.Definition != (members[0].Kind == RailwayVoxelKind.Traffic ? profile.TrafficOwner : members[0].Definition))
                throw new ArgumentException("Invalid standalone railway owner.");
        }
        if (matchedTerrainOwners != terrainOwners.Count || slopeIds.Count != 0 || byOwner.Keys.Any(id => !entities.ContainsKey(id)))
            throw new ArgumentException("Missing mixed-world owner.");
        foreach (var v in Voxels) ValidateVoxel(v);
        SpeedLimits=speedLimits ?? SpeedLimits.Empty;
        if(SpeedLimits.Low.IsDefault||SpeedLimits.Medium.IsDefault||SpeedLimits.Fast.IsDefault||
            new[]{SpeedLimits.Low.Length,SpeedLimits.Medium.Length,SpeedLimits.Fast.Length}.Any(n=>n>NormalRailwayConstruction.MaximumRailwayVoxels)||
            SpeedLimits.Locations.Any(p=>At(p)?.Traffic==null))throw new ArgumentException("Invalid speed-limit membership.");
        foreach (var (p, backup) in terrain.TunnelBackups)
            if (At(p)?.Traffic?.Rail is not {} r || profile.Resolve(r.Definition).Handler != RailwayHandler.Tunnel || r.TerrainBackup != backup)
                throw new ArgumentException("Missing tunnel boundary owner.");
        var cars = Voxels.Where(v => v.Traffic?.Car != null).Select(v => v.Traffic!.Car!).ToArray();
        if (cars.Select(c => c.CarId).Distinct().Count() != cars.Length)
            throw new ArgumentException("Car occupies multiple voxels.");
        Fleet?.ValidateWorld(this);
        Stations?.ValidateWorld(this);
        City?.ValidateWorld(this);
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
        if (v.Kind == RailwayVoxelKind.Stationary)
        {
            if (v.Traffic != null || v.Definition == null || Profile.Resolve(v.Definition).Handler != RailwayHandler.Stationary ||
                v.Sprite != 0 || v.SupportedOwnerId != null || !v.DrawGroundAbove || !v.DrawGroundBelow ||
                v.Construction is {} c && (c.State is < 0 or > 4 || c.Ground is < 0 or > 6))
                throw new ArgumentException("Invalid stationary voxel.");
            return;
        }
        if (v.Construction != null) throw new ArgumentException("Unexpected construction state.");
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
                var column = Terrain.Space.ToStorage(v.Position);
                if (v.Position.Z != Terrain.Cell(column.H,column.V).Ground)
                    throw new ArgumentException("Tunnel must replace a surface Mountain.");
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
        if (t.Road is {} road && (City?.Profile.Road(road.Definition) is {} ?
                road.Edges<1 || (road.Edges&~0x55)!=0 : Profile.Resolve(road.Definition).Handler != RailwayHandler.Road || road.Edges is < 1 or > 255))
            throw new ArgumentException("Invalid road component.");
        if (t.Accessory is {} a && (Profile.Resolve(a.Definition).Handler is not (RailwayHandler.Pole or RailwayHandler.Crossing) ||
            a.Direction is < 0 or > 7 || Profile.Resolve(a.Definition).Handler == RailwayHandler.Pole && a.Direction > 1)) throw new ArgumentException("Invalid accessory component.");
        if (t.Accessory is {} crossing && Profile.Resolve(crossing.Definition).Handler == RailwayHandler.Crossing &&
            (t.Rail == null || t.Road == null ||
                !TrafficLifecycle.HasCrossing(t)))
            throw new ArgumentException("Invalid crossing components.");
        if (t.Car is {} car && (Fleet == null && Profile.Resolve(car.Definition).Handler != RailwayHandler.Car || car.TrainId < 1 || car.CarId < 1))
            throw new ArgumentException("Invalid car reference.");
        if (t.Platform is {} platform && (t.Rail == null ||
            !(Stations != null && Profile.Stations?.IsPlatformArt(platform) == true) && Profile.Resolve(platform).Handler != RailwayHandler.Platform))
            throw new ArgumentException("Invalid platform component.");
    }

    private static bool SameEntity(SpatialEntity a, SpatialEntity b) =>
        ReferenceEquals(a, b) || a.Id == b.Id && a.Revision == b.Revision && a.Definition == b.Definition &&
        a.Anchor == b.Anchor && a.Owned == b.Owned && a.SilentlyReclaimable == b.SilentlyReclaimable && a.Value == b.Value &&
        (ReferenceEquals(a.Footprint, b.Footprint) || a.Footprint.Members.AsSpan().SequenceEqual(b.Footprint.Members.AsSpan()));

    private bool SameRailwayPayloads(MixedWorld candidate, long id)
    {
        var before = byOwner.GetValueOrDefault(id, []);
        var after = candidate.byOwner.GetValueOrDefault(id, []);
        if (before == after) return true;
        if (before.Length != after.Length) return false;
        // Position is a unique, stable index in each validated world. Input
        // enumeration order is not part of a railway owner's payload identity.
        foreach (var voxel in before)
            if (candidate.At(voxel.Position) != voxel) return false;
        return true;
    }

    private HashSet<long> ChangedOwnerDependencies(MixedWorld candidate)
    {
        var changed = new HashSet<long>();
        foreach (var voxel in Voxels)
            if (candidate.At(voxel.Position) != voxel) changed.Add(voxel.OwnerId);
        foreach (var voxel in candidate.Voxels)
            if (At(voxel.Position) != voxel) changed.Add(voxel.OwnerId);
        foreach (var slope in Slopes)
            if (candidate.slopeOwners.GetValueOrDefault(slope.EntityId) != slope) changed.Add(slope.EntityId);
        foreach (var slope in candidate.Slopes)
            if (slopeOwners.GetValueOrDefault(slope.EntityId) != slope) changed.Add(slope.EntityId);

        if (!ReferenceEquals(Stations, candidate.Stations))
        {
            AddStationDependencies(Stations);
            AddStationDependencies(candidate.Stations);
        }
        if (!ReferenceEquals(City, candidate.City))
        {
            AddCityDependencies(City);
            AddCityDependencies(candidate.City);
        }
        return changed;

        void AddStationDependencies(StationGraph? graph)
        {
            if (graph == null) return;
            foreach (var identity in graph.Identities) changed.Add(identity.Id);
            foreach (var passage in graph.Passages) changed.Add(passage.TrafficOwnerId);
            foreach (var listener in graph.Listeners) changed.Add(listener.ProviderId);
        }
        void AddCityDependencies(CityGraph? graph)
        {
            if (graph == null) return;
            foreach (var identity in graph.Identities) changed.Add(identity.Id);
            foreach (var fence in graph.State.Fences) changed.Add(fence.HostOwnerId);
        }
    }

    // Both constructors have checked unique IDs, footprints and occupancy. Compare
    // geometry by owner before expanding cells: revisions and component changes
    // alone do not assign/remove voxels. Never trust a shared entity to certify roads.
    private HashSet<Location> ConductivityChanges(MixedWorld candidate)
    {
        var columns = new HashSet<Location>();
        foreach (var owner in candidate.Spatial.Entities)
        {
            if (entities.TryGetValue(owner.Id, out var previous) &&
                owner.Anchor == previous.Anchor &&
                (ReferenceEquals(owner.Footprint, previous.Footprint) ||
                 owner.Footprint.Members.AsSpan().SequenceEqual(previous.Footprint.Members.AsSpan()))) continue;
            AddChangedCells(owner);
            if (previous != null) AddChangedCells(previous);
        }
        foreach (var owner in Spatial.Entities)
            if (!candidate.entities.ContainsKey(owner.Id)) AddChangedCells(owner);
        foreach (var voxel in candidate.Voxels)
            if (voxel.Traffic?.Road is {} road && At(voxel.Position)?.Traffic?.Road?.Definition != road.Definition)
                columns.Add(voxel.Position with { Z = 0 });
        return columns;

        void AddChangedCells(SpatialEntity owner)
        {
            foreach (var (position, _) in owner.Footprint.At(owner.Anchor))
                if (Spatial.At(position).Entity?.Id != candidate.Spatial.At(position).Entity?.Id)
                    columns.Add(position with { Z = 0 });
        }
    }

    // Candidate validation and identity checks finish before a replacement is returned.
    public MixedWorld Replace(long expectedRevision, TerrainWorld terrain, SpatialState spatial,
        IEnumerable<RailwayVoxel> voxels, IEnumerable<SlopeOwnership> slopes, SpeedLimits? speedLimits = null, FleetGraph? fleet = null,
        StationGraph? stations = null, CityGraph? city = null)
    {
        if (expectedRevision != Revision) throw new InvalidOperationException("staleRevision");
        if (spatial.NextEntityId < Spatial.NextEntityId) throw new ArgumentException("Allocator rewind.");
        var members=voxels.ToImmutableArray();
        var limits=speedLimits ?? SpeedLimits.Clean(members.Where(v=>v.Traffic!=null).Select(v=>v.Position).ToHashSet());
        var candidate = new MixedWorld(Profile, terrain, spatial, members, slopes, checked(Revision + 1),limits,fleet ?? Fleet,stations ?? Stations,city ?? City,
            ReferenceEquals(terrain, Terrain) ? Spatial : null);
        if (City != null)
        {
            var notified=CityHeat.NotifyChanges(candidate,ConductivityChanges(candidate));
            if (!ReferenceEquals(notified,candidate.City)) candidate=new(Profile,terrain,spatial,members,candidate.Slopes,candidate.Revision,
                limits,candidate.Fleet,candidate.Stations,notified,candidate.Spatial);
        }
        if (City != null) candidate.City!.ValidateSuccessor(City,Spatial.NextEntityId);
        else if (candidate.City != null && candidate.City.Identities.Any(i => i.Id < Spatial.NextEntityId || i.Revision != 0))
            throw new ArgumentException("Reused initial city identity.");
        if (Stations != null) candidate.Stations!.ValidateSuccessor(Stations,Spatial.NextEntityId);
        else if (candidate.Stations != null && candidate.Stations.Identities.Any(i => i.Id < Spatial.NextEntityId || i.Revision != 0))
            throw new ArgumentException("Reused initial station identity.");
        if (Fleet != null) candidate.Fleet!.ValidateSuccessor(Fleet,Spatial.NextEntityId);
        else if (candidate.Fleet != null && candidate.Fleet.Identities.Any(i => i.Id < Spatial.NextEntityId || i.Revision != 0))
            throw new ArgumentException("Reused initial fleet identity.");
        if (!ReferenceEquals(terrain, Terrain))
        {
            if (terrain.Profile != Terrain.Profile || terrain.Name != Terrain.Name ||
                terrain.Space.HorizontalSize != Terrain.Space.HorizontalSize || terrain.Space.VerticalSize != Terrain.Space.VerticalSize ||
                terrain.Space.Levels != Terrain.Space.Levels || terrain.Revision != checked(Terrain.Revision + 1))
                throw new ArgumentException("Invalid terrain successor.");
            if (!terrain.IsValidatedSuccessorOf(Terrain))
                Terrain.Replace(Terrain.Revision, terrain.Cells, terrain.WaterLevel, terrain.Spatial.Capture(), terrain.Occupants, terrain.TunnelBackups);
        }
        var changedDependencies = ChangedOwnerDependencies(candidate);
        foreach (var e in candidate.Spatial.Entities)
        {
            // Sharing an entity certifies only its own fields. Independently
            // check every payload/dependency that can change beneath that owner.
            if (entities.TryGetValue(e.Id, out var previous) && ReferenceEquals(e, previous) &&
                ReferenceEquals(Terrain, terrain) && !changedDependencies.Contains(e.Id)) continue;
            if (previous == null)
            {
                if (e.Id < Spatial.NextEntityId || e.Revision != 0) throw new ArgumentException("Reused entity identity.");
            }
            else if (terrainOwners.ContainsKey(e.Id) != candidate.terrainOwners.ContainsKey(e.Id) ||
                (Stations?.IsSpatialOwner(e.Id) == true) != (candidate.Stations?.IsSpatialOwner(e.Id) == true) ||
                (City?.IsSpatialOwner(e.Id) == true) != (candidate.City?.IsSpatialOwner(e.Id) == true) ||
                byOwner.ContainsKey(e.Id) && e.Definition != previous.Definition)
                throw new ArgumentException("Entity owner kind cannot be repurposed.");
            else if (e.Revision == previous.Revision)
            {
                if (!SameEntity(e, previous) || !SameRailwayPayloads(candidate, e.Id) ||
                    slopeOwners.GetValueOrDefault(e.Id) != candidate.slopeOwners.GetValueOrDefault(e.Id) ||
                    Stations?.Passage(e.Id) != candidate.Stations?.Passage(e.Id) ||
                    Stations?.Provider(e.Id) != candidate.Stations?.Provider(e.Id) ||
                    candidate.City?.SameFences(City,e.Id) == false ||
                    !(Stations?.ListenerStations(e.Id) ?? []).AsSpan().SequenceEqual((candidate.Stations?.ListenerStations(e.Id) ?? []).AsSpan()))
                    throw new ArgumentException("Changed entity requires revision.");
            }
            else if (e.Revision != checked(previous.Revision + 1)) throw new ArgumentException("Invalid entity revision.");
        }
        // Certify this exact result only after every successor check (including
        // any heat-notified reconstruction) has completed successfully.
        candidate.previousValidationIdentity = validationIdentity;
        return candidate;
    }

    public MixedWorld EnableFleet(FleetProfile profile)
    {
        if (Fleet != null) throw new InvalidOperationException("Fleet already initialized.");
        var fleet = FleetGraph.Empty(profile,Spatial.NextEntityId);
        return Replace(Revision,Terrain,new(checked(Spatial.NextEntityId+3),Spatial.Entities),Voxels,Slopes,fleet:fleet);
    }
}
