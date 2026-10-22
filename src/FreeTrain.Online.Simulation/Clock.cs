using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record ScheduledAction(long Registration, long Order, long DueMinute, string HandlerId, long Interval);
public sealed record ClockState(long AbsoluteMinute, long NextOrder, ImmutableArray<ScheduledAction> Pending);

// ExAV Clock and PriorityQueue: minute increment -> daylight invalidation -> FIFO
// due actions -> detached one-shot end-of-turn batch. Bindings belong to this clock.
// GEO-002 adaptation: remove the executing entry before dispatch; cancellation can
// never delete its neighbor, and cancellation inside a repeated callback stops it.
public sealed class Clock
{
    private readonly SortedDictionary<(long Minute, long Order), ScheduledAction> queue = [];
    private readonly IReadOnlyDictionary<string, Action> handlers;
    private readonly List<Action> endOfTurn = [];
    private readonly HashSet<long> cancelled = [];
    private ScheduledAction? executing;
    private bool advancing;
    private bool faulted;
    public const int MaximumPending = 65536;
    public GameTime Time { get; private set; }
    public long NextOrder { get; private set; }
    public event Action? DayNightChanged;

    public Clock(long absoluteMinute = GameTime.StartMinute, IReadOnlyDictionary<string, Action>? handlers = null)
    {
        Time = new(absoluteMinute);
        this.handlers = handlers == null ? new Dictionary<string, Action>() : new Dictionary<string, Action>(handlers, StringComparer.Ordinal);
        if (this.handlers.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value == null)) throw new ArgumentException("Invalid clock bindings.");
    }
    public long ScheduleRepeated(string handlerId, long interval) => ScheduleRepeated(handlerId, interval, interval);
    public long ScheduleRepeated(string handlerId, long firstDelay, long interval)
    {
        if (interval <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
        return Schedule(handlerId, firstDelay, interval);
    }
    public long Schedule(string handlerId, long delay, long interval = 0) => ScheduleAt(handlerId, checked(Time.AbsoluteMinute + delay), interval);
    public long ScheduleAt(string handlerId, long absoluteMinute, long interval = 0)
    {
        if (!handlers.ContainsKey(handlerId)) throw new ArgumentException("Unbound clock action.", nameof(handlerId));
        if (absoluteMinute <= Time.AbsoluteMinute || absoluteMinute > GameTime.MaximumMinute || interval < 0
            || interval > GameTime.MaximumMinute) throw new ArgumentOutOfRangeException(nameof(absoluteMinute));
        if (queue.Count >= MaximumPending) throw new InvalidOperationException("Clock queue full.");
        var order = NextOrder;
        var next = checked(order + 1);
        queue.Add((absoluteMinute, order), new(order, order, absoluteMinute, handlerId, interval));
        NextOrder = next;
        return order;
    }
    public void Unregister(long registration)
    {
        foreach (var key in queue.Where(p => p.Value.Registration == registration).Select(p => p.Key).ToArray()) queue.Remove(key);
        if (executing?.Registration == registration) cancelled.Add(registration);
    }
    // One-shot registrations of the same named handler mirror delegate equality.
    // Repeated timers have separate cookies, matching source wrapper identities.
    public void UnregisterOneShots(string handlerId)
    {
        foreach (var key in queue.Where(p => p.Value.HandlerId == handlerId && p.Value.Interval == 0).Select(p => p.Key).ToArray()) queue.Remove(key);
    }
    public void AtEndOfTurn(Action handler) { ArgumentNullException.ThrowIfNull(handler); endOfTurn.Add(handler); }
    public void Advance()
    {
        if (faulted) throw new InvalidOperationException("Recover a failed clock from a prior checkpoint.");
        if (advancing) throw new InvalidOperationException("Clock advance cannot reenter.");
        var nextTime = Time.AddMinutes(1); // Check before mutation.
        advancing = true;
        try
        {
            Time = nextTime;
            if (Time.HourOfDay is 6 or 18 && Time.Minute == 0) DayNightChanged?.Invoke();
            while (queue.Count != 0 && queue.First().Key.Minute == Time.AbsoluteMinute)
            {
                var entry = queue.First();
                queue.Remove(entry.Key);
                executing = entry.Value;
                try
                {
                    handlers[entry.Value.HandlerId]();
                    if (entry.Value.Interval > 0 && !cancelled.Contains(entry.Value.Registration))
                    {
                        var due = checked(Time.AbsoluteMinute + entry.Value.Interval);
                        if (due > GameTime.MaximumMinute) throw new InvalidOperationException("Clock range exhausted.");
                        if (queue.Count >= MaximumPending) throw new InvalidOperationException("Clock queue full.");
                        var order = NextOrder;
                        NextOrder = checked(order + 1);
                        queue.Add((due, order), entry.Value with { DueMinute = due, Order = order });
                    }
                }
                finally { cancelled.Clear(); executing = null; }
            }
            var batch = endOfTurn.ToArray();
            endOfTurn.Clear();
            foreach (var handler in batch) handler();
        }
        catch { faulted = true; throw; }
        finally { advancing = false; }
    }
    public ClockState Capture()
    {
        if (advancing || faulted) throw new InvalidOperationException("Capture only at a successful clock boundary.");
        return new(Time.AbsoluteMinute, NextOrder, queue.Values.ToImmutableArray());
    }
    public static Clock Restore(ClockState state, IReadOnlyDictionary<string, Action>? handlers = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var clock = new Clock(state.AbsoluteMinute, handlers);
        if (state.NextOrder < 0 || state.Pending.IsDefault || state.Pending.Length > MaximumPending) throw new ArgumentException("Invalid clock state.");
        var registrations = new HashSet<long>();
        var orders = new HashSet<long>();
        foreach (var action in state.Pending)
        {
            if (action == null || action.Registration < 0 || action.Registration > action.Order || (action.Interval == 0 && action.Registration != action.Order) || action.Order >= state.NextOrder
                || !orders.Add(action.Order) || !registrations.Add(action.Registration)
                || action.DueMinute <= state.AbsoluteMinute || action.DueMinute > GameTime.MaximumMinute
                || action.Interval < 0 || action.Interval > GameTime.MaximumMinute
                || !clock.handlers.ContainsKey(action.HandlerId)) throw new ArgumentException("Invalid or unbound clock action.");
            clock.queue.Add((action.DueMinute, action.Order), action);
        }
        clock.NextOrder = state.NextOrder;
        return clock;
    }
}
