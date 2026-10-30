using FreeTrain.Online.Content;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using FreeTrain.Online.Persistence;

namespace FreeTrain.Online.Server;

public sealed record EmptyPreviewRequest(string Name, WorldDimensions Dimensions, int WaterLevel, long Epoch, long Revision);
public sealed record EmptyCommitRequest(Guid Id, string RequestHash, string CandidateHash, long Epoch, long Revision);
public sealed partial class NetworkSession
{
    private static readonly SemaphoreSlim creationWorkers = new(WorldCreationJob.MaxActiveJobs);
    private ParticipantContext CreationParticipant(ParticipantContext identity)
    {
        if (!peers.TryGetValue(identity.ParticipantId, out var peer) || peer.Participant.Role != ParticipantRole.Owner ||
            peer.Participant.Role != identity.Role) throw new UnauthorizedAccessException();
        return peer.Participant;
    }
    public async Task<EmptyCandidateRead> PreviewEmpty(EmptyPreviewRequest input, ParticipantContext identity, CancellationToken cancellation = default)
    {
        using var lease = await ReserveCreationWorker(identity);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lease.Disconnected);
        timeout.CancelAfter(TimeSpan.FromSeconds(WorldCreationJob.WorkerDeadlineSeconds));
        WorldCreationJob? started = null;
        Task<WorldCreationJob>? begin = null;
        var participant = lease.Participant; var capturedHost = lease.Host;
        try
        {
            lock (gate) begin = capturedHost.BeginEmpty(new(WorldCreationProfile.Id, input.Name, input.Dimensions, input.WaterLevel),
                WorldCreationCapabilities.EmptyReady(), input.Epoch, input.Revision, participant, lease.Reservation);
            started = await begin.WaitAsync(timeout.Token);
            var bytes = await Task.Run(() => capturedHost.PrepareCreation(SaveCodec.Write(new TerrainSessionState(
                TerrainWorld.Empty((EmptyWorldRequest)started.Request, TerrainProfiles.Bundled), TerrainProvenance.From(started.Request))), timeout.Token), timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            Task<EmptyCandidateRead> complete;
            lock (gate) complete = capturedHost.CompletePreparedCreation(started, bytes, participant);
            return await complete.WaitAsync(timeout.Token);
        }
        catch
        {
            if (started == null && begin != null) _ = AbandonCreationStart(begin, capturedHost, participant);
            if (started != null)
            {
                Task? cancel = null; lock (gate) { if (host == capturedHost) cancel = capturedHost.CancelEmpty(started.Id, participant); }
                if (cancel != null) await cancel;
            }
            throw;
        }
    }
    public Task CancelEmpty(Guid id, ParticipantContext identity)
    { lock (gate) return host.CancelEmpty(id, CreationParticipant(identity)); }
    public async Task CommitEmpty(EmptyCommitRequest input, ParticipantContext identity)
    {
        Task committed;
        lock (gate) committed = host.CommitEmpty(input.Id, input.RequestHash, input.CandidateHash, input.Epoch, input.Revision, CreationParticipant(identity));
        await committed;
        lock (gate)
        {
            disconnectCheckpoint = true;
            foreach (var peer in peers.Values.ToArray()) Publish(peer);
        }
    }
}
