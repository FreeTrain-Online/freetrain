// ExAV 883cf7d: Car/CarState and rail/Train, TrainItem, TrainGroup.
// Original authors, LGPL-2.1-or-later. Decisions TRAIN-D002/007/008 apply.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public abstract record FleetCarState
{
    private FleetCarState() { }
    public sealed record Unplaced : FleetCarState;
    public abstract record Placed(Location Location, Heading Heading) : FleetCarState;
    public sealed record Inside(Location Location, Heading Heading) : Placed(Location, Heading);
    public sealed record Outside(Location Location, Heading Heading, int Countdown) : Placed(Location, Heading);
}
public enum TrainOperationalState { Unplaced, Moving, StoppingAtStation, StoppingAtSignal, EmergencyStopping }
public enum FleetControllerKind { Simple, Delegation, Manual, Timetable }
public sealed record FleetControllerProvider(ContentBinding Binding, FleetControllerKind Kind);
public enum FleetHarborKind { Pass, Fixed, Daily }
public sealed record FleetHarborSetting(Location Location, FleetHarborKind Kind, int Minutes, bool Reverse);
public sealed record FleetController(long Id, long Revision, string Name, FleetControllerKind Kind, ContentBinding? Provider = null,
    ImmutableArray<FleetHarborSetting> Harbors = default);
public sealed record FleetGroup(long Id, long Revision, string Name, long? ParentId, long? ControllerId,
    ImmutableArray<long> Members);
public sealed record FleetCar(long Id, long Revision, int OriginalIndex, DefinitionReference Definition, FleetCarState State);
// Cars is CURRENT head-to-tail order. OriginalIndex/Id/type survive reversal.
public sealed record FleetTrain(long Id, long Revision, string Name, ContentBinding Definition, long GroupId,
    long? ControllerId, ImmutableArray<FleetCar> Cars, bool IsReversed = false,
    TrainOperationalState State = TrainOperationalState.Unplaced, int Passengers = 0,
    FleetCarState.Placed? BoardingOrigin = null, bool Forwarding = false,
    int MoveCount = 0, int StopCallCount = 0, long? PurchaseMinute = null)
{
    public FleetCar Head => Cars[0];
    public FleetCar? Predecessor(long carId)
    {
        var index = -1;
        for (var n = 0; n < Cars.Length; n++) if (Cars[n].Id == carId) { index = n; break; }
        if (index < 0) throw new ArgumentException("Unknown consist car.");
        return index == 0 ? null : Cars[index - 1];
    }
}

// Content supplies explicitly registered stock/providers. No reflection, XML
// activation, P4 fixture-car promotion or process-global mutable definitions.
public sealed class FleetProfile
{
    public ImmutableArray<TerrainPack> Packs { get; }
    public ImmutableArray<StockTrain> Trains { get; }
    public ImmutableArray<FleetControllerProvider> Providers { get; }
    private readonly FrozenDictionary<ContentBinding, StockTrain> trains;
    public FleetProfile(IEnumerable<TerrainPack> packs, IEnumerable<StockTrain> trains,
        IEnumerable<FleetControllerProvider>? providers = null)
    {
        Packs = packs.Take(257).ToImmutableArray(); Trains = trains.Take(4097).ToImmutableArray();
        Providers = (providers ?? []).Take(129).ToImmutableArray();
        if (Packs.Length is < 1 or > 256 || Packs.Any(p => p == null) || Packs.Select(p => p.Id).Distinct().Count() != Packs.Length ||
            Trains.Length > 4096 || Trains.Any(t => t == null || !Matches(t.Binding)) ||
            Trains.Select(t => t.Binding).Distinct().Count() != Trains.Length || Providers.Length > 128 ||
            Providers.Any(p => p == null || p.Kind is not (FleetControllerKind.Manual or FleetControllerKind.Timetable) || !Matches(p.Binding)) ||
            Providers.Select(p => p.Binding).Distinct().Count() != Providers.Length)
            throw new ArgumentException("Invalid fleet profile.");
        this.trains = Trains.ToFrozenDictionary(t => t.Binding);
    }
    private bool Matches(ContentBinding b) => b != null && !string.IsNullOrWhiteSpace(b.ContributionId) &&
        Packs.Contains(new(b.PackId, b.Version, b.CatalogHash));
    public StockTrain Resolve(ContentBinding binding) => trains.TryGetValue(binding, out var t)
        ? t : throw new ArgumentException("Unavailable fleet stock/pack.");
}

// Detached graph. IDs for ALL members share SpatialState.NextEntityId with
// terrain/rail owners. Cars are components, never extra whole-voxel entities.
public sealed class FleetGraph
{
    public const int MaximumGroups = 256;
    public const int MaximumControllers = 256;
    public FleetProfile Profile { get; }
    public long RootGroupId { get; }
    public ImmutableArray<FleetTrain> Trains { get; }
    public ImmutableArray<FleetGroup> Groups { get; }
    public ImmutableArray<FleetController> Controllers { get; }
    private readonly FrozenDictionary<long, FleetTrain> trains;
    private readonly FrozenDictionary<long, FleetGroup> groups;
    private readonly FrozenDictionary<long, FleetController> controllers;
    public FleetTrain Train(long id) => trains.TryGetValue(id, out var t) ? t : throw new ArgumentException("Unknown train.");
    public FleetController Simple => Controllers.Single(c => c.Kind == FleetControllerKind.Simple);
    public IEnumerable<(long Id, long Revision)> Identities => Trains.Select(t => (t.Id, t.Revision))
        .Concat(Trains.SelectMany(t => t.Cars).Select(c => (c.Id, c.Revision)))
        .Concat(Groups.Select(g => (g.Id, g.Revision))).Concat(Controllers.Select(c => (c.Id, c.Revision)));
    public FleetGraph(FleetProfile profile, long rootGroupId, IEnumerable<FleetTrain> trains,
        IEnumerable<FleetGroup> groups, IEnumerable<FleetController> controllers)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile)); RootGroupId = rootGroupId;
        Trains = trains.Take(StockAdmission.MaximumFleetTrains + 1).ToImmutableArray();
        Groups = groups.Take(MaximumGroups + 1).ToImmutableArray();
        Controllers = controllers.Take(MaximumControllers + 1).ToImmutableArray();
        if (Trains.Length > StockAdmission.MaximumFleetTrains || Groups.Length is < 1 or > MaximumGroups ||
            Controllers.Length is < 2 or > MaximumControllers || Trains.Any(t => t == null || t.Cars.IsDefaultOrEmpty ||
                t.Cars.Length > StockAdmission.MaximumFormationCars || t.Cars.Any(c => c == null)) ||
            Groups.Any(g => g == null) || Controllers.Any(c => c == null) ||
            Trains.Sum(t => t.Cars.Length) > StockAdmission.MaximumFleetCars)
            throw new ArgumentException("Invalid fleet members/capacity.");
        var ids = Identities.ToArray();
        if (ids.Any(i => i.Id < 1 || i.Revision < 0) || ids.Select(i => i.Id).Distinct().Count() != ids.Length)
            throw new ArgumentException("Duplicate/invalid fleet identity.");
        this.trains = Trains.ToFrozenDictionary(t => t.Id); this.groups = Groups.ToFrozenDictionary(g => g.Id);
        this.controllers = Controllers.ToFrozenDictionary(c => c.Id);
        if (!this.groups.TryGetValue(rootGroupId, out var root) || root.ParentId != null ||
            Groups.Any(g => g.Id != rootGroupId && (g.ParentId == null || !this.groups.ContainsKey(g.ParentId.Value))) ||
            Controllers.Count(c => c.Kind == FleetControllerKind.Simple) != 1 ||
            Controllers.Count(c => c.Kind == FleetControllerKind.Delegation) != 1)
            throw new ArgumentException("Invalid fleet root/built-ins.");
        void Reference(long? id) { if (id != null && !this.controllers.ContainsKey(id.Value)) throw new ArgumentException("Dangling controller."); }
        foreach (var c in Controllers)
            if (!Name(c.Name) || !Enum.IsDefined(c.Kind) || (c.Kind is FleetControllerKind.Simple or FleetControllerKind.Delegation
                ? c.Provider != null : !profile.Providers.Contains(new(c.Provider!, c.Kind))))
                throw new ArgumentException("Invalid fleet controller/provider.");
        foreach (var c in Controllers)
        {
            var harbors = c.Harbors.IsDefault ? [] : c.Harbors;
            if (harbors.Length > 8192 || c.Kind != FleetControllerKind.Timetable && harbors.Length != 0 ||
                harbors.Any(h => h == null || !Enum.IsDefined(h.Kind) || h.Minutes < 0 ||
                    h.Kind == FleetHarborKind.Pass && (h.Minutes != 0 || h.Reverse) ||
                    h.Kind == FleetHarborKind.Daily && h.Minutes >= 1440) ||
                harbors.Select(h => h.Location).Distinct().Count() != harbors.Length)
                throw new ArgumentException("Invalid location-keyed harbor settings.");
        }
        foreach (var g in Groups)
        {
            if (!Name(g.Name) || g.Members.IsDefault || g.Members.Length > MaximumGroups + StockAdmission.MaximumFleetTrains ||
                g.Members.Distinct().Count() != g.Members.Length) throw new ArgumentException("Invalid group name/membership.");
            Reference(g.ControllerId);
            var expectedMembers = Groups.Where(child => child.ParentId == g.Id).Select(child => child.Id)
                .Concat(Trains.Where(t => t.GroupId == g.Id).Select(t => t.Id)).ToHashSet();
            if (!expectedMembers.SetEquals(g.Members)) throw new ArgumentException("Group membership/reference mismatch.");
            var visited = new HashSet<long>(); FleetGroup? current = g;
            while (current != null)
            {
                if (!visited.Add(current.Id)) throw new ArgumentException("Cyclic fleet groups.");
                current = current.ParentId is {} p ? this.groups[p] : null;
            }
        }
        foreach (var t in Trains)
        {
            if (!Name(t.Name) || !this.groups.ContainsKey(t.GroupId) || !Enum.IsDefined(t.State) ||
                t.MoveCount < 0 || t.StopCallCount < 0 || t.PurchaseMinute is < 0 or > GameTime.MaximumMinute)
                throw new ArgumentException("Invalid train state/reference.");
            Reference(t.ControllerId);
            var formation = profile.Resolve(t.Definition).Create(t.Cars.Length);
            if (t.Passengers < 0 || t.Passengers > formation.PackedCapacity) throw new ArgumentException("Invalid passenger count.");
            for (var n = 0; n < t.Cars.Length; n++)
            {
                var c = t.Cars[n]; var original = t.IsReversed ? t.Cars.Length - n - 1 : n;
                if (c.OriginalIndex != original || c.Definition != formation.Cars[original].Reference)
                    throw new ArgumentException("Invalid original/current consist identity.");
                ValidateState(c.State);
            }
            if (t.BoardingOrigin != null) ValidateState(t.BoardingOrigin);
            if (t.State == TrainOperationalState.Unplaced ? t.Cars.Any(c => c.State is not FleetCarState.Unplaced) :
                t.Cars.Any(c => c.State is FleetCarState.Unplaced)) throw new ArgumentException("Partially deployed train.");
        }
    }
    private static bool Name(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 256 && !s.Any(char.IsControl);
    private static void ValidateState(FleetCarState s)
    {
        if (s is not (FleetCarState.Unplaced or FleetCarState.Inside or FleetCarState.Outside) ||
            s is FleetCarState.Outside { Countdown: < 0 or > 100 }) throw new ArgumentException("Invalid car state.");
    }
    public FleetController EffectiveController(long trainId)
    {
        var t = Train(trainId); var id = t.ControllerId; FleetGroup? group = groups[t.GroupId];
        while (id is {} c && controllers[c].Kind == FleetControllerKind.Delegation && group != null)
        { id = group.ControllerId; group = group.ParentId is {} parent ? groups[parent] : null; }
        return id == null || controllers[id.Value].Kind == FleetControllerKind.Delegation ? Simple : controllers[id.Value];
    }
    // Root starts with explicit simple; no legacy null-root behavior is copied.
    public static FleetGraph Empty(FleetProfile profile, long firstId) => new(profile, checked(firstId + 2), [],
        [new(checked(firstId + 2), 0, "Root", null, firstId, [])],
        [new(firstId, 0, "Simple", FleetControllerKind.Simple), new(checked(firstId + 1), 0, "Delegation", FleetControllerKind.Delegation)]);

    internal void ValidateWorld(MixedWorld world)
    {
        var spatialIds = world.Spatial.Entities.Select(e => e.Id).ToHashSet();
        if (!Profile.Packs.OrderBy(p => p.Id, StringComparer.Ordinal).SequenceEqual(world.Terrain.Profile.Packs.OrderBy(p => p.Id, StringComparer.Ordinal)) ||
            Identities.Any(i => i.Id >= world.Spatial.NextEntityId || spatialIds.Contains(i.Id)) ||
            world.Voxels.Any(v => v.Traffic?.Rail?.ControllerId != null))
            throw new ArgumentException("Fleet pack/allocator/spatial collision.");
        var occupied = world.Voxels.Where(v => v.Traffic?.Car != null).ToDictionary(v => v.Traffic!.Car!.CarId);
        foreach (var t in Trains)
            foreach (var c in t.Cars)
            {
                if (c.State is FleetCarState.Inside inside)
                {
                    if (!occupied.Remove(c.Id, out var voxel) || voxel.Position != inside.Location || voxel.Traffic!.Rail == null ||
                        voxel.Traffic.Car != new CarComponent(c.Definition, t.Id, c.Id) ||
                        !FleetRailGuidance.CanGuide(world.Profile.Resolve(voxel.Traffic.Rail.Definition).Handler,
                            voxel.Traffic.Rail.Pattern, inside.Heading))
                        throw new ArgumentException("Fleet/Traffic occupancy mismatch.");
                }
                else if (c.State is FleetCarState.Outside outside && !world.Terrain.IsTrainBorder(outside.Location))
                    throw new ArgumentException("Outside car requires external reference.");
            }
        if (occupied.Count != 0) throw new ArgumentException("Traffic car has no Inside fleet owner.");
    }

    internal void ValidateSuccessor(FleetGraph previous, long firstNewId)
    {
        if (Profile != previous.Profile || RootGroupId != previous.RootGroupId) throw new ArgumentException("Changed fleet profile/root.");
        var old = previous.Members().ToDictionary(m => m.Id);
        foreach (var m in Members())
        {
            if (!old.TryGetValue(m.Id, out var before))
            { if (m.Id < firstNewId || m.Revision != 0) throw new ArgumentException("Reused fleet identity."); continue; }
            if (m.Value.GetType() != before.Value.GetType() ||
                m.Value is FleetTrain t && before.Value is FleetTrain b && (t.Definition != b.Definition ||
                    !t.Cars.Select(c => c.Id).Order().SequenceEqual(b.Cars.Select(c => c.Id).Order())) ||
                m.Value is FleetCar c && before.Value is FleetCar a && (c.OriginalIndex != a.OriginalIndex || c.Definition != a.Definition) ||
                m.Value is FleetController controller && before.Value is FleetController prior && (controller.Kind != prior.Kind || controller.Provider != prior.Provider))
                throw new ArgumentException("Repurposed fleet identity.");
            // Cars carry their own revisions; train equality includes their order/content.
            var equal = m.Value is FleetTrain train && before.Value is FleetTrain bt
                ? train with { Cars = bt.Cars } == bt && train.Cars.SequenceEqual(bt.Cars) :
                m.Value is FleetController fc && before.Value is FleetController bc
                ? fc with { Harbors = bc.Harbors } == bc && (fc.Harbors.IsDefault ? [] : fc.Harbors).SequenceEqual(bc.Harbors.IsDefault ? [] : bc.Harbors) :
                m.Value is FleetGroup group && before.Value is FleetGroup bg
                ? group with { Members = bg.Members } == bg && group.Members.SequenceEqual(bg.Members) : Equals(m.Value, before.Value);
            if (m.Revision == before.Revision ? !equal : m.Revision != checked(before.Revision + 1))
                throw new ArgumentException("Invalid fleet member revision.");
        }
        var oldCars = previous.Trains.SelectMany(t => t.Cars.Select(c => (c.Id, Train: t.Id))).ToDictionary(c => c.Id);
        if (Trains.Any(t => t.Cars.Any(c => oldCars.TryGetValue(c.Id, out var oldCar) && oldCar.Train != t.Id)))
            throw new ArgumentException("Car cannot change train ownership.");
        // Built-in identities cannot be deleted and recreated under new IDs.
        foreach (var c in previous.Controllers.Where(c => c.Kind is FleetControllerKind.Simple or FleetControllerKind.Delegation))
            if (!controllers.ContainsKey(c.Id)) throw new ArgumentException("Missing built-in controller.");
        if (previous.Groups.Any(g => !groups.ContainsKey(g.Id))) throw new ArgumentException("Source groups have no deletion action.");
    }
    private IEnumerable<(long Id, long Revision, object Value)> Members() => Trains.Select(t => (t.Id, t.Revision, (object)t))
        .Concat(Trains.SelectMany(t => t.Cars).Select(c => (c.Id, c.Revision, (object)c)))
        .Concat(Groups.Select(g => (g.Id, g.Revision, (object)g))).Concat(Controllers.Select(c => (c.Id, c.Revision, (object)c)));
}
