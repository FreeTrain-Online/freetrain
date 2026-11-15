using System.Net;
using System.Text;
using System.Text.Json;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

[Collection("World creation")]
public class WorldCreationHttpTests
{
    static Task<HttpResponseMessage> Post(HttpClient http, string path, object data) => http.PostAsync(path,
        new StringContent(JsonSerializer.Serialize(data, NetworkSession.Json), Encoding.UTF8, "application/json"));
    static async Task<EmptyCandidateRead> Candidate(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return JsonSerializer.Deserialize<EmptyCandidateRead>(await response.Content.ReadAsStringAsync(), NetworkSession.Json)!;
    }

    [Fact]
    public async Task Empty_confirmation_rejects_forged_hash_revision_epoch_and_replay_without_mutation()
    {
        await using var server = await NetworkTests.Harness.Start(canonical: true);
        var before = await server.Bootstrap();
        using var socket = await server.Connect(); await NetworkTests.Read(socket);
        var original = await server.Http.GetByteArrayAsync("/api/saves/download");
        async Task<EmptyCommitRequest> Preview()
        {
            var current = await server.Bootstrap();
            var c = await Candidate(await Post(server.Http, "/api/worlds/empty/preview",
                new EmptyPreviewRequest("HTTP fences", new(30,42,6), 1, current.WorldEpoch, current.Snapshot.Revision)));
            return new(c.Id, c.RequestHash, c.CandidateHash, current.WorldEpoch, current.Snapshot.Revision);
        }
        var commit = await Preview();
        Assert.Equal(HttpStatusCode.Conflict, (await Post(server.Http, "/api/worlds/empty/commit", commit with { CandidateHash = new string('0',64) })).StatusCode);
        Assert.Equal(original, await server.Http.GetByteArrayAsync("/api/saves/download"));
        await NetworkTests.Send(socket, new CommandMessage("step-fence", before.WorldEpoch, new StepCommand()));
        await NetworkTests.Until(socket, m => m is AcceptedMessage);
        var stepped = await server.Http.GetByteArrayAsync("/api/saves/download");
        Assert.Equal(HttpStatusCode.Conflict, (await Post(server.Http, "/api/worlds/empty/commit", commit)).StatusCode);
        Assert.Equal(stepped, await server.Http.GetByteArrayAsync("/api/saves/download"));
        commit = await Preview();
        Assert.True((await server.Http.PostAsync($"/api/saves/upload?expectedWorldEpoch={before.WorldEpoch}", new ByteArrayContent(original))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(server.Http, "/api/worlds/empty/commit", commit)).StatusCode);
        Assert.Equal(original, await server.Http.GetByteArrayAsync("/api/saves/download"));
        commit = await Preview();
        Assert.True((await Post(server.Http, "/api/worlds/empty/commit", commit)).IsSuccessStatusCode);
        var committed = await server.Http.GetByteArrayAsync("/api/saves/download");
        Assert.Equal(HttpStatusCode.Conflict, (await Post(server.Http, "/api/worlds/empty/commit", commit)).StatusCode);
        Assert.Equal(committed, await server.Http.GetByteArrayAsync("/api/saves/download"));
    }

    [Fact]
    public async Task Generated_preview_rejects_invalid_fields_roles_and_stale_fences_and_confirmation_rejects_forged_hash()
    {
        var participants = new Dictionary<string,string?> {
            ["FreeTrainOnline:Participants:0:Id"]="viewer", ["FreeTrainOnline:Participants:0:Role"]="observer",
            ["FreeTrainOnline:Participants:0:Secret"]="observer-test" };
        await using var server = await NetworkTests.Harness.Start(canonical: true, participants: participants);
        var before = await server.Bootstrap(); using var socket = await server.Connect(); await NetworkTests.Read(socket);
        var original = await server.Http.GetByteArrayAsync("/api/saves/download");
        var r = WorldCreationProfile.Generated(0,"HTTP relief");
        var input = new GeneratedPreviewRequest(r.Name,new(32,32,32),4,r.Options with { StartingArea=0 },before.WorldEpoch,before.Snapshot.Revision);
        foreach (var invalid in new[] { input with { Dimensions=new(2049,32,32) },
            input with { Options=input.Options with { StartingArea=257 } },
            input with { Options=input.Options with { Coast=new(CoastKind.Sides,CoastSides.None,0) } } })
            Assert.Equal(HttpStatusCode.BadRequest,(await Post(server.Http,"/api/worlds/generated/preview",invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(server.Http,"/api/worlds/generated/preview",input with { Revision=input.Revision+1 })).StatusCode);
        var c=await Candidate(await Post(server.Http,"/api/worlds/generated/preview",input));
        var commit=new EmptyCommitRequest(c.Id,c.RequestHash,new string('0',64),before.WorldEpoch,before.Snapshot.Revision);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(server.Http,"/api/worlds/empty/commit",commit)).StatusCode);
        Assert.Equal(original,await server.Http.GetByteArrayAsync("/api/saves/download"));
        Assert.True((await server.Http.PostAsync($"/api/saves/upload?expectedWorldEpoch={before.WorldEpoch}",new ByteArrayContent(original))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Post(server.Http,"/api/worlds/empty/commit",commit with { CandidateHash=c.CandidateHash })).StatusCode);
        using var observer = new HttpClient(new HttpClientHandler { CookieContainer=new() }) { BaseAddress=server.Http.BaseAddress };
        Assert.True((await Post(observer,"/api/access",new { secret="observer-test" })).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(observer,"/api/worlds/generated/preview",input)).StatusCode);
        Assert.Equal(original,await server.Http.GetByteArrayAsync("/api/saves/download"));
    }
}
