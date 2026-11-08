// Stable ExAV 883cf7d Station/Platform timer ownership. Original authors,
// LGPL-2.1-or-later. P6-17 supplies the station hour/day passenger/log dispatcher.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record StationSchedule(StationGraph Graph, ClockState Clock);
public delegate MixedConstructionState StationCallback(MixedSessionState session, string handlerId);

public static class StationLifecycle
{
    public static IEnumerable<ScheduledAction> Timers(StationGraph? graph) => graph == null ? [] :
        graph.Buildings.SelectMany(b => new[] {b.HourTimer,b.DayTimer}).Concat(graph.Platforms.Select(p => p.DayTimer)).OfType<ScheduledAction>();

    public static void Validate(StationGraph? graph, ClockState clock)
    {
        var timers=Timers(graph).ToDictionary(t=>t.HandlerId,StringComparer.Ordinal);
        var pending=clock.Pending.GroupBy(t=>t.HandlerId).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        if (pending.Keys.Any(h => (h.StartsWith("station/",StringComparison.Ordinal) || h.StartsWith("platform/",StringComparison.Ordinal)) && !timers.ContainsKey(h)))
            throw new ArgumentException("Unowned station timer.");
        foreach (var timer in timers.Values)
        {
            if (!pending.TryGetValue(timer.HandlerId,out var entries) || entries.Length != 1 || entries[0] != timer ||
                timer.DueMinute <= clock.AbsoluteMinute || timer.DueMinute-clock.AbsoluteMinute > timer.Interval ||
                timer.HandlerId.StartsWith("station/",StringComparison.Ordinal) && timer.DueMinute % timer.Interval != 0)
                throw new ArgumentException("Invalid station timer queue/alignment.");
        }
    }

    // Pure registration/cancellation on a detached copy of the existing FIFO clock.
    // Callers retain candidate geometry; no ConstructionSite, RNG or live mutation.
    public static StationSchedule Reconcile(StationGraph? previous, StationGraph candidate, ClockState state)
    {
        Validate(previous,state);
        var old = Timers(previous).ToDictionary(t => t.HandlerId,StringComparer.Ordinal);
        var oldBuildingIds=previous?.Buildings.Select(b=>b.Id).ToHashSet() ?? [];
        var oldPlatformIds=previous?.Platforms.Select(p=>p.Id).ToHashSet() ?? [];
        var newBuildings = candidate.Buildings.Where(b => !oldBuildingIds.Contains(b.Id)).Select(b=>b.Id).ToHashSet();
        var newPlatforms = candidate.Platforms.Where(p => !oldPlatformIds.Contains(p.Id)).Select(p=>p.Id).ToHashSet();
        if (candidate.Buildings.Any(b => newBuildings.Contains(b.Id) && (b.HourTimer != null || b.DayTimer != null)) ||
            candidate.Platforms.Any(p => newPlatforms.Contains(p.Id) && p.DayTimer != null))
            throw new ArgumentException("New station timers must be registered here.");
        var retained = candidate.Buildings.Where(b => !newBuildings.Contains(b.Id)).SelectMany(b => new[] {b.HourTimer,b.DayTimer})
            .Concat(candidate.Platforms.Where(p => !newPlatforms.Contains(p.Id)).Select(p => p.DayTimer)).OfType<ScheduledAction>().ToArray();
        var retainedIds = candidate.Identities.Select(i => i.Id).ToHashSet();
        var expected = Timers(previous).Where(t => retainedIds.Contains(OwnerId(t))).ToArray();
        if (!retained.ToHashSet().SetEquals(expected)) throw new ArgumentException("Cannot rewrite retained station timers.");
        var handlers = state.Pending.Select(t => t.HandlerId).Concat(candidate.Buildings.SelectMany(b => new[] {$"station/{b.Id}/hour",$"station/{b.Id}/day"}))
            .Concat(candidate.Platforms.Select(p => $"platform/{p.Id}/day")).Distinct(StringComparer.Ordinal).ToDictionary(h => h,_ => (Action)(() => {}));
        var clock = Clock.Restore(state,handlers);
        foreach (var timer in old.Values.Except(expected)) clock.Unregister(timer.Registration);
        ScheduledAction Register(string handler, long delay, long interval)
        {
            var registration = clock.ScheduleRepeated(handler,delay,interval);
            return new(registration,registration,checked(state.AbsoluteMinute+delay),handler,interval);
        }
        var buildings = candidate.Buildings.Select(b => !newBuildings.Contains(b.Id) ? b : b with {
            HourTimer=Register($"station/{b.Id}/hour",60-state.AbsoluteMinute%60,60),
            DayTimer=Register($"station/{b.Id}/day",1440-state.AbsoluteMinute%1440,1440) }).ToImmutableArray();
        var platforms = candidate.Platforms.Select(p => !newPlatforms.Contains(p.Id) ? p : p with {
            DayTimer=Register($"platform/{p.Id}/day",1440,1440) }).ToImmutableArray();
        return new(Copy(candidate,buildings,platforms),clock.Capture());
    }
    internal static void ValidateSuccessor(StationGraph? previous, StationGraph? candidate, ClockState before)
    {
        var old=Timers(previous).ToDictionary(t=>t.HandlerId,StringComparer.Ordinal);
        var active=Timers(candidate).ToDictionary(t=>t.HandlerId,StringComparer.Ordinal);
        var retained=candidate?.Identities.Select(i=>i.Id).ToHashSet() ?? [];
        if (old.Values.Any(t=>retained.Contains(OwnerId(t)) && (!active.TryGetValue(t.HandlerId,out var after) || after != t)))
            throw new ArgumentException("Cannot rewrite retained station timers.");
        var oldIds=previous?.Identities.Select(i=>i.Id).ToHashSet() ?? [];
        foreach (var b in candidate?.Buildings ?? [])
            if (!oldIds.Contains(b.Id)) { New(b.HourTimer,60); New(b.DayTimer,1440); }
        foreach (var p in candidate?.Platforms ?? [])
            if (!oldIds.Contains(p.Id)) New(p.DayTimer,1440,false);
        if (active.Values.Any(t=>!old.ContainsKey(t.HandlerId) && oldIds.Contains(OwnerId(t))))
            throw new ArgumentException("Cannot activate an existing detached station implicitly.");
        void New(ScheduledAction? timer,long interval,bool aligned=true)
        {
            var due=checked(before.AbsoluteMinute+(aligned ? interval-before.AbsoluteMinute%interval : interval));
            if (timer == null || timer.Registration < before.NextOrder || timer.Registration != timer.Order || timer.DueMinute != due || timer.Interval != interval)
                throw new ArgumentException("Invalid initial station timer.");
        }
    }
    private static long OwnerId(ScheduledAction timer) => long.Parse(timer.HandlerId.Split('/')[1],System.Globalization.CultureInfo.InvariantCulture);
    private static StationGraph Copy(StationGraph graph, IEnumerable<StationBuilding> buildings, IEnumerable<StationPlatform> platforms) =>
        new(graph.Profile,buildings,platforms,graph.Passages,graph.NextStationName,graph.NextPlatformName);

    public static MixedSessionState Commit(MixedSessionState before, MixedWorld candidate, ConstructionBooking booking, string operation)
    {
        if (candidate.Stations == null || candidate.Revision != checked(before.World.Revision+1)) throw new ArgumentException("Invalid station candidate.");
        var schedule = Reconcile(before.World.Stations,candidate.Stations,before.Clock.Capture());
        var world = before.World.Replace(before.World.Revision,candidate.Terrain,candidate.Spatial.Capture(),candidate.Voxels,candidate.Slopes,
            candidate.SpeedLimits,candidate.Fleet,schedule.Graph);
        var accounts = before.Construction.Accounts.Book(booking,world.Revision,before.Clock.Time,operation);
        return before.Successor(before.Revision,new(world,accounts),before.ConstructionMode,clock:schedule.Clock);
    }

    // Called by the trusted future station dispatcher. Upkeep is booked before the
    // hour/day effects; the repeated Clock entry keeps its registration cookie and
    // receives the next FIFO order AFTER this handler returns. Effects are pure
    // graph transformations; rejected callbacks discard this whole minute batch.
    public static MixedConstructionState Tick(MixedSessionState session, string handlerId, Func<StationGraph,StationGraph> effects)
    {
        var world = session.World;
        var graph = world.Stations ?? throw new ArgumentException("Missing station graph.");
        var timer = Timers(graph).Single(t => t.HandlerId == handlerId);
        if (timer.DueMinute != session.Clock.Time.AbsoluteMinute) throw new ArgumentException("Station callback is not due.");
        var booking = handlerId.EndsWith("/hour",StringComparison.Ordinal) ? ConstructionBooking.None :
            handlerId.StartsWith("station/",StringComparison.Ordinal) ? StationBooking.BuildingDaily(graph.Profile.Resolve(graph.Building(OwnerId(timer)).Definition)) :
            StationBooking.PlatformDaily(graph.Platform(OwnerId(timer)).Length);
        var accounts = session.Construction.Accounts.Book(booking,checked(world.Revision+1),session.Clock.Time,handlerId);
        var changed = effects(graph);
        if (!Timers(changed).ToHashSet().SetEquals(Timers(graph)) || !changed.Identities.Select(i => i.Id).ToHashSet().SetEquals(graph.Identities.Select(i => i.Id)))
            throw new ArgumentException("Station callback cannot replace timer ownership.");
        var next = timer with {Order=session.Clock.NextOrder,DueMinute=checked(timer.DueMinute+timer.Interval)};
        var buildings = changed.Buildings.Select(b => {
            var after = b with {HourTimer=b.HourTimer == timer ? next : b.HourTimer,DayTimer=b.DayTimer == timer ? next : b.DayTimer};
            return after == graph.Building(b.Id) ? after : after with {Revision=checked(graph.Building(b.Id).Revision+1)};
        }).ToImmutableArray();
        var platforms = changed.Platforms.Select(p => {
            var after = p with {DayTimer=p.DayTimer == timer ? next : p.DayTimer};
            return after == graph.Platform(p.Id) ? after : after with {Revision=checked(graph.Platform(p.Id).Revision+1)};
        }).ToImmutableArray();
        var result = Copy(changed,buildings,platforms);
        var revisions = result.Identities.ToDictionary(i => i.Id,i => i.Revision);
        var spatial = world.Spatial.Capture() with {Entities=world.Spatial.Entities.Select(e => revisions.TryGetValue(e.Id,out var revision) ? e with {Revision=revision} : e).ToImmutableArray()};
        return new(world.Replace(world.Revision,world.Terrain,spatial,world.Voxels,world.Slopes,stations:result),accounts);
    }
}
