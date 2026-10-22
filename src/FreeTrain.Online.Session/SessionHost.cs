using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using FreeTrain.Online.Simulation.Prototype;

namespace FreeTrain.Online.Session;

// Constructed by a trusted adapter, never deserialized from a command's payload.
public enum ParticipantRole { Owner, Operator, Observer }
public sealed record ParticipantContext(string ParticipantId, ParticipantRole Role = ParticipantRole.Owner, string ConnectionId = "headless");
public sealed record PumpResult(int AdvancedTicks, int AppliedCommands, ImmutableArray<SimulationEvent> Events);

public sealed class SessionHost
{
    private abstract record Work;
    private sealed record Boundary(Action Apply) : Work;
    private sealed record Queued(ParticipantContext Participant, CommandMessage Command, TaskCompletionSource<WireMessage> Completion) : Work
    {
        public bool InputInvalidated { get; set; }
    }
    private readonly object gate = new();
    private readonly Queue<Work> commands = new();
    private World world;
    private readonly ScenarioDefinition scenario;
    private readonly Func<ITrainController>[] controllerFactories;
    private readonly Dictionary<string, ParticipantContext> participants = new(StringComparer.Ordinal);
    private sealed record Lease(ParticipantContext Participant, string Id, DateTimeOffset Expires, DateTimeOffset? InputAt);
    private readonly Dictionary<string, Lease> leases = new(StringComparer.Ordinal);
    private readonly HashSet<ParticipantContext> lostParticipants = new();
    private readonly HashSet<ParticipantContext> resetParticipants = new();
    public static readonly TimeSpan ControlLeaseDuration = TimeSpan.FromSeconds(5);
    public TimeSpan InputExpiry { get; init; } = TimeSpan.FromMilliseconds(500);
    private DateTimeOffset boundaryTime;
    private long nextLease;
    // Only trusted adapters register contexts. Connection generations fence queued work
    // from a disconnected/replaced transport, even for the same participant ID.
    public void RegisterParticipant(ParticipantContext participant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participant.ParticipantId);
        if (!Enum.IsDefined(participant.Role)) throw new ArgumentOutOfRangeException(nameof(participant));
        lock (gate)
        {
            if (participants.TryGetValue(participant.ParticipantId, out var previous) && previous != participant) lostParticipants.Add(previous);
            participants[participant.ParticipantId] = participant;
        }
    }
    public void DisconnectParticipant(ParticipantContext participant)
    {
        lock (gate)
        {
            if (!Current(participant)) return;
            participants.Remove(participant.ParticipantId);
            lostParticipants.Add(participant);
        }
    }
    public void ResetParticipantInput(ParticipantContext participant)
    {
        lock (gate)
        {
            if (!Current(participant)) return;
            resetParticipants.Add(participant);
            // A pre-reset input waiting in the FIFO must not restore held keys.
            // New inputs queued after this lifecycle event remain valid.
            foreach (var work in commands.OfType<Queued>())
                if (work.Participant == participant && work.Command.Payload is ControllerInputCommand) work.InputInvalidated = true;
        }
    }
    private bool Current(ParticipantContext participant) => participants.TryGetValue(participant.ParticipantId, out var current) && current == participant;
    private void ReleaseLease(string trainId)
    {
        leases.Remove(trainId); world.ResetTrainControl(trainId, true);
    }
    private void MaintainControl()
    {
        foreach (var (id, lease) in leases.ToArray())
        {
            if (lostParticipants.Contains(lease.Participant) || boundaryTime >= lease.Expires) ReleaseLease(id);
            else if (resetParticipants.Contains(lease.Participant) || lease.InputAt is {} at && boundaryTime - at >= InputExpiry)
            { world.ResetTrainControl(id); leases[id] = lease with { InputAt = null }; }
        }
        lostParticipants.Clear(); resetParticipants.Clear();
    }
    private string? RequireLease(ParticipantContext participant, string trainId, string leaseId)
        => leases.TryGetValue(trainId, out var lease) && lease.Participant == participant && lease.Id == leaseId ? null : "leaseRequired";
    private string? Acquire(ParticipantContext participant, string trainId)
    {
        if (!world.Read().Trains.Any(t => t.Id == trainId)) return "invalidInput";
        if (leases.TryGetValue(trainId, out var lease) && lease.Participant != participant) return "controlBusy";
        if (lease == null)
        {
            world.ResetTrainControl(trainId, true);
            lease = new(participant, (++nextLease).ToString(System.Globalization.CultureInfo.InvariantCulture), boundaryTime, null);
        }
        else world.ResetTrainControl(trainId); // Renewals publish a new expiry; held keys must be resent.
        leases[trainId] = lease with { Expires = boundaryTime + ControlLeaseDuration, InputAt = null };
        return null;
    }
    private readonly int queueCapacity;
    private long sequence;
    private bool resetInput;
    private bool safetyPause;
    public const int MaximumTicksPerPump = 256;
    public const int MaximumCommandsPerBoundary = 64;
    public long WorldEpoch { get; private set; }

    public SessionHost(ScenarioDefinition scenario, string ownerId, IEnumerable<Func<ITrainController>> controllerFactories, int queueCapacity = 256, long worldEpoch = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (queueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        this.scenario = scenario;
        this.controllerFactories = controllerFactories.ToArray();
        participants.Add(ownerId, new(ownerId));
        this.queueCapacity = queueCapacity;
        WorldEpoch = worldEpoch;
        world = new(scenario, this.controllerFactories.Select(factory => factory()));
    }

    // Persistence joins exactly the same bounded FIFO as network commands.
    private Task<T> AtBoundary<T>(Func<T> action)
    {
        lock (gate)
        {
            if (commands.Count >= queueCapacity) return Task.FromException<T>(new InvalidOperationException("queueFull"));
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            commands.Enqueue(new Boundary(() => { try { completion.SetResult(action()); } catch (Exception ex) { completion.SetException(ex); } }));
            return completion.Task;
        }
    }
    public Task<byte[]> CaptureSave(long? expectedEpoch = null, ParticipantContext? participant = null) => AtBoundary<byte[]>(() =>
    {
        if (participant != null && participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (expectedEpoch != null && expectedEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        throw new InvalidDataException("savingUnavailable");
    });
    public Task<SnapshotMessage> LoadSave(ReadOnlyMemory<byte> save, long? expectedEpoch = null, ParticipantContext? participant = null) => AtBoundary<SnapshotMessage>(() =>
    {
        if (participant != null && participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (expectedEpoch != null && expectedEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        SaveCodec.RejectUnsupported(save.Span);
        throw new InvalidDataException("incompatibleSave");
    });

    // Bounded, coalesced lifecycle intent; only Pump mutates the world.
    public void RequestInputReset(bool pause)
    {
        lock (gate) { resetInput = true; safetyPause |= pause; }
    }

    // Completion means committed/rejected by Pump, never merely queued.
    public Task<WireMessage> Enqueue(ParticipantContext participant, CommandMessage command)
    {
        lock (gate)
        {
            if (!Current(participant) || participant.Role == ParticipantRole.Observer) return Task.FromResult<WireMessage>(Reject(command, "notAuthorized"));
            if (string.IsNullOrWhiteSpace(command.RequestId)) return Task.FromResult<WireMessage>(Reject(command, "invalidInput"));
            if (commands.Count >= queueCapacity) return Task.FromResult<WireMessage>(Reject(command, "queueFull"));
            var completion = new TaskCompletionSource<WireMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            commands.Enqueue(new Queued(participant, command, completion));
            return completion.Task;
        }
    }

    // The adapter supplies elapsed tick allowance. Even Pump(0) drains a bounded
    // FIFO prefix while paused. Locking serializes all callers; no timer callback reentry.
    public PumpResult Pump(int tickBudget = 0, DateTimeOffset? now = null)
    {
        if (tickBudget < 0 || tickBudget > MaximumTicksPerPump) throw new ArgumentOutOfRangeException(nameof(tickBudget));
        lock (gate)
        {
            var observedTime = now ?? DateTimeOffset.UtcNow;
            if (observedTime > boundaryTime) boundaryTime = observedTime;
            MaintainControl();
            var events = ImmutableArray.CreateBuilder<SimulationEvent>();
            var advanced = 0;
            var applied = 0;
            do
            {
                var count = Math.Min(commands.Count, MaximumCommandsPerBoundary);
                for (var i = 0; i < count && advanced < MaximumTicksPerPump; i++)
                {
                    var work = commands.Dequeue();
                    if (work is Boundary boundary)
                    {
                        var epoch = WorldEpoch;
                        boundary.Apply();
                        if (WorldEpoch != epoch) events.Clear(); // Never publish old-world effects under the new epoch.
                        applied++; continue;
                    }
                    var queued = (Queued)work;
                    var before = world.Tick;
                    var result = Apply(queued, events);
                    advanced += (int)(world.Tick - before); // Explicit single-step commands also consume this pump's tick bound.
                    queued.Completion.SetResult(result);
                    applied++;
                }
                if (resetInput) { world.ClearInputs(); resetInput = false; }
                if (safetyPause) { if (!world.Paused) world.SetPaused(true); safetyPause = false; }
                if (advanced >= tickBudget || world.Paused) break;
                events.AddRange(world.AdvanceOneTick());
                advanced++;
            } while (advanced < tickBudget);
            return new(advanced, applied, events.ToImmutable());
        }
    }

    private WireMessage Apply(Queued queued, ImmutableArray<SimulationEvent>.Builder events)
    {
        var command = queued.Command;
        if (!Current(queued.Participant) || queued.Participant.Role == ParticipantRole.Observer) return Reject(command, "notAuthorized");
        if (command.ExpectedWorldEpoch != WorldEpoch) return Reject(command, "staleEpoch");
        if (queued.InputInvalidated) return Reject(command, "invalidInput");
        string? error;
        switch (command.Payload)
        {
            case AcquireControlCommand acquire: error = Acquire(queued.Participant, acquire.TrainId); break;
            case ReleaseControlCommand release:
                error = RequireLease(queued.Participant, release.TrainId, release.LeaseId);
                if (error == null) ReleaseLease(release.TrainId);
                break;
            case RunCommand: world.SetPaused(false); error = null; break;
            case PauseCommand: world.SetPaused(true); error = null; break;
            case StepCommand:
                if (!world.Paused) { error = "invalidInput"; break; }
                events.AddRange(world.AdvanceOneTick()); error = null; break;
            case RenameStationCommand rename: error = world.RenameStation(rename.StationId, rename.ExpectedRevision, rename.Name); break;
            case BuildTrackCommand build:
                error = TryShape(build.Shape, out var shape) ? world.BuildTrack(Position(build.Position), shape) : "invalidPlacement"; break;
            case RemoveTrackCommand remove: error = world.RemoveTrack(Position(remove.Position), remove.ExpectedRevision); break;
            case SetControllerCommand controller:
                error = RequireLease(queued.Participant, controller.TrainId, controller.LeaseId)
                    ?? world.SetController(controller.TrainId, controller.ExpectedRevision, controller.Controller); break;
            case ControllerInputCommand input:
                error = RequireLease(queued.Participant, input.TrainId, input.LeaseId)
                    ?? world.SetInput(input.TrainId, input.InputSequence,
                        new(input.Pass, input.Depart, input.Reverse, input.Left, input.Right), input.Reset);
                if (error == null) leases[input.TrainId] = leases[input.TrainId] with
                    { Expires = boundaryTime + ControlLeaseDuration, InputAt = input.Reset ? null : boundaryTime };
                break;
            default: error = "invalidInput"; break;
        }
        return error == null ? new AcceptedMessage(command.RequestId, WorldEpoch, ++sequence, world.Tick, world.Revision) : Reject(command, error);
    }
    private static bool TryShape(string value, out TrackShape shape)
    {
        foreach (var candidate in Enum.GetValues<TrackShape>())
            if (Camel(candidate.ToString()) == value) { shape = candidate; return true; }
        shape = default;
        return false;
    }
    private static RejectedMessage Reject(CommandMessage command, string code) => new(command.RequestId, code, new());
    private static GridPosition Position(PositionRead p) => new(p.X, p.Y, p.Z);
    private static PositionRead Position(GridPosition p) => new(p.X, p.Y, p.Z);
    private static string Camel(string value) => char.ToLowerInvariant(value[0]) + value[1..];
    public WorldState ReadState() { lock (gate) return world.Read(); }
    public SpatialPreviewRead PreviewSpatial(SpatialQuery query)
    {
        lock (gate) return SpatialQueries.Preview(Snapshot(), query);
    }
    public SnapshotMessage Snapshot()
    {
        lock (gate)
        {
            var state = world.Read();
            return new(WorldEpoch, state.Revision, state.Tick, state.Paused, state.Cash,
                new(state.Map.Width, state.Map.Height, state.Map.Levels, "prototypeXY"),
                state.Trains.Select(x => new TrainRead(x.Id, x.Revision, x.Name, Position(x.Cars[0]), x.Cars.Length,
                    x.Controller, x.Direction.ToHeading().Index, x.Cars.Select(Position).ToArray(),
                    x.Cars.Select((car, index) => index == 0 ? x.Direction.ToHeading().Index
                        : new Location(car.X, car.Y, car.Z).DirectionTo(new(x.Cars[index - 1].X, x.Cars[index - 1].Y, car.Z)).Index).ToArray(),
                    Camel(x.Status.ToString()), leases.TryGetValue(x.Id, out var lease)
                        ? new(lease.Participant.ParticipantId, lease.Id, lease.Expires.ToUnixTimeMilliseconds()) : null)).ToArray(),
                state.Stations.Select(x => new StationRead(x.Id, x.Revision, x.Name, Position(x.Position))).ToArray(),
                state.Buildings.Select(x => new BuildingRead(x.Id, x.DefinitionId, Position(x.Position), x.Height)).ToArray(),
                state.Tracks.Select(x => new TrackRead(Position(x.Position), Camel(x.Shape.ToString()), x.Revision)).ToArray(),
                ProtocolIdentity.PrototypeRuleset, scenario.ContentVersion, ProtocolIdentity.PrototypePacks);
        }
    }
}
