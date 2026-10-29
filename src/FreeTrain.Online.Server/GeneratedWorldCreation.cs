using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Server;

public sealed record GeneratedPreviewRequest(string Name, WorldDimensions Dimensions, int WaterLevel,
    GeneratedOptions Options, long Epoch, long Revision);

public sealed partial class NetworkSession
{
    public async Task<EmptyCandidateRead> PreviewGenerated(GeneratedPreviewRequest input, ParticipantContext identity,
        CancellationToken cancellation)
    {
        using var lease = await ReserveCreationWorker(identity); // Same global input/worker/retained-candidate reservations.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lease.Disconnected);
        deadline.CancelAfter(TimeSpan.FromSeconds(WorldCreationJob.WorkerDeadlineSeconds));
        var token = deadline.Token;
        var participant = lease.Participant; var capturedHost = lease.Host;
        WorldCreationJob? started = null; Task<WorldCreationJob>? beginning = null;
        void CheckConnection()
        {
            if (host != capturedHost || CreationParticipant(identity).ConnectionId != participant.ConnectionId)
                throw new UnauthorizedAccessException();
        }
        try
        {
            var request = new GeneratedWorldRequest(WorldCreationProfile.Id, input.Name, input.Dimensions, input.WaterLevel, input.Options);
            lock (gate)
            {
                CheckConnection();
                beginning = capturedHost.BeginGenerated(request, WorldCreationCapabilities.EmptyReady(),
                    input.Epoch, input.Revision, participant, lease.Reservation);
            }
            started = await beginning.WaitAsync(token);
            var bytes = await Task.Run(() => capturedHost.PrepareCreation(
                SaveCodec.Write(new TerrainSessionState(ReliefTerrain.Create(request, TerrainProfiles.Bundled, token),
                    TerrainProvenance.From(request))), token), token);
            token.ThrowIfCancellationRequested();
            Task<EmptyCandidateRead> complete;
            lock (gate) { CheckConnection(); complete = capturedHost.CompletePreparedCreation(started, bytes, participant); }
            return await complete.WaitAsync(token);
        }
        catch
        {
            if (started == null && beginning != null) _ = AbandonCreationStart(beginning, capturedHost, participant);
            if (started != null)
            {
                Task? cancel = null; lock (gate) { if (host == capturedHost) cancel = capturedHost.CancelEmpty(started.Id, participant); }
                if (cancel != null) await cancel;
            }
            throw;
        }
    }
}
