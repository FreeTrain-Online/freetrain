using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using Xunit.Abstractions;

[Collection("World creation")]
public class ReliefTerrainTests(ITestOutputHelper output)
{
    private static readonly ParticipantContext Owner = new("owner", ParticipantRole.Owner, "connection1");
    private static GeneratedWorldRequest Request(ReliefKind kind = ReliefKind.Hills, int seed = 0, int z = 32)
    {
        var r = WorldCreationProfile.Generated(seed, "Relief / 起伏");
        return r with { Dimensions = new(32,32,z), Options = r.Options with { Relief=WorldCreationProfile.Relief(kind), StartingArea=0 } };
    }
    private static Task<T> Drain<T>(SessionHost host, Task<T> task) { host.Pump(); return task; }
    private static byte[] Save(GeneratedWorldRequest request) => SaveCodec.Write(new TerrainSessionState(
        ReliefTerrain.Create(request,TerrainProfiles.Bundled),TerrainProvenance.From(request)));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    [Fact]
    public void Independent_literal_samples_ground_and_corner_hashes_match_the_frozen_integer_profile()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Spatial/relief-fixtures.json")));
        foreach(var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            int N(string name)=>row.GetProperty(name).GetInt32();
            var kind=Enum.Parse<ReliefKind>(row.GetProperty("kind").GetString()!,true);
            var r=Request(kind,N("seed"),N("z")) with { Dimensions=new(N("h"),N("v"),N("z")),WaterLevel=N("water") };
            r=r with {Options=r.Options with {Relief=new(kind,N("amount"),N("featureScale"),N("smoothness"))}};
            var points=new[]{(0,0),(1,0),(2,1),(17,9),(2*r.Dimensions.H-1,r.Dimensions.V-1)};
            Assert.Equal(row.GetProperty("samples").EnumerateArray().Select(x=>x.GetInt32()),points.Select(p=>ReliefTerrain.SampleQuarterHeight(r,p.Item1,p.Item2)));
            var world=ReliefTerrain.Create(r,TerrainProfiles.Bundled);
            Assert.Equal(row.GetProperty("groundHash").GetString(),Hash(world.Cells.Select(c=>(byte)c.Ground).ToArray()));
            var corners=new byte[world.Cells.Length*2];
            foreach(var occupant in world.Occupants)
            {
                var entity=world.Spatial.Entities.Single(e=>e.Id==occupant.EntityId);var pos=world.Space.ToStorage(entity.Anchor);
                BinaryPrimitives.WriteUInt16LittleEndian(corners.AsSpan((pos.V*r.Dimensions.H+pos.H)*2),(ushort)occupant.Corners.Packed);
            }
            Assert.Equal(row.GetProperty("cornerHash").GetString(),Hash(corners));
            Assert.Equal(N("occupants"),world.Occupants.Length);
            Assert.All(world.Cells,c=>Assert.InRange(c.Ground,r.WaterLevel+1,r.Dimensions.Z-1));
            Assert.All(world.Occupants,c=>Assert.Null(c.Forest));Assert.Empty(world.Rails);
            var bytes=Save(r);Assert.Equal(bytes,Save(r));Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!));
        }
    }
    [Fact]
    public void Presets_and_controls_change_measured_relief_scale_and_detail_after_normalization()
    {
        static int[] Heights(GeneratedWorldRequest r)=>ReliefTerrain.Create(r,TerrainProfiles.Bundled).Cells.Select(c=>c.Ground).ToArray();
        static int Range(int[] h)=>h.Max()-h.Min();
        var plains=Heights(Request(ReliefKind.Plains));var hills=Heights(Request());var mountains=Heights(Request(ReliefKind.Mountains));
        Assert.True(Range(plains)<Range(hills));Assert.True(Range(hills)<Range(mountains));
        var r=Request(ReliefKind.Mountains) with {Options=Request(ReliefKind.Mountains).Options with {Relief=new(ReliefKind.Mountains,100,8,0)}};
        var rough=Heights(r);var smooth=Heights(r with {Options=r.Options with {Relief=r.Options.Relief with {Smoothness=100}}});
        // Measure fine detail with second differences: reducing fine octaves can
        // uncover larger ridges, so total first-difference variation is not monotonic.
        static long Curvature(int[] h)=>Enumerable.Range(0,h.Length).Where(i=>i%32>0 && i%32<31).Sum(i=>(long)Math.Abs(h[i-1]-2*h[i]+h[i+1]));
        Assert.Equal(424,Curvature(rough));Assert.Equal(329,Curvature(smooth));
        Assert.NotEqual(rough,Heights(r with {Options=r.Options with {Relief=r.Options.Relief with {FeatureScale=32}}}));
        Assert.NotEqual(rough,Heights(r with {Options=r.Options with {Seed=42}}));
        var flat=Heights(r with {Options=r.Options with {Relief=r.Options.Relief with {Amount=0}}});Assert.All(flat,g=>Assert.Equal(5,g));
        Assert.True(Range(rough)>Range(Heights(r with {Options=r.Options with {Relief=r.Options.Relief with {Amount=20}}})));
    }
    [Fact]
    public async Task Detached_preview_cancel_and_commit_preserve_gameplay_rng_and_save_exact_state()
    {
        using var host=SessionComposition.CreateReferenceHost();host.RegisterParticipant(Owner);
        var original=await Drain(host,host.CaptureSave());
        var request=Request();var job=await Drain(host,host.BeginGenerated(request,WorldCreationCapabilities.EmptyReady(),host.WorldEpoch,host.Snapshot().Revision,Owner));
        var bytes=Save(request);var candidate=await Drain(host,host.CompleteEmpty(job,bytes,Owner));Assert.Equal(original,await Drain(host,host.CaptureSave()));
        Assert.NotNull(candidate.Ground);
        var cancel=host.CancelEmpty(job.Id,Owner);host.Pump();await cancel;
        Assert.Equal(original,await Drain(host,host.CaptureSave()));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Drain(host,host.CompleteEmpty(job,bytes,Owner)));
        job=await Drain(host,host.BeginGenerated(request,WorldCreationCapabilities.EmptyReady(),host.WorldEpoch,host.Snapshot().Revision,Owner));
        candidate=await Drain(host,host.CompleteEmpty(job,bytes,Owner));
        await Drain(host,host.CommitEmpty(candidate.Id,candidate.RequestHash,candidate.CandidateHash,host.WorldEpoch,host.Snapshot().Revision,Owner));
        Assert.Equal(bytes,await Drain(host,host.CaptureSave()));
        var loaded=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        var initial=JsonSerializer.Serialize(loaded.Random.Capture());
        Save(Request(seed:42));Save(Request(seed:-1));
        Assert.Equal(initial,JsonSerializer.Serialize(loaded.Random.Capture()));
        var next=loaded.Random["development"].Next();Assert.Equal(new RandomStreams()["development"].Next(),next);
        loaded.AdvanceOneMinute();var continued=SaveCodec.Write(loaded);
        Assert.Equal(continued,SaveCodec.Write(SaveCodec.ReadWorld(continued,TerrainProfiles.Resolve).Terrain!));
    }
    [Theory]
    [InlineData(16,16,8,1,0)][InlineData(32,32,64,60,0)][InlineData(32,32,64,1,-2147483648)]
    public async Task Measured_actual_generation_save_restore_snapshot_admission(int h,int v,int z,int water,int seed)
    {
        using var host=SessionComposition.CreateReferenceHost();host.RegisterParticipant(Owner);
        var r=Request(ReliefKind.Mountains,seed,z) with {Dimensions=new(h,v,z),WaterLevel=water};
        r=r with {Options=r.Options with {Relief=new(ReliefKind.Mountains,100,4,0)}};
        // Warm content and serializers before measuring the synchronous production pipeline.
        Save(Request());var before=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
        var job=await Drain(host,host.BeginGenerated(r,WorldCreationCapabilities.EmptyReady(),host.WorldEpoch,host.Snapshot().Revision,Owner));
        var bytes=Save(r);var candidate=await Drain(host,host.CompleteEmpty(job,bytes,Owner));
        await Drain(host,host.CommitEmpty(candidate.Id,candidate.RequestHash,candidate.CandidateHash,host.WorldEpoch,host.Snapshot().Revision,Owner));
        var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        Assert.Equal(bytes,SaveCodec.Write(restored));
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        output.WriteLine($"{h}x{v}x{z} water={water} seed={seed}: occupants={restored.World.Occupants.Length}; save={candidate.SaveBytes}; snapshot={candidate.SnapshotBytes}; allocated={allocated}; ms={watch.Elapsed.TotalMilliseconds:F2}");
        Assert.True(allocated<WorldCreationProfile.MaxWorkingBytes);Assert.True(candidate.SaveBytes<WorldCreationProfile.Estimate(r).TransferBytes);
    }
    [Fact]
    public async Task Unsupported_features_sizes_roles_and_cancelled_work_fail_without_world_changes()
    {
        using var host=SessionComposition.CreateReferenceHost();host.RegisterParticipant(Owner);var r=Request();
        foreach(var invalid in new[]{r with {Options=r.Options with {StartingArea=1}},r with {Options=r.Options with {Coast=new(CoastKind.Island,CoastSides.None,25)}},r with {Options=r.Options with {Lakes=new(1,1)}},r with {Dimensions=new(2049,32,32)},r with {Options=r.Options with {Relief=new(ReliefKind.Hills,101,24,50)}}})
            await Assert.ThrowsAsync<ArgumentException>(()=>Drain(host,host.BeginGenerated(invalid,WorldCreationCapabilities.EmptyReady(),host.WorldEpoch,host.Snapshot().Revision,Owner)));
        foreach(var role in new[]{ParticipantRole.Observer,ParticipantRole.Operator})
        {
            var p=Owner with {Role=role};host.RegisterParticipant(p);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Drain(host,host.BeginGenerated(r,WorldCreationCapabilities.EmptyReady(),host.WorldEpoch,host.Snapshot().Revision,p)));
        }
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>ReliefTerrain.Create(r,TerrainProfiles.Bundled,cancellation.Token));
        Assert.Equal(1,host.WorldEpoch);
    }
    [Fact]
    public void Other_generation_streams_do_not_change_relief_geometry()
    {
        var request=Request();
        var baseline=ReliefTerrain.Create(request,TerrainProfiles.Bundled);
        foreach(var options in new[]{request.Options with {Forests=request.Options.Forests with {Clustering=0}},
            request.Options with {Forests=request.Options.Forests with {Clustering=100}},
            request.Options with {Rivers=request.Options.Rivers with {Meander=100}}})
        {
            var actual=ReliefTerrain.Create(request with {Options=options},TerrainProfiles.Bundled);
            Assert.Equal(baseline.Cells.ToArray(),actual.Cells.ToArray());Assert.Equal(baseline.Occupants.ToArray(),actual.Occupants.ToArray());
            Assert.Equal(baseline.Spatial.Entities.Select(e=>e.Anchor),actual.Spatial.Entities.Select(e=>e.Anchor));
        }
    }
    [Fact]
    public async Task Four_retained_relief_candidates_share_global_limits_and_release_on_cancel()
    {
        var hosts=Enumerable.Range(0,5).Select(_=>SessionComposition.CreateReferenceHost()).ToArray();
        var request=Request(ReliefKind.Mountains, z:64);var bytes=Save(request);
        try
        {
            foreach(var host in hosts)host.RegisterParticipant(Owner);
            var candidates=new List<EmptyCandidateRead>();
            for(int i=0;i<4;i++)
            {
                var host=hosts[i];var before=await Drain(host,host.CaptureSave());
                var job=await Drain(host,host.BeginGenerated(request,WorldCreationCapabilities.EmptyReady(),host.WorldEpoch,host.Snapshot().Revision,Owner));
                candidates.Add(await Drain(host,host.CompleteEmpty(job,bytes,Owner)));Assert.Equal(before,await Drain(host,host.CaptureSave()));
            }
            var fifth=hosts[4];
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Drain(fifth,fifth.BeginGenerated(request,WorldCreationCapabilities.EmptyReady(),fifth.WorldEpoch,fifth.Snapshot().Revision,Owner)));
            var cancel=hosts[0].CancelEmpty(candidates[0].Id,Owner);hosts[0].Pump();await cancel;
            Assert.NotNull(await Drain(fifth,fifth.BeginGenerated(request,WorldCreationCapabilities.EmptyReady(),fifth.WorldEpoch,fifth.Snapshot().Revision,Owner)));
            output.WriteLine($"Four retained generated candidates: save={candidates[0].SaveBytes}; snapshot={candidates[0].SnapshotBytes}; fifth blocked until cancel.");
        }
        finally{foreach(var host in hosts)host.Dispose();}
    }
    [Fact]
    public async Task Generated_http_confirmation_publishes_to_two_sockets_and_checkpoint_restart_restores_realized_state()
    {
        var directory=Path.Combine(Path.GetTempPath(),"freetrain-relief-"+Guid.NewGuid());byte[] saved;
        async Task<HttpResponseMessage> Post(HttpClient http,string path,object input)=>await http.PostAsync(path,
            new StringContent(JsonSerializer.Serialize(input,NetworkSession.Json),System.Text.Encoding.UTF8,"application/json"));
        try
        {
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                var before=await server.Bootstrap();using var first=await server.Connect();await NetworkTests.Read(first);
                var cookies=new System.Net.CookieContainer();using var http=new HttpClient(new HttpClientHandler{CookieContainer=cookies}){BaseAddress=server.Http.BaseAddress};
                await http.GetStringAsync("/api/bootstrap?protocolVersion=5&contentVersion=reference-spatial-content-2");
                using var second=await server.Connect(cookies);await NetworkTests.Read(second);
                var r=Request(ReliefKind.Mountains,z:64);
                var response=await Post(server.Http,"/api/worlds/generated/preview",new GeneratedPreviewRequest(r.Name,r.Dimensions,r.WaterLevel,r.Options,before.WorldEpoch,before.Snapshot.Revision));
                Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
                var candidate=JsonSerializer.Deserialize<EmptyCandidateRead>(await response.Content.ReadAsStringAsync(),NetworkSession.Json)!;
                var commit=new EmptyCommitRequest(candidate.Id,candidate.RequestHash,candidate.CandidateHash,before.WorldEpoch,before.Snapshot.Revision);
                Assert.True((await Post(server.Http,"/api/worlds/empty/commit",commit)).IsSuccessStatusCode);
                foreach(var socket in new[]{first,second})
                {
                    var snapshot=Assert.IsType<SnapshotMessage>(await NetworkTests.Until(socket,m=>m is SnapshotMessage));
                    Assert.Equal(before.WorldEpoch+1,snapshot.WorldEpoch);Assert.Equal(candidate.Ground,snapshot.Terrain!.Cells.Select(c=>c.Ground));Assert.Empty(snapshot.Tracks);
                }
                saved=await server.Http.GetByteArrayAsync("/api/saves/download");Assert.Equal(Save(r),saved);
            }
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                await server.Http.GetStringAsync("/api/bootstrap?protocolVersion=5&contentVersion=terrain-content-1");
                Assert.Equal(saved,await server.Http.GetByteArrayAsync("/api/saves/download"));
            }
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }

}
