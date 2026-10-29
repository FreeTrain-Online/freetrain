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
    public async Task<EmptyCandidateRead> PreviewEmpty(EmptyPreviewRequest input, ParticipantContext identity)
    {
        WorldCreationJob? started = null;
        ParticipantContext participant;
        SessionHost capturedHost;
        lock (gate) { capturedHost = host; participant = CreationParticipant(identity); }
        if (!await creationWorkers.WaitAsync(0)) throw new InvalidOperationException("creationBusy");
        try
        {
            Task<WorldCreationJob> begin;
            lock (gate) begin = capturedHost.BeginEmpty(new(WorldCreationProfile.Id, input.Name, input.Dimensions, input.WaterLevel),
                WorldCreationCapabilities.EmptyReady(), input.Epoch, input.Revision, participant);
            started = await begin;
            // Flat generation and canonical serialization run off the session FIFO. The
            // admitted 4096 cells are bounded; later algorithms need cooperative work/deadline checks.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(WorldCreationJob.WorkerDeadlineSeconds));
            var bytes = await Task.Run(() => SaveCodec.Write(new TerrainSessionState(
                TerrainWorld.Empty((EmptyWorldRequest)started.Request, TerrainProfiles.Bundled), TerrainProvenance.From(started.Request))), timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            Task<EmptyCandidateRead> complete;
            lock (gate) complete = capturedHost.CompleteEmpty(started, bytes, participant);
            return await complete;
        }
        catch
        {
            if (started != null) { Task cancel; lock (gate) cancel = capturedHost.CancelEmpty(started.Id, participant); await cancel; }
            throw;
        }
        finally { creationWorkers.Release(); }
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
