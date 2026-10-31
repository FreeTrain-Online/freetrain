using System.Text.Json;
using System.Threading.Channels;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Session;
using FreeTrain.Online.Persistence;

namespace FreeTrain.Online.Server;

public sealed class NetworkOptions
{
    public int MaximumPeers { get; init; } = 32;
    public int OutputCapacity { get; init; } = 32;
    public int ReceiptCapacity { get; init; } = 1024;
    public TimeSpan ReceiptRetention { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan InputExpiry { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan DisconnectGrace { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan SocketIdle { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan IdleRetention { get; init; } = TimeSpan.FromMinutes(5);
    public void Validate()
    {
        if (MaximumPeers is < 2 or > 256 || OutputCapacity is < 2 or > 1024 || ReceiptCapacity is < 1 or > 65536 ||
            ReceiptRetention < TimeSpan.FromSeconds(10) || InputExpiry <= TimeSpan.Zero ||
            DisconnectGrace < TimeSpan.Zero || SocketIdle <= InputExpiry || IdleRetention <= ReceiptRetention)
            throw new InvalidOperationException("Invalid FreeTrainOnline:Network limits.");
    }
}

// All transport state is bounded and guarded here. The host remains the sole world owner.
public sealed partial class NetworkSession
{
    private readonly NetworkOptions options;
    private readonly SaveStorage? storage;
    public NetworkSession(NetworkOptions options, SaveStorage? storage = null, ILogger<NetworkSession>? logger = null,
        Func<long, TimeSpan, SessionHost>? hostFactory = null, StartupWorldOptions? startupWorld = null)
    {
        options.Validate();
        this.options = options; this.storage = storage;
        startupWorld ??= new();
        this.hostFactory = hostFactory ?? ((epoch, _) => SessionComposition.CreateGeneratedHost(startupWorld, epoch));
        // Restore before generating: a saved world must not pay generation cost,
        // consume a fresh seed, or depend on new-world parameter validity.
        if (hostFactory == null && storage?.Exists("checkpoint") == true)
        {
            try { host = SessionComposition.CreateSavedHost(storage.Read("checkpoint")); return; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                StartupSaveError = ex is InvalidDataException ? ex.Message : "saveFailed";
                logger?.LogWarning("Ignored checkpoint ({Reason}); generating a new world. The file was preserved.", StartupSaveError);
            }
        }
        host = this.hostFactory(1, options.InputExpiry);
        if (hostFactory != null && storage?.Exists("checkpoint") == true)
        {
            try
            {
                var load = host.LoadSave(storage.Read("checkpoint")); host.Pump(); load.GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                StartupSaveError = ex is InvalidDataException ? ex.Message : "saveFailed";
                logger?.LogWarning("Ignored checkpoint ({Reason}); starting a new world. The file was preserved.", StartupSaveError);
            }
        }
    }
    private readonly Func<long, TimeSpan, SessionHost> hostFactory;
    public bool SavingAvailable => host.SavingAvailable;
    public bool AutomaticCheckpointAvailable => SavingAvailable && StartupSaveError == null;
    public string? StartupSaveError { get; }
    private bool disconnectCheckpoint;
    public bool TakeCheckpointRequest() { lock (gate) { var requested = disconnectCheckpoint; disconnectCheckpoint = false; return requested; } }
    private readonly SemaphoreSlim saveWorkers = new(2);
    public async Task<byte[]> CaptureSave(long? epoch = null, ParticipantContext? participant = null)
    {
        if (!await saveWorkers.WaitAsync(0)) throw new InvalidOperationException("saveFailed");
        try { Task<byte[]> capture; lock (gate) capture = host.CaptureSave(epoch, participant); return await capture; }
        finally { saveWorkers.Release(); }
    }
    public async Task Checkpoint(string id = "checkpoint", long? epoch = null, ParticipantContext? participant = null)
    {
        if (!SavingAvailable) throw new InvalidDataException("savingUnavailable");
        // A rejected startup checkpoint is never overwritten, including on shutdown.
        if (id == "checkpoint" && StartupSaveError != null) throw new InvalidDataException(StartupSaveError);
        if (storage == null) return;
        await storage.Write(id, await CaptureSave(epoch, participant));
    }
    public async Task LoadSave(ReadOnlyMemory<byte> document, long epoch, ParticipantContext? participant = null)
    {
        if (!await saveWorkers.WaitAsync(0)) throw new InvalidOperationException("saveFailed");
        try
        {
            SessionHost capturedHost;
            lock (gate)
            {
                if (participant != null && participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
                if (host.WorldEpoch != epoch) throw new InvalidOperationException("staleEpoch");
                capturedHost = host;
            }
            var prepared = await Task.Run(() =>
            {
                var world = capturedHost.PrepareSave(document);
                if (world.Mixed is {} mixed) _ = MixedWire.Snapshot(mixed,1);
                if (world.Terrain is { } terrain) _ = TerrainWire.Snapshot(terrain, 1);
                return world;
            });
            Task<SnapshotMessage> loaded;
            lock (gate)
            {
                if (host != capturedHost) throw new InvalidOperationException("staleEpoch");
                loaded = host.ApplyPreparedSave(prepared, epoch, participant);
            }
            await loaded;
            lock (gate)
            {
                disconnectCheckpoint = true;
                foreach (var peer in peers.Values.ToArray()) Publish(peer);
            }
        }
        finally { saveWorkers.Release(); }
    }
    public async Task ValidateSave(ReadOnlyMemory<byte> document, long epoch, ParticipantContext participant)
    {
        if (!await saveWorkers.WaitAsync(0)) throw new InvalidOperationException("saveFailed");
        try
        {
            SessionHost capturedHost;
            lock (gate)
            {
                if (participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
                if (host.WorldEpoch != epoch) throw new InvalidOperationException("staleEpoch");
                capturedHost = host;
            }
            await Task.Run(() => capturedHost.PrepareSave(document));
            lock (gate) { if (host != capturedHost || host.WorldEpoch != epoch) throw new InvalidOperationException("staleEpoch"); }
        }
        finally { saveWorkers.Release(); }
    }
    public Task LoadSlot(string id, long epoch, ParticipantContext? participant = null) => LoadSave(storage?.Read(id) ?? throw new InvalidOperationException("unavailable"), epoch, participant);
    public object[] SaveSlots() => SaveStorage.Slots.Select(id => (object)new { id, exists = storage?.Exists(id) == true }).ToArray();
    public void PrepareShutdown()
    {
        lock (gate)
        {
            foreach (var peer in peers.Values.ToArray()) Disconnect(peer, DateTimeOffset.UtcNow);
            host.RequestInputReset(true); host.Pump();
        }
    }
    // Shutdown has stopped the driver, so explicitly drain this final queued capture.
    public async Task FinalCheckpoint()
    {
        if (storage == null || !SavingAvailable || StartupSaveError != null) return;
        Task<byte[]> capture;
        lock (gate) { capture = host.CaptureSave(); host.Pump(); }
        await storage.Write("checkpoint", await capture);
    }
    public string ContentVersion => host.Snapshot().ContentVersion;
    public static readonly JsonSerializerOptions Json = WireJson.CreateOptions();
    private sealed record Receipt(string Payload, DateTimeOffset Expires, Task<WireMessage> Result);
    public sealed class Peer(int capacity, DateTimeOffset now, ParticipantContext participant, string? accessCredential = null)
    {
        internal string? AccessCredential { get; } = accessCredential;
        public ParticipantContext Participant { get; } = participant;
        public Channel<WireMessage> Output { get; } = Channel.CreateBounded<WireMessage>(new BoundedChannelOptions(capacity)
        { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        public CancellationTokenSource Stop { get; } = new();
        internal SnapshotMessage? LastSnapshot;
        internal volatile bool StateTransferInProgress;
        internal DateTimeOffset LastSeen = now;
        internal DateTimeOffset Window = now;
        internal int Messages;
    }
    private readonly object gate = new();
    private readonly Dictionary<(string ParticipantId, string RequestId), Receipt> receipts = new();
    private SessionHost host;
    private readonly Dictionary<string, Peer> peers = new(StringComparer.Ordinal);
    private bool IsActive(Peer peer) => peers.TryGetValue(peer.Participant.ParticipantId, out var current) && current == peer;
    private bool HasOperators => peers.Values.Any(p => p.Participant.Role != ParticipantRole.Observer);
    private DateTimeOffset? disconnectedAt;
    private long lastPublicationSlot;
    private bool checkpointedDisconnect;
    public string Incarnation { get; } = Guid.NewGuid().ToString("N");
    public const int MaximumAccessCredentials = 1024;
    private sealed record AccessCredential(ParticipantContext Participant, DateTimeOffset Expires);
    private readonly Dictionary<string, AccessCredential> accessCredentials = new(StringComparer.Ordinal);
    // Opaque, incarnation-local login credentials can be revoked independently of
    // configured access keys. Keep the registry bounded and prune expired logins.
    public string? IssueAccess(ParticipantContext participant, DateTimeOffset? now = null)
    {
        lock (gate)
        {
            var issuedAt = now ?? DateTimeOffset.UtcNow;
            PruneAccess(issuedAt);
            if (accessCredentials.Count >= MaximumAccessCredentials) return null;
            var credential = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            accessCredentials.Add(credential, new(participant, issuedAt.AddHours(8)));
            return credential;
        }
    }
    public ParticipantContext? ResolveAccess(string? credential, DateTimeOffset? now = null)
    {
        lock (gate)
            return credential != null && credential.Length == 64 && accessCredentials.TryGetValue(credential, out var access)
                && access.Expires > (now ?? DateTimeOffset.UtcNow) ? access.Participant : null;
    }
    public void RevokeAccess(string? credential, DateTimeOffset? now = null)
    {
        lock (gate)
        {
            if (credential == null || !accessCredentials.Remove(credential)) return;
            foreach (var peer in peers.Values.Where(p => p.AccessCredential == credential).ToArray())
                Disconnect(peer, now ?? DateTimeOffset.UtcNow);
        }
    }
    private void PruneAccess(DateTimeOffset now)
    {
        foreach (var credential in accessCredentials.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray())
            RevokeAccess(credential, now);
    }
    public JoinMessage Bootstrap(ParticipantContext? participant = null) { lock (gate) return Join(participant ?? new("operator-1")); }
    private JoinMessage Join(ParticipantContext participant)
    {
        var snapshot = host.Snapshot();
        return new(ProtocolIdentity.Version, snapshot.ContentVersion, Incarnation,
            snapshot.Map.CoordinateSystem == "hv" ? "reference-spatial" : "reference-v1", participant.ParticipantId,
            participant.Role.ToString().ToLowerInvariant(), snapshot.WorldEpoch, snapshot, snapshot.Ruleset, snapshot.RequiredPacks);
    }
    public CornerTerrainPreviewRead PreviewCorner(CornerTerrainQuery query) { lock (gate) return host.PreviewCorner(query); }
    public SlopePreviewRead PreviewSlope(SlopeQuery query) { lock (gate) return host.PreviewSlope(query); }
    public RailwayPreviewRead PreviewRailway(RailwayQuery query) { lock (gate) return host.PreviewRailway(query); }
    public RemovalPreviewRead PreviewRemoval(RemovalQuery query) { lock (gate) return host.PreviewRemoval(query); }
    public SpatialPreviewRead PreviewSpatial(SpatialQuery query) { lock (gate) return host.PreviewSpatial(query); }
    public Peer? Connect(DateTimeOffset now, ParticipantContext? participant = null, string? accessCredential = null)
    {
        lock (gate)
        {
            if (accessCredential != null)
            {
                var authenticated = ResolveAccess(accessCredential, now);
                if (authenticated == null || authenticated != participant) return null;
            }
            participant ??= new("operator-1");
            if (peers.ContainsKey(participant.ParticipantId) || peers.Count >= options.MaximumPeers) return null;
            participant = participant with { ConnectionId = Guid.NewGuid().ToString("N") };
            var peer = new Peer(options.OutputCapacity, now, participant, accessCredential);
            peers.Add(participant.ParticipantId, peer);
            host.RegisterParticipant(participant);
            var join = Join(participant);
            peer.LastSnapshot = join.Snapshot;
            Write(peer, join);
            if (HasOperators) { disconnectedAt = null; checkpointedDisconnect = false; }
            return peer;
        }
    }
    public void Disconnect(Peer peer, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!IsActive(peer)) return;
            peers.Remove(peer.Participant.ParticipantId);
            peer.Output.Writer.TryComplete();
            peer.Stop.Cancel();
            host.DisconnectParticipant(peer.Participant);
            if (!HasOperators) disconnectedAt ??= now;
        }
    }
    public bool Touch(Peer peer, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!IsActive(peer)) return false;
            if (now - peer.Window >= TimeSpan.FromSeconds(1)) { peer.Window = now; peer.Messages = 0; }
            if (++peer.Messages > 64) { Disconnect(peer, now); return false; }
            peer.LastSeen = now;
            return true;
        }
    }
    public Task<WireMessage> Submit(Peer peer, CommandMessage command, DateTimeOffset now)
    {
        lock (gate)
        {
            Task<WireMessage> Reject(string code) => Task.FromResult<WireMessage>(new RejectedMessage(command.RequestId, code, new()));
            if (!IsActive(peer)) return Reject("notAuthorized");
            if (peer.Participant.Role == ParticipantRole.Observer) return Reject("notAuthorized");
            if (command.ExpectedWorldEpoch != host.WorldEpoch) return Reject("staleEpoch");
            foreach (var key in receipts.Where(x => x.Value.Expires <= now && x.Value.Result.IsCompleted).Select(x => x.Key).ToArray()) receipts.Remove(key);
            var payload = JsonSerializer.Serialize<WireMessage>(command, Json);
            if (receipts.TryGetValue((peer.Participant.ParticipantId, command.RequestId), out var receipt))
                return receipt.Payload == payload ? receipt.Result : Reject("requestConflict");
            if (receipts.Count >= options.ReceiptCapacity) return Reject("retryWindowFull");
            var result = host.Enqueue(peer.Participant, command);
            receipts.Add((peer.Participant.ParticipantId, command.RequestId), new(payload, now + options.ReceiptRetention, result));
            return result;
        }
    }
    public void Reply(Peer peer, WireMessage result)
    {
        lock (gate)
        {
            if (!IsActive(peer)) return;
            foreach (var other in peers.Values.ToArray()) Publish(other); // Broadcast committed state before acknowledging.
            Write(peer, result);
        }
    }
    public void Resync(Peer peer)
    {
        lock (gate)
        {
            if (!IsActive(peer)) return;
            peer.LastSnapshot = host.Snapshot();
            Write(peer, peer.LastSnapshot);
        }
    }
    public void ResetInput(Peer peer)
    {
        lock (gate) { if (IsActive(peer)) host.ResetParticipantInput(peer.Participant); } // Focus loss never pauses a shared world.
    }
    public void Tick(DateTimeOffset now)
    {
        lock (gate)
        {
            PruneAccess(now);
            disconnectedAt ??= !HasOperators ? now : null;
            foreach (var peer in peers.Values.Where(p => now - p.LastSeen >= options.SocketIdle).ToArray()) Disconnect(peer, now);
            if (disconnectedAt is { } lostAt && now - lostAt >= options.DisconnectGrace)
            {
                host.RequestInputReset(true);
                if (!checkpointedDisconnect) { disconnectCheckpoint = true; checkpointedDisconnect = true; }
            }
            if (storage == null && disconnectedAt is { } idleAt && now - idleAt >= options.IdleRetention)
            {
                host.Dispose();
                host = hostFactory(host.WorldEpoch + 1, options.InputExpiry);
                foreach (var peer in peers.Values) host.RegisterParticipant(peer.Participant);
                receipts.Clear(); disconnectedAt = now;
            }
            var pump = host.Pump(1, now); // 20Hz, no unbounded catch-up after a stalled process.
            // Fixed 100ms slots avoid accumulating 50ms timer jitter into a slower rate.
            // Skip missed slots after a stall; never burst an unbounded catch-up batch.
            var slot = now.UtcTicks / TimeSpan.TicksPerMillisecond / 100;
            if (slot > lastPublicationSlot)
            { foreach (var peer in peers.Values.ToArray()) Publish(peer); lastPublicationSlot = slot; }
            foreach (var peer in peers.Values.ToArray())
                foreach (var sound in pump.Events.Where(e => e.Kind == "stationArrived"))
                    Write(peer, new SoundEventMessage($"{Incarnation}:{host.WorldEpoch}:{sound.Tick}:{sound.SubjectId}:{sound.Kind}", host.WorldEpoch, sound.Kind));
        }
    }
    private void Publish(Peer peer)
    {
        // Coalesce changes while a coherent snapshot is in flight. Once its
        // bounded writer finishes, difference the last queued revision against
        // the latest world; never buffer an unbounded chain of follow-up deltas.
        if (peer.StateTransferInProgress) return;
        var next = host.Snapshot();
        var previous = peer.LastSnapshot;
        if (previous?.Revision == next.Revision && previous.WorldEpoch == next.WorldEpoch) return;
        WireMessage message = previous == null || previous.WorldEpoch != next.WorldEpoch || previous.Ruleset != next.Ruleset ? next : Difference(previous, next);
        if (Write(peer, message)) peer.LastSnapshot = next;
    }
    private bool Write(Peer peer, WireMessage message)
    {
        // Canonical saves/candidates already validate total state. Serialize/chunk
        // snapshots on the bounded socket workers, outside the simulation gate.
        if ((message is JoinMessage or SnapshotMessage || JsonSerializer.SerializeToUtf8Bytes<WireMessage>(message, Json).Length <= 16 * 1024 * 1024) &&
            peer.Output.Writer.TryWrite(message)) return true;
        // Oversized state or a full bounded queue closes this peer; never lose a dependent delta or block on network I/O.
        Disconnect(peer, DateTimeOffset.UtcNow);
        return false;
    }
    public static DeltaMessage Difference(SnapshotMessage previous, SnapshotMessage next)
        => StateDifference.Between(previous,next);

}

public sealed class CheckpointOptions
{
    public string Directory { get; init; } = "data/saves";
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed class SessionDriver(NetworkSession session, ILogger<SessionDriver> logger, CheckpointOptions? options = null) : BackgroundService
{
    private Task? checkpoint;
    public string? CheckpointError { get; private set; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options?.Interval ?? TimeSpan.FromSeconds(30);
        var next = DateTimeOffset.UtcNow + interval;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTimeOffset.UtcNow;
                session.Tick(now);
                if (checkpoint?.IsCompleted == true)
                {
                    try { await checkpoint; CheckpointError = null; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                    { CheckpointError = "checkpointFailed"; logger.LogError(ex, "Checkpoint failed; current game remains in memory"); }
                    checkpoint = null;
                }
                if (session.AutomaticCheckpointAvailable && checkpoint == null && (now >= next || session.TakeCheckpointRequest()))
                { checkpoint = session.Checkpoint(); next = now + interval; }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        session.PrepareShutdown();
        if (checkpoint != null)
        {
            try { await checkpoint; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            { logger.LogError(ex, "Earlier checkpoint failed; attempting final shutdown checkpoint"); }
        }
        await session.FinalCheckpoint();
    }
}
