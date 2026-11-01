using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// One world-local scheduler/random bank surrounds immutable geometry/accounts.
public sealed class MixedSessionState
{
    public MixedConstructionState Construction { get; private set; }
    public MixedWorld World => Construction.World;
    public TerrainProvenance Provenance { get; }
    public bool ConstructionMode { get; }
    public Clock Clock { get; private set; }
    public RandomStreams Random { get; private set; }
    public bool Paused { get; private set; }
    public long Revision { get; private set; }
    public bool CanAdvance => Clock.Time.AbsoluteMinute < GameTime.MaximumMinute && Revision < long.MaxValue;
    public MixedSessionState(MixedConstructionState construction, TerrainProvenance provenance, bool constructionMode = false,
        ClockState? clock = null, bool paused = true, RandomStreamsState? random = null, long revision = 0)
    {
        if (revision < construction.World.Revision) throw new ArgumentException("Invalid mixed session revision.");
        provenance.Validate(construction.World.Terrain);
        Construction=construction; Provenance=provenance; ConstructionMode=constructionMode; Paused=paused; Revision=revision;
        var bindings = StationaryConstruction.Bindings(World, TickConstruction);
        Clock = clock == null ? new(handlers:bindings) : Clock.Restore(clock,bindings);
        Random = random == null ? new(RandomStreams.ReferenceSeed) : RandomStreams.Restore(random);
        var pending = Clock.Capture().Pending;
        var sites=World.Voxels.Where(v => v.Construction != null).Select(v => v.OwnerId).Distinct().Count();
        if (pending.Length != bindings.Count || pending.Any(a => a.Interval != 0 || a.DueMinute > Clock.Time.AbsoluteMinute+119) ||
            pending.Select(a => a.HandlerId).Distinct().Count() != bindings.Count ||
            sites > 0 && construction.Accounts.Transactions.Length + sites > NormalRailwayConstruction.MaximumAccountTransactions)
            throw new ArgumentException("Invalid construction schedule/capacity.");
        if (construction.Accounts.Transactions.Any(t => t.AbsoluteMinute > Clock.Time.AbsoluteMinute))
            throw new ArgumentException("Booking is newer than clock.");
    }
    private void TickConstruction(RailwayVoxel v)
    {
        var after=StationaryConstruction.Tick(this,v);
        if (!ReferenceEquals(after,Construction)) {Construction=after; Revision=checked(Revision+1);}
    }
    public TerrainSessionState CaptureTerrain() => new(World.Terrain,Provenance,ConstructionMode,
        Clock.Capture(),Paused,Random.Capture(),Revision,StationaryConstruction.Bindings(World));
    public MixedSessionState Freeze() => new(Construction,Provenance,ConstructionMode,Clock.Capture(),Paused,Random.Capture(),Revision);
    public void SetPaused(bool paused)
    { if (Paused==paused) return; var next=checked(Revision+1); Paused=paused; Revision=next; }
    public void AdvanceOneMinute()
    {
        // Run callbacks against a detached boundary. Overflow or invalid booking
        // never publishes a partial clock, progress update or consumed RNG.
        var next=Freeze(); next.Clock.Advance(); next.Revision=checked(next.Revision+1);
        Construction=next.Construction; Random=next.Random; Revision=next.Revision;
        Clock=Clock.Restore(next.Clock.Capture(),StationaryConstruction.Bindings(World,TickConstruction));
    }
    public MixedSessionState Successor(long expectedRevision, MixedConstructionState candidate, bool constructionMode,
        RandomStreamsState? random = null, ClockState? clock = null)
    {
        if (Revision != expectedRevision) throw new InvalidOperationException("staleRevision");
        if (candidate.World.Profile != World.Profile || candidate.World.Revision != checked(World.Revision+1))
            throw new ArgumentException("Invalid mixed successor.");
        World.Replace(World.Revision,candidate.World.Terrain,candidate.World.Spatial.Capture(),candidate.World.Voxels,candidate.World.Slopes);
        var before=Construction.Accounts; var after=candidate.Accounts;
        if (before.InitialCash != after.InitialCash || before.Settings != after.Settings ||
            after.Transactions.Length < before.Transactions.Length ||
            !after.Transactions.Take(before.Transactions.Length).SequenceEqual(before.Transactions) ||
            after.Transactions.Skip(before.Transactions.Length).Any(t => t.WorldRevision != candidate.World.Revision || t.AbsoluteMinute != Clock.Time.AbsoluteMinute))
            throw new ArgumentException("Invalid booking successor.");
        // Explicit cancellation discards only the removed site's one-shot actions.
        var active=StationaryConstruction.Bindings(candidate.World);
        var schedule=clock ?? Clock.Capture();
        schedule=schedule with {Pending=schedule.Pending.Where(a => active.ContainsKey(a.HandlerId)).ToImmutableArray()};
        return new(candidate,Provenance,constructionMode,schedule,Paused,random ?? Random.Capture(),checked(Revision+1));
    }
}
