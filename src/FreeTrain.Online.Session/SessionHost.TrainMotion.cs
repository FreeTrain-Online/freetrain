using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    public const int MaximumMotionSteps = 128;
    public const int MaximumMotionCars = 8192;
    private readonly Queue<TrainMotionStepRead> trainMotion = new();
    private long motionEpoch = -1, motionSequence;
    private int motionCars;
    private void EnsureMotionEpoch()
    {
        if (motionEpoch == WorldEpoch) return;
        motionEpoch = WorldEpoch; motionSequence = 0; motionCars = 0; trainMotion.Clear();
    }
    private TrainMotionRead ReadTrainMotion()
    {
        EnsureMotionEpoch();
        return new(motionSequence, false, trainMotion.ToArray());
    }
    // Called only AFTER the detached minute commits. The history is bounded by
    // both operations and car count, and cannot change simulation or save bytes.
    private void RecordTrainMotion(MixedWorld before, MixedSessionState after)
    {
        EnsureMotionEpoch();
        if (before.Fleet == null || after.Fleet == null || ReferenceEquals(before.Fleet,after.Fleet)) return;
        foreach (var train in after.Fleet.Trains)
        {
            var prior = before.Fleet.Trains.FirstOrDefault(t => t.Id == train.Id);
            if (prior == null) continue;
            if (prior.IsReversed == train.IsReversed && prior.Cars.Select(c => (c.Id,c.State)).SequenceEqual(train.Cars.Select(c => (c.Id,c.State)))) continue;
            var cars = new List<TrainMotionCarRead>();
            var continuous = prior.IsReversed == train.IsReversed && train.MoveCount == prior.MoveCount + 1 &&
                prior.Cars.Select(c => c.Id).SequenceEqual(train.Cars.Select(c => c.Id));
            for (var i = 0; continuous && i < train.Cars.Length; i++)
            {
                var from = MotionPose(before, prior, i); var to = MotionPose(after.World, train, i);
                if (from == null || to == null || from.Position.Z != to.Position.Z ||
                    Math.Abs(from.Position.X-to.Position.X) > 1 || Math.Abs(from.Position.Y-to.Position.Y) > 1 ||
                    from.Position == to.Position) { cars.Clear(); break; }
                cars.Add(new(train.Cars[i].Id, from, to));
            }
            // An empty Cars array is an explicit discontinuity marker. Retain
            // it in sequence even if another move follows in this publication.
            var pending = after.Clock.Capture().Pending.FirstOrDefault(a => a.HandlerId == MixedSessionState.TrainHandler(train.Id));
            var duration = (int)Math.Clamp((pending?.DueMinute ?? after.Clock.Time.AbsoluteMinute+1)-after.Clock.Time.AbsoluteMinute,1,4);
            var step = new TrainMotionStepRead(++motionSequence,after.Clock.Time.AbsoluteMinute,train.Id,duration,cars.ToArray());
            trainMotion.Enqueue(step); motionCars += cars.Count;
            while (trainMotion.Count > MaximumMotionSteps || motionCars > MaximumMotionCars)
                motionCars -= trainMotion.Dequeue().Cars.Length;
        }
    }
    private static TrainMotionPoseRead? MotionPose(MixedWorld world, FleetTrain train, int index)
    {
        if (train.Cars[index].State is not FleetCarState.Inside pose || world.At(pose.Location)?.Traffic?.Rail is not {} rail) return null;
        var handler = world.Profile.Resolve(rail.Definition).Handler;
        // Tunnel foregrounds are authored for voxel anchors. Until their
        // continuous interior masks are proven, preserve exact discrete hiding.
        if (handler is RailwayHandler.Slope or RailwayHandler.Tunnel) return null;
        var predecessor = index == 0 ? (Location?)null : (train.Cars[index-1].State as FleetCarState.Inside)?.Location;
        var outgoing = FleetRailGuidance.Guide(handler,rail.Pattern,pose.Heading,predecessor:predecessor,location:pose.Location);
        return new(new(pose.Location.X,pose.Location.Y,pose.Location.Z),pose.Heading.Index,outgoing.Index,train.IsReversed);
    }
}
