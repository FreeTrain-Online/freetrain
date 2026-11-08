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

public sealed partial class SessionHost : IDisposable
{
    private abstract record Work;
    private sealed record Boundary(Action Apply) : Work;
    private sealed record Queued(ParticipantContext Participant, CommandMessage Command, TaskCompletionSource<WireMessage> Completion) : Work
    {
        public bool InputInvalidated { get; set; }
    }
    private readonly object gate = new();
    private readonly Queue<Work> commands = new();
    private World world = null!;
    private ReferenceWorld? referenceWorld;
    private TerrainSessionState? terrainState;
    private MixedSessionState? mixedState;
    private readonly Func<string, string, IEnumerable<TerrainPack>, RailwayProfile?>? railwayResolver;
    private readonly Func<string, string, IEnumerable<TerrainPack>, TerrainProfile?>? terrainResolver;
    public bool SavingAvailable => referenceWorld != null || terrainState != null || mixedState != null;
    private readonly ScenarioDefinition scenario = null!;
    private readonly Func<ITrainController>[] controllerFactories = [];
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
            if (participants.TryGetValue(participant.ParticipantId, out var previous) && previous != participant)
            {
                lostParticipants.Add(previous);
                if (creationJob?.Fence.ParticipantId == previous.ParticipantId) ClearCreation();
            }
            participants[participant.ParticipantId] = participant;
        }
    }
    public void DisconnectParticipant(ParticipantContext participant)
    {
        lock (gate)
        {
            if (!Current(participant)) return;
            participants.Remove(participant.ParticipantId);
            if (creationJob?.Fence.ConnectionId == participant.ConnectionId) ClearCreation();
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
    public double PlaybackRate { get; private set; } = 1;
    private DateTimeOffset? lastRealtimePump;
    private double minuteAllowance;
    public static bool IsPlaybackRate(double rate) => rate is 0.125 or 0.25 or 0.5 or 1 or 2;
    private void ResetPlayback() { PlaybackRate = 1; minuteAllowance = 0; lastRealtimePump = null; }
    private bool IsPaused => SavingAvailable ? CanonicalPaused : world.Paused;
    private string? SetPlaybackRate(ParticipantContext participant, double rate)
    {
        if (participant.Role != ParticipantRole.Owner) return "notAuthorized";
        if (!IsPlaybackRate(rate)) return "invalidInput";
        if (mixedState != null) mixedState.ReviseSession();
        else if (terrainState != null) terrainState.ReviseSession();
        else if (referenceWorld != null) referenceWorld.ReviseSession();
        else world.ReviseSession();
        PlaybackRate = rate;
        minuteAllowance = 0;
        return null;
    }

    // Commands and leases are serviced on every 50ms network pump, even at 0.125×.
    // Preserve fractional minutes between pumps, but discard stall time and never
    // catch up more than four minutes in one pump (100ms at the fastest preset).
    public PumpResult PumpRealtime(DateTimeOffset now)
    {
        lock (gate)
        {
            var paused = IsPaused;
            var rate = PlaybackRate;
            var epoch = WorldEpoch;
            var service = Pump(0, now);
            var elapsed = lastRealtimePump is {} previous ? (now - previous).TotalMilliseconds : 50;
            lastRealtimePump = now;
            if (IsPaused || paused || rate != PlaybackRate || epoch != WorldEpoch || service.AdvancedTicks > 0)
            {
                minuteAllowance = 0;
                return service;
            }
            if (elapsed > 100) elapsed = 50;
            minuteAllowance += Math.Max(0, elapsed) * PlaybackRate / 50;
            var minutes = Math.Min(4, (int)minuteAllowance);
            minuteAllowance -= minutes;
            if (minutes == 0) return service;
            var simulation = Pump(minutes, now);
            return new(service.AdvancedTicks + simulation.AdvancedTicks,
                service.AppliedCommands + simulation.AppliedCommands, service.Events.AddRange(simulation.Events));
        }
    }

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

    public SessionHost(ReferenceWorld referenceWorld, string ownerId, int queueCapacity = 256, long worldEpoch = 1,
        Func<string, string, IEnumerable<TerrainPack>, TerrainProfile?>? terrainResolver = null,
        Func<string, string, IEnumerable<TerrainPack>, RailwayProfile?>? railwayResolver = null)
    {
        ArgumentNullException.ThrowIfNull(referenceWorld);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (queueCapacity <= 0 || worldEpoch < 1) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        this.referenceWorld = referenceWorld;
        this.terrainResolver = terrainResolver; this.railwayResolver = railwayResolver;
        this.queueCapacity = queueCapacity;
        WorldEpoch = worldEpoch;
        participants.Add(ownerId, new(ownerId));
    }

    public SessionHost(TerrainSessionState terrainState, string ownerId,
        Func<string, string, IEnumerable<TerrainPack>, TerrainProfile?> terrainResolver, int queueCapacity = 256, long worldEpoch = 1,
        Func<string, string, IEnumerable<TerrainPack>, RailwayProfile?>? railwayResolver = null)
    {
        ArgumentNullException.ThrowIfNull(terrainState);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (queueCapacity <= 0 || worldEpoch < 1) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        this.terrainState = terrainState; this.terrainResolver = terrainResolver; this.railwayResolver = railwayResolver;
        this.queueCapacity = queueCapacity; WorldEpoch = worldEpoch; participants.Add(ownerId, new(ownerId));
    }

    public SessionHost(MixedSessionState state, string ownerId,
        Func<string, string, IEnumerable<TerrainPack>, TerrainProfile?> terrainResolver,
        Func<string, string, IEnumerable<TerrainPack>, RailwayProfile?> railwayResolver, int queueCapacity = 256, long worldEpoch = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        if (queueCapacity <= 0 || worldEpoch < 1) throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        if (!ReferenceEquals(railwayResolver(ProtocolIdentity.MixedRuleset,ProtocolIdentity.MixedContent,state.World.Profile.Terrain.Packs),state.World.Profile))
            throw new ArgumentException("Unavailable mixed profile.");
        MixedWire.ValidateAdmission(state);
        mixedState = state.Freeze(); this.terrainResolver = terrainResolver; this.railwayResolver = railwayResolver;
        this.queueCapacity = queueCapacity; WorldEpoch = worldEpoch; participants.Add(ownerId,new(ownerId));
    }
    // Trusted adapter hook. Live commands must derive geometry and booking from authoritative facts.
    public Task ApplyMixed(long expectedEpoch, long expectedRevision, MixedConstructionState candidate, bool constructionMode,
        ParticipantContext participant) => AtBoundary(() =>
    {
        if (!Current(participant) || participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (WorldEpoch != expectedEpoch) throw new InvalidOperationException("staleEpoch");
        var state = mixedState ?? throw new InvalidOperationException("operationUnavailable");
        var proposed = state.Successor(expectedRevision,candidate,constructionMode);
        MixedWire.ValidateTransition(state,proposed);
        mixedState = proposed;
        return true;
    });

    // Trusted adapter hook for subsequent terrain tools; no public command/endpoint grants edit legality.
    public Task ApplyTerrain(long expectedEpoch, long expectedRevision, TerrainWorld candidate, bool constructionMode,
        ParticipantContext participant) => AtBoundary(() =>
    {
        if (!Current(participant) || participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (WorldEpoch != expectedEpoch) throw new InvalidOperationException("staleEpoch");
        var state = terrainState ?? throw new InvalidOperationException("operationUnavailable");
        if (state.Revision != expectedRevision) throw new InvalidOperationException("staleRevision");
        // Reject candidates outside persistence/transport limits before publication.
        var proposed = new TerrainSessionState(candidate, state.Provenance, constructionMode,
            state.Clock.Capture(), state.Paused, state.Random.Capture(), checked(state.Revision + 1));
        SaveCodec.Write(proposed);
        state.Replace(expectedRevision, candidate, constructionMode);
        return true;
    });

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
    public async Task<byte[]> CaptureSave(long? expectedEpoch = null, ParticipantContext? participant = null)
    {
        var capture = await AtBoundary<Func<byte[]>>(() =>
        {
            if (participant != null && participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
            if (expectedEpoch != null && expectedEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
            if (mixedState is {} mixed)
            {
                var frozenMixed = mixed.Freeze();
                return () => SaveCodec.Write(frozenMixed);
            }
            if (terrainState is { } state)
            {
                // Freeze the small mutable clock/random/session header at the FIFO
                // boundary; immutable geometry is shared with the detached encoder.
                var frozen = new TerrainSessionState(state.World, state.Provenance, state.ConstructionMode,
                    state.Clock.Capture(), state.Paused, state.Random.Capture(), state.Revision);
                return () => SaveCodec.Write(frozen);
            }
            var bytes = referenceWorld != null ? SaveCodec.Write(referenceWorld) : throw new InvalidDataException("savingUnavailable");
            return () => bytes;
        });
        return await Task.Run(capture);
    }
    public SaveCodec.LoadedWorld PrepareSave(ReadOnlyMemory<byte> save) => SaveCodec.ReadWorld(save.Span, terrainResolver, railwayResolver, mixedState?.TrainDispatcher, mixedState?.StationDispatcher);
    public Task<SnapshotMessage> LoadSave(ReadOnlyMemory<byte> save, long? expectedEpoch = null, ParticipantContext? participant = null) => AtBoundary<SnapshotMessage>(() =>
    {
        if (participant != null && participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (expectedEpoch != null && expectedEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        var replacement = PrepareSave(save); // Validate completely before touching current state.
        return ReplacePreparedSave(replacement);
    });
    public Task<SnapshotMessage> ApplyPreparedSave(SaveCodec.LoadedWorld replacement, long expectedEpoch, ParticipantContext? participant) => AtBoundary(() =>
    {
        if (participant != null && participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (expectedEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        return ReplacePreparedSave(replacement);
    });
    private SnapshotMessage ReplacePreparedSave(SaveCodec.LoadedWorld replacement)
    {
        if (!SavingAvailable) throw new InvalidDataException("incompatibleSave");
        if ((replacement.Reference != null ? 1 : 0) + (replacement.Terrain != null ? 1 : 0) + (replacement.Mixed != null ? 1 : 0) != 1)
            throw new InvalidDataException("invalidSave");
        // Only SaveCodec creates these validated handles. Recheck the receiving
        // host's exact installed profile without re-decoding large grids on FIFO.
        if (replacement.Mixed is {} mixed && !ReferenceEquals(railwayResolver?.Invoke(ProtocolIdentity.MixedRuleset,
            ProtocolIdentity.MixedContent,mixed.World.Profile.Terrain.Packs),mixed.World.Profile) ||
            replacement.Terrain is {} terrain && !ReferenceEquals(terrainResolver?.Invoke(TerrainProfile.Ruleset,
            TerrainProfile.ContentVersion,terrain.World.Profile.Packs),terrain.World.Profile))
            throw new InvalidDataException("incompatibleSave");
        var epoch = checked(WorldEpoch + 1);
        ClearCreation();
        referenceWorld = replacement.Reference;
        terrainState = replacement.Terrain; mixedState = replacement.Mixed;
        WorldEpoch = epoch;
        ResetPlayback();
        resetInput = safetyPause = false;
        return Snapshot();
    }

    // Bounded, coalesced lifecycle intent; only Pump mutates the world.
    public void RequestInputReset(bool pause)
    {
        lock (gate)
        {
            resetInput = true; safetyPause |= pause;
            foreach(var queued in commands.OfType<Queued>())
                if(queued.Command.Payload is ControllerInputCommand)queued.InputInvalidated=true;
        }
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
            if (SavingAvailable) return PumpReference(tickBudget);
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

    private bool CanonicalCanAdvance => mixedState?.CanAdvance ?? terrainState?.CanAdvance ?? referenceWorld!.CanAdvance;
    private bool CanonicalPaused => mixedState?.Paused ?? terrainState?.Paused ?? referenceWorld!.Paused;
    private long CanonicalRevision => mixedState?.Revision ?? terrainState?.Revision ?? referenceWorld!.Revision;
    private long CanonicalMinute => mixedState?.Clock.Time.AbsoluteMinute ?? terrainState?.Clock.Time.AbsoluteMinute ?? referenceWorld!.Clock.Time.AbsoluteMinute;
    private void CanonicalPause(bool paused) { if (mixedState != null) mixedState.SetPaused(paused); else if (terrainState != null) terrainState.SetPaused(paused); else referenceWorld!.SetPaused(paused); }
    private bool CanonicalAdvance()
    {
        try
        {
            if (mixedState != null)
            {
                var before = mixedState.World;
                mixedState.AdvanceOneMinute();
                RecordTrainMotion(before, mixedState);
            }
            else if (terrainState != null) terrainState.AdvanceOneMinute();
            else referenceWorld!.AdvanceOneMinute();
            return true;
        }
        catch (InvalidOperationException e) when (e.Message is "unboundJunctionController" or "unboundHarborController")
        {
            // A later-phase handler is unavailable. The detached minute has
            // already rolled back; keep the host responsive for withdrawal/load.
            CanonicalPause(true);
            return false;
        }
    }
    private PumpResult PumpReference(int tickBudget)
    {
        var advanced = 0;
        var applied = 0;
        do
        {
            var count = Math.Min(commands.Count, MaximumCommandsPerBoundary);
            for (var i = 0; i < count && advanced < MaximumTicksPerPump; i++)
            {
                var work = commands.Dequeue();
                if (work is Boundary boundary) boundary.Apply();
                else
                {
                    var queued = (Queued)work;
                    string? code = !Current(queued.Participant) || queued.Participant.Role == ParticipantRole.Observer
                        ? "notAuthorized" : queued.Command.ExpectedWorldEpoch != WorldEpoch ? "staleEpoch" : null;
                    if (code == null && queued.InputInvalidated) code="invalidInput";
                    if (code == null)
                    {
                        switch (queued.Command.Payload)
                        {
                            case DeployFleetCommand deploy when deploy.Query != null: code = CommitFleetDeployment(deploy); break;
                            case TradeFleetCommand trade when trade.Query != null: code = CommitFleetTrade(trade); break;
                            case ManageFleetControllerCommand manage: code=ManageFleetController(manage); break;
                            case EditFleetRuleCommand rule: code=EditFleetRule(rule); break;
                            case AssignFleetControllerCommand assign: code=AssignFleetController(assign); break;
                            case ManageFleetGroupCommand group: code=ManageFleetGroup(group); break;
                            case ConfigureFleetColorCommand color: code=ConfigureFleetColor(color); break;
                            case RenameFleetCommand rename when rename.Name != null: code = RenameFleet(rename); break;
                            case SetAuthoringModeCommand mode: code = SetAuthoringMode(queued.Participant,mode); break;
                            case EditCornerCommand edit when edit.Query != null: code = AuthoringTerrain.IsAction(edit.Query.Action)?CommitAuthoring(edit.Query):CommitCorner(edit.Query); break;
                            case EditRailwayCommand edit when edit.Query != null: code = CommitRailway(edit.Query); break;
                            case RenameStationCommand rename when rename.Name != null: code = RenameStationBuilding(rename); break;
                            case EditStationBuildingCommand edit when edit.Query != null: code = CommitStationBuilding(edit); break;
                            case EditSlopeCommand edit when edit.Query != null: code = CommitSlope(edit.Query); break;
                            case RemoveObjectCommand remove when remove.Query != null: code = CommitRemoval(remove.Query); break;
                            case SetPlaybackRateCommand rate: code = SetPlaybackRate(queued.Participant, rate.Rate); break;
                            case RunCommand when CanonicalCanAdvance: CanonicalPause(false); break;
                            case RunCommand: code = "invalidInput"; break;
                            case PauseCommand: CanonicalPause(true); break;
                            case StepCommand when CanonicalPaused && CanonicalCanAdvance:
                                if (CanonicalAdvance()) advanced++; else code="operationUnavailable";
                                break;
                            case StepCommand: code = "invalidInput"; break;
                            default: code = "operationUnavailable"; break;
                        }
                    }
                    queued.Completion.SetResult(code == null
                        ? new AcceptedMessage(queued.Command.RequestId, WorldEpoch, ++sequence,
                            CanonicalMinute, CanonicalRevision)
                        : Reject(queued.Command, code));
                }
                applied++;
            }
            resetInput = false;
            if (safetyPause) { CanonicalPause(true); safetyPause = false; }
            if (!CanonicalCanAdvance) CanonicalPause(true);
            if (advanced >= tickBudget || CanonicalPaused) break;
            if (!CanonicalAdvance()) break;
            advanced++;
        } while (advanced < tickBudget);
        lostParticipants.Clear(); resetParticipants.Clear();
        MaintainCreation();
        return new(advanced, applied, []);
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
            case SetPlaybackRateCommand rate: error = SetPlaybackRate(queued.Participant, rate.Rate); break;
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
    public WorldState ReadState() { lock (gate) return !SavingAvailable ? world.Read() : throw new InvalidOperationException("Canonical worlds do not expose prototype state."); }
    public SpatialPreviewRead PreviewSpatial(SpatialQuery query)
    {
        lock (gate)
        {
            var snapshot = Snapshot();
            return SpatialQueries.Preview(snapshot, query, terrainState == null && mixedState == null ? null :
                new(snapshot.Ruleset, snapshot.ContentVersion, "hv", snapshot.RequiredPacks.ToImmutableArray()));
        }
    }
    public SnapshotMessage Snapshot()
    {
        lock (gate)
        {
            if (mixedState != null) return MixedWire.Snapshot(mixedState,WorldEpoch) with { PlaybackRate = PlaybackRate, TrainMotion = ReadTrainMotion() };
            if (terrainState != null) return TerrainWire.Snapshot(terrainState, WorldEpoch, PlaybackRate);
            if (referenceWorld is {} reference)
                return new(WorldEpoch, reference.Revision, reference.Clock.Time.AbsoluteMinute, reference.Paused, 0,
                    new(reference.Space.HorizontalSize,reference.Space.VerticalSize,reference.Space.Levels,"hv"),
                    [], [], [], reference.Rails.Select(r => new TrackRead(new(r.Position.X,r.Position.Y,r.Position.Z),
                        $"normal{r.Pattern}",0,r.EntityId)).ToArray(),
                    ProtocolIdentity.ReferenceRuleset,ProtocolIdentity.ReferenceContent,ProtocolIdentity.ReferencePacks,
                    reference.Spatial.Entities.Select(e => new SpatialEntityRead(e.Id,e.Revision,
                        new(e.Definition.PackId,e.Definition.ContributionId,e.Definition.RecipeId),new(e.Anchor.X,e.Anchor.Y,e.Anchor.Z),
                        e.Footprint.Members.Select(m => new VoxelMemberRead(new(m.Offset.X,m.Offset.Y,m.Offset.Z),m.Transparent)).ToArray(),
                        e.Owned,e.SilentlyReclaimable,e.Value)).ToArray(), PlaybackRate: PlaybackRate);
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
                ProtocolIdentity.PrototypeRuleset, scenario.ContentVersion, ProtocolIdentity.PrototypePacks, PlaybackRate: PlaybackRate);
        }
    }
}
