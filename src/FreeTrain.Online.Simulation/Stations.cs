// Portable detached state from ExAV 883cf7d Station, Platform, Thin/FatPlatform.
// Original authors: Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record StationTransportLog(int Today = 0, int Yesterday = 0, double ThisWeek = 0, double LastWeekPerDay = 0);
public sealed record StationPassengers(int Gone = 0, int AccumulatedLoaded = 0, int AccumulatedUnloaded = 0);
public sealed record StationBuilding(long Id, long Revision, DefinitionReference Definition, Location Location, string Name,
    ImmutableArray<long> Children, StationPassengers Passengers, StationTransportLog Imports, StationTransportLog Exports,
    StationTransportLog Trains, ScheduledAction? HourTimer = null, ScheduledAction? DayTimer = null);
public enum PlatformKind { Thin, Wide }
public enum PlatformOutlookKind { Plain, Passage, Stair }
public sealed record PlatformOutlook(PlatformOutlookKind Kind = PlatformOutlookKind.Plain,
    PassageMaterial Material = PassageMaterial.Concrete, bool Bridge = false, bool Upward = false);
// A yard is a rail component in another entity's Traffic voxel, never a second
// whole-voxel owner. Index/lane are retained independently of its screen art.
public sealed record PlatformYard(long TrafficOwnerId, Location Location, int Lane, int Index, bool Roof, PlatformOutlook Outlook);
public sealed record StationPlatform(long Id, long Revision, PlatformKind Kind, Location Location, Heading Direction,
    int Length, string Name, bool Roof, bool Lane0, bool Lane1, DefinitionReference Bell,
    long? HostId, ImmutableArray<long> Children, ImmutableArray<PlatformYard> Yards, ScheduledAction? DayTimer = null);
public sealed record StationPassage(long TrafficOwnerId, Location Location, Heading Direction, PassageMaterial Material);

// Source catchment edges are deliberately asymmetric (STATION-D003).
public sealed record StationListener(long ProviderId, ImmutableArray<long> Stations, StationProvider? Provider = null);
public sealed record StationListenerMembership(long StationId, ImmutableArray<long> Providers);

public sealed class StationGraph
{
    public StationProfile Profile { get; }
    public ImmutableArray<StationBuilding> Buildings { get; }
    public ImmutableArray<StationPlatform> Platforms { get; }
    public ImmutableArray<StationPassage> Passages { get; }
    public ImmutableArray<StationListener> Listeners { get; }
    public ImmutableArray<StationListenerMembership> ListenerMemberships { get; }
    public int NextStationName { get; }
    public int NextPlatformName { get; }
    public bool IsEmpty => Buildings.IsEmpty && Platforms.IsEmpty && Passages.IsEmpty && Listeners.IsEmpty && ListenerMemberships.IsEmpty;
    private readonly FrozenDictionary<long, StationBuilding> buildings;
    private readonly FrozenDictionary<long, StationPlatform> platforms;
    private readonly FrozenDictionary<long, long?> resolved;
    private readonly FrozenDictionary<long, StationPassage> passages;
    private readonly FrozenDictionary<long, StationListener> listeners;
    private readonly FrozenDictionary<long, (StationPlatform Platform, PlatformYard Yard)> yards;
    public IEnumerable<(long Id, long Revision)> Identities => Buildings.Select(b => (b.Id,b.Revision)).Concat(Platforms.Select(p => (p.Id,p.Revision)));
    public StationBuilding Building(long id) => buildings.TryGetValue(id,out var b) ? b : throw new ArgumentException("Unknown service station.");
    public StationPlatform Platform(long id) => platforms.TryGetValue(id,out var p) ? p : throw new ArgumentException("Unknown platform.");
    public StationBuilding? HostStation(long platformId) => resolved[platformId] is {} id ? buildings[id] : null;
    internal ImmutableArray<long> ListenerStations(long providerId) => listeners.GetValueOrDefault(providerId)?.Stations ?? [];
    internal StationProvider? Provider(long providerId) => listeners.GetValueOrDefault(providerId)?.Provider;
    internal StationPassage? Passage(long trafficOwnerId) => passages.GetValueOrDefault(trafficOwnerId);
    internal (StationPlatform Platform, PlatformYard Yard)? Yard(long trafficOwnerId) =>
        yards.TryGetValue(trafficOwnerId, out var yard) ? yard : null;
    internal bool IsSpatialOwner(long id) => buildings.ContainsKey(id) || platforms.TryGetValue(id,out var p) && p.Kind == PlatformKind.Wide;

    public StationGraph(StationProfile profile, IEnumerable<StationBuilding> buildings, IEnumerable<StationPlatform> platforms,
        IEnumerable<StationPassage>? passages = null, int nextStationName = 1, int nextPlatformName = 1,
        IEnumerable<StationListener>? listeners = null, IEnumerable<StationListenerMembership>? listenerMemberships = null)
    {
        ArgumentNullException.ThrowIfNull(profile); Profile = profile;
        Buildings = buildings.Take(SpatialIndex.MaximumOccupiedCells+1).ToImmutableArray();
        Platforms = platforms.Take(SpatialIndex.MaximumOccupiedCells+1).ToImmutableArray();
        Passages = (passages ?? []).Take(SpatialIndex.MaximumOccupiedCells+1).ToImmutableArray();
        Listeners = (listeners ?? []).Take(SpatialIndex.MaximumOccupiedCells+1).ToImmutableArray();
        ListenerMemberships = (listenerMemberships ?? []).Take(SpatialIndex.MaximumOccupiedCells+1).ToImmutableArray();
        NextStationName = nextStationName; NextPlatformName = nextPlatformName;
        if (nextStationName < 1 || nextPlatformName < 1 || Buildings.Length + (long)Platforms.Length + Passages.Length > SpatialIndex.MaximumOccupiedCells ||
            Buildings.Any(b => b == null) || Platforms.Any(p => p == null) || Passages.Any(p => p == null))
            throw new ArgumentException("Invalid station members.");
        var ids = Identities.ToArray();
        if (ids.Any(i => i.Id < 1 || i.Revision < 0) || ids.Select(i => i.Id).Distinct().Count() != ids.Length)
            throw new ArgumentException("Duplicate/invalid station identity.");
        this.buildings = Buildings.ToFrozenDictionary(b => b.Id); this.platforms = Platforms.ToFrozenDictionary(p => p.Id);
        foreach (var b in Buildings)
        {
            Profile.Resolve(b.Definition); Name(b.Name); Children(b.Children);
            if (b.Passengers == null || b.Passengers.Gone < 0 || b.Passengers.AccumulatedLoaded < 0 || b.Passengers.AccumulatedUnloaded < 0 ||
                (b.HourTimer == null) != (b.DayTimer == null)) throw new ArgumentException("Invalid station counters/timers.");
            foreach (var log in new[] { b.Imports,b.Exports,b.Trains })
                if (log == null || log.Today < 0 || log.Yesterday < 0 || !double.IsFinite(log.ThisWeek) || log.ThisWeek < 0 ||
                    !double.IsFinite(log.LastWeekPerDay) || log.LastWeekPerDay < 0) throw new ArgumentException("Invalid station transport log.");
            Timer(b.HourTimer,$"station/{b.Id}/hour",60); Timer(b.DayTimer,$"station/{b.Id}/day",1440);
        }
        foreach (var p in Platforms)
        {
            Name(p.Name); Children(p.Children); Profile.Bell(p.Bell);
            if (!Enum.IsDefined(p.Kind) || !p.Direction.IsCardinal || p.Length < 1 || p.Length > NormalRailwayConstruction.MaximumRailwayVoxels ||
                p.Kind == PlatformKind.Thin && (!p.Lane0 || p.Lane1) || p.Kind == PlatformKind.Wide && p.Roof || p.Yards.IsDefault ||
                p.Yards.Length != p.Length * ((p.Lane0 ? 1 : 0) + (p.Lane1 ? 1 : 0)) ||
                p.HostId == p.Id || p.HostId is {} host && !this.buildings.ContainsKey(host) && !this.platforms.ContainsKey(host))
                throw new ArgumentException("Invalid platform geometry/host.");
            var keys = new HashSet<(int Lane,int Index)>();
            foreach (var y in p.Yards)
            {
                if (y == null || y.TrafficOwnerId < 1 || y.Lane is < 0 or > 1 || y.Index < 0 || y.Index >= p.Length ||
                    !(y.Lane == 0 ? p.Lane0 : p.Lane1) || !keys.Add((y.Lane,y.Index)) ||
                    y.Location != YardLocation(p,y.Lane,y.Index) ||
                    y.Roof != (p.Kind == PlatformKind.Thin && p.Roof && y.Index >= p.Length/4 && y.Index < p.Length-p.Length/4) ||
                    y.Outlook == null || !Enum.IsDefined(y.Outlook.Kind) || !Enum.IsDefined(y.Outlook.Material) ||
                    p.Kind == PlatformKind.Wide && y.Outlook != new PlatformOutlook() ||
                    y.Outlook.Kind == PlatformOutlookKind.Plain && y.Outlook != new PlatformOutlook() ||
                    y.Outlook.Kind == PlatformOutlookKind.Passage && y.Outlook.Upward ||
                    y.Outlook.Kind == PlatformOutlookKind.Stair && y.Outlook.Bridge)
                    throw new ArgumentException("Malformed platform yard/outlook.");
            }
            Timer(p.DayTimer,$"platform/{p.Id}/day",1440);
        }
        // Validate both directions, so no dangling, duplicate or omitted child can
        // be hidden by reconstructing a different host tree at load time.
        var children = Buildings.Select(b => (b.Id,b.Children)).Concat(Platforms.Select(p => (p.Id,p.Children))).ToDictionary(p => p.Id,p => p.Children);
        foreach (var (id, list) in children)
            foreach (var child in list)
                if (!this.platforms.TryGetValue(child,out var p) || p.HostId != id) throw new ArgumentException("Invalid host child reference.");
        foreach (var p in Platforms)
            if (p.HostId is {} host && !children[host].Contains(p.Id)) throw new ArgumentException("Missing reciprocal host reference.");
        var roots = new Dictionary<long,long?>();
        foreach (var p in Platforms)
        {
            var path = new HashSet<long>(); long? current = p.Id;
            while (current is {} id && !this.buildings.ContainsKey(id) && !roots.ContainsKey(id))
            {
                if (!path.Add(id)) throw new ArgumentException("Cyclic platform hosts.");
                current = this.platforms[id].HostId;
            }
            var root = current is {} last ? this.buildings.ContainsKey(last) ? last : roots[last] : null;
            foreach (var id in path) roots.Add(id,root);
        }
        if (Platforms.Any(p => p.HostId != null && roots[p.Id] == null)) throw new ArgumentException("Unresolved platform host.");
        resolved = roots.ToFrozenDictionary();
        var yards = Platforms.SelectMany(p => p.Yards).ToArray();
        if (yards.Select(y => y.Location).Distinct().Count() != yards.Length || yards.Select(y => y.TrafficOwnerId).Distinct().Count() != yards.Length ||
            Passages.Any(p => p.TrafficOwnerId < 1 || !p.Direction.IsCardinal || !Enum.IsDefined(p.Material)) ||
            Passages.Select(p => p.Location).Distinct().Count() != Passages.Length ||
            Passages.Select(p => p.TrafficOwnerId).Distinct().Count() != Passages.Length ||
            Passages.Any(p => yards.Any(y => y.Location == p.Location || y.TrafficOwnerId == p.TrafficOwnerId)))
            throw new ArgumentException("Duplicate/invalid yard or passage membership.");
        var timers = Buildings.SelectMany(b => new[] {b.HourTimer,b.DayTimer}).Concat(Platforms.Select(p => p.DayTimer)).OfType<ScheduledAction>().ToArray();
        if (timers.Select(t => t.Registration).Distinct().Count() != timers.Length || timers.Select(t => t.Order).Distinct().Count() != timers.Length)
            throw new ArgumentException("Duplicate station timer handle/order.");
        this.passages = Passages.ToFrozenDictionary(p => p.TrafficOwnerId);
        this.yards = Platforms.SelectMany(p => p.Yards.Select(y => (Platform: p, Yard: y)))
            .ToFrozenDictionary(y => y.Yard.TrafficOwnerId);
        var listenerIds = Listeners.Where(l => l != null).Select(l => l.ProviderId).ToHashSet();
        if (Listeners.Length > SpatialIndex.MaximumOccupiedCells || ListenerMemberships.Length > Buildings.Length ||
            Listeners.Any(l => l == null || l.ProviderId < 1 || l.Stations.IsDefault || l.Stations.Length > 4 ||
                l.Stations.Distinct().Count() != l.Stations.Length || l.Stations.Any(id => !this.buildings.ContainsKey(id))) ||
            Listeners.Select(l => l.ProviderId).Distinct().Count() != Listeners.Length ||
            ListenerMemberships.Any(l => l == null || !this.buildings.ContainsKey(l.StationId) || l.Providers.IsDefault ||
                l.Providers.Distinct().Count() != l.Providers.Length || l.Providers.Any(id => !listenerIds.Contains(id))) ||
            ListenerMemberships.Select(l => l.StationId).Distinct().Count() != ListenerMemberships.Length)
            throw new ArgumentException("Invalid ordered station listeners.");
        this.listeners = Listeners.ToFrozenDictionary(l => l.ProviderId);
        foreach (var listener in Listeners) listener.Provider?.Validate();
        if (ListenerMemberships.Any(m => m.Providers.Any(id => this.listeners[id].Provider != null && !this.listeners[id].Stations.Contains(m.StationId))))
            throw new ArgumentException("Bound reverse listener must have a local station link.");

    }
    private static void Name(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl)) throw new ArgumentException("Invalid station/platform name.");
    }
    private static void Children(ImmutableArray<long> children)
    {
        if (children.IsDefault || children.Any(c => c < 1) || children.Distinct().Count() != children.Length) throw new ArgumentException("Invalid host children.");
    }
    private static void Timer(ScheduledAction? timer, string handler, long interval)
    {
        if (timer != null && (timer.Registration < 0 || timer.Order < timer.Registration || timer.DueMinute < 0 ||
            timer.DueMinute > GameTime.MaximumMinute || timer.HandlerId != handler || timer.Interval != interval))
            throw new ArgumentException("Invalid station/platform timer.");
    }
    public static Location CenterLocation(StationPlatform p, int index)
    {
        var (x,y) = p.Direction.Offset;
        return new(checked(p.Location.X+x*index),checked(p.Location.Y+y*index),p.Location.Z);
    }
    public static Location YardLocation(StationPlatform p, int lane, int index) => p.Kind == PlatformKind.Thin
        ? CenterLocation(p,index) : CenterLocation(p,index).Move(lane == 0 ? p.Direction.Right90 : p.Direction.Left90);
    internal void ValidateWorld(MixedWorld world)
    {
        if (world.Profile.Stations != Profile) throw new ArgumentException("Station profile mismatch.");
        var entities = world.Spatial.Entities.ToDictionary(e => e.Id);
        if (Listeners.Any(l => !entities.ContainsKey(l.ProviderId) || IsSpatialOwner(l.ProviderId)))
            throw new ArgumentException("Dangling station listener provider.");
        var fleetIds = world.Fleet?.Identities.Select(i => i.Id).ToHashSet() ?? [];
        if (Identities.Any(i => i.Id >= world.Spatial.NextEntityId || fleetIds.Contains(i.Id))) throw new ArgumentException("Station allocator collision.");
        void Owner(long id, long revision, Location location, DefinitionReference definition, Footprint footprint)
        {
            if (!entities.TryGetValue(id,out var e) || e.Revision != revision || e.Anchor != location || e.Definition != definition ||
                !e.Owned || e.SilentlyReclaimable || e.Value != 0 || !e.Footprint.Members.ToHashSet().SetEquals(footprint.Members) ||
                e.Footprint.At(e.Anchor).Any(c => world.At(c.Position) != null)) throw new ArgumentException("Invalid station spatial owner.");
        }
        foreach (var b in Buildings) Owner(b.Id,b.Revision,b.Location,b.Definition,Profile.Resolve(b.Definition).Footprint);
        var yardPositions = new HashSet<Location>();
        foreach (var p in Platforms)
        {
            if (p.Kind == PlatformKind.Wide)
            {
                var art = Profile.Recipe(StationArtKind.WideCenter,p.Direction.Index/2%2);
                Owner(p.Id,p.Revision,p.Location,art.Reference,new(Enumerable.Range(0,p.Length).Select(i =>
                { var q = CenterLocation(p,i); return new VoxelMember(new(q.X-p.Location.X,q.Y-p.Location.Y,0),false); })));
            }
            else if (entities.ContainsKey(p.Id)) throw new ArgumentException("Thin platform cannot own a whole voxel.");
            foreach (var y in p.Yards)
            {
                var v = world.At(y.Location); var rail = v?.Traffic?.Rail;
                var art = p.Kind == PlatformKind.Thin ? Profile.ThinRecipe(p.Direction,y.Roof) : Profile.Recipe(StationArtKind.Yard,0);
                if (v?.Kind != RailwayVoxelKind.Traffic || v.OwnerId != y.TrafficOwnerId || rail == null ||
                    world.Profile.Resolve(rail.Definition).Handler != RailwayHandler.Normal ||
                    NormalRailPatterns.Ports[rail.Pattern] is var ports &&
                    (ports.A != p.Direction.Index && ports.B != p.Direction.Index || (ports.A+4)%8 != ports.B) ||
                    v.Traffic!.Platform != art.Reference) throw new ArgumentException("Yard/Traffic geometry mismatch.");
                yardPositions.Add(y.Location);
            }
        }
        if (world.Voxels.Any(v => v.Traffic?.Platform is {} art && Profile.IsPlatformArt(art) && !yardPositions.Contains(v.Position)))
            throw new ArgumentException("Orphan platform rail.");
        foreach (var p in Passages)
            if (world.At(p.Location) is not {Kind: RailwayVoxelKind.Traffic, Traffic.Rail: {} rail} v ||
                v.OwnerId != p.TrafficOwnerId || v.Traffic.Platform != null || world.Profile.Resolve(rail.Definition).Handler != RailwayHandler.Normal ||
                NormalRailPatterns.Ports[rail.Pattern] is var ports &&
                ((ports.A+4)%8 != ports.B || ports.A != p.Direction.Index && ports.B != p.Direction.Index))
                throw new ArgumentException("Passage/Traffic mismatch.");
    }
    internal void ValidateSuccessor(StationGraph previous, long firstNewId)
    {
        if (Profile != previous.Profile || NextStationName < previous.NextStationName || NextPlatformName < previous.NextPlatformName)
            throw new ArgumentException("Changed station profile/name allocator.");
        var old = previous.Buildings.Select(b => (b.Id,b.Revision,Value:(object)b)).Concat(previous.Platforms.Select(p => (p.Id,p.Revision,Value:(object)p))).ToDictionary(m => m.Id);
        foreach (var b in Buildings)
        {
            var before = previous.Buildings.FirstOrDefault(a => a.Id == b.Id);
            if (before != null && b.Revision == before.Revision &&
                !(ListenerMemberships.FirstOrDefault(l => l.StationId == b.Id)?.Providers ?? []).SequenceEqual(
                    previous.ListenerMemberships.FirstOrDefault(l => l.StationId == b.Id)?.Providers ?? []))
                throw new ArgumentException("Station listener changes require revision.");
        }
        foreach (var m in Buildings.Select(b => (b.Id,b.Revision,Value:(object)b)).Concat(Platforms.Select(p => (p.Id,p.Revision,Value:(object)p))))
        {
            if (!old.TryGetValue(m.Id,out var before))
            { if (m.Id < firstNewId || m.Revision != 0) throw new ArgumentException("Reused station identity."); continue; }
            var equal = m.Value switch
            {
                StationBuilding b when before.Value is StationBuilding a => b with { Children=a.Children } == a && b.Children.SequenceEqual(a.Children),
                StationPlatform p when before.Value is StationPlatform a => p with { Children=a.Children,Yards=a.Yards } == a &&
                    p.Children.SequenceEqual(a.Children) && p.Yards.SequenceEqual(a.Yards),
                _ => false
            };
            if (m.Value.GetType() != before.Value.GetType() ||
                m.Value is StationBuilding b2 && before.Value is StationBuilding a2 && (b2.Definition != a2.Definition || b2.Location != a2.Location) ||
                m.Value is StationPlatform p2 && before.Value is StationPlatform q2 && (p2.Kind != q2.Kind || p2.Location != q2.Location || p2.Direction != q2.Direction || p2.Length != q2.Length || p2.Roof != q2.Roof) ||
                (m.Revision == before.Revision ? !equal : m.Revision != checked(before.Revision+1)))
                throw new ArgumentException("Repurposed/changed station identity requires revision.");
        }
    }
}
