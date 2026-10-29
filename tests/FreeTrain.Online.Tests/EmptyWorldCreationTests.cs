using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using Xunit.Abstractions;

[Collection("World creation")]
public class EmptyWorldCreationTests(ITestOutputHelper output)
{
    private static readonly ParticipantContext Owner = new("owner", ParticipantRole.Owner, "connection1");
    private static SessionHost Host()
    {
        var host = SessionComposition.CreateReferenceHost(); host.RegisterParticipant(Owner); return host;
    }
    private static async Task<T> Drain<T>(SessionHost host, Task<T> task) { host.Pump(); return await task; }
    private static byte[] Generate(WorldCreationJob job) => SaveCodec.Write(new TerrainSessionState(
        TerrainWorld.Empty((EmptyWorldRequest)job.Request, TerrainProfiles.Bundled), TerrainProvenance.From(job.Request)));
    private static Task<WorldCreationJob> Start(SessionHost host, EmptyWorldRequest? request = null, ParticipantContext? participant = null) =>
        host.BeginEmpty(request ?? WorldCreationProfile.Empty("新しい世界"), WorldCreationCapabilities.EmptyReady(),
            host.WorldEpoch, host.Snapshot().Revision, participant ?? Owner);
    private static Task<SnapshotMessage> Commit(SessionHost host, EmptyCandidateRead c, ParticipantContext? participant = null) =>
        host.CommitEmpty(c.Id, c.RequestHash, c.CandidateHash, host.WorldEpoch, host.Snapshot().Revision, participant ?? Owner);

    [Fact]
    public async Task Preview_cancel_exact_commit_and_reopen_zero_rails_preserve_source_defaults()
    {
        using var host=Host(); var original=host.Snapshot(); var originalBytes=await Drain(host,host.CaptureSave());
        var job=await Drain(host,Start(host)); var bytes=Generate(job);
        var candidate=await Drain(host,host.CompleteEmpty(job,bytes,Owner));
        Assert.Equal(originalBytes,await Drain(host,host.CaptureSave()));
        var cancel=host.CancelEmpty(candidate.Id,Owner);host.Pump();await cancel;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Drain(host,Commit(host,candidate)));
        job=await Drain(host,Start(host));candidate=await Drain(host,host.CompleteEmpty(job,Generate(job),Owner));
        var snapshot=await Drain(host,Commit(host,candidate));
        Assert.Equal(original.WorldEpoch+1,snapshot.WorldEpoch);Assert.Empty(snapshot.Tracks);Assert.Empty(snapshot.Entities!);
        Assert.Equal(30,snapshot.Map.Width);Assert.Equal(42,snapshot.Map.Height);Assert.Equal(6,snapshot.Map.Levels);
        Assert.Equal(1,snapshot.Terrain!.WaterLevel);Assert.True(snapshot.Paused);
        Assert.All(snapshot.Terrain.Cells,c=>Assert.Equal(1,c.Ground));
        var captured=await Drain(host,host.CaptureSave());Assert.Equal(bytes,captured);
        var loaded=await Drain(host,host.LoadSave(captured));Assert.Equal(snapshot.WorldEpoch+1,loaded.WorldEpoch);
        Assert.Equal(captured,await Drain(host,host.CaptureSave()));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Drain(host,Commit(host,candidate)));
    }
    [Theory]
    [InlineData("operator")][InlineData("observer")][InlineData("disconnect")][InlineData("revision")][InlineData("epoch")][InlineData("hash")][InlineData("request")][InlineData("id")]
    public async Task Fenced_rejection_never_replaces_current_world(string reason)
    {
        using var host=Host();var job=await Drain(host,Start(host));var candidate=await Drain(host,host.CompleteEmpty(job,Generate(job),Owner));
        var participant=Owner;
        switch(reason)
        {
            case "operator": participant=Owner with {Role=ParticipantRole.Operator};host.RegisterParticipant(participant);break;
            case "observer": participant=Owner with {Role=ParticipantRole.Observer};host.RegisterParticipant(participant);break;
            case "disconnect":host.DisconnectParticipant(Owner);host.RegisterParticipant(Owner with {ConnectionId="connection2"});break;
            case "revision":await Drain(host,host.Enqueue(Owner,new("step",host.WorldEpoch,new StepCommand())));break;
            case "epoch":await Drain(host,host.LoadSave(Generate(job)));break;
            case "hash":candidate=candidate with {CandidateHash=new string('0',64)};break;
            case "request":candidate=candidate with {RequestHash=new string('0',64)};break;
            case "id":candidate=candidate with {Id=Guid.NewGuid()};break;
        }
        var before=await Drain(host,host.CaptureSave());var epoch=host.WorldEpoch;
        var result=Commit(host,candidate,participant);host.Pump();await Assert.ThrowsAnyAsync<Exception>(async()=>await result);
        Assert.Equal(epoch,host.WorldEpoch);Assert.Equal(before,await Drain(host,host.CaptureSave()));
    }
    [Fact]
    public async Task Late_worker_cannot_replace_a_new_job_and_unavailable_roles_cannot_start()
    {
        using var host=Host();var old=await Drain(host,Start(host));var cancelled=host.CancelEmpty(old.Id,Owner);host.Pump();await cancelled;
        var next=await Drain(host,Start(host));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Drain(host,host.CompleteEmpty(old,Generate(old),Owner)));
        var candidate=await Drain(host,host.CompleteEmpty(next,Generate(next),Owner));await Drain(host,Commit(host,candidate));
        foreach(var role in new[]{ParticipantRole.Operator,ParticipantRole.Observer})
        {
            var p=new ParticipantContext(role.ToString(),role);host.RegisterParticipant(p);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Drain(host,Start(host,participant:p)));
        }
    }
    [Theory]
    [InlineData(1,1,1,0)][InlineData(30,42,6,1)][InlineData(64,64,64,63)]
    public async Task Real_flat_state_admission_measurements(int h,int v,int z,int water)
    {
        using var host=Host();var before=GC.GetAllocatedBytesForCurrentThread();var timer=System.Diagnostics.Stopwatch.StartNew();
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Capacity",new(h,v,z),water);
        var job=await Drain(host,Start(host,request));var bytes=Generate(job);
        var candidate=await Drain(host,host.CompleteEmpty(job,bytes,Owner));await Drain(host,Commit(host,candidate));
        timer.Stop();var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        output.WriteLine($"{h}x{v}x{z}: save={candidate.SaveBytes}; snapshot={candidate.SnapshotBytes}; total managed allocations={allocated}; elapsed ms={timer.Elapsed.TotalMilliseconds:F2}");
        Assert.True(candidate.SaveBytes<=WorldCreationProfile.Estimate(request).TransferBytes);
        Assert.True(candidate.SnapshotBytes<=WorldCreationProfile.Estimate(request).TransferBytes);
        Assert.True(allocated<WorldCreationProfile.MaxWorkingBytes);
    }
    [Fact]
    public async Task Admission_rejects_larger_and_invalid_inputs_before_generation()
    {
        using var host=Host();
        foreach(var request in new[]{WorldCreationProfile.Empty() with {Dimensions=new(2049,1,6)},
            WorldCreationProfile.Empty() with {Dimensions=new(int.MaxValue,128,64)},WorldCreationProfile.Empty() with {WaterLevel=6}})
            await Assert.ThrowsAsync<ArgumentException>(()=>Drain(host,Start(host,request)));
        Assert.Equal(1,host.WorldEpoch);
    }
    [Fact]
    public async Task Unready_content_and_global_reservations_fail_closed_and_release_on_disposal()
    {
        var hosts=Enumerable.Range(0,5).Select(_=>Host()).ToArray();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(()=>Drain(hosts[0],hosts[0].BeginEmpty(WorldCreationProfile.Empty(),
                WorldCreationCapabilities.Inspect(PreparedContentCatalog.Bundled),1,0,Owner)));
            for(var i=0;i<4;i++) await Drain(hosts[i],Start(hosts[i]));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Drain(hosts[4],Start(hosts[4])));
            hosts[0].Dispose();await Drain(hosts[4],Start(hosts[4]));
        }
        finally { foreach(var host in hosts) host.Dispose(); }
    }

}
