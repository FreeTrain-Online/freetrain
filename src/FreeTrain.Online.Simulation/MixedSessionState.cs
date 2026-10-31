namespace FreeTrain.Online.Simulation;

// One session clock/random bank surrounds the immutable geometry/account unit.
public sealed class MixedSessionState
{
    public MixedConstructionState Construction { get; private set; }
    public MixedWorld World => Construction.World;
    private TerrainSessionState header;
    public TerrainProvenance Provenance => header.Provenance;
    public bool ConstructionMode => header.ConstructionMode;
    public Clock Clock => header.Clock;
    public RandomStreams Random => header.Random;
    public bool Paused => header.Paused;
    public long Revision => header.Revision;
    public bool CanAdvance => header.CanAdvance;
    public MixedSessionState(MixedConstructionState construction, TerrainProvenance provenance, bool constructionMode = false,
        ClockState? clock = null, bool paused = true, RandomStreamsState? random = null, long revision = 0)
    {
        if (revision < construction.World.Revision) throw new ArgumentException("Invalid mixed session revision.");
        Construction = construction;
        header = new(construction.World.Terrain,provenance,constructionMode,clock,paused,random,revision);
        if (construction.Accounts.Transactions.Any(t => t.AbsoluteMinute > Clock.Time.AbsoluteMinute))
            throw new ArgumentException("Booking is newer than clock.");
    }
    public TerrainSessionState CaptureTerrain() => new(World.Terrain,Provenance,ConstructionMode,
        Clock.Capture(),Paused,Random.Capture(),Revision);
    public MixedSessionState Freeze() => new(Construction,Provenance,ConstructionMode,Clock.Capture(),Paused,Random.Capture(),Revision);
    public void SetPaused(bool paused) => header.SetPaused(paused);
    public void AdvanceOneMinute() => header.AdvanceOneMinute();
    // Trusted tool handoff; no public endpoint accepts a candidate graph or price.
    public MixedSessionState Successor(long expectedRevision, MixedConstructionState candidate, bool constructionMode,
        RandomStreamsState? random = null)
    {
        if (Revision != expectedRevision) throw new InvalidOperationException("staleRevision");
        if (candidate.World.Profile != World.Profile || candidate.World.Revision != checked(World.Revision + 1))
            throw new ArgumentException("Invalid mixed successor.");
        World.Replace(World.Revision,candidate.World.Terrain,candidate.World.Spatial.Capture(),candidate.World.Voxels,candidate.World.Slopes);
        var before = Construction.Accounts; var after = candidate.Accounts;
        if (before.InitialCash != after.InitialCash || before.Settings != after.Settings ||
            after.Transactions.Length < before.Transactions.Length ||
            !after.Transactions.Take(before.Transactions.Length).SequenceEqual(before.Transactions) ||
            after.Transactions.Skip(before.Transactions.Length).Any(t => t.WorldRevision != candidate.World.Revision || t.AbsoluteMinute != Clock.Time.AbsoluteMinute))
            throw new ArgumentException("Invalid booking successor.");
        return new(candidate,Provenance,constructionMode,Clock.Capture(),Paused,random ?? Random.Capture(),checked(Revision + 1));
    }
}
