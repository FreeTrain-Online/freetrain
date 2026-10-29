using FreeTrain.Online.Content;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

[Collection("World creation")]
public class CapacityReservationTests
{
    [Fact]
    public async Task Cancelled_large_worker_keeps_its_bytes_reserved_until_the_worker_lease_finishes()
    {
        var owner = new ParticipantContext("capacity-owner", ParticipantRole.Owner, "connection");
        using var a = SessionComposition.CreateReferenceHost(); using var b = SessionComposition.CreateReferenceHost();
        a.RegisterParticipant(owner); b.RegisterParticipant(owner);
        var r = WorldCreationProfile.Generated() with { Dimensions = new(1500, 1090, 45) };
        r = r with { Options = r.Options with { StartingArea = 0 } };
        using var worker = SessionHost.ReserveCreationInput();
        var start = a.BeginGenerated(r, WorldCreationCapabilities.EmptyReady(), a.WorldEpoch, a.Snapshot().Revision, owner, worker); a.Pump();
        var job = await start;
        async Task Denied()
        {
            var attempt = b.BeginGenerated(r, WorldCreationCapabilities.EmptyReady(), b.WorldEpoch, b.Snapshot().Revision, owner); b.Pump();
            Assert.Equal("creationBusy", (await Assert.ThrowsAsync<InvalidOperationException>(() => attempt)).Message);
        }
        await Denied();
        var cancel = a.CancelEmpty(job.Id, owner); a.Pump(); await cancel;
        await Denied(); // Host invalidation cannot release still-running worker memory.
        worker.Dispose();
        var retry = b.BeginGenerated(r, WorldCreationCapabilities.EmptyReady(), b.WorldEpoch, b.Snapshot().Revision, owner); b.Pump();
        Assert.Equal(CreationJobStatus.Running, (await retry).Status);
    }
}
