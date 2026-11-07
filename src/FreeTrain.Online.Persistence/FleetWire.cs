using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

// No type names/delegates/provider activation from input. Resolve exact installed
// definitions, then validate the whole detached graph and its reciprocal occupancy.
public static class FleetWire
{
    public static FleetRuleRead Rule(FleetTimetableRule r) => new(r.Month,r.Day,r.DayOfWeek,r.Hour,r.Minutes,Tag(r.Action));
    public static FleetTimetableRule Rule(FleetRuleRead r) => new(r.Month,r.Day,r.DayOfWeek,r.Hour,r.Minutes,EnumValue<FleetRuleAction>(r.Action));
    private static FleetRuleRead[]? Rules(ImmutableArray<FleetTimetableRule> rules) => rules.IsDefaultOrEmpty?null:rules.Select(Rule).ToArray();
    private static ImmutableArray<FleetTimetableRule> Rules(FleetRuleRead[]? rules) => (rules??[]).Select(Rule).ToImmutableArray();
    private static string Tag<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
    private static T EnumValue<T>(string value) where T : struct, Enum =>
        Enum.GetValues<T>().Single(v => Tag(v) == value);
    private static ContentBindingRead Binding(ContentBinding b) => new(b.PackId,b.Version,b.CatalogHash,b.ContributionId);
    private static ContentBinding Binding(ContentBindingRead b) => new(b.PackId,b.Version,b.CatalogHash,b.ContributionId);
    private static PositionRead Position(Location p) => new(p.X,p.Y,p.Z);
    private static Location Position(PositionRead p) => new(p.X,p.Y,p.Z);
    private static FleetPoseRead Pose(FleetCarState s) => s switch
    {
        FleetCarState.Unplaced => new("unplaced",null,null,null),
        FleetCarState.Inside i => new("inside",Position(i.Location),i.Heading.Index,null),
        FleetCarState.Outside o => new("outside",Position(o.Location),o.Heading.Index,o.Countdown),
        _ => throw new ArgumentException("Unknown fleet pose.")
    };
    private static FleetCarState Pose(FleetPoseRead s) => s switch
    {
        { Kind: "unplaced", Location: null, Heading: null, Countdown: null } => new FleetCarState.Unplaced(),
        { Kind: "inside", Location: {} p, Heading: >= 0 and < 8, Countdown: null } => new FleetCarState.Inside(Position(p),new(s.Heading.Value)),
        { Kind: "outside", Location: {} p, Heading: >= 0 and < 8, Countdown: >= 0 and <= 100 } => new FleetCarState.Outside(Position(p),new(s.Heading.Value),s.Countdown.Value),
        _ => throw new ArgumentException("Malformed fleet pose.")
    };
    public static FleetColorTestRead Color(FleetColorTest c)=>new(c.Revision,new(c.Template.PackId,c.Template.ContributionId,c.Template.RecipeId),c.Body,c.Line1,c.Line2,c.Line3);
    public static FleetColorTest Color(FleetColorTestRead c)=>new(c.Revision,new(c.Template.PackId,c.Template.ContributionId,c.Template.RecipeId),c.Body,c.Line1,c.Line2,c.Line3);
    public static FleetRead? Read(FleetGraph? f) => f == null ? null : new(f.RootGroupId,
        f.Trains.Select(t => new FleetTrainRead(t.Id,t.Revision,t.Name,Binding(t.Definition),t.GroupId,t.ControllerId,
            t.Cars.Select(c => new FleetCarRead(c.Id,c.Revision,c.OriginalIndex,
                new(c.Definition.PackId,c.Definition.ContributionId,c.Definition.RecipeId),Pose(c.State))).ToArray(),
            t.IsReversed,Tag(t.State),t.Passengers,t.BoardingOrigin == null ? null : Pose(t.BoardingOrigin),
            t.Forwarding,t.MoveCount,t.StopCallCount,t.PurchaseMinute)).ToArray(),
        f.Groups.Select(g => new FleetGroupRead(g.Id,g.Revision,g.Name,g.ParentId,g.ControllerId,g.Members.ToArray())).ToArray(),
        f.Controllers.Select(c => new FleetControllerRead(c.Id,c.Revision,c.Name,Tag(c.Kind),c.Provider == null ? null : Binding(c.Provider),
            (c.Harbors.IsDefault ? [] : c.Harbors).Select(h => new FleetHarborRead(Position(h.Location),Tag(h.Kind),h.Minutes,h.Reverse,Rules(h.Rules))).ToArray(),
            c.Junctions.IsDefaultOrEmpty ? null : c.Junctions.Select(j => new FleetJunctionRead(Position(j.Location),Tag(j.DefaultRoute),Rules(j.Rules))).ToArray())).ToArray(),f.ColorTest==null?null:Color(f.ColorTest));

    public static FleetGraph? Restore(FleetRead? f, FleetProfile? profile)
    {
        if (f == null) return null;
        if (profile == null) throw new InvalidDataException("incompatibleSave");
        if (f.Trains.Length > StockAdmission.MaximumFleetTrains || f.Groups.Length > FleetGraph.MaximumGroups ||
            f.Controllers.Length > FleetGraph.MaximumControllers || f.Trains.Any(t => t.Cars.Length > StockAdmission.MaximumFormationCars) ||
            f.Trains.Sum(t => t.Cars.Length) > StockAdmission.MaximumFleetCars || f.Controllers.Any(c => c.Harbors.Length > 8192 || c.Junctions?.Length > 8192 || c.Junctions?.Any(j => j == null || j.Location == null) == true))
            throw new ArgumentException("Fleet wire limit.");
        foreach(var c in f.Controllers) {
            var rules=c.Harbors.Select(h=>h.Rules).Concat((c.Junctions??[]).Select(j=>j.Rules)).ToArray();
            if(rules.Any(r=>r is {Length: > FleetTimetable.MaximumLocationRules} || r?.Any(v=>v==null)==true) ||
                rules.Sum(r=>r?.Length??0)>FleetTimetable.MaximumControllerRules)throw new ArgumentException("Timetable wire limit.");
        }
        return new(profile,f.RootGroupId,
            f.Trains.Select(t => new FleetTrain(t.Id,t.Revision,t.Name,Binding(t.Definition),t.GroupId,t.ControllerId,
                t.Cars.Select(c => new FleetCar(c.Id,c.Revision,c.OriginalIndex,
                    new(c.Definition.PackId,c.Definition.ContributionId,c.Definition.RecipeId),Pose(c.State))).ToImmutableArray(),
                t.IsReversed,EnumValue<TrainOperationalState>(t.State),t.Passengers,
                t.BoardingOrigin == null ? null : (Pose(t.BoardingOrigin) as FleetCarState.Placed ?? throw new ArgumentException("Unplaced boarding origin.")),
                t.Forwarding,t.MoveCount,t.StopCallCount,t.PurchaseMinute)),
            f.Groups.Select(g => new FleetGroup(g.Id,g.Revision,g.Name,g.ParentId,g.ControllerId,g.Members.ToImmutableArray())),
            f.Controllers.Select(c => new FleetController(c.Id,c.Revision,c.Name,EnumValue<FleetControllerKind>(c.Kind),
                c.Provider == null ? null : Binding(c.Provider),c.Harbors.Select(h => new FleetHarborSetting(Position(h.Location),
                    EnumValue<FleetHarborKind>(h.Kind),h.Minutes,h.Reverse,Rules(h.Rules))).ToImmutableArray(),
                (c.Junctions ?? []).Select(j => new FleetJunctionSetting(Position(j.Location),EnumValue<FleetJunctionRoute>(j.DefaultRoute),Rules(j.Rules))).ToImmutableArray())),f.ColorTest==null?null:Color(f.ColorTest));
    }
    public static MixedQueueRead Queue(ClockState clock) => new(clock.NextOrder,
        clock.Pending.Select(a => new MixedActionRead(a.Registration,a.Order,a.DueMinute,a.HandlerId,a.Interval)).ToArray());
    public static void ValidateQueue(MixedRead mixed, ClockState clock)
    {
        if ((mixed.Fleet == null) != (mixed.Queue == null)) throw new ArgumentException("Fleet queue required.");
        if (mixed.Queue is {} q && (q.NextOrder != clock.NextOrder || !q.Pending.SequenceEqual(Queue(clock).Pending) ||
            !clock.Pending.SequenceEqual(clock.Pending.OrderBy(a => a.DueMinute).ThenBy(a => a.Order))))
            throw new ArgumentException("Mixed queue/clock mismatch.");
    }
}
