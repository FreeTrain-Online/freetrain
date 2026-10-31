using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Server;
using FreeTrain.Online.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class NetworkTests
{
    [Fact]
    public async Task Spatial_preview_is_authenticated_read_only_and_fenced_by_world_epoch()
    {
        await using var server = await Harness.Start();
        const string path = "/api/spatial/preview?protocolVersion=6&contentVersion=prototype-content-1";
        var query = new SpatialQuery(1, new(12, 25, 1), new(10, 20, 1), 8);
        Task<HttpResponseMessage> Preview(SpatialQuery q) => server.Http.PostAsync(path, new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(q, NetworkSession.Json)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Preview(query)).StatusCode);
        var before = await server.Bootstrap();
        Assert.Equal(ProtocolIdentity.Version, before.ProtocolVersion);
        Assert.Equal((HttpStatusCode)426, (await server.Http.GetAsync("/api/bootstrap?protocolVersion=2&contentVersion=prototype-content-1")).StatusCode);
        var response = await Preview(query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<SpatialPreviewRead>(await response.Content.ReadAsStringAsync(), NetworkSession.Json)!;
        Assert.Equal(new PositionRead(12, 22, 1), result.Position); Assert.Equal(3, result.Heading);
        Assert.Equal(HttpStatusCode.Conflict, (await Preview(query with { ExpectedWorldEpoch = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Preview(query with { Position = new(int.MaxValue, 0, 1) })).StatusCode);
        Assert.Equal(JsonSerializer.Serialize(before.Snapshot, NetworkSession.Json), JsonSerializer.Serialize((await server.Bootstrap()).Snapshot, NetworkSession.Json));
    }

    [Fact]
    public async Task Observer_can_query_spatial_geometry_without_mutation_authority()
    {
        await using var server = await Harness.Start("owner-secret", participants: new()
        {
            ["FreeTrainOnline:Participants:0:Id"] = "viewer",
            ["FreeTrainOnline:Participants:0:Role"] = "observer",
            ["FreeTrainOnline:Participants:0:Secret"] = "viewer-secret"
        });
        Assert.Equal(HttpStatusCode.NoContent, (await server.Http.PostAsJsonAsync("/api/access", new { secret = "viewer-secret" })).StatusCode);
        var before = await server.Bootstrap();
        Assert.Equal("observer", before.Role);
        var query = new SpatialQuery(1, new(10, 20, 1), null, 0);
        var response = await server.Http.PostAsync("/api/spatial/preview?protocolVersion=6&contentVersion=prototype-content-1", new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(query, NetworkSession.Json)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before.Snapshot.Revision, (await server.Bootstrap()).Snapshot.Revision);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("127.0.0.1", true)]
    public async Task Only_explicitly_trusted_proxy_can_supply_https_for_origin_and_secure_cookie(string? trustedProxy, bool trusted)
    {
        await using var server = await Harness.Start("proxy-secret", trustedProxy: trustedProxy);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/access")
        { Content = JsonContent.Create(new { secret = "proxy-secret" }) };
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("Origin", server.Http.BaseAddress!.ToString().Replace("http:", "https:").TrimEnd('/'));
        var response = await server.Http.SendAsync(request);
        Assert.Equal(trusted ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
        if (trusted)
        {
            var cookie = response.Headers.GetValues("Set-Cookie").Single();
            Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
            using var foreign = new HttpRequestMessage(HttpMethod.Post, "/api/access")
            { Content = JsonContent.Create(new { secret = "proxy-secret" }) };
            foreign.Headers.Add("X-Forwarded-Proto", "https");
            foreign.Headers.Add("Origin", "https://foreign.example");
            Assert.Equal(HttpStatusCode.Forbidden, (await server.Http.SendAsync(foreign)).StatusCode);
        }
    }
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static CommandMessage Command(string id, CommandPayload payload, long epoch = 1) => new(id, epoch, payload);
    private static async Task<WireMessage> Apply(NetworkSession session, NetworkSession.Peer peer, CommandMessage command, DateTimeOffset? now = null)
    {
        session.Touch(peer, now ?? Now);
        var task = session.Submit(peer, command, now ?? Now);
        session.Tick(now ?? Now);
        return await task;
    }
    [Fact]
    public async Task Receipts_share_pending_and_committed_outcomes_and_do_not_charge_twice()
    {
        var session = PrototypeNetwork.Create(new());
        var peer = session.Connect(Now)!;
        var command = Command("build", new BuildTrackCommand(new(37, 16, 1), "buffer"));
        var pending = session.Submit(peer, command, Now);
        Assert.False(pending.IsCompleted);
        Assert.Same(pending, session.Submit(peer, command, Now));
        session.Tick(Now);
        var accepted = Assert.IsType<AcceptedMessage>(await pending);
        Assert.Equal(1, accepted.Sequence);
        var cash = session.Bootstrap().Snapshot.Cash;
        Assert.True(cash < 1000000);
        Assert.Equal(accepted, await session.Submit(peer, command, Now.AddSeconds(10)));
        Assert.Equal(cash, session.Bootstrap().Snapshot.Cash);
        Assert.Equal("requestConflict", Assert.IsType<RejectedMessage>(await session.Submit(peer, command with { Payload = new StepCommand() }, Now)).Code);
        session.Disconnect(peer, Now);
        var next = session.Connect(Now)!;
        Assert.Equal(accepted, await session.Submit(next, command, Now.AddSeconds(11)));
    }
    [Fact]
    public async Task Receipt_capacity_rejects_new_ids_without_evicting_retry_guarantees()
    {
        var session = PrototypeNetwork.Create(new() { ReceiptCapacity = 1 });
        var peer = session.Connect(Now)!;
        var command = Command("step", new StepCommand());
        var first = await Apply(session, peer, command);
        Assert.Equal("retryWindowFull", Assert.IsType<RejectedMessage>(await session.Submit(peer, Command("other", new StepCommand()), Now)).Code);
        Assert.Equal(first, await session.Submit(peer, command, Now));
        // Expiry is explicit: a fresh ID becomes available after two minutes.
        Assert.IsType<AcceptedMessage>(await Apply(session, peer, Command("fresh", new StepCommand()), Now.AddMinutes(3)));
    }
    [Fact]
    public async Task Stale_epochs_and_entity_edits_preserve_state_and_rejections_are_deduplicated()
    {
        var session = PrototypeNetwork.Create(new()); var peer = session.Connect(Now)!;
        var initial = session.Bootstrap().Snapshot;
        var stale = Command("stale", new StepCommand(), 2);
        Assert.Equal("staleEpoch", Assert.IsType<RejectedMessage>(await Apply(session, peer, stale)).Code);
        var edit = Command("edit", new RenameStationCommand("station-1", 99, "新宿駅"));
        var rejected = await Apply(session, peer, edit);
        Assert.Equal("staleRevision", Assert.IsType<RejectedMessage>(rejected).Code);
        Assert.Equal(rejected, await session.Submit(peer, edit, Now));
        Assert.Equal(initial.Revision, session.Bootstrap().Snapshot.Revision);
    }
    [Fact]
    public async Task Wall_time_input_expiry_works_while_paused_and_disconnect_grace_pauses_running_game()
    {
        var session = PrototypeNetwork.Create(new()); var peer = session.Connect(Now)!;
        await Apply(session, peer, Command("acquire", new AcquireControlCommand("train-1")));
        await Apply(session, peer, Command("manual", new SetControllerCommand("train-1", 1, "manual", "1")));
        await Apply(session, peer, Command("input", new ControllerInputCommand("train-1", 1, false, true, false, false, false, false, "1")));
        var before = session.Bootstrap().Snapshot;
        session.Tick(Now.AddMilliseconds(600));
        var expired = session.Bootstrap().Snapshot;
        Assert.Equal(0, expired.Tick);
        Assert.True(expired.Trains[0].Revision > before.Trains[0].Revision);
        await Apply(session, peer, Command("run", new RunCommand()), Now.AddMilliseconds(650));
        session.Disconnect(peer, Now.AddMilliseconds(700));
        session.Tick(Now.AddMilliseconds(800)); Assert.False(session.Bootstrap().Snapshot.Paused);
        session.Tick(Now.AddSeconds(2)); Assert.True(session.Bootstrap().Snapshot.Paused);
        var next = session.Connect(Now.AddSeconds(2))!;
        Assert.Null(session.Connect(Now.AddSeconds(2)));
        Assert.True(session.Bootstrap().Snapshot.Paused);
        session.Disconnect(next, Now.AddSeconds(3));
        session.Tick(Now.AddMinutes(6));
        Assert.Equal(2, session.Bootstrap().WorldEpoch);
        Assert.Equal(0, session.Bootstrap().Snapshot.Tick);
        Assert.True(session.Bootstrap().Snapshot.Paused);
    }
    [Fact]
    public async Task Slow_output_is_disconnected_without_blocking_host_or_dropping_dependent_deltas()
    {
        var session = PrototypeNetwork.Create(new() { OutputCapacity = 2 }); var peer = session.Connect(Now)!;
        await Apply(session, peer, Command("run", new RunCommand()));
        session.Tick(Now.AddMilliseconds(100));
        Assert.True(peer.Stop.IsCancellationRequested);
        Assert.True(session.Bootstrap().Snapshot.Tick >= 2);
        var recovered = session.Connect(Now.AddMilliseconds(200));
        Assert.NotNull(recovered);
        Assert.IsType<JoinMessage>(await recovered!.Output.Reader.ReadAsync());
    }
    [Fact]
    public void Idle_socket_is_released_and_deltas_include_scalars_tracks_and_all_removals()
    {
        var session = PrototypeNetwork.Create(new()); var peer = session.Connect(Now)!;
        session.Tick(Now.AddSeconds(16)); Assert.True(peer.Stop.IsCancellationRequested);
        var old = session.Bootstrap().Snapshot;
        var next = old with { Revision = old.Revision + 1, Tick = 1, Cash = 9007199254740993L, Paused = false,
            Tracks = old.Tracks.Skip(1).ToArray(), Buildings = [], Stations = [], Trains = [] };
        var delta = NetworkSession.Difference(old, next);
        Assert.Equal(old.Revision, delta.BaseRevision); Assert.False(delta.Paused);
        Assert.Equal(next.Cash, delta.Cash); Assert.Single(delta.Removed.TrackPositions);
        Assert.Equal(old.Trains.Select(t => t.Id), delta.Removed.TrainIds);
        Assert.Equal(old.Buildings.Select(t => t.Id), delta.Removed.BuildingIds);
        Assert.Equal(old.Stations.Select(t => t.Id), delta.Removed.StationIds);
        Assert.Empty(delta.Tracks);
    }
    [Fact]
    public async Task Publication_slots_preserve_rate_under_timer_jitter_without_catch_up_bursts()
    {
        var started = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        var session = PrototypeNetwork.Create(new()); var peer = session.Connect(started)!;
        await Apply(session, peer, Command("run", new RunCommand()), started);
        while (peer.Output.Reader.TryRead(out _)) { }
        var publications = 0;
        for (var index = 1; index <= 20; index++)
        {
            session.Tick(started.AddMilliseconds(index * 50 - (index % 3 == 0 ? 0 : 1)));
            while (peer.Output.Reader.TryRead(out var message)) if (message is DeltaMessage) publications++;
        }
        Assert.Equal(9, publications); // 100..900ms slots; first publication was at 0ms.
        session.Tick(started.AddSeconds(3));
        Assert.True(peer.Output.Reader.TryRead(out var afterStall)); Assert.IsType<DeltaMessage>(afterStall);
        Assert.False(peer.Output.Reader.TryRead(out _));
    }
    [Fact]
    public async Task Real_http_and_socket_enforce_access_versions_origins_and_exclusive_owner()
    {
        await using var server = await Harness.Start("test-operator-secret");
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Http.GetAsync(Harness.BootstrapPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.Http.PostAsJsonAsync("/api/access", new { secret = "wrong" })).StatusCode);
        using var crossSite = new HttpRequestMessage(HttpMethod.Post, "/api/access") { Content = JsonContent.Create(new { secret = "test-operator-secret" }) };
        crossSite.Headers.Add("Origin", "https://untrusted.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Http.SendAsync(crossSite)).StatusCode);
        var login = await server.Http.PostAsJsonAsync("/api/access", new { secret = "test-operator-secret" });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
        Assert.Equal((HttpStatusCode)426, (await server.Http.GetAsync("/api/bootstrap?protocolVersion=99&contentVersion=prototype-content-1")).StatusCode);
        Assert.Equal((HttpStatusCode)426, (await server.Http.GetAsync("/api/bootstrap?protocolVersion=6&contentVersion=bad")).StatusCode);
        var join = await server.Bootstrap(); Assert.Equal("operator-1", join.ParticipantId);
        using var socket = await server.Connect(); Assert.IsType<JoinMessage>(await Read(socket));
        using var competitor = new ClientWebSocket(); competitor.Options.Cookies = server.Cookies;
        await Assert.ThrowsAsync<WebSocketException>(() => competitor.ConnectAsync(server.SocketUri, CancellationToken.None));
        using var unauthenticated = new ClientWebSocket();
        await Assert.ThrowsAsync<WebSocketException>(() => unauthenticated.ConnectAsync(server.SocketUri, CancellationToken.None));
        using var evil = new ClientWebSocket(); evil.Options.Cookies = server.Cookies; evil.Options.SetRequestHeader("Origin", "https://untrusted.invalid");
        await Assert.ThrowsAsync<WebSocketException>(() => evil.ConnectAsync(server.SocketUri, CancellationToken.None));
    }
    [Fact]
    public async Task Real_socket_commits_before_ack_retries_and_reconnects_to_confirmed_snapshot()
    {
        await using var server = await Harness.Start(); await server.Bootstrap();
        using var socket = await server.Connect(); var join = Assert.IsType<JoinMessage>(await Read(socket));
        var step = Command("retry", new StepCommand());
        await Send(socket, step);
        var delta = Assert.IsType<DeltaMessage>(await Read(socket));
        Assert.Equal(1, delta.Tick); Assert.Equal(join.Snapshot.Revision, delta.BaseRevision);
        var accepted = Assert.IsType<AcceptedMessage>(await Read(socket)); Assert.Equal(delta.Revision, accepted.Revision);
        await Send(socket, step); Assert.Equal(accepted, await Read(socket));
        await Send(socket, Command("stale", new StepCommand(), 99));
        Assert.Equal("staleEpoch", Assert.IsType<RejectedMessage>(await Read(socket)).Code);
        await Send(socket, Command("rename", new RenameStationCommand("station-1", 0, "東京駅")));
        Assert.Equal("東京駅", Assert.IsType<DeltaMessage>(await Read(socket)).Stations[0].Name);
        Assert.IsType<AcceptedMessage>(await Read(socket));
        await Send(socket, new ResyncMessage()); Assert.Equal(1, Assert.IsType<SnapshotMessage>(await Read(socket)).Tick);
        socket.Abort();
        using var recovered = await server.ConnectEventually();
        var fresh = Assert.IsType<JoinMessage>(await Read(recovered));
        Assert.Equal("東京駅", fresh.Snapshot.Stations[0].Name); Assert.Equal(1, fresh.Snapshot.Tick);
        await Send(recovered, step); Assert.Equal(accepted, await Read(recovered));
        await Send(recovered, Command("run", new RunCommand()));
        await Until(recovered, x => x is AcceptedMessage a && a.RequestId == "run");
        await Send(recovered, Command("explicit-pause", new PauseCommand()));
        var paused = Assert.IsType<DeltaMessage>(await Until(recovered, x => x is DeltaMessage d && d.Paused));
        Assert.True(paused.Paused);
    }
    [Fact]
    public async Task Fragmented_delayed_sender_does_not_freeze_clock_or_publication()
    {
        await using var server = await Harness.Start(); await server.Bootstrap();
        using var socket = await server.Connect(); await Read(socket);
        await Send(socket, Command("run", new RunCommand()));
        var accepted = Assert.IsType<AcceptedMessage>(await Until(socket, x => x is AcceptedMessage));
        // Leave a message unfinished while the real server continues ticking and publishing.
        var data = JsonSerializer.SerializeToUtf8Bytes<WireMessage>(Command("paused-step", new StepCommand()), NetworkSession.Json);
        await socket.SendAsync(new ArraySegment<byte>(data, 0, 10), WebSocketMessageType.Text, false, CancellationToken.None);
        var update = Assert.IsType<DeltaMessage>(await Until(socket, x => x is DeltaMessage d && d.Tick >= accepted.Tick + 2));
        Assert.True(update.Tick > accepted.Tick);
        await socket.SendAsync(new ArraySegment<byte>(data, 10, data.Length - 10), WebSocketMessageType.Text, true, CancellationToken.None);
        var rejected = Assert.IsType<RejectedMessage>(await Until(socket, x => x is RejectedMessage));
        Assert.Equal("invalidInput", rejected.Code);
        await Send(socket, Command("explicit-pause", new PauseCommand()));
        Assert.IsType<DeltaMessage>(await Until(socket, x => x is DeltaMessage d && d.Paused));
    }
    [Theory]
    [InlineData("{\"type\":\"command\",\"requestId\":\"bad\",\"expectedWorldEpoch\":\"1\",\"payload\":null}")]
    [InlineData("{\"type\":\"heartbeat\",\"extra\":true}")]
    [InlineData("oversized")]
    public async Task Malformed_or_oversized_messages_close_only_the_connection(string json)
    {
        await using var server = await Harness.Start(); await server.Bootstrap();
        using var socket = await server.Connect(); await Read(socket);
        var bytes = Encoding.UTF8.GetBytes(json == "oversized" ? new string('x', 16385) : json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(async () => await Read(socket));
        using var recovered = await server.ConnectEventually();
        Assert.Equal(0, Assert.IsType<JoinMessage>(await Read(recovered)).Snapshot.Tick);
    }
    [Fact]
    public async Task Incompatible_save_transfers_preserve_world_and_writing_is_unavailable()
    {
        await using var server = await Harness.Start();
        var before = await server.Bootstrap();
        var download = await server.Http.GetAsync("/api/saves/download");
        Assert.Equal(HttpStatusCode.Conflict, download.StatusCode);
        Assert.Contains("savingUnavailable", await download.Content.ReadAsStringAsync());
        var write = await server.Http.PostAsync("/api/saves/slot1/save?expectedWorldEpoch=1", null);
        Assert.Equal(HttpStatusCode.Conflict, write.StatusCode);
        Assert.Contains("savingUnavailable", await write.Content.ReadAsStringAsync());
        foreach (var body in new[] { "{\"saveVersion\":1}", "{\"saveVersion\":2}", "{\"saveVersion\":999}" })
        {
            var response = await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1", new StringContent(body));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("incompatibleSave", await response.Content.ReadAsStringAsync());
        }
        var malformed = await server.Http.PostAsync("/api/saves/upload?expectedWorldEpoch=1", new StringContent("{broken"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Contains("invalidSave", await malformed.Content.ReadAsStringAsync());
        Assert.Equal(JsonSerializer.Serialize(before.Snapshot, NetworkSession.Json), JsonSerializer.Serialize((await server.Bootstrap()).Snapshot, NetworkSession.Json));
        var status = await server.Http.GetStringAsync("/api/saves/status");
        Assert.Contains("\"savingAvailable\":false", status);
    }

    internal static Task Send(ClientWebSocket socket, WireMessage message) => socket.SendAsync(
        new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes<WireMessage>(message, NetworkSession.Json)), WebSocketMessageType.Text, true, CancellationToken.None);
    internal static async Task<WireMessage> Read(ClientWebSocket socket)
    {
        var message = await ReadFrame(socket);
        if (message is not StateChunkMessage first) return message;
        Assert.InRange(first.TotalBytes, 1, ProtocolIdentity.MaximumStateBytes);
        var bytes = new byte[first.TotalBytes];
        for (var index = 0; index < first.Count; index++)
        {
            var chunk = index == 0 ? first : Assert.IsType<StateChunkMessage>(await ReadFrame(socket));
            Assert.Equal(index, chunk.Index); Assert.Equal(first.TransferId, chunk.TransferId);
            Assert.Equal(first.WorldEpoch, chunk.WorldEpoch); Assert.Equal(first.Revision, chunk.Revision);
            Assert.Equal(first.Sha256, chunk.Sha256); Assert.InRange(chunk.Data.Length, 1, ProtocolIdentity.StateChunkBytes);
            chunk.Data.CopyTo(bytes, index * ProtocolIdentity.StateChunkBytes);
        }
        Assert.Equal(first.Sha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        var payload = JsonSerializer.Deserialize<StateTransferPayload>(bytes, NetworkSession.Json)!;
        if (payload.PackedTerrain is not { } packed) return payload.Message;
        var header = payload.Message is JoinMessage join ? join.Snapshot : Assert.IsType<SnapshotMessage>(payload.Message);
        var terrain = packed.Restore(header.Map, FreeTrain.Online.Content.TerrainProfiles.Bundled,
            new FreeTrain.Online.Simulation.Clock().Capture(), header.Paused,
            new FreeTrain.Online.Simulation.RandomStreams(FreeTrain.Online.Simulation.RandomStreams.ReferenceSeed).Capture(), header.Revision);
        var snapshot = header with { Entities = TerrainWire.Entities(terrain.World.Spatial), Terrain = TerrainWire.Read(terrain) };
        return payload.Message is JoinMessage j ? j with { Snapshot = snapshot } : snapshot;
    }
    private static async Task<WireMessage> ReadFrame(ClientWebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        using var output = new MemoryStream(); var buffer = new byte[4096]; WebSocketReceiveResult frame;
        do
        {
            frame = await socket.ReceiveAsync(buffer, timeout.Token);
            if (frame.MessageType == WebSocketMessageType.Close) throw new InvalidDataException("Closed");
            output.Write(buffer, 0, frame.Count);
        } while (!frame.EndOfMessage);
        return JsonSerializer.Deserialize<WireMessage>(output.ToArray(), NetworkSession.Json)!;
    }
    internal static async Task<WireMessage> Until(ClientWebSocket socket, Func<WireMessage, bool> predicate)
    {
        for (var count = 0; count < 40; count++) { var message = await Read(socket); if (predicate(message)) return message; }
        throw new InvalidOperationException("Expected message not received");
    }
    internal sealed class Harness(WebApplication app, HttpClient http, CookieContainer cookies, bool canonical) : IAsyncDisposable
    {
        public const string BootstrapPath = "/api/bootstrap?protocolVersion=6&contentVersion=prototype-content-1";
        public HttpClient Http => http;
        public CookieContainer Cookies => cookies;
        public Uri SocketUri => new(new Uri(http.BaseAddress!.ToString().Replace("http:", "ws:")), "/api/session?protocolVersion=6&contentVersion=prototype-content-1");
        public static async Task<Harness> Start(string? secret = null, string? saveDirectory = null, string? trustedProxy = null, Dictionary<string, string?>? participants = null, bool canonical = false, bool referenceFixture = false)
        {
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["FreeTrainOnline:OperatorSecret"] = secret, ["FreeTrainOnline:TrustedProxy"] = trustedProxy });
            if (participants != null) builder.Configuration.AddInMemoryCollection(participants);
            if (saveDirectory != null) builder.Services.AddSingleton(_ => new SaveStorage(saveDirectory));
            builder.Services.AddSingleton(new NetworkOptions()); builder.Services.AddSingleton<NetworkSession>(services => new(new NetworkOptions(), services.GetService<SaveStorage>(),
                hostFactory: referenceFixture ? (epoch,expiry) => SessionComposition.CreateReferenceHost(epoch,expiry)
                    : canonical ? null : (epoch,expiry) => SessionComposition.CreatePrototypeHost(epoch,expiry))); builder.Services.AddHostedService<SessionDriver>();
            builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = SaveCodec.MaximumBytes);
            var app = builder.Build(); app.UseDeployment(); app.MapNetwork(); await app.StartAsync();
            var cookies = new CookieContainer();
            return new(app, new(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = new(app.Urls.Single()) }, cookies, canonical);
        }
        private static string CanonicalVersions => ProtocolIdentity.ReferenceContent + "&contentVersion=" + FreeTrain.Online.Simulation.TerrainProfile.ContentVersion + "&contentVersion=" + ProtocolIdentity.MixedContent;
        public async Task<JoinMessage> Bootstrap()
        {
            var response = JsonSerializer.Deserialize<WireMessage>(await http.GetStringAsync(canonical
                ? BootstrapPath.Replace("prototype-content-1", CanonicalVersions) : BootstrapPath), NetworkSession.Json);
            if (response is JoinMessage join) return join;
            var offer = Assert.IsType<SessionOfferMessage>(response);
            // Large terrain is offered as metadata over HTTP; use the coherent
            // server read here. Socket tests separately reassemble its chunks.
            return app.Services.GetRequiredService<NetworkSession>().Bootstrap(new(offer.ParticipantId,
                Enum.Parse<FreeTrain.Online.Session.ParticipantRole>(offer.Role, ignoreCase: true)));
        }
        public async Task<ClientWebSocket> Connect(CookieContainer? participantCookies = null)
        {
            var socket = new ClientWebSocket(); socket.Options.Cookies = participantCookies ?? cookies;
            try { await socket.ConnectAsync(canonical ? new Uri(SocketUri.ToString().Replace("prototype-content-1", CanonicalVersions)) : SocketUri, CancellationToken.None); return socket; } catch { socket.Dispose(); throw; }
        }
        public async Task<ClientWebSocket> ConnectEventually(CookieContainer? participantCookies = null)
        {
            for (var count = 0; count < 50; count++) { try { return await Connect(participantCookies); } catch (WebSocketException) { await Task.Delay(20); } }
            throw new InvalidOperationException("Lease never released");
        }
        public async ValueTask DisposeAsync() { http.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
