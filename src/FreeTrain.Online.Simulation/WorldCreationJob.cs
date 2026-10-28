using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public enum CreationCapabilityKind { Terrain, Forest, HeightmapProvider, Cover }
public sealed record CreationCapability(ContentBinding Binding, CreationCapabilityKind Kind, bool Prepared, bool RuntimeReady);
public sealed record CreationContent(string CatalogSetHash, ImmutableArray<CreationCapability> Capabilities)
{
    public ImmutableArray<CreationIssue> Validate(WorldCreationRequest request)
    {
        var errors = WorldCreationProfile.Validate(request).ToBuilder();
        if (errors.Count > 0) return errors.ToImmutable();
        if (!WorldCreationProfile.IsHash(CatalogSetHash) || Capabilities.IsDefault || Capabilities.Any(c => c is null ||
            !WorldCreationProfile.ValidBinding(c.Binding) || !Enum.IsDefined(c.Kind)) ||
            Capabilities.Select(c => c.Binding).Distinct().Count() != Capabilities.Length)
            return [new("content-catalog", "content")];
        if (!Capabilities.Any(c => c.Kind == CreationCapabilityKind.Terrain && c.Prepared && c.RuntimeReady))
            errors.Add(new("terrain-unavailable", "content"));
        var requirements = new List<(ContentBinding Binding, CreationCapabilityKind Kind)>();
        if (request is HeightmapWorldRequest { Image.Provider: { } provider }) requirements.Add((provider, CreationCapabilityKind.HeightmapProvider));
        if (request is GeneratedWorldRequest generated)
            requirements.AddRange(generated.Options.Forests.Palette.Select(b => (b, CreationCapabilityKind.Forest)));
        foreach (var (binding, kind) in requirements)
        {
            var match = Capabilities.SingleOrDefault(c => c.Binding == binding && c.Kind == kind);
            if (match is null) errors.Add(new("content-missing", binding.ContributionId));
            else if (!match.Prepared || !match.RuntimeReady) errors.Add(new("content-unavailable", binding.ContributionId));
        }
        return errors.ToImmutable();
    }
}

// Captured on the session FIFO, rechecked there before replacement. Content changes,
// reconnects, edits and world replacement invalidate the fence. This is not authentication.
public sealed record CreationFence(string SessionId, string ParticipantId, string ConnectionId,
    long Epoch, long Revision, string CatalogSetHash);
public enum CreationJobStatus { Queued, Running, Ready, Committed, Cancelled, Failed, Expired }

// Immutable operation state. Workers must CAS the current job record; a result from
// an old job/request cannot overwrite the active draft. Service scheduling is P3-06.
public sealed record WorldCreationJob
{
    public const int LifetimeSeconds = 600;
    public const int MaxActiveJobs = 4, MaxJobsPerSession = 1, WorkerDeadlineSeconds = 30;
    public Guid Id { get; private init; }
    public WorldCreationRequest Request { get; private init; } = null!;
    public string RequestHash { get; private init; } = "";
    public CreationFence Fence { get; private init; } = null!;
    public DateTimeOffset ExpiresAt { get; private init; }
    public CreationJobStatus Status { get; private init; }
    public int Progress { get; private init; }
    public string? CandidateHash { get; private init; }
    public string? FailureCode { get; private init; }
    private WorldCreationJob() { }

    public static WorldCreationJob Create(Guid id, WorldCreationRequest request, CreationFence fence,
        CreationContent content, DateTimeOffset now)
    {
        if (id == Guid.Empty || fence is null || content is null || string.IsNullOrWhiteSpace(fence.SessionId) ||
            string.IsNullOrWhiteSpace(fence.ParticipantId) || string.IsNullOrWhiteSpace(fence.ConnectionId) ||
            fence.Epoch < 0 || fence.Revision < 0 || fence.CatalogSetHash != content.CatalogSetHash || !content.Validate(request).IsEmpty)
            throw new ArgumentException("Invalid creation job or unavailable content.");
        return new() { Id = id, Request = request, RequestHash = WorldCreationProfile.Fingerprint(request),
            Fence = fence, ExpiresAt = now.AddSeconds(LifetimeSeconds), Status = CreationJobStatus.Queued };
    }
    private void Guard(DateTimeOffset now, CreationFence fence, params CreationJobStatus[] allowed)
    {
        if (now >= ExpiresAt || fence != Fence || !allowed.Contains(Status)) throw new InvalidOperationException("Stale creation job.");
    }
    public WorldCreationJob Start(DateTimeOffset now, CreationFence fence)
    { Guard(now, fence, CreationJobStatus.Queued); return this with { Status = CreationJobStatus.Running }; }
    public WorldCreationJob Report(int percent, DateTimeOffset now, CreationFence fence)
    {
        Guard(now, fence, CreationJobStatus.Running);
        if (percent < Progress || percent > 99) throw new ArgumentOutOfRangeException(nameof(percent));
        return this with { Progress = percent };
    }
    public WorldCreationJob Complete(string requestHash, string candidateHash, long actualBytes, DateTimeOffset now, CreationFence fence)
    {
        Guard(now, fence, CreationJobStatus.Running);
        if (requestHash != RequestHash || !WorldCreationProfile.IsHash(candidateHash) || actualBytes <= 0 ||
            actualBytes > WorldCreationProfile.Estimate(Request).TransferBytes) throw new ArgumentException("Invalid candidate result.");
        return this with { Status = CreationJobStatus.Ready, Progress = 100, CandidateHash = candidateHash };
    }
    // Called only after the service authenticates Owner and atomically publishes the
    // candidate bytes whose canonical digest is supplied. Never regenerate on commit.
    public WorldCreationJob Commit(string requestHash, string candidateHash, bool isOwner, DateTimeOffset now, CreationFence fence)
    {
        Guard(now, fence, CreationJobStatus.Ready);
        if (!isOwner) throw new UnauthorizedAccessException();
        if (requestHash != RequestHash || candidateHash != CandidateHash) throw new ArgumentException("Candidate mismatch.");
        return this with { Status = CreationJobStatus.Committed };
    }
    public WorldCreationJob Cancel(DateTimeOffset now, CreationFence fence)
    { Guard(now, fence, CreationJobStatus.Queued, CreationJobStatus.Running, CreationJobStatus.Ready); return this with { Status = CreationJobStatus.Cancelled, CandidateHash = null }; }
    public WorldCreationJob Fail(string code, DateTimeOffset now, CreationFence fence)
    {
        Guard(now, fence, CreationJobStatus.Queued, CreationJobStatus.Running);
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128) throw new ArgumentException("Invalid failure code.");
        return this with { Status = CreationJobStatus.Failed, FailureCode = code };
    }
    public WorldCreationJob Expire(DateTimeOffset now)
    {
        if (now < ExpiresAt || Status is not (CreationJobStatus.Queued or CreationJobStatus.Running or CreationJobStatus.Ready))
            throw new InvalidOperationException("Job cannot expire.");
        return this with { Status = CreationJobStatus.Expired, CandidateHash = null };
    }
}
