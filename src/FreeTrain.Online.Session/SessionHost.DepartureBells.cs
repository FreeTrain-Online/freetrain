using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private long bellEpoch = -1, bellSequence;
    private string liveBellEpoch = "";
    private readonly Queue<DepartureBellMessage> departureBells = new();
    private void EnsureBellEpoch()
    {
        if (bellEpoch == WorldEpoch) return;
        bellEpoch = WorldEpoch; liveBellEpoch = Guid.NewGuid().ToString("N"); bellSequence = 0;
        departureBells.Clear();
    }
    public EffectsBaselineMessage EffectsBaseline()
    {
        lock (gate) { EnsureBellEpoch(); return new(WorldEpoch,liveBellEpoch,bellSequence); }
    }
    // One bounded live outbox, drained by the transport. No durable/replay history.
    public DepartureBellMessage[] TakeDepartureBells()
    {
        lock (gate) { EnsureBellEpoch(); var result=departureBells.ToArray(); departureBells.Clear(); return result; }
    }
    private void RecordDepartureBells(long sequence)
    {
        bellSequence=sequence;
        var ordinal=0;
        foreach (var bell in mixedState?.DepartureBells ?? [])
        {
            var message=new DepartureBellMessage(WorldEpoch,liveBellEpoch,sequence,ordinal++,bell.TrainId,bell.PlatformId,
                new(bell.Bell.PackId,bell.Bell.ContributionId,bell.Bell.RecipeId),new(bell.Location.X,bell.Location.Y,bell.Location.Z));
            if (departureBells.Count == MaximumTicksPerPump * StockAdmission.MaximumFleetTrains) departureBells.Dequeue();
            departureBells.Enqueue(message);
        }
    }
}
