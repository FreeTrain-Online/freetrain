using System.Collections.Immutable;
using System.Globalization;

namespace FreeTrain.Online.Simulation;

// The dispatcher is trusted domain code, retained on detached copies. It returns
// one atomic result and its next one-shot delay; no delegate enters durable state.
// The production dispatcher supplies Inside movement; tests may bind a controlled dispatcher.
public sealed record TrainCallbackResult(MixedConstructionState Candidate, long? NextDelay)
{
    public ImmutableArray<StationPassengerEffect> PassengerEffects { get; init; } = [];
    public ImmutableArray<DepartureBellIntent> DepartureBells { get; init; } = [];
}
public delegate TrainCallbackResult TrainCallback(MixedSessionState session, long trainId);

// One world-local scheduler/random bank surrounds immutable geometry/fleet/accounts.
public sealed class MixedSessionState
{
    public MixedConstructionState Construction { get; private set; }
    public MixedWorld World => Construction.World;
    public FleetGraph? Fleet => World.Fleet;
    public TerrainProvenance Provenance { get; }
    public bool ConstructionMode { get; }
    public Clock Clock { get; private set; }
    public RandomStreams Random { get; private set; }
    public bool Paused { get; private set; }
    public long Revision { get; private set; }
    // Last successful minute only; absent from saves, snapshots and Freeze.
    // Failed minutes retain the previous publication and expose no new effects.
    public ImmutableArray<StationPassengerEffect> PassengerEffects { get; private set; } = [];
    public ImmutableArray<DepartureBellIntent> DepartureBells { get; private set; } = [];
    public void ReviseSession() => Revision = checked(Revision + 1);
    private readonly TrainCallback trainCallback;
    private readonly StationCallback stationCallback;
    public TrainCallback TrainDispatcher => trainCallback;
    public StationCallback StationDispatcher => stationCallback;
    public bool CanAdvance => Clock.Time.AbsoluteMinute < GameTime.MaximumMinute && Revision < long.MaxValue;
    public static string TrainHandler(long trainId) => "train/" + trainId.ToString(CultureInfo.InvariantCulture);
    private IReadOnlyDictionary<string, Action> Bindings(MixedWorld world, bool dispatch = true)
    {
        var result = StationaryConstruction.Bindings(world, dispatch ? TickConstruction : null).ToDictionary();
        foreach (var t in world.Fleet?.Trains ?? [])
            if (t.State != TrainOperationalState.Unplaced)
                result.Add(TrainHandler(t.Id), () => { if (dispatch) TickTrain(t.Id); });
        foreach (var timer in StationLifecycle.Timers(world.Stations))
            result.Add(timer.HandlerId, () => { if (dispatch) TickStation(timer.HandlerId); });
        return result;
    }
    public MixedSessionState(MixedConstructionState construction, TerrainProvenance provenance, bool constructionMode = false,
        ClockState? clock = null, bool paused = true, RandomStreamsState? random = null, long revision = 0,
        TrainCallback? trainCallback = null, StationCallback? stationCallback = null)
    {
        if (revision < construction.World.Revision) throw new ArgumentException("Invalid mixed session revision.");
        provenance.Validate(construction.World.Terrain);
        Construction=construction; Provenance=provenance; ConstructionMode=constructionMode; Paused=paused; Revision=revision;
        this.trainCallback=trainCallback ?? FleetMovement.Tick;
        this.stationCallback=stationCallback ?? StationStatistics.Tick;
        var bindings = Bindings(World);
        Clock = clock == null ? new(handlers:bindings) : Clock.Restore(clock,bindings);
        Random = random == null ? new(RandomStreams.ReferenceSeed) : RandomStreams.Restore(random);
        ValidateBoundary();
    }
    private void ValidateBoundary()
    {
        var clock = Clock.Capture(); var pending = clock.Pending;
        StationLifecycle.Validate(World.Stations,clock);
        var timers = StationLifecycle.Timers(World.Stations).ToDictionary(t => t.HandlerId,StringComparer.Ordinal);
        var construction=StationaryConstruction.Bindings(World);
        var deployed=(Fleet?.Trains ?? []).Where(t => t.State != TrainOperationalState.Unplaced).ToArray();
        var expected=construction.Keys.Concat(deployed.Select(t => TrainHandler(t.Id))).Concat(timers.Keys).ToHashSet(StringComparer.Ordinal);
        var sites=World.Voxels.Where(v => v.Construction != null).Select(v => v.OwnerId).Distinct().Count();
        if (pending.Length != expected.Count || pending.Any(a => !expected.Contains(a.HandlerId) || (timers.ContainsKey(a.HandlerId) ? a != timers[a.HandlerId] : a.Interval != 0) ||
                construction.ContainsKey(a.HandlerId) && a.DueMinute - Clock.Time.AbsoluteMinute > 119) ||
            pending.Select(a => a.HandlerId).Distinct().Count() != expected.Count ||
            sites > 0 && Construction.Accounts.Transactions.Length + sites > NormalRailwayConstruction.MaximumAccountTransactions)
            throw new ArgumentException("Invalid mixed callback schedule/capacity/binding.");
        if (Construction.Accounts.Transactions.Any(t => t.AbsoluteMinute > Clock.Time.AbsoluteMinute) ||
            (Fleet?.Trains ?? []).Any(t => t.PurchaseMinute > Clock.Time.AbsoluteMinute))
            throw new ArgumentException("Booking/purchase is newer than clock.");
    }
    private void TickConstruction(RailwayVoxel v)
    {
        var after=StationaryConstruction.Tick(this,v);
        if (!ReferenceEquals(after,Construction)) {Construction=after; Revision=checked(Revision+1);}
    }
    private void TickTrain(long trainId)
    {
        var before=Construction;
        var result=trainCallback(this,trainId);
        var candidate=result.Candidate ?? throw new ArgumentException("Missing train callback candidate.");
        ValidateCandidate(candidate,allowUnchanged:true);
        var after=candidate.World.Fleet?.Train(trainId) ?? throw new ArgumentException("Callback cannot sell its train.");
        if (after.State == TrainOperationalState.Unplaced ? result.NextDelay != null : result.NextDelay is null or <= 0)
            throw new ArgumentException("Invalid train callback continuation.");
        if (result.NextDelay is {} delay) Clock.Schedule(TrainHandler(trainId),delay);
        Construction=candidate;
        PassengerEffects=PassengerEffects.AddRange(result.PassengerEffects);
        DepartureBells=DepartureBells.AddRange(result.DepartureBells);
        if (!ReferenceEquals(before,candidate)) Revision=checked(Revision+1);
    }
    private void TickStation(string handlerId)
    {
        var timer=StationLifecycle.Timers(World.Stations).Single(t => t.HandlerId == handlerId);
        var candidate=stationCallback(this,handlerId);
        ValidateCandidate(candidate);
        var expected=timer with {Order=Clock.NextOrder,DueMinute=checked(Clock.Time.AbsoluteMinute+timer.Interval)};
        if (!StationLifecycle.Timers(candidate.World.Stations).Contains(expected)) throw new ArgumentException("Invalid station callback continuation.");
        Construction=candidate; Revision=checked(Revision+1);
    }
    // Terrain projection for mixed encoding/authoring; fleet and queue are carried
    // separately by MixedWire. These inert bindings never execute simulation.
    public TerrainSessionState CaptureTerrain()
    {
        return new(World.Terrain,Provenance,ConstructionMode,Clock.Capture(),Paused,Random.Capture(),Revision,Bindings(World,false));
    }
    public MixedSessionState Freeze() => new(Construction,Provenance,ConstructionMode,Clock.Capture(),Paused,Random.Capture(),Revision,trainCallback,stationCallback);
    public void SetPaused(bool paused)
    { if (Paused==paused) return; var next=checked(Revision+1); Paused=paused; Revision=next; }
    public void AdvanceOneMinute()
    {
        // Validate and rebind the entire detached successor BEFORE publication.
        // Any bad callback, booking, RNG use or queue overflow discards the batch.
        var next=Freeze(); next.Clock.Advance(); next.Revision=checked(next.Revision+1);
        var passengerEffects=next.PassengerEffects; var departureBells=next.DepartureBells;
        next=next.Freeze();
        var clock=Clock.Restore(next.Clock.Capture(),Bindings(next.World));
        Construction=next.Construction; Random=next.Random; Revision=next.Revision; Clock=clock;
        PassengerEffects=passengerEffects; DepartureBells=departureBells;
    }
    private void ValidateCandidate(MixedConstructionState candidate, bool allowUnchanged = false)
    {
        if (ReferenceEquals(candidate.World,World))
        {
            if (!allowUnchanged || !ReferenceEquals(candidate.Accounts,Construction.Accounts))
                throw new ArgumentException("Invalid unchanged mixed successor.");
            return;
        }
        if (candidate.World.Profile != World.Profile || candidate.World.Revision != checked(World.Revision+1))
            throw new ArgumentException("Invalid mixed successor.");
        if (Fleet != null && candidate.World.Fleet == null) throw new ArgumentException("Cannot discard fleet graph.");
        if (World.Stations != null && candidate.World.Stations == null) throw new ArgumentException("Cannot discard station graph.");
        World.Replace(World.Revision,candidate.World.Terrain,candidate.World.Spatial.Capture(),candidate.World.Voxels,candidate.World.Slopes,
            candidate.World.SpeedLimits,candidate.World.Fleet,candidate.World.Stations);
        var before=Construction.Accounts; var after=candidate.Accounts;
        if (before.InitialCash != after.InitialCash || before.Settings != after.Settings ||
            after.Transactions.Length < before.Transactions.Length ||
            !after.Transactions.Take(before.Transactions.Length).SequenceEqual(before.Transactions) ||
            after.Transactions.Skip(before.Transactions.Length).Any(t => t.WorldRevision != candidate.World.Revision || t.AbsoluteMinute != Clock.Time.AbsoluteMinute))
            throw new ArgumentException("Invalid booking successor.");
    }
    public MixedSessionState Successor(long expectedRevision, MixedConstructionState candidate, bool constructionMode,
        RandomStreamsState? random = null, ClockState? clock = null)
    {
        if (Revision != expectedRevision) throw new InvalidOperationException("staleRevision");
        ValidateCandidate(candidate);
        var active=Bindings(candidate.World,false);
        var previous=Bindings(World,false);
        var schedule=clock ?? Clock.Capture();
        var beforeClock=Clock.Capture();
        if (schedule.AbsoluteMinute != beforeClock.AbsoluteMinute || schedule.NextOrder < beforeClock.NextOrder)
            throw new ArgumentException("Invalid successor clock boundary.");
        StationLifecycle.ValidateSuccessor(World.Stations,candidate.World.Stations,beforeClock);
        // Cancel ONLY callbacks owned by removed trains/sites/stations/platforms. Unknown
        // payloads must reject, rather than disappearing through a broad filter.
        schedule=schedule with {Pending=schedule.Pending.Where(a => !previous.ContainsKey(a.HandlerId) || active.ContainsKey(a.HandlerId)).ToImmutableArray()};
        return new(candidate,Provenance,constructionMode,schedule,Paused,random ?? Random.Capture(),checked(Revision+1),trainCallback,stationCallback);
    }
}
