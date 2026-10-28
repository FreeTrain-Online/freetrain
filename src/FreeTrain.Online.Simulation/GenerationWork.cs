namespace FreeTrain.Online.Simulation;

// One instance per detached job, one worker at a time. Consumers debit before
// doing bounded batches, including retries, and never reset the meter on repair.
public sealed class GenerationWork
{
    public const int MaxBatch = 4096;
    private readonly CancellationToken cancellation;
    public long Remaining { get; private set; }
    public GenerationWork(WorldCreationRequest request, CancellationToken cancellation = default)
    { Remaining = WorldCreationProfile.Estimate(request).WorkUnits; this.cancellation = cancellation; }
    public void Spend(int units)
    {
        cancellation.ThrowIfCancellationRequested();
        if (units is <= 0 or > MaxBatch) throw new ArgumentOutOfRangeException(nameof(units));
        if (units > Remaining) throw new InvalidOperationException("generation-work-limit");
        Remaining -= units;
    }
}
