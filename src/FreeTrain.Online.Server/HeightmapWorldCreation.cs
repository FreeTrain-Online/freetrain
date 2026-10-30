using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Server;

public sealed record HeightmapPreviewOptions(string Name, WorldDimensions Dimensions, int WaterLevel, long Epoch, long Revision, string? Provider = null);
public sealed partial class NetworkSession
{
    // Reserve before reading an upload. Slow clients and decoders share the same four slots.
    public async Task<CreationWorkerLease> ReserveCreationWorker(ParticipantContext identity)
    {
        ParticipantContext participant; SessionHost capturedHost; CancellationToken disconnected;
        lock (gate) { participant = CreationParticipant(identity); capturedHost = host; disconnected = peers[participant.ParticipantId].Stop.Token; }
        if (!await creationWorkers.WaitAsync(0)) throw new InvalidOperationException("creationBusy");
        try { return new(creationWorkers, SessionHost.ReserveCreationInput(), participant, capturedHost, disconnected); }
        catch { creationWorkers.Release(); throw; }
    }
    public sealed class CreationWorkerLease(SemaphoreSlim semaphore, SessionHost.CreationInputReservation reservation, ParticipantContext participant, SessionHost capturedHost, CancellationToken disconnected) : IDisposable
    {
        private int disposed;
        public ParticipantContext Participant { get; } = participant;
        public SessionHost Host { get; } = capturedHost;
        public SessionHost.CreationInputReservation Reservation { get; } = reservation;
        public CancellationToken Disconnected { get; } = disconnected;
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) { Reservation.Dispose(); semaphore.Release(); } }
    }

    public async Task<EmptyCandidateRead> PreviewHeightmap(HeightmapPreviewOptions input, byte[] bytes, string? mediaType,
        OptionalPluginRegistry registry, ParticipantContext identity, CreationWorkerLease lease, CancellationToken cancellation)
    {
        using var connection = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lease.Disconnected);
        cancellation = connection.Token;
        WorldCreationJob? started = null; Task<WorldCreationJob>? beginning = null;
        var participant = lease.Participant; var capturedHost = lease.Host;
        void CheckConnection()
        {
            if (host != capturedHost || CreationParticipant(identity).ConnectionId != participant.ConnectionId) throw new UnauthorizedAccessException();
        }
        lock (gate) CheckConnection();
        if (input.Dimensions is not { H: >= 1 and <= WorldCreationProfile.MaxSide, V: >= 1 and <= WorldCreationProfile.MaxSide, Z: >= 1 and <= 64 }) throw new ArgumentException("invalidInput");
        try
        {
            // No metadata from the browser enters the trusted request.
            var decoded = await Task.Run(() => HeightmapDecoder.Decode(bytes, mediaType, cancellation), cancellation);
            var image = decoded.Input;
            CreationContent content = WorldCreationCapabilities.EmptyReady();
            if (input.Provider is { } provider)
            {
                var map = FixedHeightmaps.Read(registry).SingleOrDefault(m => m.Binding.ContributionId == provider) ?? throw new ArgumentException("invalidInput");
                if (map.Dimensions != input.Dimensions || map.WaterLevel != input.WaterLevel || image.Sha256 != map.Sha256 || image.EncodedBytes != map.Bytes)
                    throw new ArgumentException("invalidInput");
                image = image with { Provider = map.Binding }; content = FixedHeightmaps.Ready(registry);
            }
            var request = new HeightmapWorldRequest(WorldCreationProfile.Id, input.Name, input.Dimensions, input.WaterLevel, image);
            lock (gate) { CheckConnection(); beginning = capturedHost.BeginHeightmap(request, content, input.Epoch, input.Revision, participant, lease.Reservation); }
            started = await beginning.WaitAsync(cancellation);
            var stateBytes = await Task.Run(() => capturedHost.PrepareCreation(SaveCodec.Write(new TerrainSessionState(
                HeightmapTerrain.Create(request, decoded.Raster, TerrainProfiles.Bundled, cancellation), TerrainProvenance.From(request))), cancellation), cancellation);
            cancellation.ThrowIfCancellationRequested();
            Task<EmptyCandidateRead> complete;
            lock (gate) { CheckConnection(); complete = capturedHost.CompletePreparedCreation(started, stateBytes, participant); }
            return (await complete.WaitAsync(cancellation)) with { ImagePreview = HeightmapDecoder.Preview(decoded.Raster) };
        }
        catch
        {
            // A cancelled wait may race the FIFO start. Observe its eventual result
            // and cancel by job ID so it cannot strand a retained reservation.
            if (started == null && beginning != null) _ = AbandonCreationStart(beginning, capturedHost, participant);
            if (started != null)
            {
                Task? cancel = null; lock (gate) { if (host == capturedHost) cancel = capturedHost.CancelEmpty(started.Id, participant); }
                if (cancel != null) await cancel;
            }
            throw;
        }
    }
    private async Task AbandonCreationStart(Task<WorldCreationJob> beginning, SessionHost capturedHost, ParticipantContext participant)
    {
        try
        {
            var job = await beginning;
            Task? cancel = null; lock (gate) { if (host == capturedHost) cancel = capturedHost.CancelEmpty(job.Id, participant); }
            if (cancel != null) await cancel;
        }
        catch (Exception) { /* Rejected starts/disconnects already preserve the world and release ownership. */ }
    }
}
