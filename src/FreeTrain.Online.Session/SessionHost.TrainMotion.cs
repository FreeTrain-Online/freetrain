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
                var origin = MotionPose(before, prior, i); var to = MotionPose(after.World, train, i);
                if (origin == null || to == null ||
                    Math.Abs(origin.Position.X-to.Position.X) > 1 || Math.Abs(origin.Position.Y-to.Position.Y) > 1 ||
                    origin.Position == to.Position) { cars.Clear(); break; }
                // Record the committed controller-selected junction route.
                // The committed destination heading records the exit
                // actually taken, for the head and every follower alike.
                var (dx,dy)=new Heading(to.Heading).Offset;
                var turn=(to.Heading-origin.Heading+8)%8;
                var dz = origin.Slope is {} slope
                    ? SlopeRailwayConstruction.ZDiff(28+slope.Climb*2+slope.Level,new(origin.Heading)) : 0;
                if(to.Position.X-origin.Position.X!=dx || to.Position.Y-origin.Position.Y!=dy ||
                    to.Position.Z-origin.Position.Z!=dz || turn is not (0 or 1 or 7))
                { cars.Clear(); break; }
                origin=origin with {Outgoing=to.Heading};
                cars.Add(new(train.Cars[i].Id, origin, to));
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
        var predecessor = index == 0 ? (Location?)null : (train.Cars[index-1].State as FleetCarState.Inside)?.Location;
        var outgoing = FleetRailGuidance.Guide(handler,rail.Pattern,pose.Heading,predecessor:predecessor,location:pose.Location);
        var slope = handler == RailwayHandler.Slope ? SlopeRailwayConstruction.Pattern(rail.Pattern) : default;
        return new(new(pose.Location.X,pose.Location.Y,pose.Location.Z),pose.Heading.Index,outgoing.Index,train.IsReversed,
            handler == RailwayHandler.Slope ? new(slope.Climb.Index,slope.Level) : null);
    }
}
