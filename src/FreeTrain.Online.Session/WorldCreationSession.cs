using System.Security.Cryptography;
using System.Text;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed record EmptyCandidateRead(Guid Id, string RequestHash, string CandidateHash, string Name,
    WorldDimensions Dimensions, int WaterLevel, int SaveBytes, int SnapshotBytes, DateTimeOffset ExpiresAt, int[]? Ground = null, string? ImagePreview = null);

public sealed partial class SessionHost
{
    private static readonly SemaphoreSlim creationReservations = new(WorldCreationJob.MaxActiveJobs);
    public sealed class CreationInputReservation : IDisposable
    {
        private int owned = 1;
        internal bool Transfer() => Interlocked.Exchange(ref owned, 0) == 1;
        public void Dispose() { if (Transfer()) creationReservations.Release(); }
    }
    public static CreationInputReservation ReserveCreationInput() => creationReservations.Wait(0)
        ? new() : throw new InvalidOperationException("creationBusy");
    private WorldCreationJob? creationJob;
    private byte[]? creationBytes;
    private void ClearCreation()
    {
        if (creationJob != null) creationReservations.Release();
        creationJob = null; creationBytes = null;
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
        long revision, ParticipantContext participant) => BeginCreation(request, content, epoch, revision, participant);
    public Task<WorldCreationJob> BeginHeightmap(HeightmapWorldRequest request, CreationContent content, long epoch,
        long revision, ParticipantContext participant, CreationInputReservation? reservation = null) => BeginCreation(request, content, epoch, revision, participant, reservation);
    public Task<WorldCreationJob> BeginGenerated(GeneratedWorldRequest request, CreationContent content, long epoch,
        long revision, ParticipantContext participant, CreationInputReservation? reservation = null) => BeginCreation(request, content, epoch, revision, participant, reservation);
    private Task<WorldCreationJob> BeginCreation(WorldCreationRequest request, CreationContent content, long epoch,
        long revision, ParticipantContext participant, CreationInputReservation? reservation = null) => AtBoundary(() =>
    {
        CheckCreationOwner(participant, epoch, revision);
        // Separate measured admissions: empty 64²; heightmap/generated 32². Larger presets await P3-16a.
        if (request is null || !WorldCreationProfile.Validate(request).IsEmpty || request.Dimensions.H > 64 || request.Dimensions.V > 64 ||
            request is HeightmapWorldRequest or GeneratedWorldRequest && (request.Dimensions.H > 32 || request.Dimensions.V > 32) ||
            request is GeneratedWorldRequest generated && !ReliefTerrain.Supports(generated))
            throw new ArgumentException("invalidInput");
        if (creationJob is { } existing && DateTimeOffset.UtcNow < existing.ExpiresAt && existing.Fence.Epoch == WorldEpoch &&
            existing.Fence.Revision == CanonicalRevision && (existing.Fence.ConnectionId != participant.ConnectionId || existing.Fence.ParticipantId != participant.ParticipantId))
            throw new InvalidOperationException("creationBusy");
        ClearCreation();
        var fence = new CreationFence("shared", participant.ParticipantId, participant.ConnectionId, epoch, revision, content.CatalogSetHash);
        if (!(reservation?.Transfer() ?? creationReservations.Wait(0))) throw new InvalidOperationException("creationBusy");
        try
        {
            creationJob = WorldCreationJob.Create(Guid.NewGuid(), request, fence, content, DateTimeOffset.UtcNow)
                .Start(DateTimeOffset.UtcNow, fence);
        }
        catch { creationReservations.Release(); throw; }
        return creationJob;
    });
    public Task<EmptyCandidateRead> CompleteEmpty(WorldCreationJob started, byte[] bytes, ParticipantContext participant) => AtBoundary<EmptyCandidateRead>(() =>
    {
        CheckCreationOwner(participant, started.Fence.Epoch, started.Fence.Revision);
        if (started.Fence.ParticipantId != participant.ParticipantId || started.Fence.ConnectionId != participant.ConnectionId || creationJob != started) throw new InvalidOperationException("staleCandidate");
        var state = SaveCodec.ReadWorld(bytes, terrainResolver).Terrain ?? throw new ArgumentException("invalidInput");
        if (state.Provenance != TerrainProvenance.From(started.Request) || state.World.WaterLevel != started.Request.WaterLevel || started.Request is EmptyWorldRequest && state.World.Occupants.Length != 0 ||
            started.Request is EmptyWorldRequest && state.World.Cells.Any(c => c.Ground != started.Request.WaterLevel)) throw new ArgumentException("invalidInput");
        var snapshotBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<WireMessage>(TerrainWire.Snapshot(state, 1), WireJson.CreateOptions()).Length;
        if (snapshotBytes > ProtocolIdentity.MaximumStateBytes) throw new ArgumentException("invalidInput");
        var digest = CandidateDigest(started, bytes);
        creationJob = started.Complete(started.RequestHash, digest, Math.Max(bytes.Length, snapshotBytes), DateTimeOffset.UtcNow, started.Fence);
        creationBytes = bytes.ToArray();
        return new(started.Id, started.RequestHash, digest, state.World.Name, started.Request.Dimensions,
            state.World.WaterLevel, bytes.Length, snapshotBytes, started.ExpiresAt, started.Request is HeightmapWorldRequest or GeneratedWorldRequest ? state.World.Cells.Select(c => c.Ground).ToArray() : null);
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
        var replacement = SaveCodec.ReadWorld(creationBytes, terrainResolver);
        var nextEpoch = checked(WorldEpoch + 1);
        terrainState = replacement.Terrain; referenceWorld = null; WorldEpoch = nextEpoch;
        resetInput = safetyPause = false; ClearCreation();
        return Snapshot();
    });
}
