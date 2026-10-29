using System.Security.Cryptography;
using System.Text;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed record EmptyCandidateRead(Guid Id, string RequestHash, string CandidateHash, string Name,
    WorldDimensions Dimensions, int WaterLevel, int SaveBytes, int SnapshotBytes, DateTimeOffset ExpiresAt, int[]? Ground = null, string? ImagePreview = null,
    GeographyPreview? Geography = null);
public sealed record GeographyPreview(GeographyMetrics Metrics, string SeaMask);

public sealed partial class SessionHost
{
    private static readonly object reservationGate = new();
    private static int reservedJobs;
    private static long reservedBytes;
    public const long MaximumCreationWorkingBytes = 4096L * 1024 * 1024;
    public sealed class CreationInputReservation : IDisposable
    {
        private bool workerHeld = true, hostHeld, charged = true;
        private long bytes;
        internal CreationInputReservation(long bytes) { this.bytes = bytes; }
        internal bool Transfer(long requiredBytes)
        {
            lock (reservationGate)
            {
                if (!workerHeld || hostHeld || !charged) return false;
                if (requiredBytes < 1 || reservedBytes - bytes + requiredBytes > MaximumCreationWorkingBytes)
                    throw new InvalidOperationException("creationBusy");
                reservedBytes += requiredBytes - bytes; bytes = requiredBytes; hostHeld = true; return true;
            }
        }
        internal void Release()
        {
            lock (reservationGate) { hostHeld = false; ReleaseUnused(); }
        }
        private void ReleaseUnused()
        {
            if (!charged || workerHeld || hostHeld) return;
            charged = false; reservedJobs--; reservedBytes -= bytes;
        }
        public void Dispose() { lock (reservationGate) { workerHeld = false; ReleaseUnused(); } }
    }
    public static CreationInputReservation ReserveCreationInput()
    {
        const long inputBytes = 96L * 1024 * 1024;
        lock (reservationGate)
        {
            if (reservedJobs >= WorldCreationJob.MaxActiveJobs || reservedBytes + inputBytes > MaximumCreationWorkingBytes)
                throw new InvalidOperationException("creationBusy");
            reservedJobs++; reservedBytes += inputBytes; return new(inputBytes);
        }
    }
    private WorldCreationJob? creationJob;
    private byte[]? creationBytes;
    private TerrainSessionState? creationState;
    private CreationInputReservation? creationReservation;
    private void ClearCreation()
    {
        creationReservation?.Release(); creationReservation = null;
        creationJob = null; creationBytes = null; creationState = null;
    }
    public void Dispose() { lock (gate) ClearCreation(); }
    private void MaintainCreation()
    {
        if (creationJob is { } job && (job.ExpiresAt <= DateTimeOffset.UtcNow ||
            job.Fence.Epoch != WorldEpoch || job.Fence.Revision != CanonicalRevision)) ClearCreation();
    }
    private void CheckCreationOwner(ParticipantContext participant, long epoch, long revision)
    {
        if (!Current(participant) || participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (!SavingAvailable) throw new InvalidOperationException("operationUnavailable");
        if (WorldEpoch != epoch) throw new InvalidOperationException("staleEpoch");
        if (CanonicalRevision != revision) throw new InvalidOperationException("staleRevision");
    }
    public Task<WorldCreationJob> BeginEmpty(EmptyWorldRequest request, CreationContent content, long epoch,
        long revision, ParticipantContext participant, CreationInputReservation? reservation = null) => BeginCreation(request, content, epoch, revision, participant, reservation);
    public Task<WorldCreationJob> BeginHeightmap(HeightmapWorldRequest request, CreationContent content, long epoch,
        long revision, ParticipantContext participant, CreationInputReservation? reservation = null) => BeginCreation(request, content, epoch, revision, participant, reservation);
    public Task<WorldCreationJob> BeginGenerated(GeneratedWorldRequest request, CreationContent content, long epoch,
        long revision, ParticipantContext participant, CreationInputReservation? reservation = null) => BeginCreation(request, content, epoch, revision, participant, reservation);
    private Task<WorldCreationJob> BeginCreation(WorldCreationRequest request, CreationContent content, long epoch,
        long revision, ParticipantContext participant, CreationInputReservation? reservation = null) => AtBoundary(() =>
    {
        CheckCreationOwner(participant, epoch, revision);
        // P3-16a admission shares the canonical structural/resource envelope.
        if (request is null || !WorldCreationProfile.Validate(request).IsEmpty ||
            request is GeneratedWorldRequest generated && !ReliefTerrain.Supports(generated))
            throw new ArgumentException("invalidInput");
        if (creationJob is { } existing && DateTimeOffset.UtcNow < existing.ExpiresAt && existing.Fence.Epoch == WorldEpoch &&
            existing.Fence.Revision == CanonicalRevision && (existing.Fence.ConnectionId != participant.ConnectionId || existing.Fence.ParticipantId != participant.ParticipantId))
            throw new InvalidOperationException("creationBusy");
        ClearCreation();
        var fence = new CreationFence("shared", participant.ParticipantId, participant.ConnectionId, epoch, revision, content.CatalogSetHash);
        var resource = reservation ?? ReserveCreationInput();
        bool transferred;
        try { transferred = resource.Transfer(WorldCreationProfile.Estimate(request).WorkingBytes); }
        catch { if (reservation == null) resource.Dispose(); throw; }
        if (!transferred) throw new InvalidOperationException("creationBusy");
        // A direct in-process start has no detached input worker. Network workers
        // keep their lease until they actually finish, even after host cancellation.
        if (reservation == null) resource.Dispose();
        try
        {
            creationReservation = resource;
            creationJob = WorldCreationJob.Create(Guid.NewGuid(), request, fence, content, DateTimeOffset.UtcNow)
                .Start(DateTimeOffset.UtcNow, fence);
        }
        catch { resource.Release(); creationReservation = null; throw; }
        return creationJob;
    });
    public sealed class PreparedCreation
    {
        internal byte[] Bytes { get; }
        internal TerrainSessionState State { get; }
        internal int SnapshotBytes { get; }
        internal GeographyPreview? Geography { get; }
        internal PreparedCreation(byte[] bytes, TerrainSessionState state, int snapshotBytes, GeographyPreview? geography)
        { Bytes = bytes; State = state; SnapshotBytes = snapshotBytes; Geography = geography; }
    }
    public PreparedCreation PrepareCreation(byte[] bytes, CancellationToken cancellation = default)
    {
        var retained = bytes.ToArray();
        var state = SaveCodec.ReadWorld(retained, terrainResolver).Terrain ?? throw new ArgumentException("invalidInput");
        var snapshot = TerrainWire.Snapshot(state, 1); // Build immutable wire geometry on the detached worker too.
        var snapshotBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new StateTransferPayload(
            TerrainWire.Header(state, 1), PackedTerrain.Capture(snapshot)), WireJson.CreateOptions()).Length;
        if (snapshotBytes > ProtocolIdentity.MaximumStateBytes) throw new ArgumentException("invalidInput");
        GeographyPreview? geography = null;
        if (state.Provenance.Source == "generated")
        {
            var request = (GeneratedWorldRequest)System.Text.Json.JsonSerializer.Deserialize<WorldCreationRequest>(state.Provenance.RequestJson)!;
            // Detached verification/overview work has its own bounded pass; generation
            // repairs share the original generator meter and never reset it.
            var work = new GenerationWork(request, cancellation);
            var metrics = CoastTerrain.Validate(request, state.World, work);
            var wet = CoastTerrain.SeaMask(state.World, work); var packed = new byte[(wet.Length + 7) / 8];
            for (int i = 0; i < wet.Length; i++) { work.Spend(1); if (wet[i]) packed[i / 8] |= (byte)(1 << (i % 8)); }
            geography = new(metrics, Convert.ToBase64String(packed));
        }
        return new(retained, state, snapshotBytes, geography);
    }
    public Task<EmptyCandidateRead> CompleteEmpty(WorldCreationJob started, byte[] bytes, ParticipantContext participant)
        => CompletePreparedCreation(started, PrepareCreation(bytes), participant);
    public Task<EmptyCandidateRead> CompletePreparedCreation(WorldCreationJob started, PreparedCreation prepared, ParticipantContext participant) => AtBoundary<EmptyCandidateRead>(() =>
    {
        CheckCreationOwner(participant, started.Fence.Epoch, started.Fence.Revision);
        if (started.Fence.ParticipantId != participant.ParticipantId || started.Fence.ConnectionId != participant.ConnectionId || creationJob != started) throw new InvalidOperationException("staleCandidate");
        var state = prepared.State; var bytes = prepared.Bytes;
        if (state.Provenance != TerrainProvenance.From(started.Request) || state.World.WaterLevel != started.Request.WaterLevel || started.Request is EmptyWorldRequest && state.World.Occupants.Length != 0 ||
            started.Request is EmptyWorldRequest && state.World.Cells.Any(c => c.Ground != started.Request.WaterLevel)) throw new ArgumentException("invalidInput");
        var snapshotBytes = prepared.SnapshotBytes;
        var digest = CandidateDigest(started, bytes);
        creationJob = started.Complete(started.RequestHash, digest, Math.Max(bytes.Length, snapshotBytes), DateTimeOffset.UtcNow, started.Fence);
        creationBytes = bytes; creationState = state;
        return new(started.Id, started.RequestHash, digest, state.World.Name, started.Request.Dimensions,
            state.World.WaterLevel, bytes.Length, snapshotBytes, started.ExpiresAt, started.Request is HeightmapWorldRequest or GeneratedWorldRequest ? state.World.Cells.Select(c => c.Ground).ToArray() : null,
            Geography: prepared.Geography);
    });
    private static string CandidateDigest(WorldCreationJob job, byte[] bytes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(job.RequestHash + "\n" + job.Fence.CatalogSetHash + "\n"));
        hash.AppendData(bytes);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public Task CancelEmpty(Guid id, ParticipantContext participant) => AtBoundary(() =>
    {
        if (!Current(participant) || participant.Role != ParticipantRole.Owner) throw new UnauthorizedAccessException();
        if (creationJob?.Id == id && creationJob.Fence.ConnectionId == participant.ConnectionId && creationJob.Fence.ParticipantId == participant.ParticipantId) ClearCreation();
        return true;
    });
    public Task<SnapshotMessage> CommitEmpty(Guid id, string requestHash, string candidateHash, long epoch, long revision,
        ParticipantContext participant) => AtBoundary(() =>
    {
        CheckCreationOwner(participant, epoch, revision);
        var job = creationJob ?? throw new InvalidOperationException("staleCandidate");
        if (job.Id != id || job.Fence.ConnectionId != participant.ConnectionId || job.Fence.ParticipantId != participant.ParticipantId || creationBytes == null ||
            CandidateDigest(job, creationBytes) != candidateHash) throw new InvalidOperationException("staleCandidate");
        var fence = job.Fence with { Epoch = WorldEpoch, Revision = CanonicalRevision };
        job.Commit(requestHash, candidateHash, true, DateTimeOffset.UtcNow, fence);
        var replacement = creationState ?? throw new InvalidOperationException("staleCandidate");
        var nextEpoch = checked(WorldEpoch + 1);
        terrainState = replacement; referenceWorld = null; WorldEpoch = nextEpoch;
        resetInput = safetyPause = false; ClearCreation();
        return Snapshot();
    });
}
